using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Data.Native;
using Qian.Authz.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace Qian.Config.Apollo;

/// <summary>可选平台草稿管理组件；绑定目标来自可信宿主，只编辑 qian.common.flags。</summary>
[QianPlugin("qian.config.flags")]
[QianConfigSchema(Schema)]
[CapabilitySource(typeof(FlagDraftCapability), nameof(FlagDraftCapability.Definition))]
[DelegatePlugin(typeof(FeatureFlagManagementImplementation))]
[Provides(typeof(FeatureFlagManagement), nameof(FeatureFlagManagement.Key))]
public sealed class FeatureFlagManagementPlugin : IQianPlugin
{
    internal const string Schema = """{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","required":["bindingId"],"properties":{"bindingId":{"type":"string","pattern":"^[a-z][a-z0-9._-]{0,99}$"}},"additionalProperties":false}""";
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [ConfigDefinition.Key, AuthzEnforcement.Key, AuditDefinition.Key, NativeDataDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) =>
        new FeatureFlagManagementImplementation().ApplyAsync(ctx, config, ct);
}

internal sealed class FeatureFlagManagementImplementation : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [ConfigDefinition.Key, AuthzEnforcement.Key, AuditDefinition.Key, NativeDataDefinition.Key];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global || ctx.Entry.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Count() != 1 ||
            !config.TryGetProperty("bindingId", out var bindingId) || bindingId.ValueKind != JsonValueKind.String ||
            ctx.Services.GetService<FlagManagementBindings>() is not { } bindings ||
            !bindings.Items.TryGetValue(bindingId.GetString()!, out var binding)) throw QianConfigurationExtensions.Invalid("flags.bindingId");
        await new FlagDraftReceipts(ctx, binding).ValidateAsync(ct).ConfigureAwait(false);
        var capability = new FlagDraftCapability(ctx, binding);
        ctx.Effect(capability);
        ctx.Provide<IFeatureFlagDrafts>(FeatureFlagManagement.Key, capability);
        ctx.Provide<IExecutableCapability>(CapabilityBindingRegistration.Key, FeatureFlagManagement.SetCapabilityId, capability);
    }
}
