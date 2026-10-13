using Kun.Core.Cqrs;
using Kun.EventBus.Outbox;
using Qian.Plugin.Sdk;

internal sealed record ThingCreated(Guid Id);
[Capability("thing.create", typeof(int), typeof(object), SideEffect = "write")]
internal sealed record CreateThing(Guid Id) : IRequest<int>;
// SDK 能力请求按 SideEffect 区分命令，write 允许发布。
internal sealed class Handler(IOutboxWriter writer) : IRequestHandler<CreateThing, int>
{
    public ValueTask<int> Handle(CreateThing request, CancellationToken token)
    {
        writer.Enqueue(new ThingCreated(request.Id));
        return ValueTask.FromResult(1);
    }
}
