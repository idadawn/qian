> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class DomainException

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Domain](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/index.md)\
Assembly: Kun.Core.dll

Base exception for systemic domain-level failures that are not expressible via `Result<T>`.

Kun handler code should prefer [Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md) for business failures.
`DomainException` is reserved for exceptional states the framework itself surfaces
(concurrency conflicts, repository contract violations, infrastructure-level faults).

```csharp
public abstract class DomainException : Exception, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ←
[DomainException](/pr-preview/pr-122/sdk/api/kun/kun-core-domain/domainexception.md)

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
[Exception.SerializeObjectState](https://learn.microsoft.com/dotnet/api/system.exception.serializeobjectstate),
[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### DomainException(string, string) \{#Kun_Core_Domain_DomainException__ctor_System_String_System_String\_}

Initializes a new instance with a stable error `code` and `message`.

```csharp
protected DomainException(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

### DomainException(string, string, Exception) \{#Kun_Core_Domain_DomainException__ctor_System_String_System_String_System_Exception\_}

Initializes a new instance wrapping an `innerException`.

```csharp
protected DomainException(string code, string message, Exception innerException)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

`innerException` [Exception](https://learn.microsoft.com/dotnet/api/system.exception)

## Properties

### Code \{#Kun_Core_Domain_DomainException_Code}

Stable machine-readable error code (e.g. `"order.concurrencyConflict"`).

```csharp
public string Code { get; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
