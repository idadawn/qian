using Qian.Audit;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Runtime;
using Qian.Tenancy;
using System.Text.Json;

namespace Qian.Capabilities;

/// <summary>清单分发的稳定入口；程序化适配模式由 CapabilitiesPlugin 保留。</summary>
[QianPlugin("qian.capabilities")]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(CapabilitiesPlugin))]
[Provides(typeof(CapabilitiesDefinition), nameof(CapabilitiesDefinition.Key))]
[Provides(typeof(CapabilitiesDefinition), nameof(CapabilitiesDefinition.ContractsKey))]
[Provides(typeof(CapabilitiesDefinition), nameof(CapabilitiesDefinition.AdmissionKey))]
[Provides(typeof(CapabilitiesDefinition), nameof(CapabilitiesDefinition.NavigationKey))]
public sealed class CapabilitiesComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, AuditDefinition.Key, RegistryDefinition.Key, TenancyDefinition.Key, CommandPolicies.TenancyKey, CommandPolicies.AuthorizationKey, CommandPolicies.AuditKey];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        return new CapabilitiesPlugin().ApplyAsync(ctx, config, ct);
    }
}
