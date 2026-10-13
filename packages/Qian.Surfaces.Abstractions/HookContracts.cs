using System.Text.Json;
using Qian.Runtime;

namespace Qian.Surfaces.Abstractions;

/// <summary>受信任生产方在当前 scope 派发的进程内事件，不是可反序列化的调用者身份。</summary>
public sealed class HookNotification
{
    /// <summary>复制载荷；租户、主体和调用者上下文必须由事件生产方从可信宿主取得。</summary>
    public HookNotification(IPluginContext source, string subject, JsonElement payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentException.ThrowIfNullOrWhiteSpace(subject);
        Source = source; Subject = subject; Payload = payload.Clone(); CancellationToken = cancellationToken;
    }
    /// <summary>实际生产方的 scope 视图。</summary>
    public IPluginContext Source { get; }
    /// <summary>matcher 使用的事件主体。</summary>
    public string Subject { get; }
    /// <summary>不可变事件载荷。</summary>
    public JsonElement Payload { get; }
    /// <summary>调用者取消令牌。</summary>
    public CancellationToken CancellationToken { get; }
    /// <inheritdoc />
    public override string ToString() => "HookNotification(redacted)";
}

/// <summary>由代码实际登记的 handler 端口，声明不能伪造已存在的执行器。</summary>
public interface IHookHandler
{
    /// <summary>调用当前 scope 可见的真实实现。</summary>
    ValueTask<JsonElement> InvokeAsync(HookNotification notification, CancellationToken cancellationToken = default);
}

/// <summary>Hooks seam 的服务与处理器词汇。</summary>
public static class HookDefinition
{
    /// <summary>具名 handler 集合，名称为 component/handler。</summary>
    public static readonly ServiceKey Handlers = new("HookHandlers");
}

/// <summary>R39 中已经确定的进程内事件定义；不是跨进程 EventEnvelope 的替身。</summary>
public static class SurfaceHookEvents
{
    /// <summary>租户已开通后的顺序分发。</summary>
    public static readonly QianEvent<HookNotification, IReadOnlyList<JsonElement>> TenantProvisioned = QianEvent.Define<HookNotification, IReadOnlyList<JsonElement>>("tenant/provisioned", DispatchMode.Serial);
    /// <summary>租户已清除后的顺序分发。</summary>
    public static readonly QianEvent<HookNotification, IReadOnlyList<JsonElement>> TenantPurged = QianEvent.Define<HookNotification, IReadOnlyList<JsonElement>>("tenant/purged", DispatchMode.Serial);
    /// <summary>条目已挂载后的顺序分发。</summary>
    public static readonly QianEvent<HookNotification, IReadOnlyList<JsonElement>> EntryMounted = QianEvent.Define<HookNotification, IReadOnlyList<JsonElement>>("registry/entry-mounted", DispatchMode.Serial);
    /// <summary>条目已禁用后的顺序分发。</summary>
    public static readonly QianEvent<HookNotification, IReadOnlyList<JsonElement>> EntryDisabled = QianEvent.Define<HookNotification, IReadOnlyList<JsonElement>>("registry/entry-disabled", DispatchMode.Serial);
}

/// <summary>代码插件对新表面的实际运行时引用做最后校验，发生在替换旧效果之前。</summary>
public interface ISurfaceRuntimeValidator
{
    /// <summary>声明可通过静态 Schema，但必须继续核对当前真实处理器等依赖。</summary>
    void ValidateSurfaces(IPluginContext context, SurfaceValidationResult candidate);
}
