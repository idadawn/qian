using Kun.Core.Cqrs;
using Kun.EventBus.Outbox;

internal sealed record CreateThing(Guid Id) : ICommand<int>;
// 即使在命令处理器里，直接向缓冲器追加也会绕过 v1 信封。
internal sealed class Handler(IntegrationEventBuffer buffer) : IRequestHandler<CreateThing, int>
{
    public ValueTask<int> Handle(CreateThing request, CancellationToken token)
    {
        buffer.Add(new OutboxMessage(request.Id, "thing.created.v1", "{}", null, null, null, DateTimeOffset.UtcNow));
        return ValueTask.FromResult(1);
    }
}
