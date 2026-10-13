using Kun.Core.Cqrs;
using Kun.EventBus.Outbox;
using Qian.Plugin.Sdk;

internal sealed record ThingRead(Guid Id);
[Capability("thing.get", typeof(int), typeof(object))]
internal sealed record GetThing(Guid Id) : IRequest<int>;
// 未声明 SideEffect 的能力默认是 read 查询，不能发布。
internal sealed class Handler(IOutboxWriter writer) : IRequestHandler<GetThing, int>
{
    public ValueTask<int> Handle(GetThing request, CancellationToken token)
    {
        writer.Enqueue(new ThingRead(request.Id));
        return ValueTask.FromResult(1);
    }
}
