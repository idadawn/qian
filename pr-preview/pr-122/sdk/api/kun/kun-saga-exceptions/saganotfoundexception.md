> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class SagaNotFoundException

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Exceptions](/pr-preview/pr-122/sdk/api/kun/kun-saga-exceptions/index.md)\
Assembly: Kun.Saga.dll

Thrown when a saga instance cannot be found in the persistent store by its
[SagaNotFoundException.SagaId](/pr-preview/pr-122/sdk/api/kun/kun-saga-exceptions/saganotfoundexception.md#Kun_Saga_Exceptions_SagaNotFoundException_SagaId), typically indicating the instance was never started or has been
purged.

```csharp
public sealed class SagaNotFoundException : Exception, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ←
[SagaNotFoundException](/pr-preview/pr-122/sdk/api/kun/kun-saga-exceptions/saganotfoundexception.md)

#### Implements

[ISerializable](https://learn.microsoft.com/dotnet/api/system.runtime.serialization.iserializable)

#### Inherited Members

[Exception.GetBaseException()](https://learn.microsoft.com/dotnet/api/system.exception.getbaseexception),
[Exception.ToString()](https://learn.microsoft.com/dotnet/api/system.exception.tostring),
[Exception.GetType()](https://learn.microsoft.com/dotnet/api/system.exception.gettype),
[Exception.TargetSite](https://learn.microsoft.com/dotnet/api/system.exception.targetsite),
[Exception.Message](https://learn.microsoft.com/dotnet/api/system.exception.message),
[Exception.Data](https://learn.microsoft.com/dotnet/api/system.exception.data),
[Exception.InnerException](https://learn.microsoft.com/dotnet/api/system.exception.innerexception),
[Exception.HelpLink](https://learn.microsoft.com/dotnet/api/system.exception.helplink),
[Exception.Source](https://learn.microsoft.com/dotnet/api/system.exception.source),
[Exception.HResult](https://learn.microsoft.com/dotnet/api/system.exception.hresult),
[Exception.StackTrace](https://learn.microsoft.com/dotnet/api/system.exception.stacktrace),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SagaNotFoundException(Guid) \{#Kun_Saga_Exceptions_SagaNotFoundException__ctor_System_Guid\_}

Initializes a new [SagaNotFoundException](/pr-preview/pr-122/sdk/api/kun/kun-saga-exceptions/saganotfoundexception.md).

```csharp
public SagaNotFoundException(Guid sagaId)
```

#### Parameters

`sagaId` [Guid](https://learn.microsoft.com/dotnet/api/system.guid)

The saga instance ID that was not found.

## Properties

### SagaId \{#Kun_Saga_Exceptions_SagaNotFoundException_SagaId}

Gets the saga instance ID that was not found.

```csharp
public Guid SagaId { get; }
```

#### Property Value

[Guid](https://learn.microsoft.com/dotnet/api/system.guid)
