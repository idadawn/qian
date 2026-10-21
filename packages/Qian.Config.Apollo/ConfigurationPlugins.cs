using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config.Apollo;

/// <summary>标准配置适配器，借用宿主配置根；只撤销本组件拥有的服务登记。</summary>
[QianPlugin("qian.config")]
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
        // 两个组件各自直接声明同一 Config 服务，生成器分别派生，不执行插件来猜测。
        var root = ctx.Services.GetRequiredService<IConfiguration>();
        ctx.Effect(ConfigErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        ctx.Provide<IConfigService>(ConfigDefinition.Key, new ConfigurationService(root, ctx.Services.GetService<ISecretResolverFactory>()));
        return ValueTask.CompletedTask;
    }
}

/// <summary>Apollo 配置适配器；要求宿主已安装真实配置源，声明与引导不一致即拒绝挂载。</summary>
[QianPlugin("qian.config.apollo")]
[QianConfigSchema(ComponentConfiguration.ApolloSchema)]
public sealed class ApolloConfigurationPlugin : IQianPlugin
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
        var root = ctx.Services.GetRequiredService<IConfiguration>();
        ctx.Effect(ConfigErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        ctx.Provide<IConfigService>(ConfigDefinition.Key, new ConfigurationService(root, ctx.Services.GetService<ISecretResolverFactory>()));
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
