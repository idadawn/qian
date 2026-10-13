using Kun.Core.Cqrs;
using Kun.EventBus;

internal sealed record ThingCreated(Guid Id);
internal sealed record CreateThing(Guid Id) : ICommand<int>;
// 写命令的处理器：允许在同一作用域发布集成事件。
internal sealed class Handler(IIntegrationEventBus bus) : IRequestHandler<CreateThing, int>
{
    public ValueTask<int> Handle(CreateThing request, CancellationToken token)
    {
        bus.Publish(new ThingCreated(request.Id));
        return ValueTask.FromResult(1);
    }
}
