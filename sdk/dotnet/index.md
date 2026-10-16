> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 后端 SDK（.NET）

`Qian.Runtime` 极小、无业务语义：共享上下文 `ctx`、服务注册、类型化事件、可逆副作用、装载器，是组件库唯一必需的 Qian 依赖。平台能力都是向 `ctx` 贡献服务键的插件。

## 安装

`Qian.*` 包族以同一 SemVer 版本发布在 GitHub Packages 的 NuGet 源（ADR-019 D19.7），包随公开仓库 `idadawn/qian` 公开可见；Kun 正式包在 nuget.org。项目的 `nuget.config` 用 `packageSourceMapping` 把 `Qian.*` 指向该源，从 GitHub Packages 安装需要带 `read:packages` 权限的 GitHub 令牌：

```xml
<packageSources>
  <clear />
  <add key="qian" value="https://nuget.pkg.github.com/idadawn/index.json" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources>
<packageSourceMapping>
  <packageSource key="qian"><package pattern="Qian.*" /></packageSource>
  <packageSource key="nuget.org"><package pattern="*" /></packageSource>
</packageSourceMapping>
```

组件项目引用同一版本的 `Qian.Plugin.Sdk` 与 `Qian.Plugin.Sdk.Generators`；源生成器是开发依赖，写 `PrivateAssets="all"`：

```xml
<PackageReference Include="Qian.Plugin.Sdk" Version="0.1.0" />
<PackageReference Include="Qian.Plugin.Sdk.Generators" Version="0.1.0" PrivateAssets="all" />
```

## 编程模型

- **插件**：实现 `IQianPlugin` / `IQianPlugin<TConfig>`，在 `ApplyAsync` 里向上下文贡献内容；运行时先按清单的 `configSchema` 校验配置再反序列化。
- **服务**：`Provide(key, instance)` 填充服务键，`Inject<T>(key)` 取用；`ctx.Files()` 这类扩展方法由 Definition 包提供，等价于 `Inject`。装载器按 `inject → provides` 决定挂载顺序，依赖未就绪的插件保持 `Pending`。
- **事件**：事件在 Definition 包里定义并标注分发模式，见[事件与中间件](/sdk/dotnet/events.md)。
- **副作用皆可逆**：`Provide`、`On` 等注册都返回释放函数并压入插件的效果栈，`Effect(...)` 把第三方资源也纳入同一栈；卸载时按注册逆序释放。
- **scope**：`Scoped(scope)` 返回子上下文，之后的注册归属该 scope；见 [scope](/start/concepts/scope.md)。

## API 草图

节选自[组件模型规格](/design/prd/specs/component-model.md) §5.2 的 API 草图（以规格为准）：

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

实现类用 `[QianPlugin("<component-id>")]` 标注，`Qian.Plugin.Sdk` 的源生成器据此派生清单里的 `runtime.entry`、`inject[]` 与 `provides[]`。`Provide` 底层是 .NET DI keyed services 的可撤销封装；跨进程的集成事件由 [qian.events](/components/platform/qian.events.md) 桥接到 Kun 的 Mediator + Outbox。

## 示例

把 `Kun.Storage` 包装为 `ctx.Files` Provider 的完整示例见规格 §5.7。API 参考（DocFX 生成）随 S1-DOCS-002 上线。
