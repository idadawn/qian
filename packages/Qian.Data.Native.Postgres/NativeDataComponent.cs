using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Data.Native.Postgres;

/// <summary>qian.data.native 唯一入口；仅挂载于 global，提供独立 NativeData 键。</summary>
[QianPlugin("qian.data.native")]
[QianConfigSchema(Schema)]
public sealed class NativeDataComponent : IQianPlugin
{
    private const string Schema = """
    {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false,
     "required":["connections"],"properties":{"connections":{"type":"array","minItems":1,"uniqueItems":true,
       "items":{"type":"string","pattern":"^[a-z][a-z0-9_-]{0,62}$"},"description":"受信任宿主登记的连接绑定名，不是连接串。"}}}
    """;
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        if (ctx.Scope != ScopeKey.Global || ctx.Entry.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object ||
            config.EnumerateObject().Count() != 1 || !config.TryGetProperty("connections", out var names) || names.ValueKind != JsonValueKind.Array)
            throw NativeErrors.Invalid();
        var bindings = ctx.Services.GetServices<NativeDataBinding>().ToDictionary(item => item.Name, StringComparer.Ordinal);
        var selected = new List<NativeDataBinding>(); var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in names.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } name || !seen.Add(name) || !bindings.TryGetValue(name, out var binding))
                throw NativeErrors.Invalid();
            selected.Add(binding);
        }
        if (selected.Count == 0) throw NativeErrors.Invalid();
        var service = new NativeDataService(ctx.Scope, ctx.Entry.Scope, ctx.Services.GetRequiredService<ITenantContext>());
        ctx.Effect(ctx.Services.GetRequiredService<ErrorCatalog>().Register(NativeDataErrors.Misconfigured));
        ctx.Effect(service.DisposeAsync);
        await service.InitializeAsync(selected, ct).ConfigureAwait(false);
        ctx.Provide<INativeDataService>(NativeDataDefinition.Key, service);
    }
}

internal static class NativeErrors
{
    internal static QianErrorException Invalid() => new(new QianError(NativeDataErrors.Misconfigured));
    internal static QianErrorException Denied() => new(new QianError(PlatformErrors.AuthzDenied));
}
