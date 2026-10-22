using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using System.Text.Json;

namespace Qian.Tenancy;

/// <summary>清单分发的稳定入口；程序化适配模式由 TenancyPlugin 保留。</summary>
[QianPlugin("qian.tenancy")]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(TenancyPlugin))]
[CapabilitySource(typeof(TenantExpiryCapability), nameof(TenantExpiryCapability.Definition))]
[Provides(typeof(TenancyDefinition), nameof(TenancyDefinition.Key))]
[Provides(typeof(TenantSettings), nameof(TenantSettings.Key))]
[Provides(typeof(TenantSettings), nameof(TenantSettings.OperationsKey))]
[Provides(typeof(TenantSettings), nameof(TenantSettings.QuotaKey))]
[Provides(typeof(TenantLifecycle), nameof(TenantLifecycle.Key))]
[Provides(typeof(TenantOperations), nameof(TenantOperations.Key))]
[Provides(typeof(TenantProvisioning), nameof(TenantProvisioning.Key))]
[Provides(typeof(CommandPolicies), nameof(CommandPolicies.TenancyKey))]
public sealed class TenancyComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        return new TenancyPlugin().ApplyAsync(ctx, config, ct);
    }
}
