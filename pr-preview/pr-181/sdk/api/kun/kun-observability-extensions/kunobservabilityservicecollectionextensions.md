> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunObservabilityServiceCollectionExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Observability` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Observability`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Observability)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Observability.Extensions](/pr-preview/pr-181/sdk/api/kun/kun-observability-extensions/index.md)\
Assembly: Kun.Observability.dll

Service-collection extensions that register unified Kun observability.

```csharp
public static class KunObservabilityServiceCollectionExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunObservabilityServiceCollectionExtensions](/pr-preview/pr-181/sdk/api/kun/kun-observability-extensions/kunobservabilityservicecollectionextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Methods

### AddKunObservability(IServiceCollection, Action\<KunObservabilityOptions>?) \{#Kun_Observability_Extensions_KunObservabilityServiceCollectionExtensions_AddKunObservability_Microsoft_Extensions_DependencyInjection_IServiceCollection_System_Action_Kun_Observability_Abstractions_KunObservabilityOptions\_\_}

Registers traces, metrics, and logs via OpenTelemetry with all
Kun ActivitySources pre-wired and OTLP export enabled. Safe to
call multiple times against the same service collection — the
second invocation is a no-op guarded by an internal marker.

```csharp
public static IServiceCollection AddKunObservability(this IServiceCollection services, Action<KunObservabilityOptions>? configure = null)
```

#### Parameters

`services` [IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The [`IServiceCollection`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection) to configure.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[KunObservabilityOptions](/pr-preview/pr-181/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md)>?

Optional callback to customize [KunObservabilityOptions](/pr-preview/pr-181/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md).

#### Returns

[IServiceCollection](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.iservicecollection)

The same `services` for chaining.
