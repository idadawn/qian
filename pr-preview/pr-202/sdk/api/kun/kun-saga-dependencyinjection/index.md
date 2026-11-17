> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Namespace Kun.Saga.DependencyInjection

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

### Classes

[KunSagaBuilder](/pr-preview/pr-202/sdk/api/kun/kun-saga-dependencyinjection/kunsagabuilder.md)

Builder for registering saga types and their steps with the Kun Saga infrastructure.
Obtained via [KunSagaServiceCollectionExtensions.AddKunSaga](/pr-preview/pr-202/sdk/api/kun/kun-saga-dependencyinjection/kunsagaservicecollectionextensions.md#Kun_Saga_DependencyInjection_KunSagaServiceCollectionExtensions_AddKunSaga_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Saga_DependencyInjection_KunSagaBuilder__).

[KunSagaServiceCollectionExtensions](/pr-preview/pr-202/sdk/api/kun/kun-saga-dependencyinjection/kunsagaservicecollectionextensions.md)

Extension methods for [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) to register Kun Saga services.
