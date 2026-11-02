using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Qian.Runtime;

namespace Qian.Tenancy;

/// <summary>平台运营维护租户管理员的 seam（S1-TEN-010）；调用方必须经平台超管旁路授权。</summary>
public static class TenantAdministration
{
    /// <summary>由 TenancyPlugin 提供的管理员维护端口。</summary>
    public static readonly ServiceKey Key = new("TenancyAdministration");
}

/// <summary>
/// 管理员维护：选择或新建管理员账号、替换管理员、重置管理员密码。账号、成员与凭据都经 qian.identity 的端口读写，
/// 本组件只保存成员指针；拒绝租户上下文，不能直接作为无授权接口。
/// </summary>
public interface ITenantAdministration
{
    /// <summary>按手机号、邮箱或用户名查找可任租户管理员的账号；不存在、已清除或具备平台访问资格的返回 null。</summary>
    ValueTask<TenantAdminAccount?> FindAccountAsync(TenantAdminAccountLookup request, CancellationToken cancellationToken = default);
    /// <summary>新建自然人：一个登录标识加初始密码，下次登录强制改密；不建立任何成员身份。</summary>
    ValueTask<TenantAdminAccount> CreateAccountAsync(CreateTenantAdminAccount request, CancellationToken cancellationToken = default);
    /// <summary>读取租户当前管理员；成员已不可解析时不列出。</summary>
    ValueTask<TenantAdminList> GetAsync(Guid id, CancellationToken cancellationToken = default);
    /// <summary>替换租户管理员（S1 单管理员，Q12）：新管理员得到 tenant.owner，原管理员撤销 tenant.owner 与管理员标志、保留成员身份；同一请求可重试收敛。</summary>
    ValueTask<TenantAdminList> ReplaceAsync(Guid id, ReplaceTenantAdmins request, string actor, CancellationToken cancellationToken = default);
    /// <summary>重置当前管理员的密码：旧密码立即失效、下次登录强制改密、吊销该自然人的全部会话；成员不是当前管理员时返回 NOT_FOUND。</summary>
    ValueTask ResetPasswordAsync(Guid id, Guid membershipId, ResetTenantAdminPassword request, string actor, CancellationToken cancellationToken = default);
}

/// <summary>登录标识的类型。</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TenantAdminIdentifierKind>))]
public enum TenantAdminIdentifierKind
{
    /// <summary>用户名：字母开头，不得形似手机号或邮箱。</summary>
    [JsonStringEnumMemberName("username")] Username,
    /// <summary>手机号。</summary>
    [JsonStringEnumMemberName("phone")] Phone,
    /// <summary>邮箱。</summary>
    [JsonStringEnumMemberName("email")] Email
}

/// <summary>登录标识的掩码视图；原值不出身份组件。</summary>
public sealed record TenantAdminIdentifier(TenantAdminIdentifierKind Kind, string Masked, bool Verified);

/// <summary>可任租户管理员的账号（自然人）投影；SubjectId 即令牌的 sub，也是开户 administrator.subjectId 的取值。</summary>
public sealed record TenantAdminAccount(Guid SubjectId, string DisplayName, string Status, IReadOnlyList<TenantAdminIdentifier> Identifiers);

/// <summary>按登录标识查找账号；标识放请求体，不进 URL 与访问日志。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TenantAdminAccountLookup([property: MinLength(1), MaxLength(254)] string Identifier);

/// <summary>新建管理员账号；初始密码只交给身份组件哈希保存，下次登录强制修改。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateTenantAdminAccount(
    [property: MinLength(1), MaxLength(200)] string DisplayName,
    TenantAdminIdentifierKind IdentifierKind,
    [property: MinLength(1), MaxLength(254)] string Identifier,
    [property: MinLength(8), MaxLength(1024)] string InitialPassword)
{
    /// <summary>不输出初始密码。</summary>
    public override string ToString() => "CreateTenantAdminAccount(redacted)";
}

/// <summary>租户管理员：成员指针加成员与自然人的掩码投影；Status 为成员状态。</summary>
public sealed record TenantAdmin(Guid MembershipId, Guid SubjectId, string DisplayName, string Account, string Status, IReadOnlyList<TenantAdminIdentifier> Identifiers);

/// <summary>租户管理员列表；S1 单管理员（Q12），至多一项。</summary>
public sealed record TenantAdminList(IReadOnlyList<TenantAdmin> Items);

/// <summary>指定一位管理员：已存在的自然人。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TenantAdminDesignation(Guid SubjectId);

/// <summary>替换租户管理员；S1 恰好一项（单管理员，Q12），多管理员与邀请在 S3。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ReplaceTenantAdmins([property: MinLength(1), MaxLength(1)] IReadOnlyList<TenantAdminDesignation> Items);

/// <summary>运营重置管理员密码；新密码是临时密码，下次登录强制修改。</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ResetTenantAdminPassword([property: MinLength(8), MaxLength(1024)] string NewPassword)
{
    /// <summary>不输出新密码。</summary>
    public override string ToString() => "ResetTenantAdminPassword(redacted)";
}
