using Kun.Core.Cqrs;
using Kun.EventBus;

internal sealed record ThingRead(Guid Id);
internal sealed record GetThing(Guid Id) : IQuery<int>;
// 查询处理器不得发布集成事件。
internal sealed class Handler(IIntegrationEventBus bus) : IRequestHandler<GetThing, int>
{
    public ValueTask<int> Handle(GetThing request, CancellationToken token)
    {
        bus.Publish(new ThingRead(request.Id));
        return ValueTask.FromResult(1);
    }
}
