using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Identity.Client;

/// <summary>
/// 当前请求的主体。规范快照只来自 qian.session 或 S2S 方案验证后放进请求项的 <see cref="Principal"/>，不读请求头或参数；
/// 未配置 Resource 的会话（任意 OIDC 发行方）没有规范身份，只给出会话主体，<see cref="Principal"/> 为空，授权 PEP 因此一律拒绝。
/// </summary>
internal sealed class IdentityClientCurrentUser(IHttpContextAccessor accessor, IServiceProvider services) : ICurrentUser
{
    internal static readonly object PrincipalKey = new();

    public Principal? Principal => accessor.HttpContext is { } http && http.User.Identity?.IsAuthenticated == true ? http.Items[PrincipalKey] as Principal : null;
    public string? UserId => Principal?.Subject ?? Session()?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    public bool IsAuthenticated => UserId is not null;
    public IReadOnlyList<string> Roles => Principal?.Roles ?? Session()?.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray() ?? [];

    // 只有不做规范投影的宿主才把 Kun 会话本身当作已认证主体；配置了 Resource 时访问令牌校验失败即匿名。
    private ClaimsIdentity? Session() =>
        services.GetService<IdentityClientOptions>() is { Resource: null } && accessor.HttpContext?.User.Identity is ClaimsIdentity { IsAuthenticated: true } identity &&
        identity.AuthenticationType == Kun.Identity.AspNetCore.KunIdentityAspNetCoreServiceCollectionExtensions.CookieScheme ? identity : null;
}

/// <summary>
/// Identity seam 的依赖方 Provider：只回答当前主体。人员、成员与会话管理属于身份服务，这里明确拒绝（IDENTITY_OPERATION_UNSUPPORTED），
/// 不伪造空结果；连接器只能注册在身份服务一侧。
/// </summary>
internal sealed class IdentityClientService(IHttpContextAccessor accessor, IServiceProvider services) : IIdentityService, IIdentityConnectorRegistry
{
    private readonly IdentityClientCurrentUser _user = new(accessor, services);

    public Principal? CurrentPrincipal => _user.Principal;
    public IIdentityConnectorRegistry Connectors => this;
    public Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task<IReadOnlyList<Session>> ListSessionsAsync(Guid personId, CancellationToken cancellationToken = default) => throw Unsupported();
    public Task RevokeSessionsAsync(SessionRevocation request, CancellationToken cancellationToken = default) => throw Unsupported();
    public IDisposable Register(string name, IIdentityConnector connector) => throw Unsupported();
    public IIdentityConnector? Find(string name) => null;
    public IReadOnlyList<ConnectorDescriptor> List() => [];

    private static QianErrorException Unsupported() => new(new QianError(IdentityErrors.OperationUnsupported));
}
