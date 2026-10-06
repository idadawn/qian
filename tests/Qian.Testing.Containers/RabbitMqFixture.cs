using Testcontainers.RabbitMq;

namespace Qian.Testing.Containers;

/// <summary>RabbitMQ 容器。韧性测试可经 <see cref="ContainerFixture{TContainer}.Container"/> 停启 broker。</summary>
public sealed class RabbitMqFixture : ContainerFixture<RabbitMqContainer>
{
    /// <summary>AMQP 连接串（<c>amqp://user:password@host:port/</c>）。</summary>
    public string ConnectionString { get; private set; } = string.Empty;

    /// <inheritdoc />
    protected override RabbitMqContainer CreateContainer() =>
        new RabbitMqBuilder(ContainerImages.RabbitMq)
            .WithUsername("qian")
            .WithPassword(NewSecret())
            .Build();

    /// <inheritdoc />
    protected override ValueTask OnStartedAsync(RabbitMqContainer container, CancellationToken cancellationToken)
    {
        ConnectionString = container.GetConnectionString();
        return ValueTask.CompletedTask;
    }
}
