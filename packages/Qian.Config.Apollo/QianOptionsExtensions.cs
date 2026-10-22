using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Qian.Config.Apollo;

/// <summary>
/// 强类型配置的注册入口：重载先构造并校验候选值，失败保留上一份有效值、计数 qian.config.reload.failed 并发出 config/reload-failed。
/// 绑定由调用方以委托给出（可用配置绑定源生成器或手写赋值），本入口不调用反射绑定，可用于 AOT profile。
/// </summary>
public static class QianOptionsExtensions
{
    /// <summary>
    /// 登记 <typeparamref name="T"/> 绑定到 <paramref name="section"/>：<paramref name="bind"/> 收到该配置节；校验器照常以 IValidateOptions 登记
    /// （DataAnnotations 可用 OptionsValidator 源生成器）。critical 为 true 时宿主启动即校验，否则首次读取时校验；给出 <paramref name="schema"/> 时同时登记
    /// JSON Schema 2020-12 导出。同一类型只能登记一次。
    /// </summary>
    public static OptionsBuilder<T> AddQianOptions<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicParameterlessConstructor)] T>(
        this IServiceCollection services, string section, Action<IConfiguration, T> bind, bool critical = true,
        JsonTypeInfo<T>? schema = null, string? plugin = null) where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(section);
        ArgumentNullException.ThrowIfNull(bind);
        if (plugin is not null) ArgumentException.ThrowIfNullOrWhiteSpace(plugin);
        if (services.Any(service => service.ServiceType == typeof(OptionsMarker<T>))) throw new InvalidOperationException("同一配置类型不能重复登记。");
        var registration = new OptionsRegistration(typeof(T).FullName ?? typeof(T).Name, section, plugin);
        services.AddSingleton(new OptionsMarker<T>());
        services.TryAddSingleton<ConfigReloadNotifier>();
        services.AddSingleton<IOptionsChangeTokenSource<T>>(sp => new SectionChangeTokenSource<T>(sp.GetRequiredService<IConfiguration>().GetSection(section)));
        services.AddSingleton<IOptionsMonitor<T>>(sp => new ValidatedOptionsMonitor<T>(sp.GetRequiredService<IOptionsFactory<T>>(),
            sp.GetServices<IOptionsChangeTokenSource<T>>(), registration, sp.GetRequiredService<ConfigReloadNotifier>(),
            sp.GetService<ILoggerFactory>()?.CreateLogger("Qian.Config.Options") ?? NullLogger.Instance).Start());
        if (schema is not null) services.AddSingleton(new QianOptionsSchema(section, registration.OptionsType, schema));
        var builder = services.AddOptions<T>().Configure<IConfiguration>((options, root) => bind(root.GetSection(section), options));
        return critical ? builder.ValidateOnStart() : builder;
    }

    private sealed class OptionsMarker<T>;

    private sealed class SectionChangeTokenSource<T>(IConfiguration section) : IOptionsChangeTokenSource<T>
    {
        public string Name => Options.DefaultName;
        public IChangeToken GetChangeToken() => section.GetReloadToken();
    }
}

/// <summary>由源生成 JSON 元数据导出的强类型配置契约（与 Kun 的 OptionsSchema 等价），不含任何运行时配置值；宿主以 IEnumerable 取得全部登记。</summary>
public sealed class QianOptionsSchema
{
    private readonly JsonTypeInfo _typeInfo;
    internal QianOptionsSchema(string section, string optionsType, JsonTypeInfo typeInfo)
    {
        Section = section; OptionsType = optionsType; _typeInfo = typeInfo;
    }
    /// <summary>该配置绑定的节。</summary>
    public string Section { get; }
    /// <summary>配置类型全名。</summary>
    public string OptionsType { get; }
    /// <summary>每次返回独立的 JSON Schema 2020-12 节点，调用方修改不会影响下一次导出。</summary>
    public JsonObject Export()
    {
        var schema = _typeInfo.GetJsonSchemaAsNode(new JsonSchemaExporterOptions { TreatNullObliviousAsNonNullable = true }).AsObject();
        schema["$schema"] = "https://json-schema.org/draft/2020-12/schema";
        return schema;
    }
}
