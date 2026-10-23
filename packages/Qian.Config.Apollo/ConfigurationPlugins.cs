using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

/// <summary>标准配置适配器，借用宿主配置根；只撤销本组件拥有的服务登记。</summary>
[QianPlugin("qian.config", MetadataTypes = [typeof(ConfigRuntimeEvents)])]
[QianConfigSchema(ComponentConfiguration.Schema)]
public sealed class ConfigurationPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any())
            throw QianConfigurationExtensions.Invalid("qian.config");
        // 标准入口保持直接登记，清单派生不会执行插件或辅助方法。
        var root = ctx.Services.GetRequiredService<IConfiguration>();
        ctx.Effect(ConfigErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        var flags = CreateFlags(ctx.Services, root);
        ctx.Effect(flags);
        ctx.Provide<IConfigService>(ConfigDefinition.Key, new ConfigurationService(root, flags, ctx.Services.GetService<ISecretResolverFactory>()));
        // 宿主用 AddQianOptions 登记过强类型配置时，挂载期间把重载失败作为进程内事件发出。
        if (ctx.Services.GetService<ConfigReloadNotifier>() is { } notifier)
            ctx.Effect(notifier.Subscribe(async notice => await ctx.DispatchAsync(ConfigRuntimeEvents.ReloadFailed, notice.Payload, CancellationToken.None).ConfigureAwait(false)));
        return ValueTask.CompletedTask;
    }
    private static FeatureFlagsService CreateFlags(IServiceProvider services, IConfiguration root)
    {
        var flags = new FeatureFlagsService(root, services.GetService<ConfigReloadNotifier>() ?? new ConfigReloadNotifier(),
            services.GetService<ILoggerFactory>()?.CreateLogger("Qian.Config.Flags") ?? NullLogger.Instance);
        return flags;
    }

    // Apollo 适配器校验引导后复用同一注册；两处须保持一致。
    internal static void Register(IPluginContext ctx)
    {
        var root = ctx.Services.GetRequiredService<IConfiguration>();
        ctx.Effect(ConfigErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        var flags = CreateFlags(ctx.Services, root);
        ctx.Effect(flags);
        ctx.Provide<IConfigService>(ConfigDefinition.Key, new ConfigurationService(root, flags, ctx.Services.GetService<ISecretResolverFactory>()));
        if (ctx.Services.GetService<ConfigReloadNotifier>() is { } notifier)
            ctx.Effect(notifier.Subscribe(async notice => await ctx.DispatchAsync(ConfigRuntimeEvents.ReloadFailed, notice.Payload, CancellationToken.None).ConfigureAwait(false)));
    }
}

/// <summary>Apollo 配置适配器；要求宿主已安装真实配置源，声明与引导不一致即拒绝挂载。</summary>
[QianPlugin("qian.config.apollo")]
[QianConfigSchema(ComponentConfiguration.ApolloSchema)]
[DelegatePlugin(typeof(ApolloConfigurationImplementation))]
[Provides(typeof(ConfigDefinition), nameof(ConfigDefinition.Key))]
[SharedEvents(typeof(ConfigRuntimeEvents), "qian.config")]
public sealed class ApolloConfigurationPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) =>
        new ApolloConfigurationImplementation().ApplyAsync(ctx, config, ct);
}

// 保留公开入口名及构造方式；配置校验、通知转发与发布审计属于这个明确的实现构件。
internal sealed class ApolloConfigurationImplementation : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object)
            throw QianConfigurationExtensions.Invalid("qian.config.apollo");
        var providers = Find(ctx.Services.GetRequiredService<IConfiguration>(), new HashSet<IConfiguration>(ReferenceEqualityComparer.Instance)).ToArray();
        if (providers.Length != 1) throw QianConfigurationExtensions.Invalid("Apollo.provider");
        var source = providers[0];
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in config.EnumerateObject())
        {
            if (!names.Add(property.Name)) throw QianConfigurationExtensions.Invalid("Apollo.config");
            var matches = property.Name switch
            {
                "appId" => property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == source.AppId,
                "env" => property.Value.ValueKind == JsonValueKind.String && property.Value.GetString() == source.Environment,
                "meta" => property.Value.ValueKind == JsonValueKind.String && Uri.TryCreate(property.Value.GetString(), UriKind.Absolute, out var uri) && source.Servers[0] == uri,
                "namespaces" => property.Value.ValueKind == JsonValueKind.Array &&
                    property.Value.EnumerateArray().All(value => value.ValueKind == JsonValueKind.String) &&
                    property.Value.EnumerateArray().Select(value => value.GetString()!).Order(StringComparer.Ordinal)
                        .SequenceEqual(source.Namespaces.Order(StringComparer.Ordinal)),
                _ => false
            };
            if (!matches) throw QianConfigurationExtensions.Invalid("Apollo.config." + property.Name);
        }
        ConfigurationPlugin.Register(ctx);
        ApolloReleaseAudit.Attach(ctx, source.Releases);
        return ValueTask.CompletedTask;
    }

    private static IEnumerable<QianApolloProvider> Find(IConfiguration configuration, HashSet<IConfiguration> visited)
    {
        if (!visited.Add(configuration) || configuration is not IConfigurationRoot root) yield break;
        foreach (var provider in root.Providers)
        {
            if (provider is QianApolloProvider apollo) yield return apollo;
            if (provider is ChainedConfigurationProvider chained)
                foreach (var nested in Find(chained.Configuration, visited)) yield return nested;
        }
    }
}
