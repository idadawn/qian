using Kun.Core.Domain;
using Kun.EventBus;
using Qian.Runtime;
using Qian.Runtime.Errors;
using System.Text.Json.Serialization;

namespace Qian.Identity.Abstractions;

/// <summary>稳定跨进程事件 ID；破坏性载荷改动必须升版本，不能原地改名。</summary>
public static class IdentityEventIds
{
    /// <summary>登录成功。</summary>
    public const string Login = "identity.login.v1";
    /// <summary>退出登录。</summary>
    public const string Logout = "identity.logout.v1";
    /// <summary>密码已更新。</summary>
    public const string PasswordChanged = "identity.password_changed.v1";
    /// <summary>账号已锁定。</summary>
    public const string Locked = "identity.locked.v1";
    /// <summary>会话已吊销。</summary>
    public const string SessionRevoked = "identity.session_revoked.v1";
    /// <summary>全部 S1 身份事件。</summary>
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[] { Login, Logout, PasswordChanged, Locked, SessionRevoked });
}
/// <summary>身份集成事件共用字段；日志只输出事件类型，不输出会话或凭据。</summary>
public abstract record IdentityIntegrationEvent(Guid EventId, DateTime OccurredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? TenantId, Guid PersonId, long IdentityRevision) : IIntegrationEvent
{
    /// <inheritdoc />
    public sealed override string ToString() => "IdentityIntegrationEvent(redacted)";
}
/// <summary>登录成功载荷，不含 access/refresh token 或认证答案。</summary>
[IntegrationEventId(IdentityEventIds.Login)]
public sealed record IdentityLoginEvent(Guid EventId, DateTime OccurredAt, Guid? TenantId, Guid PersonId, long IdentityRevision,
    Guid SessionId, string ClientId, IReadOnlyList<string> Amr, Guid? MembershipId = null)
    : IdentityIntegrationEvent(EventId, OccurredAt, TenantId, PersonId, IdentityRevision);
/// <summary>退出登录载荷。</summary>
[IntegrationEventId(IdentityEventIds.Logout)]
public sealed record IdentityLogoutEvent(Guid EventId, DateTime OccurredAt, Guid? TenantId, Guid PersonId, long IdentityRevision,
    Guid SessionId, string Reason, Guid? MembershipId = null)
    : IdentityIntegrationEvent(EventId, OccurredAt, TenantId, PersonId, IdentityRevision);
/// <summary>密码变更只通知身份修订，不发送密码或哈希。</summary>
[IntegrationEventId(IdentityEventIds.PasswordChanged)]
public sealed record IdentityPasswordChangedEvent(Guid EventId, DateTime OccurredAt, Guid? TenantId, Guid PersonId, long IdentityRevision)
    : IdentityIntegrationEvent(EventId, OccurredAt, TenantId, PersonId, IdentityRevision);
/// <summary>账号锁定，不发送失败的认证材料。</summary>
[IntegrationEventId(IdentityEventIds.Locked)]
public sealed record IdentityLockedEvent(Guid EventId, DateTime OccurredAt, Guid? TenantId, Guid PersonId, long IdentityRevision,
    string Reason, DateTimeOffset? LockedUntil = null)
    : IdentityIntegrationEvent(EventId, OccurredAt, TenantId, PersonId, IdentityRevision);
/// <summary>会话吊销供网关、缓存与实时通道消费。</summary>
[IntegrationEventId(IdentityEventIds.SessionRevoked)]
public sealed record IdentitySessionRevokedEvent(Guid EventId, DateTime OccurredAt, Guid? TenantId, Guid PersonId, long IdentityRevision,
    Guid SessionId, string Reason, Guid? MembershipId = null)
    : IdentityIntegrationEvent(EventId, OccurredAt, TenantId, PersonId, IdentityRevision);

/// <summary>统一登录流水线输入；临时证明不进入日志或事件。</summary>
public sealed record LoginInput(string ClientId, Guid? TenantId, string? ConnectorId, string? FactorId, AuthenticationProof? Proof)
{
    /// <inheritdoc />
    public override string ToString() => "LoginInput(redacted)";
}
/// <summary>登录流水线结果；选择令牌与真正签发由 Provider 定义。</summary>
public sealed record SignInResult(bool Succeeded, Principal? Principal, QianError? Error);
/// <summary>成员失效通知。</summary>
public sealed record MembershipChanged(Guid MembershipId, Guid TenantId, long IdentityRevision);
/// <summary>进程内扩展事件，与上述 Outbox 事件分别建模。</summary>
public static class IdentityRuntimeEvents
{
    /// <summary>登录风控/step-up 包裹点。</summary>
    public static readonly QianEvent<LoginInput, SignInResult> SigningIn = QianEvent.Define<LoginInput, SignInResult>("identity/signing-in", DispatchMode.Waterfall);
    /// <summary>登录完成。</summary>
    public static readonly QianEvent<Principal, object?> SignedIn = QianEvent.Define<Principal, object?>("identity/signed-in", DispatchMode.Emit);
    /// <summary>退出完成。</summary>
    public static readonly QianEvent<SessionRevocation, object?> SignedOut = QianEvent.Define<SessionRevocation, object?>("identity/signed-out", DispatchMode.Emit);
    /// <summary>成员停用后的并行清理。</summary>
    public static readonly QianEvent<MembershipChanged, IReadOnlyList<object?>> MemberDisabled = QianEvent.Define<MembershipChanged, IReadOnlyList<object?>>("identity/member-disabled", DispatchMode.Parallel);
}
/// <summary>框架事件目录注册；只登记元数据，不启动 broker 或后台发布器。</summary>
public static class IdentityEventRegistration
{
    /// <summary>显式传入源生成 JsonTypeInfo，避免 Attribute 反射或序列化回退。</summary>
    public static IEventTypeRegistrar RegisterIdentityEvents(this IEventTypeRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(registrar);
        registrar.Register(IdentityEventIds.Login, IdentityJson.Context.IdentityLoginEvent);
        registrar.Register(IdentityEventIds.Logout, IdentityJson.Context.IdentityLogoutEvent);
        registrar.Register(IdentityEventIds.PasswordChanged, IdentityJson.Context.IdentityPasswordChangedEvent);
        registrar.Register(IdentityEventIds.Locked, IdentityJson.Context.IdentityLockedEvent);
        return registrar.Register(IdentityEventIds.SessionRevoked, IdentityJson.Context.IdentitySessionRevokedEvent);
    }
}
