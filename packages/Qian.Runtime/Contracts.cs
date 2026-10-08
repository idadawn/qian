using System.Text.Json;
using Microsoft.Extensions.Logging;
using Qian.Runtime.Errors;

namespace Qian.Runtime;

/// <summary>Definition 拥有的服务键；具名注册可用 Key:name 声明就绪依赖。</summary>
public readonly record struct ServiceKey(string Name);

/// <summary>两层扁平作用域，不隐含租户、应用与会话之间的继承关系。</summary>
public readonly record struct ScopeKey(string Kind, string Id)
{
    public static readonly ScopeKey Global = new("global", "");

    internal void Validate()
    {
        if (this != Global && (Kind is not ("tenant" or "app" or "agent-session") || string.IsNullOrWhiteSpace(Id)))
            throw new ArgumentException("scope 必须为 global 或非空的 tenant / app / agent-session。 ");
    }
}

/// <summary>已由清单与配置装配器解析的插件条目。</summary>
public sealed record PluginEntry(string Id, string Plugin, string Version, ScopeKey Scope, int Layer, JsonElement Config);

/// <summary>装载输入；Provides 来自清单，不通过试运行 Apply 推测。</summary>
public sealed record PluginDefinition(PluginEntry Entry, IQianPlugin Plugin, IReadOnlyList<ServiceKey> Provides);

/// <summary>插件的最小接口。</summary>
public interface IQianPlugin
{
    IReadOnlyList<ServiceKey> Inject { get; }
    ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct);
}

/// <summary>类型化配置接口；Schema 校验由清单装配器在调用前完成。</summary>
public interface IQianPlugin<TConfig> : IQianPlugin
{
    ValueTask ApplyAsync(IPluginContext ctx, TConfig config, CancellationToken ct);

    ValueTask IQianPlugin.ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        => ApplyAsync(ctx, config.Deserialize<TConfig>() ?? throw new JsonException("配置不能为空。"), ct);
}

/// <summary>插件的服务、效果与作用域入口。</summary>
public interface IPluginContext
{
    PluginEntry Entry { get; }
    ScopeKey Scope { get; }
    IServiceProvider Services { get; }
    ILogger Logger { get; }
    IDisposable Provide<TService>(ServiceKey key, TService instance) where TService : class;
    TService Inject<TService>(ServiceKey key) where TService : class;
    TService? TryInject<TService>(ServiceKey key) where TService : class;
    ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class;
}

/// <summary>某次 Apply 的状态；失败保留原异常供管理面查询。</summary>
public enum PluginState { Pending, Applying, Ready, Failed }

/// <summary>当前服务尚不可见。不存在与被过滤的服务返回相同错误。</summary>
public sealed class ServiceNotReadyException : InvalidOperationException
{
    public ServiceNotReadyException() : base("服务不存在或尚未就绪。") { }
    public QianError Error { get; } = new(PlatformErrors.NotFound);
}
