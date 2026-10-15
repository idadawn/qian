using System.Text.Json;
using Qian.Data;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Runtime;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;

namespace Qian.Tenancy;

/// <summary>qian.tenancy 的目录 Provider，消费 Data；不在全局 DI 偷装另一份租户上下文。</summary>
public sealed class TenancyPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx); ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object)
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var initializationCapabilities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in config.EnumerateObject())
        {
            if (property.Name != "initializationCapabilities" || property.Value.ValueKind != JsonValueKind.Array)
                throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
            foreach (var capability in property.Value.EnumerateArray())
                if (capability.ValueKind != JsonValueKind.String || capability.GetString() is not { Length: > 0 and <= 384 } id ||
                    id.Contains('*', StringComparison.Ordinal) || !id.Contains('/', StringComparison.Ordinal) || !initializationCapabilities.Add(id))
                    throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        }
        var directory = new PostgresTenantDirectory(ctx.Data(), ctx.Services.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>());
        ctx.Effect(directory.Dispose);
        ctx.Provide<ITenantDirectory>(TenancyDefinition.Key, directory);
        ctx.Effect(ctx.Services.GetRequiredService<ErrorCatalog>().Register(TenancyErrors.StateConflict));
        var lifecycle = new TenantStateMachine(ctx, directory);
        ctx.Provide<ITenantLifecycle>(TenantLifecycle.Key, lifecycle);
        var scanner = new TenantExpiryCapability(ctx, lifecycle);
        ctx.Effect(scanner.Dispose);
        ctx.Services.GetService<ICapabilityContractCheck>()?.Check(scanner.Contract);
        ctx.Provide<IExecutableCapability>(CapabilityBindingRegistration.Key, scanner.Contract.Id, scanner);
        ctx.Provide<ITenantOperations>(TenantOperations.Key, new TenantOperationsService(ctx, directory));
        var provisioning = new TenantProvisioningOrchestrator(ctx, directory, initializationCapabilities);
        ctx.Effect(provisioning.Dispose);
        ctx.Provide<ITenantProvisioning>(TenantProvisioning.Key, provisioning);
        CommandPolicies.Register(ctx, CommandPolicyKind.Tenancy, async (invocation, next) =>
        {
            var tenant = await directory.GetByIdAsync(Guid.Parse(invocation.Principal.TenantId!), invocation.CancellationToken).ConfigureAwait(false);
            var initializing = invocation.Channel == "hook" && TenantInitializationAuthorization.Allows(invocation.Contract.Id, invocation.Source.Scope);
            if (tenant is null || (tenant.Status is not (TenantStatus.Active or TenantStatus.Trial) && !(tenant.Status == TenantStatus.Pending && initializing)))
                throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied));
            return await next().ConfigureAwait(false);
        });
        return ValueTask.CompletedTask;
    }
}
