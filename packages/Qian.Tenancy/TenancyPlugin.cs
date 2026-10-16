using System.Text.Json;
using System.Text.RegularExpressions;
using Qian.Data;
using Microsoft.Extensions.DependencyInjection;
using Qian.Authz.Abstractions;
using Qian.Runtime;
using Qian.Plugin.Sdk;
using Qian.Runtime.Errors;
using Qian.Surfaces.Abstractions;

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
        var initializationCapabilities = ReadConfiguration(config);
        var directory = new PostgresTenantDirectory(ctx.Data(), ctx.Services.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>());
        ctx.Effect(directory.Dispose);
        ctx.Provide<ITenantDirectory>(TenancyDefinition.Key, directory);
        ctx.Effect(ctx.Services.GetRequiredService<ErrorCatalog>().Register(TenancyErrors.UserLimit));
        var settings = new TenantSettingsService(ctx, directory); ctx.Effect(settings.Dispose);
        ctx.Provide<ITenantSettings>(TenantSettings.Key, settings);
        ctx.Provide<ITenantSettingsOperations>(TenantSettings.OperationsKey, settings);
        ctx.Provide<ITenantQuota>(TenantSettings.QuotaKey, settings);
        ctx.On(SurfaceHookEvents.EntryMounted, DispatchMode.Serial, (Func<HookNotification, ValueTask<JsonElement>>)(notification =>
        { settings.Invalidate(notification); return ValueTask.FromResult(JsonSerializer.SerializeToElement<object?>(null)); }));
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

    internal static IReadOnlySet<string> ReadConfiguration(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object) throw Invalid();
        var initializationCapabilities = new HashSet<string>(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in config.EnumerateObject())
        {
            if (!seen.Add(property.Name)) throw Invalid();
            // 兼容 base profile 的既有声明；这里只确认严格生产边界，不提供 header 兜底开关。
            if (property.Name == "resolver")
            {
                if (property.Value.ValueKind != JsonValueKind.Object) throw Invalid();
                var resolverKeys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var option in property.Value.EnumerateObject())
                    if (!resolverKeys.Add(option.Name) || !(option.Name == "mode" && option.Value.ValueKind == JsonValueKind.String && option.Value.GetString() == "production" ||
                        option.Name == "allowHeaderFallback" && option.Value.ValueKind == JsonValueKind.False)) throw Invalid();
                continue;
            }
            if (property.Name != "initializationCapabilities" || property.Value.ValueKind != JsonValueKind.Array)
                throw Invalid();
            foreach (var capability in property.Value.EnumerateArray())
                if (capability.ValueKind != JsonValueKind.String || capability.GetString() is not { Length: > 0 and <= 384 } id ||
                    !CapabilityId.IsMatch(id) || !initializationCapabilities.Add(id)) throw Invalid();
        }
        return initializationCapabilities;
    }
    private static readonly Regex CapabilityId = new("\\A[a-z][a-z0-9.-]*/[a-z][A-Za-z0-9_.-]*\\z", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static QianErrorException Invalid() => new(new QianError(PlatformErrors.InputInvalid));
}
