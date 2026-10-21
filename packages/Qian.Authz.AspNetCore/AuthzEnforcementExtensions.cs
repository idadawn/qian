using Grpc.AspNetCore.Server;
using Kun.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Qian.Authz.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz.AspNetCore;

/// <summary>
/// HTTP / gRPC 强制执行点（PEP）的宿主装配：端点以 <see cref="RequireQianPermission{TBuilder}"/> 声明权限点（同时写进 OpenAPI 的 x-qian-permission），
/// 判定、identity_revision 比对与决策审计都经 qian.authz 的执行端口（<see cref="IAuthzEnforcement"/>），与命令管道 PEP 同一结论。
/// 未声明权限点的组件端点在生产模式缺省拒绝（403 并记录），开发模式放行并告警。
/// </summary>
public static class AuthzEnforcementExtensions
{
    /// <summary>
    /// 登记 PEP：<paramref name="source"/> 返回当前请求可注入 <c>AuthzEnforcement</c> 的插件上下文（通常是 authz 组件按请求租户收窄的上下文）。
    /// 宿主须先装配身份（<c>ICurrentUser</c>）、可信租户边界与 ErrorDetail 中间件；gRPC 宿主把 <see cref="AuthzGrpcServerInterceptor"/> 加进拦截器。
    /// </summary>
    public static IServiceCollection AddQianAuthzEnforcement(this IServiceCollection services, Func<HttpContext, IPluginContext> source, Action<AuthzEnforcementOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(source);
        if (services.Any(service => service.ServiceType == typeof(AuthzEnforcementSource))) throw new InvalidOperationException("授权 PEP 的上下文来源不能重复登记。");
        var options = new AuthzEnforcementOptions(); configure?.Invoke(options);
        services.AddSingleton(new AuthzEnforcementSource(source, options));
        services.AddAuthorization();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAuthorizationHandler, QianPermissionHandler>());
        // 拒绝写成 ErrorDetail（403 AUTHZ_DENIED / 401 UNAUTHENTICATED / 404 NOT_FOUND），其余结果交给默认处理器。
        services.TryAddSingleton<AuthorizationMiddlewareResultHandler>();
        services.AddSingleton<IAuthorizationMiddlewareResultHandler, QianAuthzResultHandler>();
        services.TryAddSingleton<AuthzGrpcServerInterceptor>();
        return services;
    }

    /// <summary>给端点声明所需权限点（R9 全名）并接入 PEP；多次调用表示全部权限都须放行。契约同时得到 401 / 403 的 ErrorDetail 响应声明。</summary>
    public static TBuilder RequireQianPermission<TBuilder>(this TBuilder builder, string permission) where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        if (!PermissionCode.IsValid(permission)) throw new ArgumentException("权限点必须是 <plugin-id>:<resource>:<action> 全名。", nameof(permission));
        builder.Add(endpoint =>
        {
            endpoint.Metadata.Add(new RequiresPermissionAttribute(permission));
            if (!endpoint.Metadata.OfType<IProducesResponseTypeMetadata>().Any(item => item.StatusCode == StatusCodes.Status403Forbidden))
            {
                endpoint.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status401Unauthorized, typeof(ErrorDetail), [ErrorDetailJson.MediaType]));
                endpoint.Metadata.Add(new ProducesResponseTypeMetadata(StatusCodes.Status403Forbidden, typeof(ErrorDetail), [ErrorDetailJson.MediaType]));
            }
        });
        builder.RequireAuthorization(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().AddRequirements(new QianPermissionRequirement(permission)).Build());
        return builder;
    }

    /// <summary>
    /// 放在 UseAuthorization 之后：带组件端点绑定（<c>PluginEndpointBinding</c>）却没有声明权限点的端点，在生产模式缺省拒绝并记录决策审计；
    /// 开发模式记一条警告后放行，便于本地调试。匿名端点（<c>AllowAnonymous</c>）不在范围内。
    /// </summary>
    public static IApplicationBuilder UseQianAuthzEnforcement(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<UndeclaredPermissionMiddleware>();
    }

    /// <summary>把 gRPC 拦截器加进服务选项；放在认证与租户拦截器之后。</summary>
    public static GrpcServiceOptions AddQianAuthzInterceptor(this GrpcServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Interceptors.Add<AuthzGrpcServerInterceptor>();
        return options;
    }
}

/// <summary>PEP 的宿主选项。</summary>
public sealed class AuthzEnforcementOptions
{
    /// <summary>是否按生产模式对未声明权限点的组件端点缺省拒绝；null 时取宿主环境（Production 即拒绝）。</summary>
    public bool? Production { get; set; }
}

/// <summary>宿主登记的 PEP 上下文来源与选项；由 <see cref="AuthzEnforcementExtensions.AddQianAuthzEnforcement"/> 创建，适配器经 DI 取用。</summary>
public sealed class AuthzEnforcementSource
{
    internal AuthzEnforcementSource(Func<HttpContext, IPluginContext> source, AuthzEnforcementOptions options) { Source = source; Options = options; }
    internal Func<HttpContext, IPluginContext> Source { get; }
    internal AuthzEnforcementOptions Options { get; }
    internal bool IsProduction(IServiceProvider services) => Options.Production ?? services.GetService<IHostEnvironment>()?.IsProduction() ?? true;
}

internal sealed record QianPermissionRequirement(string Permission) : IAuthorizationRequirement;
