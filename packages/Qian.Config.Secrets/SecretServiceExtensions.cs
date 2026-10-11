using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime.Errors;

namespace Qian.Config.Secrets;

/// <summary>独立宿主与平台共用的安全解析装配。</summary>
public static class SecretServiceExtensions
{
    /// <summary>登记受控文件存储与不可变权限；宿主必须显式提供 ISecretAccessObserver。</summary>
    public static IServiceCollection AddQianSecrets(this IServiceCollection services, SecretResolverOptions options)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(options);
        services.AddSingleton<ISecretResolverFactory>(sp => new SecretResolverFactory(options, sp.GetRequiredService<ISecretAccessObserver>()));
        var existing = services.LastOrDefault(service => service.ServiceType == typeof(ErrorCatalog));
        var catalog = existing?.ImplementationInstance as ErrorCatalog;
        if (existing is not null && catalog is null) throw new InvalidOperationException("ErrorCatalog 必须按实例登记。");
        if (catalog is null) { catalog = ErrorCatalog.CreateDefault(); services.AddSingleton(catalog); }
        SecretErrors.Register(catalog);
        return services;
    }
}
