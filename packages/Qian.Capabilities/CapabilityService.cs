using System.Buffers;
using System.Data;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Tenancy;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Capabilities;

internal sealed partial class CapabilityService(IRegistryService registry, IPermissionChecker permissions, CapabilityContractCheck contracts,
    IDataService data, CapabilitiesOptions options, IHttpContractSource? httpContracts = null, ILogger? logger = null) : ICapabilityService, IDisposable
{
    // 回放窗口：同一主体、同一能力、同一幂等键的首次成功响应保留 24 小时，之后同键请求重新执行。
    internal static readonly TimeSpan ReplayWindow = TimeSpan.FromHours(24);
    // 执行中标记的租约：超过租约仍未写完成的标记（进程崩溃、连接中断）视为失效，同键请求重新执行。
    internal static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);
    // 登记时顺带清理本租户的过期回执：每次至多 100 行，跳过其他请求正在清理的行，不让单个请求承担大批删除或互相等待。
    private const string PurgeSql = "DELETE FROM " + CapabilitiesDbContext.Schema + "." + CapabilitiesDbContext.ReceiptsTable + " WHERE ctid IN (SELECT ctid FROM " +
        CapabilitiesDbContext.Schema + "." + CapabilitiesDbContext.ReceiptsTable + " WHERE tenant_id = {0} AND expires_at <= {1} LIMIT 100 FOR UPDATE SKIP LOCKED)";
    private static readonly string[] Kinds = ["command", "query", "event", "entity"];
    private readonly (CancellationTokenSource Source, CancellationToken Token) _lifetime = CreateLifetime();
    private int _disposed;

    public async ValueTask<CapabilityCatalog> ListAsync(IPluginContext caller, ICurrentUser user, string? kind = null, string? query = null, CancellationToken cancellationToken = default)
    {
        var errors = new List<QianFieldError>();
        if (kind is not null && !Kinds.Contains(kind, StringComparer.Ordinal))
            errors.Add(new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input("kind")) { Allowed = AllowedValues.Of(Kinds) });
        if (query?.Length > 200) errors.Add(new QianFieldError(PlatformErrors.OutOfRange, ErrorPointer.Input("q")));
        if (errors.Count != 0) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid) { Errors = errors });
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, false, linked.Token).ConfigureAwait(false);
        // 事件与实体条目尚未交付（R77：事件归 S1-EVT-003，实体待分配），这两类 kind 在交付前返回空集合。
        return new CapabilityCatalog(visible.Where(item => (kind is null || item.Entry.GetProperty("kind").GetString() == kind) &&
            (string.IsNullOrWhiteSpace(query) || item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || item.Entry.GetProperty("description").GetString()!.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .Select(item => item.Entry).ToArray(), options.CatalogTtlMs);
    }

    public async ValueTask<string> ComposePromptAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, true, linked.Token).ConfigureAwait(false);
        var output = new StringBuilder();
        foreach (var item in visible)
        {
            output.Append("工具：").AppendLine(item.Id);
            output.AppendLine(item.Entry.GetProperty("description").GetString());
            output.Append("输入契约：").AppendLine(item.Entry.GetProperty("inputSchema").GetRawText());
        }
        return output.ToString();
    }

    public async ValueTask<JsonElement> GetOpenApiAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default)
    {
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, false, linked.Token).ConfigureAwait(false);
        var result = CapabilityOpenApi.Build(visible.Select(item => item.Entry));
        if (httpContracts is null) return result;
        var tenant = await caller.Tenancy().GetByIdAsync(Guid.Parse(caller.Scope.Id), linked.Token).ConfigureAwait(false);
        if (tenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) return result;
        var native = await httpContracts.GetDocumentAsync(caller, user, linked.Token).ConfigureAwait(false);
        var merged = JsonNode.Parse(result.GetRawText())!.AsObject();
        Merge(merged["paths"]!.AsObject(), native.GetProperty("paths"));
        foreach (var category in native.GetProperty("components").EnumerateObject())
        {
            var components = merged["components"]!.AsObject();
            if (components[category.Name] is not JsonObject target) { target = new JsonObject(); components[category.Name] = target; }
            Merge(target, category.Value);
        }
        using var document = JsonDocument.Parse(merged.ToJsonString());
        var parsed = Microsoft.OpenApi.OpenApiDocument.Parse(document.RootElement.GetRawText(), "json");
        if (parsed.Document is null || parsed.Diagnostic?.Errors.Count > 0)
        {
            var error = new InvalidOperationException("聚合 HTTP 文档无效。");
            error.Data["contractDiagnostics"] = string.Join("; ", parsed.Diagnostic?.Errors.Select(item => item.ToString()) ?? []);
            throw error;
        }
        return document.RootElement.Clone();
    }

    public async ValueTask<CapabilityInvocation> InvokeAsync(IPluginContext caller, ICurrentUser user, string id, JsonElement input, Guid? idempotencyKey = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (idempotencyKey == Guid.Empty) throw Error(PlatformErrors.BadRequest);
        using var linked = Link(cancellationToken);
        var visible = await Visible(caller, user, false, linked.Token).ConfigureAwait(false);
        var item = visible.SingleOrDefault(item => item.Id == id) ?? throw Error(PlatformErrors.NotFound);
        // 不缓存目录结果为授权；执行前还会由绑定和实际 Mediator 策略核验可见性与权限。
        if (!Current(caller, item)) throw Error(PlatformErrors.NotFound);
        // 先过可见性再校验输入：不可见的能力不暴露其 Schema 约束。
        contracts.Inputs.Validate(item.Entry, input);
        // 只读能力重复执行没有副作用：忽略幂等键，不存回执。
        if (idempotencyKey is not { } key || item.Entry.GetProperty("sideEffect").GetString() == "read")
            return new CapabilityInvocation(await item.Binding.InvokeAsync(caller, input, linked.Token).ConfigureAwait(false), false);
        return await InvokeOnce(caller, user, item, input, key, linked.Token).ConfigureAwait(false);
    }

    // 与 Kun 幂等消费同一语义（R63），分三段：短事务登记「执行中」→ 不占数据库连接执行 → 写完成或撤销登记。同键请求经事务级
    // advisory lock 串行登记：未过期的完成回执回放，仍在执行的同键请求返回 409，失败或中断不留回执。登记后、写完成前进程崩溃时，
    // 标记在租约到期后失效，重试会再次执行；写类能力仍须按 inputSchema 中的 idempotencyKey 业务幂等。
    private async Task<CapabilityInvocation> InvokeOnce(IPluginContext caller, ICurrentUser user, VisibleCapability item, JsonElement input, Guid idempotencyKey, CancellationToken ct)
    {
        var principal = user.Principal!;
        var key = new ReceiptKey(principal.TenantId!, Hash(KindName(principal.Kind) + ":" + principal.Subject), item.Id, idempotencyKey);
        var fingerprint = Hash(CanonicalJson(input));
        var (replay, claimed) = await ClaimAsync(key, fingerprint, ct).ConfigureAwait(false);
        if (replay is not null)
        {
            using var stored = JsonDocument.Parse(replay);
            Check(user, principal);
            return new CapabilityInvocation(stored.RootElement.Clone(), true);
        }
        JsonElement output;
        try { output = await item.Binding.InvokeAsync(caller, input, ct).ConfigureAwait(false); }
        catch
        {
            await SettleAsync(key, fingerprint, claimed, null).ConfigureAwait(false);
            throw;
        }
        await SettleAsync(key, fingerprint, claimed, output.GetRawText()).ConfigureAwait(false);
        return new CapabilityInvocation(output, false);
    }

    // 第一段：同键串行登记。未过期的完成回执返回其响应；未过期的执行中标记或输入不同返回 409；否则删除同键旧行、清理少量过期回执，
    // 登记执行中标记后立即提交。显式 ReadCommitted：取得锁之后的查询要看到上一持锁者已提交的登记。
    private async Task<(string? Replay, DateTimeOffset Claimed)> ClaimAsync(ReceiptKey key, string fingerprint, CancellationToken ct)
    {
        await using var session = await data.OpenAsync<CapabilitiesDbContext>(ct).ConfigureAwait(false);
        if (session.Context.TenantIdForModelCacheKey != key.TenantId) throw Error(PlatformErrors.NotFound);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        var name = "capabilities.invoke:" + key.TenantId + ":" + key.SubjectKey + ":" + key.CapabilityId + ":" + key.IdempotencyKey.ToString("D");
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({name}, 0))", ct).ConfigureAwait(false);
        var now = Now();
        var receipt = await Find(session.Context, key).AsNoTracking().SingleOrDefaultAsync(ct).ConfigureAwait(false);
        if (receipt is not null && receipt.ExpiresAt > now)
        {
            if (receipt.RequestSha256 != fingerprint || receipt.Response is null) throw Error(PlatformErrors.Conflict);
            return (receipt.Response, default);
        }
        if (receipt is not null) await Find(session.Context, key).ExecuteDeleteAsync(ct).ConfigureAwait(false);
        await session.Context.Database.ExecuteSqlRawAsync(PurgeSql, [key.TenantId, now], ct).ConfigureAwait(false);
        session.Context.InvokeReceipts.Add(new InvokeReceiptRow
        {
            TenantId = key.TenantId, SubjectKey = key.SubjectKey, CapabilityId = key.CapabilityId, IdempotencyKey = key.IdempotencyKey,
            RequestSha256 = fingerprint, Response = null, CreatedAt = now, ExpiresAt = now + ClaimLease
        });
        await session.SaveChangesAsync(ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return (null, now);
    }

    // 第三段：成功时写入原样响应并把过期时间延到回放窗口，失败时删除本次登记。只动本次登记的行（登记时间即标识），租约过期后被
    // 同键新请求接手的行不受影响。显式事务：租户会话变量只在显式事务内生效（ADR-004 D4.8，PgBouncer 事务池同样如此）。
    // 用 CancellationToken.None：请求此时取消也要收尾。收尾失败不掩盖执行结果或执行异常：标记在租约到期后失效，期间同键请求返回 409。
    private async Task SettleAsync(ReceiptKey key, string fingerprint, DateTimeOffset claimed, string? response)
    {
        var step = response is null ? "撤销登记" : "写完成";
        try
        {
            await using var session = await data.OpenAsync<CapabilitiesDbContext>(CancellationToken.None).ConfigureAwait(false);
            await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, CancellationToken.None).ConfigureAwait(false);
            var pending = Find(session.Context, key).Where(row => row.Response == null && row.RequestSha256 == fingerprint && row.CreatedAt == claimed);
            var expires = Now() + ReplayWindow;
            var affected = response is null
                ? await pending.ExecuteDeleteAsync(CancellationToken.None).ConfigureAwait(false)
                : await pending.ExecuteUpdateAsync(row => row.SetProperty(item => item.Response, response).SetProperty(item => item.ExpiresAt, expires), CancellationToken.None).ConfigureAwait(false);
            await transaction.CommitAsync(CancellationToken.None).ConfigureAwait(false);
            // 本次登记已不在：租约到期后被同键请求接手，或会话没有带上租户。
            if (affected == 0 && logger is not null) Log.SettleMissed(logger, key.CapabilityId, step);
        }
        catch (Exception error) when (error is DbException or DbUpdateException or InvalidOperationException or TimeoutException)
        {
            if (logger is not null) Log.SettleFailed(logger, error, key.CapabilityId, step);
        }
    }

    private static IQueryable<InvokeReceiptRow> Find(CapabilitiesDbContext context, ReceiptKey key) => context.InvokeReceipts.Where(row =>
        row.TenantId == key.TenantId && row.SubjectKey == key.SubjectKey && row.CapabilityId == key.CapabilityId && row.IdempotencyKey == key.IdempotencyKey);

    // PostgreSQL timestamptz 精确到微秒：截到微秒后，登记时间可以原样用来找回本次登记。
    private static DateTimeOffset Now()
    {
        var now = DateTimeOffset.UtcNow;
        return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerMicrosecond));
    }

    private async Task<IReadOnlyList<VisibleCapability>> Visible(IPluginContext caller, ICurrentUser user, bool model, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(caller); ArgumentNullException.ThrowIfNull(user);
        var principal = user.Principal;
        if (!user.IsAuthenticated || principal is null || user.UserId != principal.Subject) throw Error(PlatformErrors.Unauthenticated);
        if (caller.Scope.Kind != "tenant" || caller.Scope.Id != principal.TenantId || principal.Actor is not null ||
            principal.Kind is not (PrincipalKind.User or PrincipalKind.Service or PrincipalKind.Agent) ||
            !ReferenceEquals(caller.TryInject<IRegistryService>(RegistryDefinition.Key), registry)) throw Error(PlatformErrors.NotFound);
        if (!Guid.TryParseExact(principal.TenantId, "D", out var tenantId)) throw Error(PlatformErrors.NotFound);
        var tenant = await caller.Tenancy().GetByIdAsync(tenantId, ct).ConfigureAwait(false);
        if (tenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) return [];
        var subject = new AuthorizationSubject(principal.Subject, principal.TenantId, KindName(principal.Kind));
        var scopes = principal.Scope.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);
        var view = await registry.GetScopeEntriesAsync(caller.Scope, CapabilityBindingRegistration.Key, ct).ConfigureAwait(false);
        var registrations = caller.List(CapabilityBindingRegistration.Key); var result = new List<VisibleCapability>();
        foreach (var registration in registrations.OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            if (registration.Value is not IExecutableCapability binding || binding.Contract.Id != registration.Name ||
                !view.Any(entry => entry.EntryId == registration.OwnerEntryId && entry.Scope == registration.Scope && entry.Name == registration.Name && entry.PluginId == binding.Contract.ComponentId)) continue;
            // 不合格的契约在登记期已被拒绝；宿主未登记检查时在这里排除，不影响其余能力。
            if (contracts.Entry(binding.Contract) is not { } entry) continue;
            var metadata = binding.Contract.Metadata;
            if ((model || principal.Kind == PrincipalKind.Agent) && !metadata.GetProperty("agent").GetProperty("expose").GetBoolean()) continue;
            if (metadata.GetProperty("requiredScopes").EnumerateArray().Any(scope => !scopes.Contains(scope.GetString()!))) continue;
            if (!metadata.TryGetProperty("requiredPermission", out var permission) || string.IsNullOrWhiteSpace(permission.GetString())) continue;
            if (!(await permissions.CheckAsync(new PermissionRequest(subject, permission.GetString()!), ct).ConfigureAwait(false)).Allow) continue;
            var item = new VisibleCapability(binding.Contract.Id, entry, binding);
            if (Current(caller, item)) result.Add(item);
        }
        ct.ThrowIfCancellationRequested();
        if (!user.IsAuthenticated || user.UserId != subject.Id || user.Principal?.TenantId != subject.TenantId || user.Principal?.Kind != principal.Kind || user.Principal?.Scope != principal.Scope)
            throw Error(PlatformErrors.NotFound);
        return result.AsReadOnly();
    }

    private static void Merge(JsonObject target, JsonElement source)
    {
        foreach (var item in source.EnumerateObject())
        {
            var node = JsonNode.Parse(item.Value.GetRawText());
            if (target[item.Name] is { } old && !JsonNode.DeepEquals(old, node)) throw new InvalidOperationException("组件 HTTP 契约发生命名冲突。");
            target[item.Name] = node;
        }
    }
    private static bool Current(IPluginContext caller, VisibleCapability item)
    {
        try { return ReferenceEquals(caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, item.Id), item.Binding); }
        catch (Exception error) when (error is QianErrorException or ObjectDisposedException) { return false; }
    }
    private CancellationTokenSource Link(CancellationToken ct)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        return CancellationTokenSource.CreateLinkedTokenSource(ct, _lifetime.Token);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        { try { _lifetime.Source.Cancel(); } finally { _lifetime.Source.Dispose(); } }
    }
    private static (CancellationTokenSource, CancellationToken) CreateLifetime()
    { var source = new CancellationTokenSource(); return (source, source.Token); }
    // 回放前再次确认当前请求主体没有在判定之后变化。
    private static void Check(ICurrentUser user, Principal principal)
    {
        if (!user.IsAuthenticated || user.UserId != principal.Subject || user.Principal?.TenantId != principal.TenantId ||
            user.Principal?.Kind != principal.Kind || user.Principal?.Scope != principal.Scope || user.Principal?.Actor is not null) throw Error(PlatformErrors.NotFound);
    }
    // 按成员名排序的确定性 JSON，用作幂等请求指纹；数值保留原文，数组顺序不变。
    private static string CanonicalJson(JsonElement value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer)) Write(writer, value);
        return Encoding.UTF8.GetString(buffer.WrittenSpan);
        static void Write(Utf8JsonWriter writer, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var member in value.EnumerateObject().OrderBy(member => member.Name, StringComparer.Ordinal)) { writer.WritePropertyName(member.Name); Write(writer, member.Value); }
                    writer.WriteEndObject(); break;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray()) Write(writer, item);
                    writer.WriteEndArray(); break;
                default: value.WriteTo(writer); break;
            }
        }
    }
    private static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string KindName(PrincipalKind kind) => kind switch { PrincipalKind.User => "user", PrincipalKind.Service => "service", _ => "agent" };
    private static QianErrorException Error(ErrorCode code) => new(new QianError(code));
    private sealed record VisibleCapability(string Id, JsonElement Entry, IExecutableCapability Binding);
    private sealed record ReceiptKey(string TenantId, string SubjectKey, string CapabilityId, Guid IdempotencyKey);
    private static partial class Log
    {
        [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "能力 {Capability} 的幂等回执{Step}失败；该键在执行中租约到期前返回 409。")]
        public static partial void SettleFailed(ILogger logger, Exception error, string capability, string step);
        [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "能力 {Capability} 的幂等回执{Step}时找不到本次登记：租约已到期被同键请求接手，或会话未带租户。")]
        public static partial void SettleMissed(ILogger logger, string capability, string step);
    }
}
