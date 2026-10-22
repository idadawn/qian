using System.Text;
using Qian.Testing.Containers;
using RabbitMQ.Client;

namespace Qian.Events.Tests;

/// <summary>死信拓扑的 broker 侧行为（真实 RabbitMQ 4）：投递上限触发的死信按死信队列名转入本订阅的死信队列；at-least-once 下死信目标不可达时消息滞留源队列。</summary>
public sealed class DeadLetterTopologyTests(RabbitMqFixture rabbit) : IClassFixture<RabbitMqFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 竞争队列被反复放回达到投递上限后_broker只把消息转入本订阅的死信队列()
    {
        var service = "qian.events-tests.d" + Guid.NewGuid().ToString("N")[..8];
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) }.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        var crashing = await RabbitTopology.DeclareSubscriptionAsync(channel, service, "crashing", ["tests.ping.sent"], Ct);
        var healthy = await RabbitTopology.DeclareSubscriptionAsync(channel, service, "healthy", ["tests.ping.sent"], Ct);
        await channel.BasicPublishAsync(QianEventTopology.Exchange, "tests.ping.sent", mandatory: true, new BasicProperties { MessageId = "crash-loop" },
            Encoding.UTF8.GetBytes("{}"), Ct);

        // 崩溃循环：每次取到消息后通道直接断开、不确认（等价于处理中途进程退出）。RabbitMQ 4 的 quorum 队列只对这种「消费者消失」计投递次数，
        // basic.nack 放回不计（只记 x-acquired-count），所以消费端停机时的放回不会耗掉投递上限。
        var deliveries = 0;
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "crashing");
        for (var round = 0; round < 50 && await CountAsync(channel, deadLetter) == 0; round++)
        {
            await using var consumer = await connection.CreateChannelAsync(cancellationToken: Ct);
            if (await consumer.BasicGetAsync(crashing, autoAck: false, Ct) is not null) deliveries++;
            else await Task.Delay(50, Ct);
        }
        Assert.Equal(RabbitTopology.DeliveryLimit + 1, deliveries);
        Assert.Equal(1, await CountAsync(channel, QianEventTopology.DeadLetterQueue(service, "crashing")));
        // 死信按死信队列名精确路由：订阅同一 subject 的另一个订阅既不会收到这条死信，自己的副本也不受影响。
        Assert.Equal(0, await CountAsync(channel, QianEventTopology.DeadLetterQueue(service, "healthy")));
        Assert.Equal(1, await CountAsync(channel, healthy));
        var dead = await channel.BasicGetAsync(QianEventTopology.DeadLetterQueue(service, "crashing"), autoAck: true, Ct);
        Assert.Equal(("crash-loop", QianEventTopology.DeadLetterExchange), (dead!.BasicProperties.MessageId, dead.Exchange));
    }

    [Fact]
    public async Task at_least_once死信目标没有绑定队列时_消息滞留在源队列而不是丢失_所以死信队列先于主队列声明()
    {
        var service = "qian.events-tests.o" + Guid.NewGuid().ToString("N")[..8];
        var queue = QianEventTopology.SubscriptionQueue(service, "orphan");
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) }.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(new CreateChannelOptions(true, true), Ct);
        await RabbitTopology.DeclareExchangesAsync(channel, Ct);
        // 与竞争队列同样的死信参数，但死信路由键指向一个没有任何队列绑定的名字。
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false, new Dictionary<string, object?>
        {
            ["x-queue-type"] = "quorum",
            ["x-dead-letter-exchange"] = QianEventTopology.DeadLetterExchange,
            ["x-dead-letter-routing-key"] = QianEventTopology.DeadLetterQueue(service, "orphan"),
            ["x-dead-letter-strategy"] = "at-least-once",
            ["x-overflow"] = "reject-publish",
        }, cancellationToken: Ct);
        await channel.BasicPublishAsync("", queue, mandatory: true, new BasicProperties { MessageId = "orphan" }, Encoding.UTF8.GetBytes("{}"), Ct);
        var message = await channel.BasicGetAsync(queue, autoAck: false, Ct);
        await channel.BasicRejectAsync(message!.DeliveryTag, requeue: false, Ct);
        await Task.Delay(TimeSpan.FromSeconds(1), Ct);

        // 已死信、等待目标确认的消息不计入 messages，只在 quorum 队列的 messages_dlx 里；rabbitmqctl list_queues 没有这一列，经 eval 读队列信息。
        var info = await rabbit.Container.ExecAsync(["rabbitmqctl", "eval",
            "{ok, Q} = rabbit_amqqueue:lookup(rabbit_misc:r(<<\"/\">>, queue, <<\"" + queue + "\">>)), rabbit_amqqueue:info(Q, [messages, messages_dlx])."], Ct);
        Assert.Equal(0, info.ExitCode);
        Assert.Contains("{messages,0}", info.Stdout, StringComparison.Ordinal);
        Assert.Contains("{messages_dlx,1}", info.Stdout, StringComparison.Ordinal);
    }

    private static async Task<long> CountAsync(IChannel channel, string queue) => (await channel.QueueDeclarePassiveAsync(queue, Ct)).MessageCount;
}
