using System.Text;
using RabbitMQ.Client;

namespace Qian.Testing.Containers.IntegrationTests;

[Collection(SharedInfrastructure.Name)]
public sealed class RabbitMqFixtureTests(InfrastructureFixture infrastructure)
{
    [Fact]
    public async Task Published_message_can_be_consumed()
    {
        var ct = TestContext.Current.CancellationToken;
        var factory = new ConnectionFactory { Uri = new Uri(infrastructure.RabbitMq.ConnectionString) };
        await using var connection = await factory.CreateConnectionAsync(ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        var queue = await channel.QueueDeclareAsync(queue: string.Empty, durable: false, exclusive: true, autoDelete: true, cancellationToken: ct);
        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queue.QueueName, body: Encoding.UTF8.GetBytes("ping"), cancellationToken: ct);

        BasicGetResult? received = null;
        for (var attempt = 0; attempt < 100 && received is null; attempt++)
        {
            received = await channel.BasicGetAsync(queue.QueueName, autoAck: true, ct);
            if (received is null)
            {
                await Task.Delay(50, ct);
            }
        }

        Assert.NotNull(received);
        Assert.Equal("ping", Encoding.UTF8.GetString(received.Body.Span));
    }
}
