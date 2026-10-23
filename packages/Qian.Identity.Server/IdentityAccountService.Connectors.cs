using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Identity.Server;

// 上游连接器登录（PRD 7.4.2、ADR-005 D5.6 / D5.11）。连接器只做「跳转 → 拿回外部断言」；state / nonce / PKCE verifier、
// 回调幂等、外部身份绑定、JIT 建号与会话签发都在这里。连接器注册在运行时的 Identity.Connectors 注册表里，一律按流程所属租户的
// scope 解析，被过滤与不存在同样查不到。会话经统一登录流水线签发：绑定已存在（或刚确认）时发一张一次性连接器票据，
// 再以 ConnectorId + 票据构造同一个 LoginInput 走 SignInAsync，审计、单端与并发策略和密码登录完全一致。
internal sealed partial class IdentityAccountService
{
    internal const string ConnectorPurpose = "connector";
    /// <summary>经上游身份源联合认证的 amr。</summary>
    internal const string FederatedAmr = "fed";
    private const string FlowPending = "pending", FlowProcessing = "processing", FlowBind = "bind", FlowCompleted = "completed", FlowFailed = "failed", FlowCancelled = "cancelled";
    private const string ConnectorFlowsTable = IdentitySql.ConnectorFlows;
    // 出站与回调交给连接器的时限；与请求是否断开无关，避免流程卡在 processing。
    private static readonly TimeSpan ConnectorCallTimeout = TimeSpan.FromSeconds(30);
    // 连接器票据只在同一请求内由服务端自己兑换，寿命很短。
    private static readonly TimeSpan ConnectorTicketLifetime = TimeSpan.FromMinutes(1);
    // 回调失败时可以记进流程的错误码；连接器抛出的其他错误码一律按上游响应无效处理。
    private static readonly ErrorCode[] ConnectorErrors =
        [IdentityErrors.ConnectorStateInvalid, IdentityErrors.ConnectorDenied, PlatformErrors.BadGateway, PlatformErrors.GatewayTimeout, PlatformErrors.Unavailable];

    // ---- 可见性 ----

    /// <summary>某租户（null 为全局）scope 下可见的连接器：租户 scope 的同名注册遮蔽全局项，restriction 过滤的与不存在的一样查不到。</summary>
    internal IIdentityConnectorRegistry ConnectorsIn(Guid? tenantId) =>
        (tenantId is { } id ? _ctx.Scoped(new ScopeKey("tenant", id.ToString("D"))) : _ctx).IdentityConnectors();

    /// <summary>按租户号解析租户；未知、为空或格式不对都返回 null（调用方回落全局集合或按不存在处理，不泄露租户是否存在）。</summary>
    internal async Task<TenantDescriptor?> FindTenantAsync(string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length > 64 || code.Any(char.IsControl)) return null;
        return await Directory.GetAsync(code.Trim(), ct).ConfigureAwait(false);
    }

    // IIdentityService.Connectors：按当前租户上下文解析；Register 落在本组件的全局 scope（连接器组件请用自己的 ctx.IdentityConnectors()）。
    private sealed class TenantConnectorView(IdentityAccountService owner) : IIdentityConnectorRegistry
    {
        public IDisposable Register(string name, IIdentityConnector connector) => owner._ctx.IdentityConnectors().Register(name, connector);
        public IIdentityConnector? Find(string name) => owner.ConnectorsIn(owner.CurrentTenantOrNull()).Find(name);
        public IReadOnlyList<ConnectorDescriptor> List() => owner.ConnectorsIn(owner.CurrentTenantOrNull()).List();
    }

    /// <summary>回调处理后 302 回到的界面落点模板（含 {connectorId}）。</summary>
    internal string ConnectorLanding => _account.Connectors.LandingPath;

    private Guid? CurrentTenantOrNull() => Guid.TryParseExact(_tenant.TenantId, "D", out var id) && id != Guid.Empty ? id : null;

    // ---- 发起 ----

    internal sealed record ConnectorStart(Guid Flow, Uri RedirectUri, DateTimeOffset ExpiresAt);

    /// <summary>
    /// 建立流程并取得上游跳转地址。流程与浏览器密钥绑定（库里只存其哈希），PKCE verifier 由该密钥派生，state 只存哈希。
    /// 租户必填（绑定键是 tenant × connector × subject）；租户未知与连接器在该租户不可见一样返回 NOT_FOUND。
    /// </summary>
    internal async Task<ConnectorStart> BeginConnectorAsync(string connectorId, string? clientId, string? tenantCode, string? returnUrl, string browserSecret, Uri callbackUri,
        LoginContext context, CancellationToken ct)
    {
        if (FindClient(clientId) is not { } client) throw new QianErrorException(Field(PlatformErrors.ValueNotAllowed, "#/client_id"));
        if (string.IsNullOrWhiteSpace(tenantCode)) throw new QianErrorException(Field(PlatformErrors.Required, "#/tenant"));
        if (returnUrl is not null && !IsLocalReturnUrl(returnUrl)) throw new QianErrorException(Field(PlatformErrors.ValueNotAllowed, "#/return_url"));
        var tenant = await FindTenantAsync(tenantCode, ct).ConfigureAwait(false) ?? throw Error(PlatformErrors.NotFound);
        var connector = ConnectorsIn(tenant.Id).Find(connectorId) ?? throw Error(PlatformErrors.NotFound);
        var now = Now; var flowId = Guid.CreateVersion7(now);
        var state = Base64Url(RandomNumberGenerator.GetBytes(32)); var nonce = Base64Url(RandomNumberGenerator.GetBytes(32));
        var expires = now + _account.Connectors.FlowLifetime;
        // 先落流程再出站：回调可能比 BeginSignInAsync 返回还快。顺手清掉一天前就已结束的流程。
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            await IdentitySql.ExecuteAsync(session.Context, $"DELETE FROM {ConnectorFlowsTable} WHERE expires_at < @cutoff", ct, ("cutoff", now - TimeSpan.FromDays(1))).ConfigureAwait(false);
            session.Context.ConnectorFlows.Add(new ConnectorFlowRow
            {
                Id = flowId, StateHash = SHA256.HashData(Encoding.ASCII.GetBytes(state)), BrowserHash = BrowserHash(browserSecret), ConnectorId = connectorId,
                TenantId = tenant.Id.ToString("D"), ClientId = client.Application.ClientId!, DeviceType = DeviceTypeOf(client, context), ReturnUrl = returnUrl, Nonce = nonce,
                Status = FlowPending, RemoteAddress = Truncate(context.RemoteAddress, 64), UserAgent = Truncate(context.UserAgent, 256), CreatedAt = now, ExpiresAt = expires
            });
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        ChallengeResult? challenge = null;
        try
        {
            // 连接器在流程租户的上下文里运行：租户 scope 挂载的连接器解析 secretRef://tenant/… 时按这个租户核对。
            using var scope = TenantScope(tenant.Id);
            using var timeout = new CancellationTokenSource(ConnectorCallTimeout);
            challenge = await connector.BeginSignInAsync(new ConnectorContext(client.Application.ClientId!, tenant.Id, callbackUri, state, nonce)
                { CodeVerifier = Verifier(browserSecret, flowId) }, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            var code = ConnectorFailure(error);
            ConnectorCallFailed(_logger, connectorId, FailureType(error));
            await SetFlowStatusAsync(flowId, FlowPending, FlowFailed, code, null, ct).ConfigureAwait(false);
            throw Error(code == IdentityErrors.ConnectorStateInvalid || code == IdentityErrors.ConnectorDenied ? PlatformErrors.BadGateway : code);
        }
        if (challenge?.RedirectUri is not { IsAbsoluteUri: true } target || target.Scheme is not ("https" or "http") || target.UserInfo.Length != 0)
        {
            await SetFlowStatusAsync(flowId, FlowPending, FlowFailed, PlatformErrors.BadGateway, null, ct).ConfigureAwait(false);
            throw Error(PlatformErrors.BadGateway);
        }
        return new ConnectorStart(flowId, target, expires);
    }

    // 只接受同源路径：以单个「/」开头，不含反斜杠、控制字符或 fragment；外部地址一律拒绝（开放重定向）。
    private static bool IsLocalReturnUrl(string value) =>
        value.Length is > 0 and <= 2048 && value[0] == '/' && !value.StartsWith("//", StringComparison.Ordinal) && !value.StartsWith("/\\", StringComparison.Ordinal) &&
        !value.Contains('\\', StringComparison.Ordinal) && !value.Contains('#', StringComparison.Ordinal) && !value.Any(char.IsControl) &&
        Uri.TryCreate(value, UriKind.Relative, out _);

    // ---- 回调 ----

    /// <summary>回调结果：Flow 非空时 302 回界面落点（Session 非空先写账号 Cookie）；Flow 为空时以 Error 直接响应。</summary>
    internal sealed record ConnectorCallbackResult(Guid? Flow, QianError? Error, AccountSession? Session);

    /// <summary>
    /// 统一回调 <c>/connect/callback/{connectorId}</c>：校验 state（未知、属于别的连接器或别的浏览器一律 AUTH_CONNECTOR_STATE_INVALID，流程不受影响）；
    /// 回调幂等——流程一旦离开 pending，重复的回调只回到落点，不再调用连接器；上游带 error 时记 AUTH_CONNECTOR_DENIED，
    /// 不把 error_description 当文案；断言交给连接器校验（含 nonce）后按 tenant × connector × subject 查绑定：
    /// 已绑定直接签发会话，未绑定进入确认绑定（票据 10 分钟）。每次回调恰好一条登录审计。
    /// </summary>
    internal async Task<ConnectorCallbackResult> CompleteConnectorAsync(string connectorId, IReadOnlyDictionary<string, string> parameters, string? browserSecret, Uri callbackUri,
        LoginContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        context ??= new LoginContext();
        if (!ConnectorManifest.IsValidId(connectorId)) return new(null, new QianError(PlatformErrors.NotFound), null);
        var subject = new AuditSubject("connector", null, "connector:" + Truncate(connectorId, 48), null, null, context);
        var hash = parameters.TryGetValue("state", out var state) ? TicketHash(state) : null;
        var flow = hash is null ? null : await LoadFlowAsync(f => f.StateHash == hash, ct).ConfigureAwait(false);
        if (flow is null || flow.ConnectorId != connectorId || !SameBrowser(flow, browserSecret))
            return await RejectCallbackAsync(subject, flow is null ? "state_invalid" : "state_mismatch", ct).ConfigureAwait(false);
        subject = subject with { ClientId = flow.ClientId };
        var tenantId = TenantOf(flow);
        // 发起之后连接器在该租户被卸载或过滤：与「不存在」同一响应。
        if (ConnectorsIn(tenantId).Find(connectorId) is not { } connector)
        {
            await WriteAuditAsync(NewAudit(subject, "failure", "connector_unavailable", Now, tenantId: tenantId), ct).ConfigureAwait(false);
            return new(null, new QianError(PlatformErrors.NotFound), null);
        }
        // 快路径：已离开 pending 的流程直接回落点；真正的幂等保证是下面的条件抢占。
        if (flow.Status != FlowPending) return new(flow.Id, null, null);
        var now = Now;
        if (!await ClaimFlowAsync(flow.Id, FlowPending, now, true, ct).ConfigureAwait(false))
        {
            // 抢占失败：要么并发的同一回调已在处理（什么都不做），要么流程已过期（标记失败并审计一次）。
            if (flow.ExpiresAt <= now && await SetFlowStatusAsync(flow.Id, FlowPending, FlowFailed, IdentityErrors.ConnectorStateInvalid, null, ct).ConfigureAwait(false))
                await WriteAuditAsync(NewAudit(subject, "failure", "state_expired", now, tenantId: tenantId), ct).ConfigureAwait(false);
            return new(flow.Id, null, null);
        }
        if (parameters.TryGetValue("error", out _))
        {
            await SetFlowStatusAsync(flow.Id, FlowProcessing, FlowFailed, IdentityErrors.ConnectorDenied, Sanitize(parameters.GetValueOrDefault("error_description")), ct).ConfigureAwait(false);
            await WriteAuditAsync(NewAudit(subject, "failure", "upstream_denied", now, tenantId: tenantId), ct).ConfigureAwait(false);
            return new(flow.Id, null, null);
        }
        ExternalAssertion assertion;
        try
        {
            using var scope = TenantScope(tenantId);
            using var timeout = new CancellationTokenSource(ConnectorCallTimeout);
            assertion = await connector.CompleteSignInAsync(new ConnectorCallback(new ConnectorContext(flow.ClientId, tenantId, callbackUri, state!, flow.Nonce)
                { CodeVerifier = Verifier(browserSecret!, flow.Id) }, parameters), timeout.Token).ConfigureAwait(false);
            if (!IsUsable(assertion)) throw Error(PlatformErrors.BadGateway);
        }
        catch (Exception error)
        {
            var code = ConnectorFailure(error);
            ConnectorCallFailed(_logger, connectorId, FailureType(error));
            await SetFlowStatusAsync(flow.Id, FlowProcessing, FlowFailed, code, null, ct).ConfigureAwait(false);
            await WriteAuditAsync(NewAudit(subject, "failure", code == IdentityErrors.ConnectorStateInvalid ? "nonce_invalid" : "assertion_invalid", Now, tenantId: tenantId), ct).ConfigureAwait(false);
            return new(flow.Id, null, null);
        }
        var bound = await FindBindingAsync(tenantId, connectorId, assertion.Subject, ct).ConfigureAwait(false);
        if (bound is null)
        {
            await MarkBindingAsync(flow.Id, assertion, Now + _account.Connectors.BindingLifetime, ct).ConfigureAwait(false);
            await WriteAuditAsync(NewAudit(subject, "binding_required", null, Now, tenantId: tenantId), ct).ConfigureAwait(false);
            return new(flow.Id, null, null);
        }
        var result = await SignInWithConnectorTicketAsync(flow, bound.Value.Person, bound.Value.Membership, [FederatedAmr], Now, "external_identity", context, ct).ConfigureAwait(false);
        await FinishFlowAsync(flow.Id, result, ct).ConfigureAwait(false);
        return new(flow.Id, null, result.Status == SignInStatus.Authenticated ? result.Session : null);
    }

    private async Task<ConnectorCallbackResult> RejectCallbackAsync(AuditSubject subject, string reason, CancellationToken ct)
    {
        await WriteAuditAsync(NewAudit(subject, "failure", reason, Now), ct).ConfigureAwait(false);
        return new(null, new QianError(IdentityErrors.ConnectorStateInvalid), null);
    }

    private static bool IsUsable(ExternalAssertion? assertion) =>
        assertion is not null && !string.IsNullOrWhiteSpace(assertion.Subject) && assertion.Subject.Length <= 256 && !assertion.Subject.Any(char.IsControl) &&
        !string.IsNullOrWhiteSpace(assertion.Issuer) && assertion.Issuer.Length <= 512 && assertion.UnionId is not { Length: > 256 };

    // ---- 确认绑定 ----

    /// <summary>确认绑定第一步：在流程的租户内登录已有账号（新鲜一因子认证，走与密码登录同一条限流与审计）。成功只记入流程，不建立会话。</summary>
    internal async Task<(ConnectorFlowView? View, SignInResult? Failure)> VerifyConnectorAccountAsync(Guid flowId, string? browserSecret, LoginInput input, LoginContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);
        context ??= new LoginContext();
        var flow = await LoadFlowAsync(f => f.Id == flowId, ct).ConfigureAwait(false);
        if (flow is null || !SameBrowser(flow, browserSecret)) return (null, SignInResult.Failure(new QianError(PlatformErrors.NotFound)));
        var tenantId = TenantOf(flow);
        var (kind, masked) = IdentifierRules.MaskInput(input.Identifier, _account.DefaultPhoneCountryCode);
        var subject = new AuditSubject("connector_verify", flow.ClientId, input.FactorId ?? PasswordFactorId, kind, masked, context);
        if (await BindingGateAsync(flow, subject, tenantId, ct).ConfigureAwait(false) is { } closed) return (null, closed);
        var first = await AuthenticateFirstFactorAsync(input with { ClientId = flow.ClientId, TenantId = tenantId, ConnectorId = null }, subject, ct).ConfigureAwait(false);
        if (first.Failure is { } failure) return (null, failure);
        var person = first.Person!;
        MembershipRow? member;
        {
            using var scope = TenantScope(tenantId);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            member = await session.Context.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.PersonId == person.Id && m.Status == AccountStatuses.Active, ct).ConfigureAwait(false);
        }
        if (member is null) return (null, await RejectAsync(subject, new QianError(IdentityErrors.NoAvailableTenant), "no_membership", person.Id, ct, null, tenantId).ConfigureAwait(false));
        var now = Now;
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            var updated = await IdentitySql.ExecuteAsync(session.Context, $"""
                UPDATE {ConnectorFlowsTable} SET person_id = @person, membership_id = @member, verified_at = @now, verified_amr = @amr
                WHERE id = @id AND status = '{FlowBind}'
                """, ct, ("person", person.Id), ("member", member.Id), ("now", now), ("amr", first.Verified!.Amr.Count == 0 ? new[] { PasswordAmr } : first.Verified.Amr.ToArray()), ("id", flow.Id)).ConfigureAwait(false);
            if (updated != 1) return (null, await RejectAsync(subject, new QianError(IdentityErrors.ConnectorStateInvalid), "flow_state", person.Id, ct, null, tenantId).ConfigureAwait(false));
        }
        await WriteAuditAsync(NewAudit(subject, "verified", "connector_binding", now, person.Id, member.Id, tenantId), ct).ConfigureAwait(false);
        return (await GetConnectorFlowAsync(flow.Id, browserSecret, ct).ConfigureAwait(false), null);
    }

    /// <summary>
    /// 确认绑定第二步：link 把外部身份绑定到刚登录的账号（同租户已绑定到别的账号返回 AUTH_BINDING_CONFLICT，不透露对方），
    /// create 按租户策略 JIT 建号（缺省关闭，受配额约束），cancel 作废流程。票据 10 分钟过期返回 AUTH_BINDING_EXPIRED。
    /// 成功后经统一流水线签发会话；流程先被抢占为 processing，所以重复确认不会签出第二个会话。cancel 返回 null。
    /// </summary>
    internal async Task<SignInResult?> ConfirmConnectorBindingAsync(Guid flowId, string? browserSecret, string? mode, LoginContext context, CancellationToken ct)
    {
        context ??= new LoginContext();
        var flow = await LoadFlowAsync(f => f.Id == flowId, ct).ConfigureAwait(false);
        if (flow is null || !SameBrowser(flow, browserSecret)) return SignInResult.Failure(new QianError(PlatformErrors.NotFound));
        if (mode is not ("link" or "create" or "cancel")) return SignInResult.Failure(Field(PlatformErrors.ValueNotAllowed, "#/mode"));
        if (mode == "cancel")
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            await IdentitySql.ExecuteAsync(session.Context, $"UPDATE {ConnectorFlowsTable} SET status = '{FlowCancelled}', completed_at = @now WHERE id = @id AND status IN ('{FlowPending}', '{FlowBind}')",
                ct, ("now", Now), ("id", flow.Id)).ConfigureAwait(false);
            return null;
        }
        var tenantId = TenantOf(flow);
        var subject = new AuditSubject("connector_bind", flow.ClientId, "connector:" + Truncate(flow.ConnectorId, 48), null, null, context);
        if (await BindingGateAsync(flow, subject, tenantId, ct).ConfigureAwait(false) is { } closed) return closed;
        var now = Now;
        if (mode == "link" && (flow.PersonId is null || flow.MembershipId is null || flow.VerifiedAt is not { } verifiedAt || now - verifiedAt > _account.Connectors.ReauthenticationWindow))
            return await RejectAsync(subject, new QianError(PlatformErrors.Unauthenticated), "reauthentication_required", flow.PersonId, ct, null, tenantId).ConfigureAwait(false);
        if (mode == "create" && !await JitAllowedAsync(tenantId, ct).ConfigureAwait(false))
            return await RejectAsync(subject, new QianError(PlatformErrors.BusinessRuleViolated), "jit_disabled", null, ct, null, tenantId).ConfigureAwait(false);
        if (!await ClaimFlowAsync(flow.Id, FlowBind, now, false, ct).ConfigureAwait(false))
            return await RejectAsync(subject, new QianError(IdentityErrors.ConnectorStateInvalid), "flow_state", flow.PersonId, ct, null, tenantId).ConfigureAwait(false);
        Guid personId, membershipId;
        try
        {
            if (mode == "link") { personId = flow.PersonId!.Value; membershipId = flow.MembershipId!.Value; await LinkFlowIdentityAsync(tenantId, flow, personId, membershipId, ct).ConfigureAwait(false); }
            else (personId, membershipId) = await ProvisionFromFlowAsync(tenantId, flow, ct).ConfigureAwait(false);
        }
        catch (QianErrorException failure)
        {
            await SetFlowStatusAsync(flow.Id, FlowProcessing, FlowFailed, failure.Error.Code, null, ct).ConfigureAwait(false);
            var reason = failure.Error.Code == IdentityErrors.BindingConflict ? "binding_conflict" : mode == "create" ? "jit_rejected" : "link_rejected";
            return await RejectAsync(subject, failure.Error, reason, flow.PersonId, ct, null, tenantId).ConfigureAwait(false);
        }
        var linked = mode == "link";
        var amr = linked ? [.. flow.VerifiedAmr.Where(value => value != FederatedAmr), FederatedAmr] : new[] { FederatedAmr };
        var result = await SignInWithConnectorTicketAsync(flow, personId, membershipId, amr, linked ? flow.VerifiedAt!.Value : now,
            linked ? "external_identity_linked" : "external_identity_jit", context, ct).ConfigureAwait(false);
        await FinishFlowAsync(flow.Id, result, ct).ConfigureAwait(false);
        return result;
    }

    // 绑定前的公共闸门：流程必须停在 bind；票据过期则把流程标记失败并返回 AUTH_BINDING_EXPIRED（已因过期失败的流程同样如此）。各写一条审计。
    private async Task<SignInResult?> BindingGateAsync(ConnectorFlowRow flow, AuditSubject subject, Guid tenantId, CancellationToken ct)
    {
        if (flow.Status == FlowFailed && flow.ErrorCode == IdentityErrors.BindingExpired.Code)
            return await RejectAsync(subject, new QianError(IdentityErrors.BindingExpired), "binding_expired", null, ct, null, tenantId).ConfigureAwait(false);
        if (flow.Status != FlowBind) return await RejectAsync(subject, new QianError(IdentityErrors.ConnectorStateInvalid), "flow_state", null, ct, null, tenantId).ConfigureAwait(false);
        if (flow.BindingExpiresAt is not { } expires || expires <= Now)
        {
            await SetFlowStatusAsync(flow.Id, FlowBind, FlowFailed, IdentityErrors.BindingExpired, null, ct).ConfigureAwait(false);
            return await RejectAsync(subject, new QianError(IdentityErrors.BindingExpired), "binding_expired", null, ct, null, tenantId).ConfigureAwait(false);
        }
        return null;
    }

    // 绑定键 tenant × connector × subject；已绑到同一成员时幂等，绑到别的成员时冲突（不透露是谁）。成员必须仍属于该 Person 且可用。
    private async Task LinkFlowIdentityAsync(Guid tenantId, ConnectorFlowRow flow, Guid personId, Guid membershipId, CancellationToken ct)
    {
        using var scope = TenantScope(tenantId); using var origin = OpenOrigin(personId);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var db = session.Context;
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var member = await db.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == membershipId && m.PersonId == personId, ct).ConfigureAwait(false);
        if (member is null || member.Status != AccountStatuses.Active) throw Error(IdentityErrors.NoAvailableTenant);
        var existing = await db.ExternalIdentities.AsNoTracking().SingleOrDefaultAsync(e => e.ConnectorId == flow.ConnectorId && e.Subject == flow.ExternalSubject, ct).ConfigureAwait(false);
        if (existing is not null) { if (existing.MembershipId == member.Id) return; throw Error(IdentityErrors.BindingConflict); }
        db.ExternalIdentities.Add(new ExternalIdentityRow
        {
            Id = Guid.CreateVersion7(), MembershipId = member.Id, PersonId = member.PersonId, ConnectorId = flow.ConnectorId, Subject = flow.ExternalSubject!, UnionId = flow.ExternalUnionId
        });
        try { await session.SaveChangesAsync(ct).ConfigureAwait(false); }
        catch (DbUpdateException error) when (IsUniqueViolation(error)) { throw Error(IdentityErrors.BindingConflict); }
        await transaction.CommitAsync(ct).ConfigureAwait(false);
    }

    // JIT 建号：先过配额再建 Person（标识用上游邮箱，未验证；没有邮箱就用由绑定键派生的用户名），再建成员并绑定。
    private async Task<(Guid Person, Guid Membership)> ProvisionFromFlowAsync(Guid tenantId, ConnectorFlowRow flow, CancellationToken ct)
    {
        var profile = ReadProfile(flow.ExternalProfile);
        var name = profile.Name is { Length: > 0 and <= 200 } display && !display.Any(char.IsControl) ? display : profile.Username ?? "外部用户";
        {
            using var scope = TenantScope(tenantId);
            var quota = _ctx.TryInject<ITenantQuota>(TenantSettings.QuotaKey);
            if (quota is not null) await quota.EnsureCanAddUserAsync(await CountUsersAsync(ct).ConfigureAwait(false), TenantMemberKind.Member, ct).ConfigureAwait(false);
        }
        var email = IdentifierRules.Normalize(IdentifierKind.Email, profile.Email, _account.DefaultPhoneCountryCode);
        var identifier = email is not null ? new IdentifierInput(IdentifierKind.Email, email)
            : new IdentifierInput(IdentifierKind.Username, "ext-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(flow.ConnectorId + "\n" + flow.ExternalSubject)))[..20]);
        var person = await CreatePersonAsync(new CreatePerson(name, [identifier]) { RequirePasswordChange = false }, ct).ConfigureAwait(false);
        var member = await CreateMembershipCoreAsync(tenantId, person.Id, name, null, false, null, ct).ConfigureAwait(false);
        await LinkFlowIdentityAsync(tenantId, flow, person.Id, member.Id, ct).ConfigureAwait(false);
        return (person.Id, member.Id);
    }

    private async Task<bool> JitAllowedAsync(Guid tenantId, CancellationToken ct)
    {
        var settings = _ctx.TryInject<ITenantSettings>(TenantSettings.Key);
        using var scope = TenantScope(tenantId);
        return await JitEnabledAsync(settings, _ctx.Entry.Plugin, ct).ConfigureAwait(false);
    }

    // JIT 是租户策略：经 ITenantSettings 读取本组件在当前租户的 connectors:jitProvisioning，读不到（未装租户设置、未配置、读失败）一律视为关闭。
    // 读失败含租户设置服务自身的 InvalidOperationException：本组件直接挂载而没有已安装的组件记录时（开发宿主、独立宿主），它解析组件清单会这样抛。
    internal static async Task<bool> JitEnabledAsync(ITenantSettings? settings, string pluginId, CancellationToken ct)
    {
        if (settings is null) return false;
        try { return await settings.Get<bool?>(pluginId, "connectors:jitProvisioning", ct).ConfigureAwait(false) == true; }
        catch (QianErrorException) { return false; }
        catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }

    // ---- 统一流水线：连接器票据 ----

    // 绑定已确认之后由服务端自己发一张一次性票据，再以 ConnectorId + 票据走 SignInAsync：审计、单端与并发策略、登录事件与密码登录同一路径。
    private async Task<SignInResult> SignInWithConnectorTicketAsync(ConnectorFlowRow flow, Guid personId, Guid membershipId, IReadOnlyList<string> amr, DateTimeOffset authenticatedAt,
        string reason, LoginContext context, CancellationToken ct)
    {
        var now = Now; var token = Base64Url(RandomNumberGenerator.GetBytes(32));
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            session.Context.LoginTickets.Add(new LoginTicketRow
            {
                TokenHash = SHA256.HashData(Encoding.ASCII.GetBytes(token)), Purpose = ConnectorPurpose, PersonId = personId, ClientId = flow.ClientId, DeviceType = flow.DeviceType,
                TenantId = flow.TenantId, Choices = [$"m:{membershipId:D}:{flow.TenantId}"], Amr = [.. amr], AuthenticatedAt = authenticatedAt, Reason = reason,
                RemoteAddress = Truncate(context.RemoteAddress, 64), UserAgent = Truncate(context.UserAgent, 256), CreatedAt = now, ExpiresAt = now + ConnectorTicketLifetime
            });
            await session.SaveChangesAsync(ct).ConfigureAwait(false);
        }
        using var proof = new AuthenticationProof(Encoding.ASCII.GetBytes(token));
        return await SignInAsync(new LoginInput(flow.ClientId, TenantOf(flow), flow.ConnectorId, null, proof), context, ct).ConfigureAwait(false);
    }

    // SignInAsync 的连接器分支：只认服务端刚发的连接器票据，兑换与会话建立同一事务（票据只能用一次）。
    // 连接器登录不要求修改初始密码（这次没用密码）；登记了第二因子的账号与密码登录一样拒绝，不降级成单因子。
    private async ValueTask<SignInResult> SignInWithConnectorAsync(LoginInput input, LoginContext context, CancellationToken ct)
    {
        var subject = new AuditSubject("authenticate", input.ClientId, "connector:" + Truncate(input.ConnectorId, 48), null, null, context);
        if (FindClient(input.ClientId) is null) return await RejectAsync(subject, Field(PlatformErrors.ValueNotAllowed, "#/client_id"), "client_unknown", null, ct).ConfigureAwait(false);
        // 该租户 scope 下看不见的连接器（未挂载、被过滤或不存在）不是可用的登录方式。
        if (ConnectorsIn(input.TenantId).Find(input.ConnectorId!) is null)
            return await RejectAsync(subject, Field(PlatformErrors.ValueNotAllowed, "#/connector_id"), "method_unsupported", null, ct).ConfigureAwait(false);
        var token = input.Proof is null ? null : TryAscii(input.Proof.Data.Span);
        var hash = TicketHash(token);
        var ticket = hash is null ? null : await PeekTicketAsync(hash, ConnectorPurpose, ct).ConfigureAwait(false);
        if (hash is null || ticket is null || ticket.ClientId != input.ClientId || ticket.Choices.Count != 1 || ticket.Choices[0].Tenant is not { } tenant || tenant != input.TenantId)
            return await RejectAsync(subject, new QianError(IdentityErrors.LoginTicketInvalid), "ticket_invalid", ticket?.PersonId, ct).ConfigureAwait(false);
        PersonRow? person;
        {
            using var neutral = TenantScope(null);
            await using var session = await OpenAsync(ct).ConfigureAwait(false);
            person = await session.Context.Persons.AsNoTracking().SingleOrDefaultAsync(p => p.Id == ticket.PersonId, ct).ConfigureAwait(false);
        }
        if (person is null || person.Status != AccountStatuses.Active)
            return await RejectAsync(subject, new QianError(IdentityErrors.AccountDisabled), "account_" + (person?.Status ?? "missing"), ticket.PersonId, ct).ConfigureAwait(false);
        if (HasEnrolledFactors(person))
            return await RejectAsync(subject, new QianError(IdentityErrors.MfaUnavailable), "mfa_unavailable", person.Id, ct).ConfigureAwait(false);
        var state = new AuthState(ticket.PersonId, ticket.ClientId, ticket.DeviceType, ticket.Amr, ticket.Acr, ticket.AuthenticatedAt);
        return await EstablishAsync(subject, state, ticket.Choices[0], hash, ConnectorPurpose, null, ticket.Reason, ct).ConfigureAwait(false);
    }

    private static string? TryAscii(ReadOnlySpan<byte> data)
    {
        foreach (var b in data) if (b is < 0x21 or > 0x7E) return null;
        return Encoding.ASCII.GetString(data);
    }

    // ---- 流程视图 ----

    internal sealed record ConnectorFlowView(Guid Id, string ConnectorId, string? ConnectorName, string? Protocol, string? TenantCode, string? TenantName, string Status, string Next,
        DateTimeOffset ExpiresAt, ConnectorBindingView? Binding, QianError? Error, string? UpstreamDescription, string? ReturnUrl);

    internal sealed record ConnectorBindingView(string? DisplayName, string? Email, string? Username, DateTimeOffset ExpiresAt, bool AllowJit, bool Verified,
        string? AccountDisplayName, string? Account);

    /// <summary>界面落点读取流程；只认发起它的浏览器，否则与不存在一样返回 null。过期的流程在此按失败呈现。返回地址只在完成后给出。</summary>
    internal async Task<ConnectorFlowView?> GetConnectorFlowAsync(Guid flowId, string? browserSecret, CancellationToken ct)
    {
        var flow = await LoadFlowAsync(f => f.Id == flowId, ct).ConfigureAwait(false);
        if (flow is null || !SameBrowser(flow, browserSecret)) return null;
        var now = Now;
        // 处理中的流程正常几十秒内结束；过了时限再一分钟仍在处理（进程中途退出）就按上游超时失败呈现，界面不会一直等。
        if (flow.Status == FlowBind && flow.BindingExpiresAt <= now && await SetFlowStatusAsync(flow.Id, FlowBind, FlowFailed, IdentityErrors.BindingExpired, null, ct).ConfigureAwait(false) ||
            flow.Status == FlowPending && flow.ExpiresAt <= now && await SetFlowStatusAsync(flow.Id, FlowPending, FlowFailed, IdentityErrors.ConnectorStateInvalid, null, ct).ConfigureAwait(false) ||
            flow.Status == FlowProcessing && (flow.BindingExpiresAt ?? flow.ExpiresAt) + TimeSpan.FromMinutes(1) <= now &&
            await SetFlowStatusAsync(flow.Id, FlowProcessing, FlowFailed, PlatformErrors.GatewayTimeout, null, ct).ConfigureAwait(false))
            flow = await LoadFlowAsync(f => f.Id == flowId, ct).ConfigureAwait(false) ?? flow;
        var tenantId = TenantOf(flow);
        var tenant = await Directory.GetByIdAsync(tenantId, ct).ConfigureAwait(false);
        var descriptor = ConnectorsIn(tenantId).Find(flow.ConnectorId)?.Descriptor;
        ConnectorBindingView? binding = null;
        if (flow.Status == FlowBind)
        {
            var profile = ReadProfile(flow.ExternalProfile);
            string? accountName = null, account = null;
            if (flow.MembershipId is { } membershipId)
            {
                using var scope = TenantScope(tenantId);
                await using var session = await OpenAsync(ct).ConfigureAwait(false);
                var member = await session.Context.Memberships.AsNoTracking().SingleOrDefaultAsync(m => m.Id == membershipId, ct).ConfigureAwait(false);
                (accountName, account) = (member?.DisplayName, member?.Account);
            }
            var verified = flow.VerifiedAt is { } at && now - at <= _account.Connectors.ReauthenticationWindow;
            binding = new ConnectorBindingView(profile.Name, profile.Email is { } email ? IdentifierRules.Mask(IdentifierKind.Email, email) : null,
                profile.Username is { } username ? IdentifierRules.Mask(IdentifierKind.Username, username) : null, flow.BindingExpiresAt!.Value,
                await JitAllowedAsync(tenantId, ct).ConfigureAwait(false), verified, verified ? accountName : null, verified ? account : null);
        }
        // 流程视图内嵌的 ErrorDetail 不带参数：带占位符的错误码（如配额）退回同状态码的通用码。
        QianError? error = null;
        if (flow.Status == FlowFailed)
        {
            var known = flow.ErrorCode is { } code && _ctx.Services.GetService<ErrorCatalog>() is { } catalog && catalog.TryGet(code, out var registered) && registered.Kind != ErrorKind.Field
                ? registered : PlatformErrors.BadGateway;
            error = new QianError(known.Parameters.Count == 0 ? known : PlatformErrors.ForStatus(known.Status ?? 500));
        }
        var next = flow.Status switch
        {
            FlowPending or FlowProcessing => "wait", FlowBind => "bind", FlowCompleted => "done", FlowFailed => "error", _ => "restart"
        };
        return new ConnectorFlowView(flow.Id, flow.ConnectorId, descriptor?.DisplayName, descriptor?.Protocol, tenant?.Code, tenant?.Name, flow.Status, next,
            flow.BindingExpiresAt ?? flow.ExpiresAt, binding, error, flow.Status == FlowFailed ? flow.ErrorDetail : null, flow.Status == FlowCompleted ? flow.ReturnUrl : null);
    }

    // ---- 流程存储 ----

    private async Task<ConnectorFlowRow?> LoadFlowAsync(System.Linq.Expressions.Expression<Func<ConnectorFlowRow, bool>> where, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await session.Context.ConnectorFlows.AsNoTracking().SingleOrDefaultAsync(where, ct).ConfigureAwait(false);
    }

    // 抢占：pending → processing（回调，必须未过期）或 bind → processing（确认绑定）。只有一个并发请求能抢到。
    private async Task<bool> ClaimFlowAsync(Guid flowId, string from, DateTimeOffset now, bool callback, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await IdentitySql.ExecuteAsync(session.Context, $"""
            UPDATE {ConnectorFlowsTable} SET status = '{FlowProcessing}', callback_at = CASE WHEN @callback THEN @now ELSE callback_at END
            WHERE id = @id AND status = @from AND (NOT @callback OR expires_at > @now) AND (@callback OR binding_expires_at > @now)
            """, ct, ("id", flowId), ("from", from), ("now", now), ("callback", callback)).ConfigureAwait(false) == 1;
    }

    private async Task<bool> SetFlowStatusAsync(Guid flowId, string from, string to, ErrorCode? error, string? detail, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        return await IdentitySql.ExecuteAsync(session.Context, $"""
            UPDATE {ConnectorFlowsTable} SET status = @to, error_code = @error, error_detail = @detail, completed_at = @now WHERE id = @id AND status = @from
            """, ct, ("to", to), ("error", error?.Code), ("detail", detail), ("now", Now), ("id", flowId), ("from", from)).ConfigureAwait(false) == 1;
    }

    private async Task FinishFlowAsync(Guid flowId, SignInResult result, CancellationToken ct) =>
        await SetFlowStatusAsync(flowId, FlowProcessing, result.Status == SignInStatus.Authenticated ? FlowCompleted : FlowFailed,
            result.Status == SignInStatus.Authenticated ? null : result.Error?.Code ?? PlatformErrors.Internal, null, ct).ConfigureAwait(false);

    // 未绑定：记下绑定键与展示资料（只在服务端），流程进入 bind，绑定票据从此刻起算。
    private async Task MarkBindingAsync(Guid flowId, ExternalAssertion assertion, DateTimeOffset bindingExpires, CancellationToken ct)
    {
        using var neutral = TenantScope(null);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        await IdentitySql.ExecuteAsync(session.Context, $"""
            UPDATE {ConnectorFlowsTable} SET status = '{FlowBind}', external_issuer = @issuer, external_subject = @subject, external_union_id = @union,
                external_profile = CAST(@profile AS jsonb), binding_expires_at = @expires
            WHERE id = @id AND status = '{FlowProcessing}'
            """, ct, ("issuer", assertion.Issuer), ("subject", assertion.Subject), ("union", assertion.UnionId), ("profile", WriteProfile(assertion.Profile)),
            ("expires", bindingExpires), ("id", flowId)).ConfigureAwait(false);
    }

    // 已绑定的外部身份 → (Person, 成员)。外部身份是租户表，只在流程所属租户的作用域内查。
    private async Task<(Guid Person, Guid Membership)?> FindBindingAsync(Guid tenantId, string connectorId, string subject, CancellationToken ct)
    {
        using var scope = TenantScope(tenantId);
        await using var session = await OpenAsync(ct).ConfigureAwait(false);
        var row = await session.Context.ExternalIdentities.AsNoTracking().SingleOrDefaultAsync(e => e.ConnectorId == connectorId && e.Subject == subject, ct).ConfigureAwait(false);
        return row is null ? null : (row.PersonId, row.MembershipId);
    }

    // ---- 小工具 ----

    private sealed record ExternalProfile(string? Name, string? Email, string? Username);

    // 只留展示需要的三项，丢弃断言里的其他资料；界面上邮箱与用户名一律掩码。
    private static string WriteProfile(JsonElement profile)
    {
        static string? Text(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object && source.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 and <= 320 } text && !text.Any(char.IsControl) ? text : null;
        return new JsonObject
        {
            ["name"] = Text(profile, "name"), ["email"] = Text(profile, "email"), ["username"] = Text(profile, "preferred_username") ?? Text(profile, "username")
        }.ToJsonString();
    }

    private static ExternalProfile ReadProfile(string? json)
    {
        if (string.IsNullOrEmpty(json)) return new(null, null, null);
        using var document = JsonDocument.Parse(json);
        string? Text(string name) => document.RootElement.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return new(Text("name"), Text("email"), Text("username"));
    }

    // 上游 error_description 是不可信数据：去掉控制字符，截断到 200 字（不劈开代理对），只作技术详情。
    private static string? Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = new string([.. value.Where(c => !char.IsControl(c))]).Trim();
        if (clean.Length > 200) clean = clean[..(char.IsHighSurrogate(clean[199]) ? 199 : 200)];
        return clean.Length == 0 ? null : clean;
    }

    private static ErrorCode ConnectorFailure(Exception error) => error switch
    {
        QianErrorException failure when ConnectorErrors.Contains(failure.Error.Code) => failure.Error.Code,
        OperationCanceledException or TimeoutException => PlatformErrors.GatewayTimeout,
        _ => PlatformErrors.BadGateway
    };

    private static string FailureType(Exception error) => error is QianErrorException failure ? failure.Error.Code.Code : error.GetType().Name;

    private static Guid TenantOf(ConnectorFlowRow flow) =>
        Guid.ParseExact(flow.TenantId ?? throw new InvalidOperationException("连接器流程缺少所属租户。"), "D");

    internal static byte[] BrowserHash(string secret) => SHA256.HashData(Encoding.ASCII.GetBytes(secret));

    private static bool SameBrowser(ConnectorFlowRow flow, string? secret) =>
        secret is not null && CryptographicOperations.FixedTimeEquals(BrowserHash(secret), flow.BrowserHash);

    // PKCE verifier（RFC 7636，43 位 base64url）由浏览器密钥与流程标识派生：库里没有它，偷到授权码也换不出令牌。
    private static string Verifier(string browserSecret, Guid flowId) =>
        Base64Url(HMACSHA256.HashData(Encoding.ASCII.GetBytes(browserSecret), flowId.ToByteArray()));

    [LoggerMessage(EventId = 2201, Level = LogLevel.Warning, Message = "连接器 {ConnectorId} 的出站或回调处理失败；ErrorType={ErrorType}")]
    private static partial void ConnectorCallFailed(ILogger logger, string connectorId, string errorType);
}
