using Microsoft.Extensions.DependencyInjection;
using Qian.Config;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Runtime;
using System.Text.Json;

namespace Qian.Lifecycle;

/// <summary>清单分发的稳定入口；程序化适配模式由 LifecyclePlugin 保留。</summary>
[QianPlugin("qian.lifecycle")]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(LifecyclePlugin))]
[Provides(typeof(LifecycleDefinition), nameof(LifecycleDefinition.Key))]
public sealed class LifecycleComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, RegistryDefinition.Key, ConfigDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        return new LifecyclePlugin(ctx.Services.GetService<LifecycleRuntimeOptions>() ?? new LifecycleRuntimeOptions()).ApplyAsync(ctx, config, ct);
    }
}
