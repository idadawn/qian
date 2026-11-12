> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.EventBus.RabbitMQ

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.EventBus.RabbitMQ` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.EventBus.RabbitMQ`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.EventBus.RabbitMQ)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Namespaces

[Kun.EventBus.RabbitMQ.Consuming](/sdk/api/kun/kun-eventbus-rabbitmq-consuming/index.md)

[Kun.EventBus.RabbitMQ.DependencyInjection](/sdk/api/kun/kun-eventbus-rabbitmq-dependencyinjection/index.md)

[Kun.EventBus.RabbitMQ.Options](/sdk/api/kun/kun-eventbus-rabbitmq-options/index.md)

[Kun.EventBus.RabbitMQ.Publishing](/sdk/api/kun/kun-eventbus-rabbitmq-publishing/index.md)

### Classes

[RabbitMqConnectionProvider](/sdk/api/kun/kun-eventbus-rabbitmq/rabbitmqconnectionprovider.md)

Singleton that owns and lazily opens the `IConnection` to RabbitMQ.
Automatic recovery is enabled via `AutomaticRecoveryEnabled`.
