using System.Globalization;
using System.Text.RegularExpressions;

namespace Qian.Events;

/// <summary>
/// RabbitMQ 拓扑命名（PRD 7.10）：topic exchange <c>qian.events</c>，路由键为信封 subject；
/// 竞争消费用每服务一个 quorum 队列 <c>q.&lt;service&gt;.&lt;subscription&gt;</c>，广播用每实例一个自动删除队列 <c>q.&lt;service&gt;.&lt;instance&gt;</c>。
/// 消费端另有每订阅一个死信队列 <c>q.&lt;service&gt;.&lt;subscription&gt;-dlq</c> 与按次数、间隔命名的重试队列 <c>&lt;主队列&gt;-retry-&lt;n&gt;-&lt;毫秒&gt;</c>；
/// 这两种后缀保留给消费端，订阅名与实例名不能以它们结尾。
/// </summary>
public static partial class QianEventTopology
{
    /// <summary>集成事件 topic exchange。</summary>
    public const string Exchange = "qian.events";

    /// <summary>死信 exchange；消费端重试耗尽或确定性失败的消息投递到这里，路由键为订阅的死信队列名。</summary>
    public const string DeadLetterExchange = "qian.dlx";

    /// <summary>事件的路由键，即信封 subject。</summary>
    public static string RoutingKey(string type) => IntegrationEventTypes.SubjectOf(type);

    /// <summary>竞争消费队列名：同一服务的所有实例共享，quorum 类型。</summary>
    public static string SubscriptionQueue(string service, string subscription) => Queue(service, subscription, nameof(subscription));

    /// <summary>广播队列名：每个实例一个，实例下线即删除。</summary>
    public static string BroadcastQueue(string service, string instance) => Queue(service, instance, nameof(instance));

    /// <summary>
    /// 订阅的死信队列名，也是发往 <see cref="DeadLetterExchange"/> 的路由键：每订阅一个、跨实例共享，竞争与广播订阅都有。
    /// 死信按队列名精确绑定，不按 subject，否则订阅同一事件的各个订阅会收到彼此的死信。
    /// </summary>
    public static string DeadLetterQueue(string service, string subscription) => Checked(Queue(service, subscription, nameof(subscription)) + "-dlq", nameof(subscription));

    /// <summary>
    /// 第 <paramref name="attempt"/> 次失败后的重试队列名：消息在其中停留 <paramref name="delay"/> 后经默认 exchange 回到 <paramref name="name"/> 的主队列。
    /// 名字带次数与毫秒数，改了退避起点也不会以不同参数重声明同名队列。<paramref name="name"/> 为竞争订阅名或广播实例名。
    /// </summary>
    public static string RetryQueue(string service, string name, int attempt, TimeSpan delay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempt, 1);
        var milliseconds = (long)delay.TotalMilliseconds;
        ArgumentOutOfRangeException.ThrowIfLessThan(milliseconds, 1L, nameof(delay));
        return Checked(Queue(service, name, nameof(name)) + "-retry-" + attempt.ToString(CultureInfo.InvariantCulture) + "-" +
            milliseconds.ToString(CultureInfo.InvariantCulture), nameof(name));
    }

    private static string Queue(string service, string name, string parameter)
    {
        if (service is null || !ServicePattern().IsMatch(service)) throw new ArgumentException("服务名必须为小写点分标识。", nameof(service));
        if (name is null || !NamePattern().IsMatch(name)) throw new ArgumentException("名称必须为小写短横线标识。", parameter);
        if (ReservedSuffix().IsMatch(name)) throw new ArgumentException("名称不能以消费端保留的 -dlq 或 -retry-<n>-<毫秒> 结尾。", parameter);
        return Checked("q." + service + "." + name, parameter);
    }

    // AMQP 短字符串上限 255 字节；名称只含 ASCII。
    private static string Checked(string queue, string parameter) => queue.Length <= 255 ? queue : throw new ArgumentException("队列名超过 255 字节。", parameter);

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ServicePattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();

    [GeneratedRegex("(-dlq|-retry-[0-9]+-[0-9]+)$", RegexOptions.CultureInvariant)]
    private static partial Regex ReservedSuffix();
}
