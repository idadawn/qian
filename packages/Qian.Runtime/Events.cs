namespace Qian.Runtime;

/// <summary>进程内扩展事件的分发模式。</summary>
public enum DispatchMode { Emit, Waterfall, Parallel, Serial, Bail }

/// <summary>Definition 定义的事件。TResult 是整个分发的结果；扇出与按序模式用 IReadOnlyList&lt;T&gt;。</summary>
public sealed record QianEvent<TPayload, TResult>(string Name, DispatchMode Mode)
{
    /// <summary>waterfall 的默认链尾；调用方也可在 DispatchAsync 时覆盖。</summary>
    public Func<TPayload, ValueTask<TResult>>? Terminal { get; init; }
}

/// <summary>事件定义工厂。</summary>
public static class QianEvent
{
    public static QianEvent<TPayload, TResult> Define<TPayload, TResult>(string name, DispatchMode mode) => new(name, mode);
}

/// <summary>所有并行监听器结束后报告失败；Partial 仅含成功结果，顺序与监听器一致。</summary>
public sealed class ParallelDispatchException<TResult> : AggregateException
{
    public ParallelDispatchException(IEnumerable<Exception> errors, IReadOnlyList<TResult> partial)
        : base("并行分发中的部分监听器失败。", errors)
    {
        ArgumentNullException.ThrowIfNull(partial);
        Partial = Array.AsReadOnly(partial.ToArray());
    }

    public IReadOnlyList<TResult> Partial { get; }
}
