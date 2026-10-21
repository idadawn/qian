namespace Qian.Identity.Abstractions;

/// <summary>
/// 统一登录流水线：密码、短信、TOTP 与连接器都构造同一 <see cref="LoginInput"/>，走同一条审计、限流与签发路径（PRD 7.4.1、ADR-005 D5.6）。
/// 本接口只产生「已认证账号会话」；真正的访问令牌由 OIDC 协议端点据此签发，令牌只含身份与会话类 claim。
/// </summary>
public interface ISignInPipeline
{
    /// <summary>
    /// 校验第一认证因子并决定下一步：直接建立会话、要求选择租户（5 分钟选择令牌）或要求先修改初始密码。
    /// 任一失败都写且仅写一条登录审计。
    /// </summary>
    ValueTask<SignInResult> SignInAsync(LoginInput input, LoginContext context, CancellationToken cancellationToken = default);
    /// <summary>用选择令牌换取具体 Membership（或平台）的会话；令牌一次性，过期或已使用时失败。切换租户同样走本入口。</summary>
    ValueTask<SignInResult> SelectAsync(TenantSelection selection, LoginContext context, CancellationToken cancellationToken = default);
    /// <summary>修改初始（或被运营重置的）密码；不可跳过，成功后继续同一登录流程。</summary>
    ValueTask<SignInResult> ChangeInitialPasswordAsync(InitialPasswordChange change, LoginContext context, CancellationToken cancellationToken = default);
    /// <summary>已登录用户发起租户切换：校验当前会话仍有效，签发新的选择令牌，不提升认证强度。</summary>
    ValueTask<SignInResult> BeginSwitchAsync(AccountSession current, LoginContext context, CancellationToken cancellationToken = default);
}

/// <summary>登录请求的网络与终端上下文，来自宿主的 HTTP 层；不含凭据。</summary>
public sealed record LoginContext(string? RemoteAddress = null, string? UserAgent = null)
{
    /// <summary>设备端类型覆盖；缺省取登记客户端的端类型。</summary>
    public string? DeviceType { get; init; }
}

/// <summary>流水线下一步。</summary>
public enum SignInStatus
{
    /// <summary>失败，原因见 <see cref="SignInResult.Error"/>。</summary>
    Failed = 0,
    /// <summary>已建立账号会话。</summary>
    Authenticated = 1,
    /// <summary>命中多个可选身份，须用选择令牌指定其一。</summary>
    SelectionRequired = 2,
    /// <summary>须先修改初始密码，不可跳过。</summary>
    PasswordChangeRequired = 3
}

/// <summary>一次性登录票据的用途。</summary>
public enum LoginTicketPurpose
{
    /// <summary>租户选择（5 分钟）。</summary>
    Selection,
    /// <summary>强制修改初始密码。</summary>
    PasswordChange
}

/// <summary>短期一次性票据；Token 是随机不透明串，服务端只存哈希。</summary>
public sealed record LoginTicket(LoginTicketPurpose Purpose, string Token, DateTimeOffset ExpiresAt)
{
    /// <inheritdoc />
    public override string ToString() => "LoginTicket(redacted)";
}

/// <summary>选择令牌阶段可选的身份：某租户的 Membership，或平台身份。</summary>
public sealed record TenantChoice(Guid? MembershipId, Guid? TenantId, string? TenantCode, string? TenantName, string DisplayName, bool IsPlatform, bool IsTenantAdministrator);

/// <summary>流水线建立的账号会话事实；宿主据此写入可信账号 Cookie，字段对应令牌 claim 规范集。</summary>
public sealed record AccountSession(Guid PersonId, Guid? TenantId, Guid? MembershipId, Guid SessionId, long IdentityRevision,
    IReadOnlyList<string> Amr, string? Acr, DateTimeOffset AuthenticatedAt, DateTimeOffset ExpiresAt, string ClientId, string DeviceType)
{
    /// <inheritdoc />
    public override string ToString() => "AccountSession(redacted)";
}

/// <summary>选择令牌换会话的请求。<c>MembershipId</c> 为 null 表示选择平台身份。</summary>
public sealed record TenantSelection(string Ticket, Guid? MembershipId)
{
    /// <inheritdoc />
    public override string ToString() => "TenantSelection(redacted)";
}

/// <summary>强制改密请求；新密码为临时认证材料，使用后由调用方释放。</summary>
public sealed record InitialPasswordChange(string Ticket, AuthenticationProof NewPassword)
{
    /// <inheritdoc />
    public override string ToString() => "InitialPasswordChange(redacted)";
}
