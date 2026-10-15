# Qian.Runtime

依据：[组件模型规格 §5](../../docs/prd/specs/component-model.md#5-运行时模型)。此包提供 .NET 10 的上下文、服务键、事件、效果栈与装载器，引用已有的 `Qian.Runtime.Errors`。

## 装配位置

- 文件夹包含运行时代码、公共 API 清单与本文；业务插件的声明式表面由对应组件管理。
- 每个 profile 的宿主都可引用此包，`minimal` 无需数据库注册表就能装载插件。
- 运行时服务于所有 seam；Definition 与 Provider 各自位于组件包中。
- `qian add` 的安装编排由 CLI 任务提供；本包承接装配后已解析的条目与清单 `provides[]`。

```csharp
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;

using var services = new ServiceCollection().BuildServiceProvider();
await using var runtime = new PluginLoader(services);
await runtime.MountAsync(definitions, cancellationToken);
```

`PluginDefinition` 绑定 `PluginEntry`、`IQianPlugin` 实例与清单的 `Provides`。每次 `MountAsync` 提交一组完整依赖，装载器在调用任何 `ApplyAsync` 前检查缺失键与环路。已装载条目的依赖被撤销后进入 `Pending`，同键恢复时重新 `ApplyAsync`。等待超过 30 秒会记录依赖警告，管理面可用 `GetPendingDependencies` 查询等待的键。Apply 失败会逆序回滚自身效果，独立条目继续启动，失败原因可由 `GetFailure` 查询。配置重载用 `ReloadAsync`。

声明式目录键见 `SurfaceKeys`（Skills / Agents / Commands / Hooks / McpServers / Tools）；目录中的文档由 [Qian.Surfaces](../Qian.Surfaces/README.md) 校验和登记，沿用同一 scope 与 Effect 语义。

管理面用 `DumpConfig()` 读取当前真实挂载树的配置快照，正在移除与已卸载条目不进入结果；这是运行时快照，静态 profile 的完整来源层仍由 `Qian.Profiles` 提供。整树释放会先标记全部条目正在移除，再逐个逆序撤销，避免已释放依赖者被二次失效。共享 TCK 同时检查释放轨迹和清理异常为零。

## 服务与效果

`Provide` 创建可撤销的 .NET keyed service 描述；`ctx.Services.GetRequiredKeyedService<T>(key)` 与 `ctx.Inject<T>(key)` 使用相同的可见性规则，宿主的其它 DI 服务仍以只读方式访问。注册与宿主已构建的容器分离，因此撤销不会重建或销毁宿主 DI。

`Provide`、`On`、`Restrict` 自动进入当前 Apply 的效果栈。第三方资源用 `Effect(IDisposable)`、`Effect(Action)` 或 `Effect(Func<ValueTask>)` 登记。卸载按逆序逐项等待，某一撤销失败仍继续其它项并记录失败。服务实例的资源所有权由插件用 `Effect` 显式管理。

`Provide` 返回注册句柄 `IRegistrationHandle<T>`，`Dispose()` 撤销且幂等。手动 `Dispose()` 后，受影响依赖者立即退出 Ready，异步清理和恢复可用 `runtime.WhenIdleAsync(ct)` 等待；`UnmountAsync` 自身会等待清理完成。撤销、替换与 restriction 变更可以来自任意线程（例如表面热重载的文件监视线程）；即使发生在另一次级联清理的中途，装载器也先逆序撤销所有回到 Pending 的旧上下文，再重新 Apply。依赖若在某个条目的 Apply 尚未结束时被撤销或替换，这次 Apply 无论成败都作废：逆序撤销后回到 Pending，键再就绪时重新 Apply，不记为 Failed；Apply 自己的注册或 restriction 改变了所注入键的可见性时仍为 Failed。已经卸载的上下文不能再注册或等待服务。

`Replace(instance)` 在原注册位原子替换实例：撤销与再登记在装载器同一把锁内完成，`Inject`、`TryInject`、keyed service 与 `List` 只会看到旧实例或新实例，不会看到缺失；scope、注册顺序、独占性与效果栈位置不变。注册身份随之改变，以 `inject` 依赖该键的条目与撤销后再登记一样回到 Pending 并重新 Apply，拿到新实例。句柄已撤销或上下文已卸载时抛 `ObjectDisposedException`。声明式表面热重载用它替换内容改变的目录项。

具名 seam 使用 `Provide(key, name, value)` / `Inject<T>(key, name)` / `List(key)`；就绪键为 `Key:name`，因此可出现在清单 `provides[]` 与 `Inject` 中。普通键的 `Registration.Name` 为空。

## 事件

事件 Definition 复用同一 `QianEvent` 实例，模式在定义与 `On` 处一致。监听器按 priority 降序、同级挂载顺序执行。集合模式把**整个分发结果**声明为 `IReadOnlyList<T>`，单个监听器返回 `ValueTask<T>`：

```csharp
var changed = QianEvent.Define<string, IReadOnlyList<int>>("sample/changed", DispatchMode.Parallel);
ctx.On(changed, DispatchMode.Parallel,
    (Func<string, ValueTask<int>>)(_ => ValueTask.FromResult(1)));
IReadOnlyList<int> values = await ctx.DispatchAsync(changed, "value", ct);
```

emit 使用 `Func<P, ValueTask>`；waterfall 使用 `Func<P, Func<ValueTask<R>>, ValueTask<R>>`，链尾由 `DispatchAsync` 的 terminal 参数或 Definition 的 `Terminal` 提供；bail 的结果用可空类型（值类型如 `int?`），首个非空值包括 `0`、`false`。parallel 等全部完成后抛 `ParallelDispatchException<T>`，`Partial` 按监听器顺序保留成功结果。分发令牌在开始和监听器之间检查，监听器需要取消 I/O 时自行传递令牌。

## scope 与模型体验

`Scoped` 共享条目效果栈，指定的 tenant、app 与 agent-session 彼此独立。同 `(key, name)` 的 scoped 注册遮蔽全局注册；restriction 只过滤目标 scope 的全局注册，多条取交集。目录 `List`、注入和事件执行使用同一可见性规则。被过滤的具名注册与不存在的注册均抛携带 `NOT_FOUND` 的 `QianErrorException`，不暴露过滤原因。监听器目录名为条目 id 加本次 Apply 内的事件序号；同条目不同 scope 的同序号监听器可遮蔽。

restriction 谓词应为无副作用的纯函数，抛异常时记录日志并按不可见处理。模型可见工具与目录消费方每次调用时都从当前 scope 重新解析；业务鉴权与租户策略由相应插件的事件监听器实施。scope 不推测任何业务 lineage。

## 错误桥接与包源

`KunErrorBridge.ToQianError()` 按 `ErrorType` 映射内置码；未指定领域错误码的 `DomainException` 映射到 `BUSINESS_RULE_VIOLATED`。原始 Kun 文案不进入 ErrorDetail。Kun 校验异常的 HTTP 桥接由 `Qian.Runtime.Errors.AspNetCore` 承担，只有该包引用 `Kun.Validation`。

`KunVersion` 复用根目录属性，正式版 `Kun.*` 显式从 nuget.org 还原。启动诊断新增的 `DEPENDENCY_MISSING`、`DEPENDENCY_CYCLE` 与注册冲突码均在平台目录中，owner 为 `qian.runtime`。

## 验证与已知限制

运行 `dotnet run --project tests/Qian.Runtime.Tests -c Release`，执行 [语言无关场景](../../tck/runtime/README.md) 与 .NET 单元测试。

本任务只实现 .NET 最小运行时。TS harness 属于 S1-RT-003，完整 TCK 属于 S1-QA-002；profile/patch 解析、清单/configSchema 校验、声明式表面重载由对应装配器任务提供，传入本包前完成。代码实例随宿主程序集加载，S1 代码更新通过进程重启。已有对象引用的业务执行仍由调用方的权限检查负责，运行时不代理任意对象的方法。KunExceptionHandler 的上游修正见 S1-KUN-006。
