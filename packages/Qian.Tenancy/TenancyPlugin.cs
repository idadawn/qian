using System.Text.Json;
using Qian.Data;
using Qian.Runtime;
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
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any())
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var directory = new PostgresTenantDirectory(ctx.Data());
        ctx.Effect(directory.Dispose);
        ctx.Provide<ITenantDirectory>(TenancyDefinition.Key, directory);
        return ValueTask.CompletedTask;
    }
}
