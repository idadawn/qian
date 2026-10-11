using System.Text.Json;
using Kun.EventBus;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;

namespace Qian.Data;

/// <summary>qian.data 的真实 Provider；挂载提供 Data，卸载撤销服务并释放未关闭的会话。</summary>
public sealed class DataPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [];

    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ct.ThrowIfCancellationRequested();
        if (config.ValueKind != JsonValueKind.Object || config.EnumerateObject().Any())
            throw new QianDataMisconfiguredException("当前数据 Provider 仅接受空配置；数据库连接应显式配置到业务上下文，不能忽略连接或隔离设置。");
        // 事件类型必须由宿主在构建时显式登记；不偷偷装一个空目录覆盖后续登记。
        _ = ctx.Services.GetService<IEventTypeRegistry>()
            ?? throw new QianDataMisconfiguredException("数据 Provider 需要显式登记 Kun 事件类型目录。");
        var service = new QianDataService(ctx.Services.GetRequiredService<IServiceScopeFactory>());
        ctx.Effect(service.DisposeAsync);
        ctx.Provide<IDataService>(DataDefinition.Key, service);
        return ValueTask.CompletedTask;
    }
}
