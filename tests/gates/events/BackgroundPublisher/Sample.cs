using Kun.EventBus.Outbox;

internal sealed record Tick(DateTimeOffset At);
// 后台服务不在命令作用域内，经方法组引用同样会被拦下。
internal sealed class Publisher(IOutboxWriter writer)
{
    public void Run() => writer.Enqueue(new Tick(DateTimeOffset.UtcNow));
    public Action<Tick> Deferred() => writer.Enqueue;
}
