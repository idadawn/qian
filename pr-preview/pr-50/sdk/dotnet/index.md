> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# 后端 SDK（.NET）

`Qian.Runtime` 极小、无业务语义：共享上下文 `ctx`、服务注册、类型化事件、可逆副作用、装载器，是组件库唯一必需的 Qian 依赖。平台能力都是向 `ctx` 贡献服务键的插件。

## 编程模型

- **插件**：实现 `IQianPlugin` / `IQianPlugin<TConfig>`，在 `ApplyAsync` 里向上下文贡献内容；运行时先按清单的 `configSchema` 校验配置再反序列化。
- **服务**：`Provide(key, instance)` 填充服务键，`Inject<T>(key)` 取用；`ctx.Files()` 这类扩展方法由 Definition 包提供，等价于 `Inject`。装载器按 `inject → provides` 决定挂载顺序，依赖未就绪的插件保持 `Pending`。
- **事件**：事件在 Definition 包里定义并标注分发模式，见[事件与中间件](/pr-preview/pr-50/sdk/dotnet/events.md)。
- **副作用皆可逆**：`Provide`、`On` 等注册都返回释放函数并压入插件的效果栈，`Effect(...)` 把第三方资源也纳入同一栈；卸载时按注册逆序释放。
- **scope**：`Scoped(scope)` 返回子上下文，之后的注册归属该 scope；见 [scope](/pr-preview/pr-50/start/concepts/scope.md)。

## API 草图

节选自[组件模型规格](/pr-preview/pr-50/design/prd/specs/component-model.md) §5.2 的 API 草图（以规格为准）：

```csharp
public interface IQianPlugin
{
    IReadOnlyList<ServiceKey> Inject { get; }
    ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct);
}

public interface IPluginContext
{
    ScopeKey Scope { get; }
    IDisposable Provide<TService>(ServiceKey key, TService instance) where TService : class;
    TService Inject<TService>(ServiceKey key) where TService : class;
    void Effect(Func<ValueTask> disposeAsync);
    IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority = 0);
    ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct);
    IPluginContext Scoped(ScopeKey scope);
}
```

实现类用 `[QianPlugin("<component-id>")]` 标注，`Qian.Plugin.Sdk` 的源生成器据此派生清单里的 `runtime.entry`、`inject[]` 与 `provides[]`。`Provide` 底层是 .NET DI keyed services 的可撤销封装；跨进程的集成事件由 [qian.events](/pr-preview/pr-50/components/platform/qian.events.md) 桥接到 Kun 的 Mediator + Outbox。

## 示例

把 `Kun.Storage` 包装为 `ctx.Files` Provider 的完整示例见规格 §5.7。API 参考（DocFX 生成）随 S1-DOCS-002 上线。
