using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace Qian.Events.Tests;

// 失败路径的信号与上界（S1-EVT-004）：并发重复投递让 handler 执行两次时有日志与计数；重试或死信副本持续转发失败时，handler 不再反复执行。
public sealed partial class SubscriberTests
{
    [Fact]
    public async Task 同一事件并发投递到两个实例_两边都执行handler_后写幂等记录的一方记Warning并计duplicate_executed()
    {
        var service = Service();
        // 屏障：两次执行都进了 handler（都已越过「先查幂等记录」）才一起放行，于是两边都会去写记录，后写的一方 TryMarkProcessedAsync 返回 false。
        var entered = 0;
        var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Gate()
        {
            if (Interlocked.Increment(ref entered) == 2) both.TrySetResult();
            await both.Task.WaitAsync(TimeSpan.FromSeconds(30), Ct);
        }
        var a = new Probe(_ => null, gate: Gate);
        var b = new Probe(_ => null, gate: Gate);
        using var metrics = new ConsumerMetrics(service + ".work");
        using var hostA = Host(service, a, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"), instance: "node-a", prefetch: 1);
        using var hostB = Host(service, b, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"), instance: "node-b", prefetch: 1);
        await Task.WhenAll(StartAsync(hostA), StartAsync(hostB));
        // Outbox 发布器在确认丢失时会重发同一行。两个实例的预取都是 1：第一份停在屏障前未确认，第二份只能投给另一个实例。
        var envelope = await PublishAsync(TenantA, sequence: 40, times: 2);

        // 结束态：两次投递都已确认并计 processed（duplicate_executed 与 Warning 日志在确认之前写出），主队列已清空，之后不会再有投递。
        await Until(async () => metrics.Count("processed") == 2 && await SettledAsync([QianEventTopology.SubscriptionQueue(service, "work")]), "两份副本没有都处理完");
        Assert.Equal((1, 1), (a.Calls.Count, b.Calls.Count));
        Assert.Equal(1, metrics.Count("duplicate_executed"));
        Assert.Equal(0, metrics.Count("duplicate"));
        var warning = Assert.Single(a.Logs.Entries.Concat(b.Logs.Entries), entry => entry.EventId == 35);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains(envelope.EventId.ToString(), warning.Message, StringComparison.Ordinal);
        Assert.Contains(service + ".work", warning.Message, StringComparison.Ordinal);
        Assert.Equal(["idemp:" + service + ".work:" + envelope.EventId], await IdempotencyKeysAsync(service + ".work"));
        await Task.WhenAll(hostA.StopAsync(Ct), hostB.StopAsync(Ct));
    }

    [Fact]
    public async Task 死信转发持续失败_原地重发3次后nack不放回_handler只执行一次_消息按队列死信参数滞留源队列不丢失()
    {
        var service = Service();
        var probe = new Probe(_ => new InvalidOperationException("确定性失败"));
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"));
        await StartAsync(host);
        var queue = QianEventTopology.SubscriptionQueue(service, "work");
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");
        // 运行中删掉死信队列：消费端的死信副本（mandatory）无处可路由，broker 退回，转发失败。
        await DeleteQueueAsync(deadLetter);
        var envelope = await PublishAsync(TenantA, sequence: 50);

        // 结束态：forward_failed 已计数，原消息已 nack 不放回；broker 按 at-least-once 死信参数转发同样无处可去，消息滞留源队列（messages 0、messages_dlx 1），
        // 不会再投给消费者。旧实现在这里把原消息放回、handler 立刻再次执行，永远到不了这个状态。
        await Until(async () => metrics.Count("forward_failed") == 1 && await DeadLetterPendingAsync(queue) == (0, 1), "转发持续失败后原消息没有交给 broker 死信");
        Assert.Single(probe.Calls);
        Assert.Equal(QianEventSubscriber.ForwardAttempts, probe.Logs.Entries.Count(entry => entry.EventId == 28 && entry.Message.Contains(deadLetter, StringComparison.Ordinal)));
        var abandoned = Assert.Single(probe.Logs.Entries, entry => entry.EventId == 36);
        Assert.Equal(LogLevel.Error, abandoned.Level);
        Assert.Contains(envelope.EventId.ToString(), abandoned.Message, StringComparison.Ordinal);
        Assert.Contains("handler_rejected", abandoned.Message, StringComparison.Ordinal);
        Assert.Equal((0, 0, 0), (metrics.Count("dead_lettered"), metrics.Count("retried"), metrics.Count("processed")));
        Assert.Empty(await IdempotencyKeysAsync(service + ".work"));
        await host.StopAsync(Ct);
    }

    [Fact]
    public async Task 重试转发持续失败_nack不放回由broker按队列死信参数转入本订阅死信队列_handler只执行一次()
    {
        var service = Service();
        var probe = new Probe(_ => new FlakyDependencyException());
        using var metrics = new ConsumerMetrics(service + ".work");
        using var host = Host(service, probe, subscriber => subscriber.Subscribe<PingSent, ProbeHandler>("work"), retryBaseDelay: TimeSpan.FromMilliseconds(100));
        await StartAsync(host);
        var queue = QianEventTopology.SubscriptionQueue(service, "work");
        var deadLetter = QianEventTopology.DeadLetterQueue(service, "work");
        var retry = QianEventTopology.RetryQueue(service, "work", 1, TimeSpan.FromMilliseconds(100));
        // 抢先以不同参数声明第 1 个重试队列：消费端声明它时 PRECONDITION_FAILED，重试副本转发不出去。
        await DeclareClassicQueueAsync(retry);
        var envelope = await PublishAsync(TenantA, sequence: 51);

        // 结束态：forward_failed 已计数，broker 已把原消息转入本订阅的死信队列，主队列已清空，之后不会再有投递。
        await Until(async () => metrics.Count("forward_failed") == 1 && (await QueuesAsync()).GetValueOrDefault(deadLetter) == 1 && await SettledAsync([queue]),
            "重试转发持续失败后原消息没有由 broker 转入死信队列");
        Assert.Single(probe.Calls);
        Assert.Equal((0, 0, 0), (metrics.Count("retried"), metrics.Count("dead_lettered"), metrics.Count("processed")));
        Assert.Contains("retry", Assert.Single(probe.Logs.Entries, entry => entry.EventId == 36).Message, StringComparison.Ordinal);
        // broker 侧死信：消息体与 MessageId 不变，没有消费端的 x-qian-* 头，死信原因记在 broker 的 x-first-death-reason。
        var message = await GetAsync(deadLetter);
        Assert.Equal(envelope.ToJson(), Encoding.UTF8.GetString(message.Body.Span));
        Assert.Equal(envelope.EventId.ToString("D"), message.BasicProperties.MessageId);
        Assert.Null(Header(message, "x-qian-reason"));
        Assert.Equal(("rejected", queue), (Header(message, "x-first-death-reason"), Header(message, "x-first-death-queue")));
        await host.StopAsync(Ct);
    }

    private async Task DeleteQueueAsync(string queue)
    {
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) }.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.QueueDeleteAsync(queue, ifUnused: false, ifEmpty: false, cancellationToken: Ct);
    }

    private async Task DeclareClassicQueueAsync(string queue)
    {
        await using var connection = await new ConnectionFactory { Uri = new Uri(rabbit.ConnectionString) }.CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            new Dictionary<string, object?> { ["x-queue-type"] = "classic" }, cancellationToken: Ct);
    }

    // quorum 队列已死信、等待目标确认的消息不计入 messages，只在 messages_dlx；rabbitmqctl list_queues 没有这一列，经 eval 读队列信息。
    private async Task<(long Messages, long DeadLettered)> DeadLetterPendingAsync(string queue)
    {
        var info = await rabbit.Container.ExecAsync(["rabbitmqctl", "eval",
            "{ok, Q} = rabbit_amqqueue:lookup(rabbit_misc:r(<<\"/\">>, queue, <<\"" + queue + "\">>)), rabbit_amqqueue:info(Q, [messages, messages_dlx])."], Ct);
        Assert.Equal(0, info.ExitCode);
        return (Count(info.Stdout, "messages"), Count(info.Stdout, "messages_dlx"));

        static long Count(string stdout, string key)
        {
            var match = System.Text.RegularExpressions.Regex.Match(stdout, "\\{" + key + ",(\\d+)\\}");
            return match.Success ? long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : -1;
        }
    }
}
