using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Config;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;

namespace Qian.Gateway;

/// <summary>网关唯一组件入口；宿主准备网络装配，插件挂载与卸载决定业务路由是否可以执行。</summary>
[QianPlugin("qian.gateway")]
[QianConfigSchema(ComponentConfiguration.Schema)]
public sealed class GatewayComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [IdentityDefinition.Key, ConfigDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var runtime = ctx.Services.GetRequiredService<GatewayRuntime>();
        if (ctx.Scope != ScopeKey.Global || GatewayConfigurationReader.Serialize(GatewayConfigurationReader.Read(config)) != runtime.SerializedConfiguration)
            throw new InvalidOperationException("网关仅允许 global scope，组件配置必须与宿主已校验的静态装配一致。");
        runtime.Activate();
        ctx.Effect(runtime.Deactivate);
        ctx.Provide<IGatewayService>(GatewayDefinition.Key, runtime);
        return ValueTask.CompletedTask;
    }
}

internal sealed class GatewayRuntime(GatewayConfiguration configuration) : IGatewayService
{
    internal static readonly object TokenKey = new();
    private int _active;
    internal GatewayConfiguration Configuration { get; } = configuration;
    internal string SerializedConfiguration { get; } = GatewayConfigurationReader.Serialize(configuration);
    public bool IsReady => Volatile.Read(ref _active) == 1;
    public string AudienceFor(string clusterId) => Configuration.Clusters.SingleOrDefault(cluster => cluster.Id == clusterId)?.Id
        ?? throw new InvalidOperationException("目标 cluster 未登记。");
    internal void Activate()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0) throw new InvalidOperationException("网关组件已经挂载。");
    }
    internal void Deactivate() => Interlocked.Exchange(ref _active, 0);
}
