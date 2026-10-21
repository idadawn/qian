using Kun.Core.Domain;
using Qian.Identity.Abstractions;

namespace Qian.Identity.Server;

// 身份数据库的行类型。Person 及其凭据、标识、会话、审计是全局表（无租户列，RLS 不适用）；
// Membership 与 ExternalIdentity 带 tenant_id，是强制 RLS 的租户表，只能在对应租户上下文中读写。

/// <summary>全局自然人。Id 即令牌的 sub，创建后永不改写；凭据只存 Argon2id 哈希。</summary>
internal sealed class PersonRow : IAuditedObject<string>, IAggregateRoot
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = AccountStatuses.Active;
    public bool PlatformAccess { get; set; }
    public string? PasswordHash { get; set; }
    public DateTimeOffset? PasswordChangedAt { get; set; }
    public bool MustChangePassword { get; set; }
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LastFailedAt { get; set; }
    public DateTimeOffset? LockoutUntil { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }
    // 第二因子登记位（类型、登记时间、密钥引用）；密钥本身只存 secretRef，由认证因子任务（S1-IDP-006）写入。
    public string MfaFactors { get; set; } = "[]";
    // Person 级修订：改密、停用、锁定、标识变更时递增；令牌修订 = Person 修订 + 成员修订，二者只增不减。
    public long IdentityRevision { get; set; }
    // 登录索引：持有 Membership 的租户 ID（规范小写 UUID）。Membership 受租户 RLS 保护，登录前无租户上下文，
    // 靠它逐租户进入作用域读取；只由建立或清除 Membership 的同一事务以原子 SQL 维护，不是授权依据。
    public string[] TenantIds { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;
    public void ClearDomainEvents() => _events.Clear();
    internal void Raise(IDomainEvent change) => _events.Add(change);
}

/// <summary>Person 的登录标识。软删除：解绑后保留行但释放唯一性，换绑不改写 Person.Id。</summary>
internal sealed class PersonIdentifierRow : ISoftDelete
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Kind { get; set; } = "";
    public string Value { get; set; } = "";
    public DateTimeOffset? VerifiedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

/// <summary>密码历史：每次设置密码都追加一条哈希（含当前密码），用于「历史哈希不可复用」。</summary>
internal sealed class PasswordHistoryRow
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Hash { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>租户成员（person × tenant）。租户表：强制 RLS，TenantId 由 Kun 租户拦截器按上下文填写。</summary>
internal sealed class MembershipRow : IMultiTenant, IAuditedObject<string>
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public Guid PersonId { get; set; }
    public string Account { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Status { get; set; } = AccountStatuses.Active;
    public bool IsTenantAdministrator { get; set; }
    public long IdentityRevision { get; set; }
    // 身份切换（profile）的预留位，S1 不启用。
    public string? Profile { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}

/// <summary>外部身份绑定，唯一键 tenant × connector × subject；软删除。</summary>
internal sealed class ExternalIdentityRow : IMultiTenant, ISoftDelete
{
    public Guid Id { get; set; }
    public string TenantId { get; set; } = "";
    public Guid MembershipId { get; set; }
    public Guid PersonId { get; set; }
    public string ConnectorId { get; set; } = "";
    public string Subject { get; set; } = "";
    public string? UnionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
}

/// <summary>客户端登记镜像：与 OpenIddict 应用同源于宿主可信配置，另记 Qian 的类型、端类型与归属。</summary>
internal sealed class ClientRow
{
    public string ClientId { get; set; } = "";
    public string Type { get; set; } = "";
    public string DeviceType { get; set; } = "pc";
    public string RedirectUris { get; set; } = "[]";
    public string Scopes { get; set; } = "[]";
    public string Resources { get; set; } = "[]";
    public Guid? OwnerPersonId { get; set; }
    // 机器客户端归属的租户（null 为平台服务）；非租户表，列可空。
    public string? TenantId { get; set; }
    public string? PluginId { get; set; }
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>账号会话（SSO 会话）。Id 即令牌的 sid：内部失效关联字段，日志只记其哈希。</summary>
internal sealed class SessionRow
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid? MembershipId { get; set; }
    public string? TenantId { get; set; }
    public string ClientId { get; set; } = "";
    public string DeviceType { get; set; } = "pc";
    public string[] Amr { get; set; } = [];
    public string? Acr { get; set; }
    public long IdentityRevision { get; set; }
    public string? RemoteAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset AuthenticatedAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }
    public DateTimeOffset AbsoluteExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? RevokeReason { get; set; }
}

/// <summary>登录审计：每次登录请求恰好一条；标识只存类型与掩码，不存凭据、验证码或原始 sid。</summary>
internal sealed class LoginAuditRow
{
    public Guid Id { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string Stage { get; set; } = "";
    public string Outcome { get; set; } = "";
    public string? Reason { get; set; }
    public Guid? PersonId { get; set; }
    public Guid? MembershipId { get; set; }
    public string? TenantId { get; set; }
    public string? IdentifierKind { get; set; }
    public string? IdentifierMasked { get; set; }
    public string? ClientId { get; set; }
    public string? Method { get; set; }
    public string? RemoteAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionHash { get; set; }
    public string? TraceId { get; set; }
}

/// <summary>一次性登录票据（选择令牌、强制改密令牌）。令牌是随机不透明串，库里只存 SHA-256。</summary>
internal sealed class LoginTicketRow
{
    public byte[] TokenHash { get; set; } = [];
    public string Purpose { get; set; } = "";
    public Guid PersonId { get; set; }
    public string ClientId { get; set; } = "";
    public string DeviceType { get; set; } = "pc";
    // 登录时指定了租户则只能选择该租户的身份。
    public string? TenantId { get; set; }
    // 选择令牌签发时确认过的可选身份：「m:成员ID:租户ID」或「p」（平台）。兑换时只能在这个集合内选，且逐项重新校验。
    public string[] Choices { get; set; } = [];
    // 切换租户时被替换的当前会话（sid），兑换成功的同一事务内吊销。
    public Guid? FromSessionId { get; set; }
    public string[] Amr { get; set; } = [];
    public string? Acr { get; set; }
    public DateTimeOffset AuthenticatedAt { get; set; }
    public string? RemoteAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }
}

/// <summary>图形验证码挑战：答案只存加盐哈希，单次有效、短寿命；放数据库是为了多副本部署时任一实例都能校验。</summary>
internal sealed class CaptchaRow
{
    public Guid Id { get; set; }
    public byte[] AnswerHash { get; set; } = [];
    public DateTimeOffset ExpiresAt { get; set; }
}
