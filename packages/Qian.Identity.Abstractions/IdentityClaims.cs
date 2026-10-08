namespace Qian.Identity.Abstractions;

/// <summary>身份 claim 的唯一词汇表；OIDC 用户令牌与冻结 S2S 令牌的字段集合分别列出。</summary>
public static class IdentityClaimNames
{
    /// <summary>发行方。</summary>
    public const string Issuer = "iss";
    /// <summary>受众数组。</summary>
    public const string Audience = "aud";
    /// <summary>用户、服务或 Agent 主体。</summary>
    public const string Subject = "sub";
    /// <summary>统一主体类型。</summary>
    public const string PrincipalKind = "principal_kind";
    /// <summary>租户，平台主体为 null。</summary>
    public const string TenantId = "tenant_id";
    /// <summary>租户成员。</summary>
    public const string MembershipId = "membership_id";
    /// <summary>身份修订。</summary>
    public const string IdentityRevision = "identity_revision";
    /// <summary>项目透传字段。</summary>
    public const string ProjectId = "project_id";
    /// <summary>服务主体的插件标识。</summary>
    public const string PluginId = "plugin_id";
    /// <summary>粗粒度角色，不是权限点集合。</summary>
    public const string Roles = "roles";
    /// <summary>空格分隔的 OAuth scope。</summary>
    public const string Scope = "scope";
    /// <summary>S2S 会话标识；机器主体等于 jti。</summary>
    public const string SessionId = "session_id";
    /// <summary>OIDC 会话标识；换签时映射至 session_id。</summary>
    public const string Sid = "sid";
    /// <summary>认证方法。</summary>
    public const string AuthenticationMethods = "amr";
    /// <summary>认证上下文等级。</summary>
    public const string AuthenticationContext = "acr";
    /// <summary>身份切换上下文的预留字段，S1 不启用。</summary>
    public const string Profile = "profile";
    /// <summary>委托链的预留字段，S1 不签发。</summary>
    public const string Actor = "act";
    /// <summary>签发时刻。</summary>
    public const string IssuedAt = "iat";
    /// <summary>生效时刻。</summary>
    public const string NotBefore = "nbf";
    /// <summary>过期时刻。</summary>
    public const string ExpiresAt = "exp";
    /// <summary>令牌标识。</summary>
    public const string TokenId = "jti";

    /// <summary>与 contracts/v0/s2s-claims.schema.json 的 required 精确一致。</summary>
    public static IReadOnlyList<string> S2sRequired { get; } = Array.AsReadOnly(new[] { Issuer, Audience, Subject, PrincipalKind, TenantId, Roles, Scope, SessionId, IssuedAt, NotBefore, ExpiresAt, TokenId });
    /// <summary>冻结 S2S 契约允许的附加字段，不能加入 sid、amr、acr 或 profile。</summary>
    public static IReadOnlyList<string> S2sOptional { get; } = Array.AsReadOnly(new[] { MembershipId, IdentityRevision, ProjectId, PluginId, Actor });
    /// <summary>用户身份令牌词汇；其中 profile 与 act 是未启用的预留位置。</summary>
    public static IReadOnlyList<string> IdentityToken { get; } = Array.AsReadOnly(new[] { Subject, TenantId, MembershipId, Scope, IdentityRevision, Sid, AuthenticationMethods, AuthenticationContext, Profile, Actor });
}
