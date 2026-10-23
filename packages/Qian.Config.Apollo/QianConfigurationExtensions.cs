using Kun.Configuration.Apollo;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

/// <summary>固定配置源顺序，复用 Kun 的协议、缓存、签名与重载实现。</summary>
public static class QianConfigurationExtensions
{
    /// <summary>向空构建器加入 appsettings、Apollo、Secret 文件、环境变量和命令行。</summary>
    public static IConfigurationBuilder AddQianConfigurationSources(this IConfigurationBuilder builder, QianConfigurationOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        if (builder.Sources.Count != 0) throw Invalid("configuration.sources");
        ArgumentException.ThrowIfNullOrWhiteSpace(options.BasePath);
        var basePath = Path.GetFullPath(options.BasePath);
        if (options.EnvironmentName is not null && (options.EnvironmentName.Length == 0 ||
            options.EnvironmentName.IndexOfAny(['/', '\\']) >= 0)) throw Invalid("EnvironmentName");
        AddFiles(builder, options, basePath, reload: true);
        if (options.UseApollo)
        {
            var bootstrapBuilder = new ConfigurationBuilder();
            AddFiles(bootstrapBuilder, options, basePath, reload: false);
            AddOverrides(bootstrapBuilder, options, reload: false);
            var bootstrap = bootstrapBuilder.Build();
            using var owner = (IDisposable)bootstrap;
            var settings = ReadApollo(bootstrap, options, basePath);
            builder.Add(new QianApolloSource(settings.Create, settings.Environment, settings.Cluster, settings.AppId, options));
        }
        AddOverrides(builder, options, reload: true);
        return builder;
    }

    /// <summary>向独立宿主登记同一配置根与只读服务；配置根的释放仍由创建方负责。</summary>
    public static IServiceCollection AddQianConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        var entry = services.LastOrDefault(service => service.ServiceType == typeof(ErrorCatalog));
        var catalog = entry?.ImplementationInstance as ErrorCatalog;
        if (entry is not null && catalog is null) throw new InvalidOperationException("ErrorCatalog 必须以实例方式登记。");
        if (catalog is null) { catalog = ErrorCatalog.CreateDefault(); services.AddSingleton(catalog); }
        ConfigErrors.Register(catalog);
        services.AddSingleton(configuration);
        services.TryAddSingleton<ConfigReloadNotifier>();
        services.AddSingleton<IFeatureFlags>(sp => new FeatureFlagsService(configuration, sp.GetRequiredService<ConfigReloadNotifier>(),
            sp.GetService<ILoggerFactory>()?.CreateLogger("Qian.Config.Flags") ?? NullLogger.Instance));
        services.AddSingleton<IConfigService>(sp => new ConfigurationService(configuration, sp.GetRequiredService<IFeatureFlags>(), sp.GetService<ISecretResolverFactory>()));
        return services;
    }

    private static void AddFiles(IConfigurationBuilder builder, QianConfigurationOptions options, string basePath, bool reload)
    {
        builder.SetBasePath(basePath).AddJsonFile("appsettings.json", optional: true, reloadOnChange: reload);
        if (options.EnvironmentName is { } environment)
            builder.AddJsonFile("appsettings." + environment + ".json", optional: true, reloadOnChange: reload);
    }

    private static void AddOverrides(IConfigurationBuilder builder, QianConfigurationOptions options, bool reload)
    {
        if (options.SecretDirectory is { } secrets)
            builder.AddKeyPerFile(Path.GetFullPath(secrets, Path.GetFullPath(options.BasePath)), optional: false, reloadOnChange: reload);
        builder.AddEnvironmentVariables(options.EnvironmentPrefix);
        builder.AddCommandLine(options.Arguments.ToArray());
    }

    // 校验委托绑定到每个 provider 自己的发布记录器：扫描与追加校验的结论原样返回给 Kun，同时记成「接受 / 拒绝」。
    private static (Func<ApolloReleaseTracker, ApolloOptions> Create, string Environment, string Cluster, string AppId) ReadApollo(
        IConfiguration bootstrap, QianConfigurationOptions options, string basePath)
    {
        var appId = Required(bootstrap, "Apollo:AppId");
        var environment = Required(bootstrap, "Apollo:Env").ToUpperInvariant();
        if (environment is not ("UAT" or "PRO")) throw Invalid("Apollo:Env");
        var servers = bootstrap.GetSection("Apollo:ConfigServer").GetChildren().Select(section =>
        {
            if (!Uri.TryCreate(section.Value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") ||
                uri.UserInfo.Length != 0 || uri.Query.Length != 0 || uri.Fragment.Length != 0)
                throw Invalid("Apollo:ConfigServer");
            return uri;
        }).ToArray();
        if (servers.Length == 0) throw Invalid("Apollo:ConfigServer");
        var namespaces = bootstrap.GetSection("Apollo:Namespaces").GetChildren().Select(section => section.Value ?? "").ToArray();
        if (namespaces.Length == 0) namespaces = ["application"];
        if (namespaces.Distinct(StringComparer.Ordinal).Count() != namespaces.Length) throw Invalid("Apollo:Namespaces");
        namespaces = namespaces.Append("application").Distinct(StringComparer.Ordinal).OrderBy(NamespaceOrder).ToArray();
        var accessKeyReference = bootstrap["Apollo:AccessKey"];
        if (accessKeyReference is not null && (!SecretReference.IsValid(accessKeyReference) ||
            SecretReference.Parse(accessKeyReference).Scope is not ("platform" or "dev") ||
            options.ResolveAccessKey is null)) throw Invalid("Apollo:AccessKey");
        var cache = bootstrap["Apollo:CacheDir"];
        var cluster = bootstrap["Apollo:Cluster"] ?? "default";
        if (string.IsNullOrWhiteSpace(cluster)) throw Invalid("Apollo:Cluster");
        // 引导根在返回前释放，工厂里只用已读出的值。
        var label = bootstrap["Apollo:Label"];
        var schema = options.ApolloConfigurationSchema?.Clone();
        return (tracker => new ApolloOptions
        {
            AppId = appId, ConfigServers = servers, Cluster = cluster,
            Label = label, Namespaces = namespaces,
            CacheDirectory = cache is null ? null : Path.Combine(Path.GetFullPath(cache, basePath), environment),
            Validate = snapshot =>
            {
                var diagnostics = ScanSnapshot(snapshot, schema);
                return tracker.Merged(snapshot, diagnostics, diagnostics.Count == 0 && (options.ValidateApolloSnapshot?.Invoke(snapshot) ?? true));
            },
            ValidateNamespace = (name, snapshot) => tracker.Namespace(name, snapshot, ScanSnapshot(snapshot, schema)),
            ResolveAccessKey = accessKeyReference is null ? null : () =>
            {
                var value = options.ResolveAccessKey!(accessKeyReference);
                return string.IsNullOrWhiteSpace(value)
                    ? throw new UnauthorizedAccessException("Apollo AccessKey 引用未解析，拒绝发送未签名请求。") : value;
            }
        }, environment, cluster, appId);
    }

    private static IReadOnlyList<SecretDiagnostic> ScanSnapshot(IReadOnlyDictionary<string, string?> snapshot, System.Text.Json.JsonElement? schema)
    {
        var diagnostics = SecretSafety.ScanConfiguration(snapshot, schema);
        return FeatureFlagRules.Validate(snapshot) ? diagnostics :
            [.. diagnostics, new SecretDiagnostic(ConfigErrors.ReloadRejected.Code, "#", "feature-flag-rule")];
    }

    private static int NamespaceOrder(string name)
    {
        if (name.StartsWith("qian.common.", StringComparison.Ordinal) && name.Length > "qian.common.".Length) return 0;
        if (name.StartsWith("qian.plugin.", StringComparison.Ordinal) && name.Length > "qian.plugin.".Length) return 1;
        if (name == "application") return 2;
        throw Invalid("Apollo:Namespaces");
    }
    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value && !string.IsNullOrWhiteSpace(value) ? value : throw Invalid(key);
    internal static QianErrorException Invalid(string key) => new(new QianError(ConfigErrors.BootstrapInvalid,
        new Dictionary<string, string> { ["key"] = key }));
}
