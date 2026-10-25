> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class ValidationException

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation](/pr-preview/pr-122/sdk/api/kun/kun-validation/index.md)\
Assembly: Kun.Validation.dll

Thrown by [ValidationPipelineBehavior\<TRequest, TResponse>](/pr-preview/pr-122/sdk/api/kun/kun-validation-behaviors/validationpipelinebehavior-2.md)
when one or more validators report errors. Carries the full error list so
upstream handlers (e.g. ASP.NET Core exception handlers) can translate it.

```csharp
public sealed class ValidationException : Exception, ISerializable
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Exception](https://learn.microsoft.com/dotnet/api/system.exception) ←
[ValidationException](/pr-preview/pr-122/sdk/api/kun/kun-validation/validationexception.md)

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

### ValidationException(IReadOnlyList\<ValidationError>) \{#Kun_Validation_ValidationException__ctor_System_Collections_Generic_IReadOnlyList_Kun_Validation_ValidationError\_\_}

Initializes a new instance of [ValidationException](/pr-preview/pr-122/sdk/api/kun/kun-validation/validationexception.md) with the given errors.

```csharp
public ValidationException(IReadOnlyList<ValidationError> errors)
```

#### Parameters

`errors` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ValidationError](/pr-preview/pr-122/sdk/api/kun/kun-validation/validationerror.md)>

The validation errors. Must not be null.

## Properties

### Errors \{#Kun_Validation_ValidationException_Errors}

Errors reported by the validators.

```csharp
public IReadOnlyList<ValidationError> Errors { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ValidationError](/pr-preview/pr-122/sdk/api/kun/kun-validation/validationerror.md)>
