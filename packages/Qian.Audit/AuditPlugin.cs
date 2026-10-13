using System.Text.Json;
using System.Data.Common;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Qian.Data;
using Qian.Runtime;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;

namespace Qian.Audit;

/// <summary>提供真实审计服务并在卸载时取消、排空未完成调用。</summary>
public sealed class AuditPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global && ctx.Scope.Kind != "tenant" || config.ValueKind != JsonValueKind.Object) throw Invalid();
        string? role = null;
        var fields = config.EnumerateObject().ToArray();
        if (fields.Length > 1 || fields.Any(field => field.Name != "chain")) throw Invalid();
        if (config.TryGetProperty("chain", out var chain))
        {
            if (chain.ValueKind != JsonValueKind.Object) throw Invalid();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in chain.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw Invalid();
                if (property.Name == "store" && property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == "pg") continue;
                if (property.Name != "platformRole" || property.Value.ValueKind != JsonValueKind.String || ctx.Scope != ScopeKey.Global) throw Invalid();
                role = property.Value.GetString()!; AuditPlatformAccess.ValidateRoleName(role);
            }
        }
        var service = new AuditService(ctx.Data(), ctx.Services.GetRequiredService<ITenantContext>(), ctx.Scope, role);
        ctx.Effect(AuditErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        ctx.Effect(service.DisposeAsync);
        await service.ValidateAsync(ct).ConfigureAwait(false);
        ctx.Provide<IAuditService>(AuditDefinition.Key, service);
        CommandPolicies.Register(ctx, CommandPolicyKind.Audit, new AuditCommandPolicy(service).Handle);
    }
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}

internal sealed class AuditService(IDataService data, ITenantContext tenant, ScopeKey scope, string? platformRole) : IAuditService, IAsyncDisposable
{
    private readonly PostgresAuditStore _tenants = new(data, tenant);
    private readonly PostgresAuditStore<PlatformAuditDbContext>? _platform = platformRole is null ? null : new(data, tenant, platformRole);
    private readonly Lock _gate = new();
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource _drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _closed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _pending; private bool _stopping;

    internal async Task ValidateAsync(CancellationToken ct)
    {
        if (_platform is null) return;
        await _platform.ValidateAsync(ct).ConfigureAwait(false);
        await using var session = await data.OpenAsync<AuditDbContext>(ct).ConfigureAwait(false);
        var db = session.Context; await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT session_user::text";
        if (await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is string login && login == platformRole)
            throw new QianDataMisconfiguredException("平台与租户审计不能复用同一登录凭据。");
    }
    public Task<AuditChainEntry> AppendAsync(AuditRecord record, CancellationToken cancellationToken = default) => Run(ct => _tenants.AppendAsync(record, ct), cancellationToken);
    public Task<AuditChainEntry> AppendEnlistedAsync(AuditRecord record, DbTransaction transaction, CancellationToken cancellationToken = default) =>
        Run(ct => AuditTransactionWriter.AppendAsync(record, transaction, tenant, ct), cancellationToken);
    public Task<AuditChainEntry> AppendPlatformAsync(IPluginContext producer, AuditRecord record, CancellationToken cancellationToken = default) => Run(ct => Platform(producer).AppendAsync(record, ct), cancellationToken);
    public Task<IReadOnlyList<AuditChainEntry>> ReadAsync(long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => Run(ct => _tenants.ReadAsync(afterSequence, take, onBehalfOf, ct), cancellationToken);
    public Task<IReadOnlyList<AuditChainEntry>> ReadPlatformAsync(IPluginContext producer, long afterSequence, int take, string? onBehalfOf = null, CancellationToken cancellationToken = default) => Run(ct => Platform(producer).ReadAsync(afterSequence, take, onBehalfOf, ct), cancellationToken);
    public Task<AuditChainVerification> VerifyAsync(CancellationToken cancellationToken = default) => Run(_tenants.VerifyAsync, cancellationToken);
    public Task<AuditChainVerification> VerifyPlatformAsync(IPluginContext producer, CancellationToken cancellationToken = default) => Run(ct => Platform(producer).VerifyAsync(ct), cancellationToken);
    private PostgresAuditStore<PlatformAuditDbContext> Platform(IPluginContext producer)
    {
        ArgumentNullException.ThrowIfNull(producer);
        if (scope != ScopeKey.Global || producer.Scope != ScopeKey.Global || producer.Entry.Scope != ScopeKey.Global ||
            !ReferenceEquals(producer.Audit(), this) || _platform is null)
            throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
        return _platform;
    }
    private async Task<T> Run<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_stopping, this);
            if (scope.Kind == "tenant" && tenant.TenantId != scope.Id) throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            _pending++;
        }
        try
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
            return await operation(linked.Token).ConfigureAwait(false);
        }
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
        try
        {
            try { await _stop.CancelAsync().ConfigureAwait(false); }
            finally { await _drained.Task.ConfigureAwait(false); _stop.Dispose(); }
            _closed.TrySetResult();
        }
        catch (Exception error) { _closed.TrySetException(error); throw; }
    }
}
