using System.Data.Common;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data.Native;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Audit.Native;

/// <summary>原生 PostgreSQL 审计的唯一组件入口。</summary>
[QianPlugin("qian.audit.native")]
[QianConfigSchema(NativeAuditConfiguration.Schema)]
[DelegatePlugin(typeof(NativeAuditPlugin))]
[Provides(typeof(AuditDefinition), nameof(AuditDefinition.Key))]
[Provides(typeof(CommandPolicies), nameof(CommandPolicies.AuditKey))]
public sealed class NativeAuditComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [NativeDataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => new NativeAuditPlugin().ApplyAsync(ctx, config, ct);
}

/// <summary>服务与审计策略同属 Apply 效果栈，卸载取消并排空在途调用。</summary>
public sealed class NativeAuditPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [NativeDataDefinition.Key];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        var settings = NativeAuditSettings.Read(config);
        var tenant = ctx.Services.GetRequiredService<ITenantContext>();
        if (ctx.Scope == ScopeKey.Global)
        {
            // 不用虚构租户打开 tenant 连接校验元数据；global 必须显式提供独立平台绑定。
            if (tenant.TenantId is not null || settings.PlatformBinding is null) throw Misconfigured();
        }
        else if (ctx.Scope.Kind != "tenant" || ctx.Entry.Scope != ctx.Scope || tenant.TenantId != ctx.Scope.Id) throw Misconfigured();
        var service = new NativeAuditService(ctx, tenant, settings);
        ctx.Effect(AuditErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        ctx.Effect(ctx.Services.GetRequiredService<ErrorCatalog>().Register(NativeAuditErrors.Misconfigured));
        ctx.Effect(service.DisposeAsync);
        await service.ValidateAsync(ctx.Scope == ScopeKey.Global, ct).ConfigureAwait(false);
        ctx.Provide<IAuditService>(AuditDefinition.Key, service);
        CommandPolicies.Register(ctx, CommandPolicyKind.Audit, new NativeAuditCommandPolicy(service).Handle);
    }
    private static QianErrorException Misconfigured() => new(new QianError(NativeAuditErrors.Misconfigured));
}

internal sealed class NativeAuditService(IPluginContext owner, ITenantContext tenant, NativeAuditSettings settings) : IAuditService, IAsyncDisposable
{
    private readonly NativeAuditStore _store = new(owner.NativeData(), settings, owner);
    private readonly ScopeKey _scope = owner.Scope;
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pending; private bool _stopping;
    internal Task ValidateAsync(bool platform, CancellationToken ct) => _store.ValidateAsync(platform, true, ct);
    public Task<AuditChainEntry> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => Run(ct =>
    { CheckRecord(record); return _store.AppendAsync(record, false, ct); }, cancellationToken);
    public Task<AuditChainEntry> AppendEnlistedAsync(AuditRecord record, DbTransaction transaction, CancellationToken cancellationToken = default) => Run(ct =>
    { CheckRecord(record); return _store.AppendEnlistedAsync(record, transaction, ct); }, cancellationToken);
    public Task<AuditChainEntry> AppendPlatformAsync(IPluginContext producer, AuditRecord record, CancellationToken cancellationToken = default) => Run(ct =>
    { Platform(producer); if (record.TenantId is not null) throw Denied(); return _store.AppendAsync(record, true, ct); }, cancellationToken);
    // 原生存储尚未提供平台链的同事务入口（S1-AUD-009 裁决 ①：不在范围）；平台记录仍走 AppendPlatformAsync 的独立事务。
    public Task<AuditChainEntry> AppendPlatformEnlistedAsync(IPluginContext producer, AuditRecord record, DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Task.FromException<AuditChainEntry>(new NotSupportedException("原生审计存储尚未提供平台链的同事务入口，平台记录请用 AppendPlatformAsync。"));
    public Task<IReadOnlyList<AuditChainEntry>> ReadAsync(long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default)
        => Run(ct => _store.ReadAsync(false, afterSequence, take, onBehalfOf, ct), cancellationToken);
    public Task<IReadOnlyList<AuditChainEntry>> ReadPlatformAsync(IPluginContext producer, long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => Run(ct =>
    { Platform(producer); return _store.ReadAsync(true, afterSequence, take, onBehalfOf, ct); }, cancellationToken);
    public Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default) => Run(ct => _store.VerifyAsync(false, ct), cancellationToken);
    public Task<AuditChainVerification> VerifyPlatformAsync(IPluginContext producer, CancellationToken cancellationToken = default) => Run(ct =>
    { Platform(producer); return _store.VerifyAsync(true, ct); }, cancellationToken);
    private void CheckRecord(AuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (!Guid.TryParseExact(tenant.TenantId, "D", out var id) || id == Guid.Empty || id.ToString("D") != tenant.TenantId || record.TenantId != tenant.TenantId) throw Denied();
    }
    private void Platform(IPluginContext producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (_scope != ScopeKey.Global || producer.Scope != ScopeKey.Global || producer.Entry.Scope != ScopeKey.Global || tenant.TenantId is not null ||
            settings.PlatformBinding is null || !ReferenceEquals(producer.TryInject<IAuditService>(AuditDefinition.Key), this)) throw Denied();
    }
    private async Task<T> Run<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (_scope.Kind == "tenant" && tenant.TenantId != _scope.Id) throw Denied();
            _pending++;
        }
        try { using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token); return await operation(linked.Token).ConfigureAwait(false); }
        finally { lock (_gate) { if (--_pending == 0 && _stopping) _drained.TrySetResult(); } }
    }
    public ValueTask DisposeAsync()
    {
        lock (_gate)
        {
            if (_stopping) return new ValueTask(_closed.Task);
            _stopping = true; if (_pending == 0) _drained.TrySetResult();
        }
        return new ValueTask(StopAsync());
    }
    private async Task StopAsync()
    {
        try { try { await _stop.CancelAsync().ConfigureAwait(false); } finally { await _drained.Task.ConfigureAwait(false); _stop.Dispose(); } _closed.TrySetResult(); }
        catch (Exception error) { _closed.TrySetException(error); throw; }
    }
    private static QianErrorException Denied() => new(new QianError(PlatformErrors.AuthzDenied));
}

internal sealed record NativeAuditSettings(string TenantBinding, string? PlatformBinding, NativeAuditRoles Roles, int MaxAttempts)
{
    internal static NativeAuditSettings Read(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object || config.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw NativeAuditRoles.Invalid();
        string? tenant = null, writer = null, platformRole = null, tenantBinding = null, platformBinding = null; var attempts = 6;
        foreach (var p in config.EnumerateObject())
        {
            switch (p.Name)
            {
                case "tenantBinding" when p.Value.ValueKind == JsonValueKind.String: tenantBinding = p.Value.GetString(); break;
                case "platformBinding" when p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null: platformBinding = p.Value.GetString(); break;
                case "tenantRole" when p.Value.ValueKind == JsonValueKind.String: tenant = p.Value.GetString(); break;
                case "functionOwner" when p.Value.ValueKind == JsonValueKind.String: writer = p.Value.GetString(); break;
                case "platformRole" when p.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null: platformRole = p.Value.GetString(); break;
                case "maxAttempts" when p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var n): attempts = n; break;
                default: throw NativeAuditRoles.Invalid();
            }
        }
        if (tenant is null || writer is null || string.IsNullOrWhiteSpace(tenantBinding) || tenantBinding.Length > 100 || attempts is < 1 or > 10 ||
            platformBinding is not null && (platformRole is null || string.IsNullOrWhiteSpace(platformBinding) || platformBinding.Length > 100 || platformBinding == tenantBinding)) throw NativeAuditRoles.Invalid();
        return new NativeAuditSettings(tenantBinding, platformBinding, new NativeAuditRoles(tenant, writer, platformRole), attempts);
    }
}
internal static class NativeAuditConfiguration
{
    internal const string Schema = """
        {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false,
        "required":["tenantBinding","tenantRole","functionOwner"],"description":"global挂载须显式提供独立platformBinding/platformRole用于只读启动校验；tenant挂载须有真实可信租户，可只声明已有platformRole而不绑定其凭据。",
        "properties":{"tenantBinding":{"type":"string","minLength":1,"maxLength":100},"platformBinding":{"type":["string","null"],"minLength":1,"maxLength":100},
        "tenantRole":{"type":"string","pattern":"^[a-z][a-z0-9_]{0,62}$"},"functionOwner":{"type":"string","pattern":"^[a-z][a-z0-9_]{0,62}$"},
        "platformRole":{"type":["string","null"],"pattern":"^[a-z][a-z0-9_]{0,62}$"},"maxAttempts":{"type":"integer","minimum":1,"maximum":10,"default":6}},
        "allOf":[{"if":{"required":["platformBinding"],"properties":{"platformBinding":{"type":"string"}}},
        "then":{"required":["platformRole"],"properties":{"platformRole":{"type":"string"}}}}]}
        """;
}
