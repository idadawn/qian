namespace Qian.Runtime;

/// <summary>进程内扩展事件的分发模式。</summary>
public enum DispatchMode
{
    /// <summary>依次通知，记录并隔离监听器异常。</summary>
    Emit,
    /// <summary>通过 next 委托的环绕中间件。</summary>
    Waterfall,
    /// <summary>并发执行，等待全部完成并聚合失败。</summary>
    Parallel,
    /// <summary>依次执行，首个失败中断。</summary>
    Serial,
    /// <summary>依次执行，首个非空结果中断。</summary>
    Bail,
}

/// <summary>Definition 定义的事件。TResult 是整个分发的结果；扇出与按序模式用 IReadOnlyList&lt;T&gt;。</summary>
public sealed record QianEvent<TPayload, TResult>(string Name, DispatchMode Mode)
{
    /// <summary>waterfall 的默认链尾；调用方也可在 DispatchAsync 时覆盖。</summary>
    public Func<TPayload, ValueTask<TResult>>? Terminal { get; init; }
}

/// <summary>事件定义工厂。</summary>
public static class QianEvent
{
    /// <summary>定义一个事件；同一运行时内同名 Definition 必须复用同一实例。</summary>
    public static QianEvent<TPayload, TResult> Define<TPayload, TResult>(string name, DispatchMode mode) => new(name, mode);
}

/// <summary>所有并行监听器结束后报告失败；Partial 仅含成功结果，顺序与监听器一致。</summary>
public sealed class ParallelDispatchException<TResult> : AggregateException
{
    /// <summary>记录失败与监听器顺序的成功结果快照。</summary>
    public ParallelDispatchException(IEnumerable<Exception> errors, IReadOnlyList<TResult> partial)
        : base("并行分发中的部分监听器失败。", errors)
    {
        ArgumentNullException.ThrowIfNull(partial);
        Partial = Array.AsReadOnly(partial.ToArray());
    }

    /// <summary>成功结果快照，不含失败项的占位值。</summary>
    public IReadOnlyList<TResult> Partial { get; }
}
