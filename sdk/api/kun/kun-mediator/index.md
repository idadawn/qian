> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Mediator

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Mediator` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Mediator`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Mediator)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.Mediator.Behaviors](/sdk/api/kun/kun-mediator-behaviors/index.md)

[Kun.Mediator.DependencyInjection](/sdk/api/kun/kun-mediator-dependencyinjection/index.md)

### Classes

[CapabilityDescriptor](/sdk/api/kun/kun-mediator/capabilitydescriptor.md)

命令或查询的稳定标识与 JSON 请求／响应契约。

[KunMediator](/sdk/api/kun/kun-mediator/kunmediator.md)

Reflection-free CQRS dispatcher. Handler lookup is O(1) via a pre-built
[`FrozenDictionary`](https://learn.microsoft.com/dotnet/api/system.collections.frozen.frozendictionary-2). All handler types are explicitly
registered at startup through [KunMediatorBuilder](/sdk/api/kun/kun-mediator/kunmediatorbuilder.md), so no runtime
scanning or generic type construction is needed — this class is fully AOT-safe.

[KunMediatorBuilder](/sdk/api/kun/kun-mediator/kunmediatorbuilder.md)

Fluent builder used to register request handlers, pipeline behaviors, and notification
handlers with [KunMediator](/sdk/api/kun/kun-mediator/kunmediator.md). All registrations are performed via
compile-time-closed generics — no runtime reflection or `MakeGenericType` occurs.

### Interfaces

[ICapabilityRegistry](/sdk/api/kun/kun-mediator/icapabilityregistry.md)

只读能力目录；调用权限与 scope 过滤由上层服务完成。

[INotification](/sdk/api/kun/kun-mediator/inotification.md)

Marker interface for cross-cutting notifications broadcast to many handlers.

[INotificationHandler\<TNotification>](/sdk/api/kun/kun-mediator/inotificationhandler-1.md)

Handler for a specific [INotification](/sdk/api/kun/kun-mediator/inotification.md).

[IPipelineBehavior\<TRequest, TResponse>](/sdk/api/kun/kun-mediator/ipipelinebehavior-2.md)

Pipeline behavior wrapping a [IRequestHandler\<TRequest, TResponse>](/sdk/api/kun/kun-core-cqrs/irequesthandler-2.md).
Behaviors form a nested "onion": outer behaviors see both the pre- and post-handler phases
of all inner behaviors.

[IPublisher](/sdk/api/kun/kun-mediator/ipublisher.md)

Publishes a notification to all registered handlers.

[IScopedPublisher](/sdk/api/kun/kun-mediator/iscopedpublisher.md)

由事务宿主显式指定作用域，保证通知处理器复用同一 DbContext 与 Outbox 缓冲。

[ISender](/sdk/api/kun/kun-mediator/isender.md)

Dispatches a single request to its registered handler.

### Delegates

[RequestHandlerDelegate\<TResponse>](/sdk/api/kun/kun-mediator/requesthandlerdelegate-1.md)

Delegate passed to a pipeline behavior representing the next step in the chain.
