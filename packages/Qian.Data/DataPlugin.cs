using System.Text.Json;
using Kun.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Plugin.Sdk;

namespace Qian.Data;

/// <summary>qian.data 的真实 Provider；挂载提供 Data，卸载撤销服务并释放未关闭的会话。</summary>
[QianPlugin("qian.data")]
[QianConfigSchema(ComponentConfiguration.Schema)]
public sealed class DataPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();
        if (ctx.Scope != ScopeKey.Global && ctx.Scope.Kind != "tenant")
            throw new QianDataMisconfiguredException("Data 只允许挂载在 global 或 tenant scope。");
        var reference = DataConfiguration.ReadConnection(config);
        var contexts = ctx.Services.GetServices<DataConnectionBinding>()
            .Where(binding => reference is null || binding.Reference == reference).Select(binding => binding.ContextType).ToHashSet();
        if (reference is not null && contexts.Count == 0)
            throw new QianDataMisconfiguredException("Data 连接引用没有绑定任何业务上下文。");
        // 事件类型必须由宿主在构建时显式登记；不偷偷装一个空目录覆盖后续登记。
        _ = ctx.Services.GetService<IEventTypeRegistry>()
            ?? throw new QianDataMisconfiguredException("数据 Provider 需要显式登记 Kun 事件类型目录。");
        var service = new QianDataService(ctx.Services.GetRequiredService<IServiceScopeFactory>(), contexts, ctx.Scope);
        ctx.Effect(service.DisposeAsync);
        ctx.Provide<IDataService>(DataDefinition.Key, service);
        return ValueTask.CompletedTask;
    }
}
