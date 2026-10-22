using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Tenancy;
using System.Text.Json;

namespace Qian.Identity.Server;

/// <summary>清单分发的稳定入口；程序化适配模式由 IdentityServerPlugin 保留。</summary>
[QianPlugin("qian.identity.server", MetadataTypes = [typeof(IdentityRuntimeEvents)])]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(IdentityServerPlugin))]
[Provides(typeof(IdentityDefinition), nameof(IdentityDefinition.Key))]
[Provides(typeof(IdentityDefinition), nameof(IdentityDefinition.AccountsKey))]
[Provides(typeof(IdentityDefinition), nameof(IdentityDefinition.SignInKey))]
[Provides(typeof(IdentityDefinition), nameof(IdentityDefinition.FactorsKey))]
[Provides(typeof(TenantProvisioning), nameof(TenantProvisioning.MembershipKey))]
[Provides(typeof(TenantSettings), nameof(TenantSettings.UserCounterKey))]
public sealed class IdentityServerComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, TenancyDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        return new IdentityServerPlugin().ApplyAsync(ctx, config, ct);
    }
}
