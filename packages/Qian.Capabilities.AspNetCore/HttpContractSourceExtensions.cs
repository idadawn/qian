using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Qian.Capabilities.AspNetCore;

/// <summary>登记原生端点描述适配，不建立另一份业务路由或权限目录。</summary>
public static class HttpContractSourceExtensions
{
    /// <summary>读取 ApiExplorer 的实际 Minimal API/MVC 描述与组件绑定元数据。</summary>
    public static IServiceCollection AddQianHttpContracts(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddEndpointsApiExplorer(); services.AddHttpContextAccessor();
        services.TryAddSingleton<IHttpContractSource, AspNetCoreHttpContractSource>();
        return services;
    }
}
