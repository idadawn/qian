using System.Text.RegularExpressions;

namespace Qian.Events;

/// <summary>
/// RabbitMQ 拓扑命名（PRD 7.10）：topic exchange <c>qian.events</c>，路由键为信封 subject；
/// 竞争消费用每服务一个 quorum 队列 <c>q.&lt;service&gt;.&lt;subscription&gt;</c>，广播用每实例一个自动删除队列 <c>q.&lt;service&gt;.&lt;instance&gt;</c>。
/// </summary>
public static partial class QianEventTopology
{
    /// <summary>集成事件 topic exchange。</summary>
    public const string Exchange = "qian.events";

    /// <summary>死信 exchange；消费端重试耗尽后投递到这里。</summary>
    public const string DeadLetterExchange = "qian.dlx";

    /// <summary>事件的路由键，即信封 subject。</summary>
    public static string RoutingKey(string type) => IntegrationEventTypes.SubjectOf(type);

    /// <summary>竞争消费队列名：同一服务的所有实例共享，quorum 类型。</summary>
    public static string SubscriptionQueue(string service, string subscription) => Queue(service, subscription, nameof(subscription));

    /// <summary>广播队列名：每个实例一个，实例下线即删除。</summary>
    public static string BroadcastQueue(string service, string instance) => Queue(service, instance, nameof(instance));

    private static string Queue(string service, string name, string parameter)
    {
        if (service is null || !ServicePattern().IsMatch(service)) throw new ArgumentException("服务名必须为小写点分标识。", nameof(service));
        if (name is null || !NamePattern().IsMatch(name)) throw new ArgumentException("名称必须为小写短横线标识。", parameter);
        var queue = "q." + service + "." + name;
        // AMQP 短字符串上限 255 字节；名称只含 ASCII。
        return queue.Length <= 255 ? queue : throw new ArgumentException("队列名超过 255 字节。", parameter);
    }

    [GeneratedRegex("^[a-z][a-z0-9-]*(\\.[a-z][a-z0-9-]*)*$", RegexOptions.CultureInvariant)]
    private static partial Regex ServicePattern();

    [GeneratedRegex("^[a-z][a-z0-9-]*$", RegexOptions.CultureInvariant)]
    private static partial Regex NamePattern();
}
