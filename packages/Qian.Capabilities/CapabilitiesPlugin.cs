using System.Text.Json;
using Kun.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Qian.Plugin.Sdk;
using Qian.Registry;
using Qian.Audit;
using Qian.Data;
using Qian.Tenancy;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

namespace Qian.Capabilities;

/// <summary>平台能力目录，消费真实 Registry 与授权端口，不把预装元数据作为执行资格。</summary>
public sealed class CapabilitiesPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, AuditDefinition.Key, RegistryDefinition.Key, TenancyDefinition.Key, CommandPolicies.TenancyKey, CommandPolicies.AuthorizationKey, CommandPolicies.AuditKey];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any())
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var service = new CapabilityService(ctx.Registry(), ctx.Services.GetRequiredService<IPermissionChecker>(),
            ctx.Services.GetRequiredService<ISurfaceReader>(), ctx.Services.GetRequiredService<IToolContractHasher>());
        ctx.Effect(service.Dispose);
        var contracts = new InstalledContractRepository(ctx.Registry().GetInstalledComponents(), ctx.Services.GetService<IOpenApiCompatibilityProbe>(), ctx.Services.GetRequiredService<ISurfaceReader>(), ctx.Services.GetService<IProtoCompatibilityProbe>());
        ctx.Effect(contracts.Dispose);
        await contracts.ValidateProtoAsync(ct).ConfigureAwait(false);
        ctx.Provide<IContractRepository>(CapabilitiesDefinition.ContractsKey, contracts);
        var navigation = new NavigationService(ctx.Registry(), ctx.Services.GetRequiredService<IPermissionChecker>(), ctx.Data());
        ctx.Effect(navigation.Dispose);
        ctx.Provide<INavigationService>(CapabilitiesDefinition.NavigationKey, navigation);
        ctx.Provide<ICapabilityService>(CapabilitiesDefinition.Key, service);

    }
}
