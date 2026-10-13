namespace Qian.Events;

/// <summary>集成事件的来源：谁、以哪个组件的名义、因哪一次调用发布。信封的 producer、actor 与因果链取自这里。</summary>
/// <param name="Producer">发布方组件 id，如 <c>qian.sample.crm</c>。</param>
/// <param name="Actor">触发事件的主体。</param>
public sealed record EventOrigin(string Producer, EventActor Actor)
{
    /// <summary>业务关联 id；跨多个事件的同一业务流程沿用同一个值。</summary>
    public Guid? CorrelationId { get; init; }
    /// <summary>直接引起事件的命令或事件 id；SDK 命令管道填本次调用 id。</summary>
    public Guid? CausationId { get; init; }
    /// <summary>W3C traceparent；为空时按当前 Activity 或新 trace 生成。</summary>
    public string? TraceParent { get; init; }

    /// <summary>按信封规则校验来源字段。</summary>
    public void Validate()
    {
        if (!QianEventEnvelope.IsComponentId(Producer)) throw QianEventsException.Rejected("producer 必须为小写点分的组件 id。");
        if (Actor is null) throw QianEventsException.Rejected("actor 必填。");
        Actor.Validate();
        if (CorrelationId == Guid.Empty || CausationId == Guid.Empty) throw QianEventsException.Rejected("correlation_id 与 causation_id 不能为空 UUID。");
        if (TraceParent is not null && !QianEventEnvelope.IsTraceParent(TraceParent)) throw QianEventsException.Rejected("trace_parent 必须为 W3C traceparent。");
    }

    /// <inheritdoc />
    public override string ToString() => "EventOrigin(redacted)";
}

/// <summary>
/// 当前异步流的事件来源。集成事件只能在命令 handler 的同一作用域内发出（ADR-008 D8.2）：
/// SDK 命令管道在执行 handler 与保存期间打开它，Outbox 写入端缺少来源时拒绝发布。
/// 定时作业、生命周期控制器等不经命令管道的平台发布方显式以服务主体打开。
/// </summary>
public static class EventEnvelopeScope
{
    private static readonly AsyncLocal<Frame?> Ambient = new();

    /// <summary>当前生效的来源；不在任何事件作用域内时为 null。</summary>
    public static EventOrigin? Current => Ambient.Value?.Origin;

    /// <summary>在当前异步流打开事件作用域，释放后恢复外层来源；必须按打开的逆序释放。</summary>
    public static IDisposable Begin(EventOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(origin);
        origin.Validate();
        var frame = new Frame(origin, Ambient.Value);
        Ambient.Value = frame;
        return frame;
    }

    private sealed class Frame(EventOrigin origin, Frame? outer) : IDisposable
    {
        private int _disposed;
        internal EventOrigin Origin { get; } = origin;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            if (!ReferenceEquals(Ambient.Value, this)) throw new InvalidOperationException("事件作用域必须按打开的逆序释放。");
            Ambient.Value = outer;
        }
    }
}
