> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class SagaStepFailedException

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Saga` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Saga`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Saga)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Saga.Exceptions](/sdk/api/kun/kun-saga-exceptions/index.md)\
Assembly: Kun.Saga.dll

Thrown by the saga step executor when a step's `ExecuteAsync` or `CompensateAsync`
terminates with an unhandled exception. The inner exception carries the original failure.

```csharp
public sealed class SagaStepFailedException : Exception, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ←
[SagaStepFailedException](/sdk/api/kun/kun-saga-exceptions/sagastepfailedexception.md)

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

### SagaStepFailedException(string, Exception) \{#Kun_Saga_Exceptions_SagaStepFailedException__ctor_System_String_System_Exception\_}

Initializes a new [SagaStepFailedException](/sdk/api/kun/kun-saga-exceptions/sagastepfailedexception.md).

```csharp
public SagaStepFailedException(string stepName, Exception innerException)
```

#### Parameters

`stepName` [string](https://learn.microsoft.com/dotnet/api/system.string)

The name of the step that failed.

`innerException` [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

The exception raised by the step.

## Properties

### StepName \{#Kun_Saga_Exceptions_SagaStepFailedException_StepName}

Gets the name of the step that failed.

```csharp
public string StepName { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
