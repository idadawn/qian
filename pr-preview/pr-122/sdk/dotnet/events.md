> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# 事件与中间件

事件在 Definition 包的**定义处**标注分发模式，`ctx.On` 调用点必须写同一模式：重复一次是为了让调用点可读，也让 CI 能做一致性校验。

| 模式             | 调用顺序                               | 返回值           | 异常               | 典型用途                   |
| -------------- | ---------------------------------- | ------------- | ---------------- | ---------------------- |
| `emit` 观察      | 按 priority 降序逐个 await              | 无             | 记录日志、不传播，其余监听器继续 | 通知、审计、索引               |
| `waterfall` 环绕 | 按 priority 组成洋葱，调用 `next()` 才进入下一层 | 最外层的返回值       | 传播给发起方           | 鉴权、租户校验、输入改写、阻断        |
| `parallel` 扇出  | 全部并发                               | 结果列表，顺序同监听器顺序 | 全部完成后抛聚合异常       | 多渠道通知、并行校验             |
| `serial` 按序    | 逐个 await                           | 结果列表          | 首个异常处中断          | 逐步迁移、有序收尾              |
| `bail` 首值      | 逐个 await                           | 首个非空返回值       | 传播给发起方           | 否决、解析器链、首个匹配的 Provider |

## waterfall 即中间件

运行时本身没有业务语义：租户、鉴权、审计等强制执行点都是 base 组合包里的 `waterfall` 监听器。下面的示意按规格 §5.2 的 API 写成，类型名为举例：

```csharp
// Definition 包：定义事件并标注分发模式
public static class FileEvents
{
    public static readonly QianEvent<UploadRequest, UploadResult> Uploading =
        QianEvent.Define<UploadRequest, UploadResult>("files.uploading", DispatchMode.Waterfall);
}

// 某个插件的 ApplyAsync 里：挂一层环绕监听器，调用 next() 才进入下一层
ctx.On(FileEvents.Uploading, DispatchMode.Waterfall,
    new Func<UploadRequest, Func<ValueTask<UploadResult>>, ValueTask<UploadResult>>(async (request, next) =>
    {
        if (request.Size > MaxUploadBytes)
            throw new UploadRejectedException("超过单文件上限");
        return await next();
    }),
    priority: 100);
```

- 分发的 scope 等于发起方的 `ctx.Scope`；全局监听器对所有 scope 可见，scope 内的监听器只对该 scope 可见。
- 事件名全局唯一，两个 Definition 定义同名事件即挂载失败。
- 组件 `hooks.json` 里的 command / http 监听器由 [qian.hooks](/pr-preview/pr-122/components/platform/qian.hooks.md) 转成同模式的进程内监听器，priority 默认 `-100`。
- 跨进程的集成事件走 [qian.events](/pr-preview/pr-122/components/platform/qian.events.md) 的 Outbox。

详见[组件模型规格](/pr-preview/pr-122/design/prd/specs/component-model.md) §5.4。
