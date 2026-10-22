namespace Qian.Events;

/// <summary>
/// 订阅器正在派发的一次投递：已解析、已与 AMQP 属性核对过的 v1 信封，以及订阅与重试计数。
/// 订阅器为每条消息新建一个依赖注入作用域，handler 可在构造函数里注入本类型读取 producer、actor 与因果链；
/// 幂等键、发生时间与租户一律以这里的信封为准，不读载荷里的同名字段（S1-DEC-020）。
/// 只能在订阅器派发的作用域内解析，其他位置解析即抛 EVENTS_MISCONFIGURED。
/// </summary>
public sealed class QianEventDelivery
{
    /// <summary>已校验的信封。</summary>
    public required QianEventEnvelope Envelope { get; init; }

    /// <summary>订阅方服务名。</summary>
    public required string Service { get; init; }

    /// <summary>订阅名。</summary>
    public required string Subscription { get; init; }

    /// <summary>订阅模式。</summary>
    public required EventSubscriptionMode Mode { get; init; }

    /// <summary>幂等记录使用的消费者名：竞争消费为 <c>&lt;service&gt;.&lt;subscription&gt;</c>，广播另带 <c>@&lt;instance&gt;</c>。</summary>
    public required string Consumer { get; init; }

    /// <summary>本次是第几次投递，首投为 1；每经过一次退避重试加一。</summary>
    public required int Attempt { get; init; }

    /// <inheritdoc />
    public override string ToString() => "QianEventDelivery(redacted)";
}
