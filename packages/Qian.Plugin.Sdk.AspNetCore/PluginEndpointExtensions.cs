using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;

namespace Qian.Plugin.Sdk.AspNetCore;

/// <summary>HTTP 组合根的显式装配，不在 SDK 内创建另一棵运行时或替身注册表。</summary>
public static class PluginEndpointExtensions
{
    /// <summary>登记当前平台宿主的 Registry 上下文工厂。</summary>
    public static IServiceCollection AddQianPluginEndpoints(this IServiceCollection services, Func<IServiceProvider, IPluginContext> registryContext)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(registryContext);
        if (services.Any(service => service.ServiceType == typeof(PluginEndpointOptions))) throw new InvalidOperationException("插件端点的注册表来源不能重复登记。");
        return services.AddSingleton(new PluginEndpointOptions(registryContext));
    }
    /// <summary>放在 UseRouting、认证和可信租户边界之后，endpoint 执行之前。</summary>
    public static IApplicationBuilder UseQianPluginEndpoints(this IApplicationBuilder app)
    { ArgumentNullException.ThrowIfNull(app); return app.UseMiddleware<PluginEnabledMiddleware>(); }
    /// <summary>标注 Minimal API 的实际绑定，并附加拒绝缺失/错误排序中间件的 endpoint filter。</summary>
    public static RouteHandlerBuilder RequireQianPlugin(this RouteHandlerBuilder builder, PluginEndpointBinding binding)
    {
        ArgumentNullException.ThrowIfNull(builder); ArgumentNullException.ThrowIfNull(binding);
        builder.WithMetadata(binding);
        return builder.AddEndpointFilter(async (context, next) =>
        {
            var tenant = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            if (!PluginEnabledMiddleware.HasEvidence(context.HttpContext, binding, tenant)) throw PluginEnabledMiddleware.NotFound();
            return await next(context).ConfigureAwait(false);
        });
    }
}
