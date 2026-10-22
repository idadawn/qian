using System.Text.Json;
using Kun.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        if (ctx.Scope != ScopeKey.Global) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var options = CapabilitiesOptions.Parse(config);
        ctx.Effect(CapabilitiesErrors.Register(ctx.Services.GetRequiredService<ErrorCatalog>()));
        var loggers = ctx.Services.GetService<ILoggerFactory>();
        // 宿主经 AddQianCapabilities 登记了登记期检查时复用同一实例：组件登记时已生成的条目与预编译的校验器直接进入目录与执行入口。
        var checks = ctx.Services.GetService<CapabilityContractCheck>()
            ?? throw new InvalidOperationException("platform 宿主必须先调用 AddQianCapabilities()，不能在首次访问时补做能力契约检查。");
        var service = new CapabilityService(ctx.Registry(), ctx.Services.GetRequiredService<IPermissionChecker>(), checks, ctx.Data(), options,
            ctx.Services.GetService<IHttpContractSource>(), loggers?.CreateLogger<CapabilityService>());
        ctx.Effect(service.Dispose);
        var contracts = new InstalledContractRepository(ctx.Registry().GetInstalledComponents(), ctx.Services.GetService<IOpenApiCompatibilityProbe>(), ctx.Services.GetRequiredService<ISurfaceReader>(), ctx.Services.GetService<IProtoCompatibilityProbe>());
        ctx.Effect(contracts.Dispose);
        await contracts.ValidateProtoAsync(ct).ConfigureAwait(false);
        ctx.Provide<IContractRepository>(CapabilitiesDefinition.ContractsKey, contracts);
        // 平台挂载路径（Registry scope Reconciler 与 Lifecycle 挂载）经此套用同一组契约包装（R76）。
        ctx.Provide<IContractAdmission>(CapabilitiesDefinition.AdmissionKey, new ContractAdmission(contracts, ctx.Services.GetService<IGrpcContractPeers>()));
        var navigation = new NavigationService(ctx.Registry(), ctx.Services.GetRequiredService<IPermissionChecker>(), ctx.Data());
        ctx.Effect(navigation.Dispose);
        ctx.Provide<INavigationService>(CapabilitiesDefinition.NavigationKey, navigation);
        ctx.Provide<ICapabilityService>(CapabilitiesDefinition.Key, service);

    }
}
