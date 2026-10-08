using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Qian.Runtime.Errors.AspNetCore;

/// <summary>装配 Qian 结构化错误。</summary>
public static class QianErrorsExtensions
{
    /// <summary>
    /// 注册错误码目录（含平台内置错误码）、<see cref="QianErrorException"/> 的异常处理，
    /// 并把经 <see cref="IProblemDetailsService"/> 写出的一切 ProblemDetails 规范为 ErrorDetail 形状。
    /// 可重复调用：目录只建一次，<paramref name="configure"/> 每次都作用于同一目录。
    /// </summary>
    /// <param name="services">服务集合。</param>
    /// <param name="configure">登记组件自己的错误码。</param>
    public static IServiceCollection AddQianErrors(this IServiceCollection services, Action<ErrorCatalog>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var existing = services.LastOrDefault(d => d.ServiceType == typeof(ErrorCatalog) && !d.IsKeyedService);
        ErrorCatalog catalog;
        if (existing is null)
        {
            catalog = ErrorCatalog.CreateDefault();
            services.AddSingleton(catalog);
        }
        else
        {
            catalog = existing.ImplementationInstance as ErrorCatalog
                ?? throw new InvalidOperationException("ErrorCatalog 已以工厂或类型方式注册；AddQianErrors 需要实例注册，才能在装配期登记错误码。");
        }
        configure?.Invoke(catalog);

        services.AddProblemDetails();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<ProblemDetailsOptions>, NormalizeProblemDetails>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExceptionHandler, QianExceptionHandler>());
        // 已知 Qian / Kun 校验异常先进入封闭契约；不认识的异常仍交给后续宿主处理器。
        var handler = services.Single(d => d.ServiceType == typeof(IExceptionHandler) && d.ImplementationType == typeof(QianExceptionHandler));
        services.Remove(handler);
        services.Insert(0, handler);
        return services;
    }

    /// <summary>
    /// 在管道最前面启用异常处理与状态码兜底：未处理异常、空响应体的 4xx / 5xx（未认证、404、请求体无法解析等）
    /// 都写成 ErrorDetail。等价于 <c>UseExceptionHandler()</c> + <c>UseStatusCodePages()</c>。
    /// </summary>
    public static IApplicationBuilder UseQianErrors(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        return app;
    }

    private sealed class NormalizeProblemDetails : IPostConfigureOptions<ProblemDetailsOptions>
    {
        public void PostConfigure(string? name, ProblemDetailsOptions options)
        {
            var previous = options.CustomizeProblemDetails;
            options.CustomizeProblemDetails = context =>
            {
                previous?.Invoke(context);
                ProblemDetailsNormalizer.Normalize(context);
            };
        }
    }
}
