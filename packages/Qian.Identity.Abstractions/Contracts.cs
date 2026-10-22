using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Abstractions;

/// <summary>Identity seam 的所有者。</summary>
public static class IdentityDefinition
{
    /// <summary>可替换的身份服务键。</summary>
    public static readonly ServiceKey Key = new("Identity");
    /// <summary>账号管理端口（<see cref="IIdentityAccounts"/>）的服务键，与 <see cref="Key"/> 同由身份 Provider 提供。</summary>
    public static readonly ServiceKey AccountsKey = new("IdentityAccounts");
    /// <summary>统一登录流水线（<see cref="ISignInPipeline"/>）的服务键。</summary>
    public static readonly ServiceKey SignInKey = new("IdentitySignIn");
    /// <summary>认证因子注册表（<see cref="IAuthenticatorFactorRegistry"/>）的服务键。</summary>
    public static readonly ServiceKey FactorsKey = new("IdentityFactors");
    /// <summary>连接器注册表子服务（R38 注册表 seam）：具名注册写作 <c>Identity.Connectors:&lt;name&gt;</c>，可出现在清单 provides / inject 中。</summary>
    public static readonly ServiceKey ConnectorsKey = new("Identity.Connectors");
    /// <summary>在当前 scope 中解析身份 Provider。</summary>
    public static IIdentityService Identity(this IPluginContext context) => context.Inject<IIdentityService>(Key);
    /// <summary>在当前 scope 中解析账号管理端口。</summary>
    public static IIdentityAccounts IdentityAccounts(this IPluginContext context) => context.Inject<IIdentityAccounts>(AccountsKey);
    /// <summary>在当前 scope 中解析统一登录流水线。</summary>
    public static ISignInPipeline IdentitySignIn(this IPluginContext context) => context.Inject<ISignInPipeline>(SignInKey);
    /// <summary>在当前 scope 中解析认证因子注册表。</summary>
    public static IAuthenticatorFactorRegistry IdentityFactors(this IPluginContext context) => context.Inject<IAuthenticatorFactorRegistry>(FactorsKey);
    /// <summary>
    /// 绑定当前上下文的连接器注册表：连接器组件在 Apply 中 <c>ctx.IdentityConnectors().Register("oidc", connector)</c>，
    /// 注册落在本条目的 scope 与效果栈里（卸载即撤销），租户 scope 的同名注册遮蔽全局项；查找与列表按当前 scope 的可见性返回。
    /// </summary>
    public static IIdentityConnectorRegistry IdentityConnectors(this IPluginContext context) =>
        new PluginConnectorRegistry(context ?? throw new ArgumentNullException(nameof(context)));
}
/// <summary>基于 Kun 当前用户端口，补充平台身份快照；匿名时相关属性为空。</summary>
public interface ICurrentUser : Kun.Core.Security.ICurrentUser
{
    /// <summary>已经过身份校验的主体；不从请求头推导。</summary>
    Principal? Principal { get; }
    /// <summary>给 Kun 鉴权端口提供实际主体种类，不让服务身份继承旧适配器的 user 默认值。</summary>
    string Kun.Core.Security.ICurrentUser.PrincipalKind => Principal?.Kind switch
    {
        global::Qian.Identity.Abstractions.PrincipalKind.User => "user",
        global::Qian.Identity.Abstractions.PrincipalKind.Service => "service",
        global::Qian.Identity.Abstractions.PrincipalKind.Agent => "agent",
        _ => string.Empty
    };
}
/// <summary>明确要求经过认证的用户视图；Provider 保证 IsAuthenticated 为 true。</summary>
public interface IAuthenticatedUser : ICurrentUser
{
    /// <summary>有效主体快照。</summary>
    new Principal Principal { get; }
    /// <summary>稳定用户或客户端标识。</summary>
    new string UserId { get; }
}
/// <summary>身份 seam，服务端与客户端 Provider 共用。</summary>
public interface IIdentityService
{
    /// <summary>当前有效主体；匿名为 null。</summary>
    Principal? CurrentPrincipal { get; }
    /// <summary>连接器注册表子服务：按当前租户上下文的 scope 解析（未挂载与不存在同样查不到）。连接器组件注册请用 <c>ctx.IdentityConnectors()</c>。</summary>
    IIdentityConnectorRegistry Connectors { get; }
    /// <summary>读取 person 公共投影。</summary>
    Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    /// <summary>读取当前租户的成员投影。</summary>
    Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default);
    /// <summary>读取受权限保护的会话列表。</summary>
    Task<IReadOnlyList<Session>> ListSessionsAsync(Guid personId, CancellationToken cancellationToken = default);
    /// <summary>幂等吊销指定会话或 person 的全部会话。</summary>
    Task RevokeSessionsAsync(SessionRevocation request, CancellationToken cancellationToken = default);
}
/// <summary>连接器注册表；实现必须使用同名独占、scope 遮蔽与可撤销注册。</summary>
public interface IIdentityConnectorRegistry
{
    /// <summary>注册连接器，返回卸载句柄；名称必须等于描述的 Id，清单不合法（见 <see cref="ConnectorManifest"/>）时抛 INPUT_INVALID。</summary>
    IDisposable Register(string name, IIdentityConnector connector);
    /// <summary>查找当前 scope 可见的连接器。</summary>
    IIdentityConnector? Find(string name);
    /// <summary>列出当前 scope 可见的描述。</summary>
    IReadOnlyList<ConnectorDescriptor> List();
}
/// <summary>自然人投影，不包含凭据或历史密码哈希。Email 与 Phone 是掩码值（如 <c>138****5678</c>、<c>a***@example.com</c>），原值不出身份组件。</summary>
public sealed record Person(Guid Id, string DisplayName, string? UserName, string? Email, string? Phone, long IdentityRevision)
{
    /// <summary>账号状态，见 <see cref="AccountStatuses"/>。</summary>
    public string Status { get; init; } = AccountStatuses.Active;
    /// <summary>是否具备平台控制台访问资格。</summary>
    public bool PlatformAccess { get; init; }
    /// <summary>全部有效标识的掩码视图。</summary>
    public IReadOnlyList<IdentifierView> Identifiers { get; init; } = [];
}
/// <summary>租户成员投影。</summary>
public sealed record Membership(Guid Id, Guid PersonId, Guid TenantId, string DisplayName, string Status, bool IsTenantAdministrator)
{
    /// <summary>租户内账号，租户内唯一、不可改。</summary>
    public string Account { get; init; } = "";
    /// <summary>成员的身份修订；令牌的 identity_revision 是 Person 修订与成员修订之和。</summary>
    public long IdentityRevision { get; init; }
}
/// <summary>会话投影；日志不得打印原始 ID。</summary>
public sealed record Session(Guid Id, Guid PersonId, Guid? MembershipId, string ClientId, string DeviceType, DateTimeOffset ExpiresAt, long IdentityRevision)
{
    /// <inheritdoc />
    public override string ToString() => "Session(redacted)";
}
/// <summary>客户端登记契约，Agent 类型仅预留。</summary>
public sealed record Client(string Id, ClientType Type, IReadOnlyList<Uri> RedirectUris, IReadOnlyList<string> Scopes, IReadOnlyList<string> Resources, Guid? OwnerPersonId);
/// <summary>会话吊销请求；SessionId 为 null 时吊销 person 全部会话。</summary>
public sealed record SessionRevocation(Guid PersonId, Guid? SessionId, string Reason, string IdempotencyKey)
{
    /// <inheritdoc />
    public override string ToString() => "SessionRevocation(redacted)";
}
/// <summary>
/// 连接器机器可读描述，即连接器清单（connector.manifest）：协议、支持终端（<see cref="ConnectorTerminals"/>）、配置 JSON Schema 与回调模板。
/// 配置里的敏感字段在 Schema 中标 <c>x-qian-sensitive</c>，取值只能是 secretRef；描述本身不含任何配置值。校验规则见 <see cref="ConnectorManifest"/>。
/// </summary>
public sealed record ConnectorDescriptor(string Id, string DisplayName, string Protocol, IReadOnlyList<string> Terminals, JsonElement ConfigSchema)
{
    /// <summary>回调路由模板，固定为 <see cref="ConnectorManifest.CallbackTemplate"/>：回调由身份 Provider 统一接收。</summary>
    public string CallbackTemplate { get; init; } = ConnectorManifest.CallbackTemplate;
}
/// <summary>身份服务创建并校验 state/nonce，连接器只处理外部协议。</summary>
public sealed record ConnectorContext(string ClientId, Guid? TenantId, Uri CallbackUri, string State, string Nonce)
{
    /// <summary>本次挑战的 PKCE code_verifier（RFC 7636，43–128 位）；由身份服务按流程生成并在回调时原样交回，不支持 PKCE 的协议忽略即可。</summary>
    public string? CodeVerifier { get; init; }
    /// <inheritdoc />
    public override string ToString() => "ConnectorContext(redacted)";
}
/// <summary>连接器挑战；URL 只包含协议所需的短期参数。</summary>
public sealed record ChallengeResult(Uri? RedirectUri, string? ChallengeId)
{
    /// <inheritdoc />
    public override string ToString() => "ChallengeResult(redacted)";
}
/// <summary>外部回调的短期参数，由身份服务检查关联状态后传入。</summary>
public sealed record ConnectorCallback(ConnectorContext Context, IReadOnlyDictionary<string, string> Parameters)
{
    /// <inheritdoc />
    public override string ToString() => "ConnectorCallback(redacted)";
}
/// <summary>
/// 标准化的外部断言；绑定、建号、合并属于身份 Provider，不属于连接器。<c>Profile</c> 只放展示用的资料（name、email 等），
/// 身份服务对外只给掩码后的值。
/// </summary>
public sealed record ExternalAssertion(string Issuer, string Subject, string? UnionId, JsonElement Profile, IReadOnlyList<string> Amr)
{
    /// <inheritdoc />
    public override string ToString() => "ExternalAssertion(redacted)";
}
/// <summary>
/// 上游连接器 SPI；方法名按组件模型 §6.3，与 PRD 7.4.2 的 <c>BeginChallengeAsync</c> / <c>HandleCallbackAsync</c> 一一对应，
/// 免登（<c>ExchangeImplicitAsync</c>）与目录同步拆为可选扩展接口。连接器无状态：state、nonce、PKCE verifier 由身份服务生成、保存并校验，
/// 回调只在关联状态核对通过后才交给连接器。协议失败抛 <see cref="QianErrorException"/>：nonce 不符用
/// <see cref="IdentityErrors.ConnectorStateInvalid"/>，上游响应无效或签名不符用 BAD_GATEWAY，上游超时用 GATEWAY_TIMEOUT。
/// </summary>
public interface IIdentityConnector
{
    /// <summary>连接器能力与配置契约（清单）。</summary>
    ConnectorDescriptor Descriptor { get; }
    /// <summary>开始外部登录挑战（PRD 的 BeginChallengeAsync）：返回带 state / nonce / PKCE challenge 的上游跳转地址。</summary>
    Task<ChallengeResult> BeginSignInAsync(ConnectorContext context, CancellationToken cancellationToken = default);
    /// <summary>完成协议回调并返回断言（PRD 的 HandleCallbackAsync），不直接签发平台令牌；上游的 error 回调由身份服务先行处理，不会传到这里。</summary>
    Task<ExternalAssertion> CompleteSignInAsync(ConnectorCallback callback, CancellationToken cancellationToken = default);
}
/// <summary>支持工作台免登的可选连接器端口。</summary>
public interface IImplicitIdentityConnector : IIdentityConnector
{
    /// <summary>交换短期 code；不支持时返回 null。</summary>
    Task<ExternalAssertion?> ExchangeImplicitAsync(string code, ConnectorContext context, CancellationToken cancellationToken = default);
}
/// <summary>S3 可选目录同步端口，不使 S1 连接器被迫实现占位方法。</summary>
public interface IDirectorySyncConnector : IIdentityConnector
{
    /// <summary>分页同步外部断言投影。</summary>
    Task<IReadOnlyList<ExternalAssertion>> SyncDirectoryAsync(string? cursor, CancellationToken cancellationToken = default);
}
/// <summary>认证因子描述。</summary>
public sealed record FactorDescriptor(string Id, string DisplayName, string Amr, bool RequiresChallenge);
/// <summary>认证过程的上下文，不包含密码或 OTP。</summary>
public sealed record AuthenticationFactorContext(Guid PersonId, string ClientId, Guid? TenantId, string CorrelationId);
/// <summary>因子挑战投影，不包含验证答案。</summary>
public sealed record AuthenticationChallenge(string Id, DateTimeOffset ExpiresAt, JsonElement PublicParameters);
/// <summary>因子验证结果，平台签发与会话策略由身份服务统一执行。</summary>
public sealed record AuthenticationResult(bool Succeeded, QianError? Error, IReadOnlyList<string> Amr, string? Acr);
/// <summary>临时认证材料，不可 JSON 序列化或打印，使用后清零。</summary>
public sealed class AuthenticationProof : IDisposable
{
    private byte[]? _data;
    /// <summary>复制调用方材料，由本对象管理生命周期。</summary>
    public AuthenticationProof(ReadOnlySpan<byte> data) => _data = data.ToArray();
    /// <summary>供因子读取的临时数据。</summary>
    [JsonIgnore] public ReadOnlyMemory<byte> Data => _data ?? throw new ObjectDisposedException(nameof(AuthenticationProof));
    /// <summary>不输出材料。</summary>
    public override string ToString() => "AuthenticationProof(redacted)";
    /// <summary>清零敏感缓冲，释放后不可再读取。</summary>
    public void Dispose() { if (_data is not null) { CryptographicOperations.ZeroMemory(_data); _data = null; } }
}
/// <summary>
/// 认证因子注册表：与连接器注册表同一模式——同名独占、可撤销。内置的 <c>password</c> 因子由身份 Provider 占用；
/// 短信 OTP、TOTP 等因子由各自组件在 Apply 时注册，句柄交给 <c>ctx.Effect</c>，卸载即撤销。
/// </summary>
public interface IAuthenticatorFactorRegistry
{
    /// <summary>注册因子，标识取自 <see cref="FactorDescriptor.Id"/>；已被占用时抛 REGISTRATION_CONFLICT。</summary>
    IDisposable Register(IAuthenticatorFactor factor);
    /// <summary>按标识查找当前可见的因子。</summary>
    IAuthenticatorFactor? Find(string id);
    /// <summary>列出当前可见因子的描述。</summary>
    IReadOnlyList<FactorDescriptor> List();
}
/// <summary>可插拔认证因子，不绑定密码、短信或 TOTP 的实现。</summary>
public interface IAuthenticatorFactor
{
    /// <summary>因子词汇与挑战需求。</summary>
    FactorDescriptor Descriptor { get; }
    /// <summary>创建挑战。</summary>
    Task<AuthenticationChallenge?> BeginAsync(AuthenticationFactorContext context, CancellationToken cancellationToken = default);
    /// <summary>验证临时材料。</summary>
    Task<AuthenticationResult> VerifyAsync(AuthenticationProof proof, AuthenticationFactorContext context, CancellationToken cancellationToken = default);
}
