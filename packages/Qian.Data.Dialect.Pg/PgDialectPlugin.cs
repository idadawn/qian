using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Data.Dialect.Pg;

/// <summary>PostgreSQL 方言组件入口；具名运行时贡献与宿主数据目录登记随卸载一起撤销。</summary>
[QianPlugin("qian.data.dialect.pg")]
[QianConfigSchema("""{"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false}""")]
[Contributes(typeof(DataDialectContract), nameof(DataDialectContract.Key), "pg", RegistryType = typeof(IDataDialectRegistry))]
public sealed class PgDialectPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();
        // 现有数据目录由宿主全局维护；尚未迁为 scope 视图前，禁止租户挂载污染其他租户的目录。
        if (ctx.Scope != ScopeKey.Global || config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any())
            throw new QianErrorException(new QianError(PlatformErrors.InputInvalid));
        var dialect = new PgDialect();
        var registry = ctx.Services.GetRequiredService<IDataDialectRegistry>();
        ctx.Provide<IDataDialect>(DataDialectContract.Key, "pg", dialect);
        ctx.Effect(registry.Register("pg", dialect));
        return ValueTask.CompletedTask;
    }
}
