using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using System.Text.Json;

namespace Qian.Audit;

/// <summary>清单分发的稳定入口；程序化适配模式由 AuditPlugin 保留。</summary>
[QianPlugin("qian.audit")]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(AuditPlugin))]
[Provides(typeof(AuditDefinition), nameof(AuditDefinition.Key))]
[Provides(typeof(CommandPolicies), nameof(CommandPolicies.AuditKey))]
public sealed class AuditComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        return new AuditPlugin().ApplyAsync(ctx, config, ct);
    }
}
