using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Core.MultiTenancy;
using Kun.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Server;

// 与 HTTP 目录面共用账号服务的真实规则。标准能力目录、输入校验和 24h 回执仍由 Capabilities 提供；这里不新建第二个目录或去重存储。
[Permission("member", "disable")]
internal sealed class DirectoryDisableCapability(IPluginContext owner, IdentityAccountService accounts) : IExecutableCapability, IDisposable
{
    internal const string Permission = "qian.identity.server:member:disable";
    internal static CapabilityContract Definition { get; } = new("qian.identity.server",
        """{"name":"directory.member.disable","description":"批量禁用当前租户成员并吊销其本租户会话；租户管理员逐项拒绝，返回三态结果。","sideEffect":"write","policyHint":"confirm","idempotent":true,"async":"none","requiredScopes":[],"requiredSecrets":[],"dataClassification":"internal","costTier":"low","contractVersion":"directory-v1","audit":true,"agent":{"expose":true,"requiresConfirmation":true},"binding":{"kind":"command","id":"qian.identity.server/directory.member.disable"},"requiredPermission":"qian.identity.server:member:disable"}""",
        DirectoryCapabilityJson.Default.DisableDirectoryMembers, DirectoryCapabilityJson.Default.DirectoryBatchResult, ["reason"]);
    private readonly (CancellationTokenSource Source, CancellationToken Token) _lifetime = CreateLifetime();
    private int _disposed;
    public CapabilityContract Contract => Definition;

    public async ValueTask<JsonElement> InvokeAsync(IPluginContext caller, JsonElement input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (Volatile.Read(ref _disposed) != 0) throw Missing();
        try
        {
            _ = owner.Services;
            if (!ReferenceEquals(caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, Definition.Id), this)) throw Missing();
        }
        catch (ObjectDisposedException) { throw Missing(); }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        var services = caller.Services.GetService<IHttpContextAccessor>()?.HttpContext?.RequestServices ?? caller.Services;
        var user = services.GetService<ICurrentUser>(); var principal = user?.Principal;
        var tenant = services.GetRequiredService<ITenantContext>().TenantId;
        if (string.IsNullOrWhiteSpace(tenant) || caller.Scope.Kind != "tenant" || caller.Scope.Id != tenant) throw Missing();
        // 服务端口的可信快照来自已完成的宿主认证；输入中没有主体、租户或 session 字段。S1 不执行 Agent 委托。
        if (user is not { IsAuthenticated: true } || principal is not { Kind: PrincipalKind.User, Actor: null } || user.UserId != principal.Subject ||
            principal.TenantId != tenant ||
            !Guid.TryParseExact(tenant, "D", out var tenantId) || !Guid.TryParseExact(principal.Subject, "D", out var personId) ||
            !Guid.TryParseExact(principal.MembershipId, "D", out var memberId) || !Guid.TryParseExact(principal.SessionId, "D", out var sessionId) || principal.IdentityRevision is not { } revision)
            throw Denied();
        var permissions = services.GetService<IPermissionChecker>() ?? throw Denied();
        if (!(await permissions.CheckAsync(new PermissionRequest(new AuthorizationSubject(principal.Subject, tenant, "user"), Permission), linked.Token).ConfigureAwait(false)).Allow) throw Denied();
        var validation = await accounts.ValidateAsync(sessionId, personId, memberId, linked.Token).ConfigureAwait(false);
        if (validation is null || validation.TenantId != tenantId || validation.IdentityRevision != revision) throw Denied();
        var session = new AccountSession(personId, tenantId, memberId, sessionId, revision, [], null, default, default, "", "");
        var actor = await accounts.ResolveCallerAsync(session, linked.Token).ConfigureAwait(false);
        if (actor is not { TenantAdministrator: true } || actor.TenantId != tenantId) throw Denied();
        DisableDirectoryMembers request;
        try { request = input.Deserialize(DirectoryCapabilityJson.Default.DisableDirectoryMembers) ?? throw new JsonException(); }
        catch (JsonException) { throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)); }
        if (request.IdempotencyKey == Guid.Empty) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        using var origin = EventEnvelopeScope.Begin(new EventOrigin("qian.identity", EventActor.User(personId.ToString("D"))));
        // 与现有独立写能力相同：开始审计失败则不执行；回执重放发生在此绑定之前，不重复写审计或发吊销事件。
        var audit = caller.Audit(); var attempt = Guid.CreateVersion7(); var trace = System.Diagnostics.ActivityTraceId.CreateRandom().ToHexString();
        var fingerprint = new AuditInputProjection(JsonSerializer.SerializeToElement(Definition.InputSchema), isCommand: true).InputHash(input)!;
        AuditRecord Record(string result, JsonElement payload) => new(Guid.CreateVersion7(), tenantId.ToString("D"), "user", personId.ToString("D"), null,
            Definition.Id, "directory-v1", fingerprint, "api", null, result, trace, DateTimeOffset.UtcNow, payload);
        await audit.AppendAsync(Record("started", JsonSerializer.SerializeToElement(new { attempt, request.IdempotencyKey })), linked.Token).ConfigureAwait(false);
        DirectoryBatchResult result;
        try { result = await accounts.DisableMembersAsync(request.MembershipIds, request.Reason, linked.Token).ConfigureAwait(false); }
        catch
        {
            using var finish = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await audit.AppendAsync(Record("failure", JsonSerializer.SerializeToElement(new { attempt })), finish.Token).ConfigureAwait(false);
            throw;
        }
        using (var finish = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            await audit.AppendAsync(Record("success", JsonSerializer.SerializeToElement(new { attempt, result.Status, succeeded = result.Items.Count(item => item.Succeeded) })), finish.Token).ConfigureAwait(false);
        return JsonSerializer.SerializeToElement(result, DirectoryCapabilityJson.Default.DirectoryBatchResult);
    }
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0) { _lifetime.Source.Cancel(); _lifetime.Source.Dispose(); }
    }
    private static (CancellationTokenSource Source, CancellationToken Token) CreateLifetime() { var source = new CancellationTokenSource(); return (source, source.Token); }
    private static QianErrorException Missing() => new(new QianError(PlatformErrors.NotFound));
    private static QianErrorException Denied() => new(new QianError(PlatformErrors.AuthzDenied));
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DisableDirectoryMembers(Guid IdempotencyKey, IReadOnlyList<Guid> MembershipIds, string Reason);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(DisableDirectoryMembers))]
[JsonSerializable(typeof(DirectoryBatchResult))]
internal sealed partial class DirectoryCapabilityJson : JsonSerializerContext;
