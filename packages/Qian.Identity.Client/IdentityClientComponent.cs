using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace Qian.Identity.Client;

/// <summary>清单分发的身份依赖方入口；公开客户端保持无 Config 或 Data Provider 的独立装配。</summary>
[QianPlugin("qian.identity.client")]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(IdentityClientPlugin))]
[Provides(typeof(IdentityDefinition), nameof(IdentityDefinition.Key))]
public sealed class IdentityClientComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) =>
        new IdentityClientPlugin().ApplyAsync(ctx, config, ct);
}
