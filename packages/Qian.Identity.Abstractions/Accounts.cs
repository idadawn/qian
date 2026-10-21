namespace Qian.Identity.Abstractions;

/// <summary>账号与成员状态的稳定词汇。</summary>
public static class AccountStatuses
{
    /// <summary>可登录。</summary>
    public const string Active = "active";
    /// <summary>被停用；已有会话失效，密码验证通过后也不签发会话。</summary>
    public const string Disabled = "disabled";
    /// <summary>被管理员锁定（区别于错误次数触发的限时锁定）。</summary>
    public const string Locked = "locked";
    /// <summary>已清除，标识释放，不可恢复登录。</summary>
    public const string Purged = "purged";
}

/// <summary>登录标识类型。S1 的三类标识；用户名不得形似手机或邮箱，因此按语法即可判定类型。</summary>
public enum IdentifierKind
{
    /// <summary>手机号，规范化为带国家码的 E.164 形式。</summary>
    Phone,
    /// <summary>邮箱，规范化为小写。</summary>
    Email,
    /// <summary>用户名，字母开头的 3–32 位字母、数字、下划线、点或连字符。</summary>
    Username
}

/// <summary>待绑定的标识。Verified 只允许可信流程（验证码、邮件链接、运营已核验）置为 true。</summary>
public sealed record IdentifierInput(IdentifierKind Kind, string Value, bool Verified = false);

/// <summary>对外标识视图：只含掩码，不含原值。</summary>
public sealed record IdentifierView(IdentifierKind Kind, string Masked, bool Verified);

/// <summary>新建自然人。Person 全局唯一；其 Id 即令牌的 <c>sub</c>，创建后永不改写。</summary>
public sealed record CreatePerson(string DisplayName, IReadOnlyList<IdentifierInput> Identifiers)
{
    /// <summary>初始密码；为空表示仅上游登录。使用后由调用方释放。</summary>
    public AuthenticationProof? InitialPassword { get; init; }
    /// <summary>设置了初始密码时，是否强制首次登录修改（不可取消）。运营开户默认 true。</summary>
    public bool RequirePasswordChange { get; init; } = true;
    /// <summary>平台控制台访问资格；有资格的 Person 登录后得到 tenant_id 为 null 的平台身份，且不计入租户用户配额。</summary>
    public bool PlatformAccess { get; init; }
    /// <inheritdoc />
    public override string ToString() => "CreatePerson(redacted)";
}

/// <summary>在当前租户为 Person 建立成员身份。租户只取自当前已验证的租户上下文，不接受请求字段。</summary>
public sealed record CreateMembership(Guid PersonId, string DisplayName)
{
    /// <summary>指定成员 ID（开户编排的幂等指针）；缺省由服务端生成。</summary>
    public Guid? MembershipId { get; init; }
    /// <summary>租户内账号，租户内唯一、创建后不可改；缺省取用户名或成员 ID 前缀。</summary>
    public string? Account { get; init; }
    /// <summary>是否租户管理员；计入租户用户配额。</summary>
    public bool IsTenantAdministrator { get; init; }
}

/// <summary>已登录用户自行改密：必须验证当前密码。</summary>
public sealed record PasswordChange(AuthenticationProof Current, AuthenticationProof New)
{
    /// <inheritdoc />
    public override string ToString() => "PasswordChange(redacted)";
}

/// <summary>运营重置密码：旧凭据立即失效，默认下次登录强制改密，并使该 Person 的既有会话失效。</summary>
public sealed record PasswordReset(AuthenticationProof New, string Reason)
{
    /// <summary>重置后是否强制下次登录改密。</summary>
    public bool RequireChange { get; init; } = true;
    /// <inheritdoc />
    public override string ToString() => "PasswordReset(redacted)";
}

/// <summary>外部身份绑定：同租户内 connector × subject 唯一，绑定须显式确认，软删除。</summary>
public sealed record ExternalIdentityLink(Guid MembershipId, string ConnectorId, string Subject, string? UnionId = null);

/// <summary>
/// 账号管理端口，由身份 Provider 实现。创建、绑定、改密、状态变更都经这里，保证 sub 稳定、标识唯一、
/// 凭据只存 Argon2id 哈希、绑定不会让账号自锁，并在同一事务写入审计与事件。
/// 返回的 <see cref="Person"/> 与 <see cref="Membership"/> 是对外投影，手机与邮箱已掩码。
/// </summary>
public interface IIdentityAccounts
{
    /// <summary>创建 Person；任一标识已被其他 Person 占用时返回 IDENTIFIER_IN_USE，不创建半个账号。</summary>
    ValueTask<Person> CreatePersonAsync(CreatePerson request, CancellationToken cancellationToken = default);
    /// <summary>按手机、邮箱或用户名解析 Person；不存在返回 null。</summary>
    ValueTask<Person?> FindPersonAsync(string identifier, CancellationToken cancellationToken = default);
    /// <summary>在当前租户建立成员，经租户配额判定端口计数（同一事务内持计数锁）；同一 Person 重复建立时幂等返回既有成员。</summary>
    ValueTask<Membership> CreateMembershipAsync(CreateMembership request, CancellationToken cancellationToken = default);
    /// <summary>绑定标识（软删除模型下的新增）；已被占用返回 IDENTIFIER_IN_USE。</summary>
    ValueTask<Person> BindIdentifierAsync(Guid personId, IdentifierInput identifier, CancellationToken cancellationToken = default);
    /// <summary>解绑标识（软删除）。解绑最后一个已验证标识，或让账号没有任何标识，返回 LAST_VERIFIED_IDENTIFIER。</summary>
    ValueTask<Person> UnbindIdentifierAsync(Guid personId, IdentifierKind kind, string value, CancellationToken cancellationToken = default);
    /// <summary>换绑：同一事务内新增并软删除旧标识，Person 的 sub 不变。</summary>
    ValueTask<Person> ReplaceIdentifierAsync(Guid personId, IdentifierKind kind, string oldValue, IdentifierInput replacement, CancellationToken cancellationToken = default);
    /// <summary>用户自行改密：校验当前密码、策略与历史不可复用，成功后使既有会话失效。</summary>
    ValueTask ChangePasswordAsync(Guid personId, PasswordChange change, CancellationToken cancellationToken = default);
    /// <summary>运营重置密码：旧凭据失效，可审计，默认强制下次登录改密。</summary>
    ValueTask ResetPasswordAsync(Guid personId, PasswordReset reset, CancellationToken cancellationToken = default);
    /// <summary>变更 Person 状态（active / disabled / locked）；递增身份修订，使既有令牌与会话失效。</summary>
    ValueTask<Person> SetPersonStatusAsync(Guid personId, string status, string reason, CancellationToken cancellationToken = default);
    /// <summary>变更当前租户内成员状态；递增成员修订。</summary>
    ValueTask<Membership> SetMembershipStatusAsync(Guid membershipId, string status, string reason, CancellationToken cancellationToken = default);
    /// <summary>绑定外部身份到当前租户的成员；同租户 connector × subject 已被绑定返回 IDENTIFIER_IN_USE。</summary>
    ValueTask LinkExternalIdentityAsync(ExternalIdentityLink link, CancellationToken cancellationToken = default);
    /// <summary>解绑外部身份（软删除）；若这是 Person 仅剩的登录方式则拒绝。</summary>
    ValueTask UnlinkExternalIdentityAsync(Guid membershipId, string connectorId, string subject, CancellationToken cancellationToken = default);
    /// <summary>
    /// 授权或组织归属变化后，递增当前租户内该 Person 的成员修订（<c>identity_revision</c>），使此前签发的令牌失效：
    /// 旧令牌的修订落后于当前修订，userinfo 等服务端校验据此拒绝，持有者经 refresh 换到带新修订的令牌。
    /// 租户只取自当前已验证的租户上下文；该 Person 在本租户没有成员身份返回 NOT_FOUND。<paramref name="reason"/> 是不超过 64 字符的变更原因。
    /// 返回新的令牌修订（Person 修订 + 成员修订）。授权引擎在 <c>authz.grant.changed.v1</c> 之后调用（PRD 7.5）。
    /// </summary>
    ValueTask<long> BumpIdentityRevisionAsync(Guid personId, string reason, CancellationToken cancellationToken = default);
}
