> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class Error

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Core)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Results](/sdk/api/kun/kun-core-results/index.md)\
Assembly: Kun.Core.dll

强类型错误。相比抛异常，[Error](/sdk/api/kun/kun-core-results/error.md) 是显式的、可测试的失败信号，
也更符合 CQRS Handler 的函数式风格。

```csharp
public sealed record Error : IEquatable<Error>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[Error](/sdk/api/kun/kun-core-results/error.md)

#### Implements

[IEquatable\<Error>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### Error(string, string, ErrorType) \{#Kun_Core_Results_Error__ctor_System_String_System_String_Kun_Core_Results_ErrorType\_}

强类型错误。相比抛异常，[Error](/sdk/api/kun/kun-core-results/error.md) 是显式的、可测试的失败信号，
也更符合 CQRS Handler 的函数式风格。

```csharp
public Error(string Code, string Message, ErrorType Type)
```

#### Parameters

`Code` [string](https://learn.microsoft.com/dotnet/api/system.string)

机器可读错误代码（如 `order.notFound`）。

`Message` [string](https://learn.microsoft.com/dotnet/api/system.string)

人类可读错误描述。

`Type` [ErrorType](/sdk/api/kun/kun-core-results/errortype.md)

错误分类，决定映射到哪个 HTTP/gRPC 状态码。

## Fields

### None \{#Kun_Core_Results_Error_None}

Sentinel value representing the absence of an error on a success result.

```csharp
public static readonly Error None
```

#### Field Value

[Error](/sdk/api/kun/kun-core-results/error.md)

## Properties

### Code \{#Kun_Core_Results_Error_Code}

机器可读错误代码（如 `order.notFound`）。

```csharp
public string Code { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Message \{#Kun_Core_Results_Error_Message}

人类可读错误描述。

```csharp
public string Message { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Type \{#Kun_Core_Results_Error_Type}

错误分类，决定映射到哪个 HTTP/gRPC 状态码。

```csharp
public ErrorType Type { get; init; }
```

#### Property Value

[ErrorType](/sdk/api/kun/kun-core-results/errortype.md)

## Methods

### \<Clone>$() \{#Kun_Core_Results_Error__Clone\_\_}

```csharp
public Error <Clone>$()
```

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### Conflict(string, string) \{#Kun_Core_Results_Error_Conflict_System_String_System_String\_}

Creates a [ErrorType.Conflict](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_Conflict) error.

```csharp
public static Error Conflict(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### Deconstruct(out string, out string, out ErrorType) \{#Kun_Core_Results_Error_Deconstruct_System_String__System_String__Kun_Core_Results_ErrorType\_\_}

```csharp
public void Deconstruct(out string Code, out string Message, out ErrorType Type)
```

#### Parameters

`Code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Message` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Type` [ErrorType](/sdk/api/kun/kun-core-results/errortype.md)

### Equals(object?) \{#Kun_Core_Results_Error_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(Error?) \{#Kun_Core_Results_Error_Equals_Kun_Core_Results_Error\_}

```csharp
public bool Equals(Error? other)
```

#### Parameters

`other` [Error](/sdk/api/kun/kun-core-results/error.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Failure(string, string) \{#Kun_Core_Results_Error_Failure_System_String_System_String\_}

Creates a generic [ErrorType.Failure](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_Failure) error.

```csharp
public static Error Failure(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### Forbidden(string, string) \{#Kun_Core_Results_Error_Forbidden_System_String_System_String\_}

Creates a [ErrorType.Forbidden](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_Forbidden) error.

```csharp
public static Error Forbidden(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### GetHashCode() \{#Kun_Core_Results_Error_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### NotFound(string, string) \{#Kun_Core_Results_Error_NotFound_System_String_System_String\_}

Creates a [ErrorType.NotFound](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_NotFound) error.

```csharp
public static Error NotFound(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### ToString() \{#Kun_Core_Results_Error_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Unauthorized(string, string) \{#Kun_Core_Results_Error_Unauthorized_System_String_System_String\_}

Creates an [ErrorType.Unauthorized](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_Unauthorized) error.

```csharp
public static Error Unauthorized(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### Unavailable(string, string) \{#Kun_Core_Results_Error_Unavailable_System_String_System_String\_}

Creates an [ErrorType.Unavailable](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_Unavailable) error.

```csharp
public static Error Unavailable(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

### Validation(string, string) \{#Kun_Core_Results_Error_Validation_System_String_System_String\_}

Creates an [ErrorType.Validation](/sdk/api/kun/kun-core-results/errortype.md#Kun_Core_Results_ErrorType_Validation) error.

```csharp
public static Error Validation(string code, string message)
```

#### Parameters

`code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`message` [string](https://learn.microsoft.com/dotnet/api/system.string)

#### Returns

[Error](/sdk/api/kun/kun-core-results/error.md)

## Operators

### operator ==(Error?, Error?) \{#Kun_Core_Results_Error_op_Equality_Kun_Core_Results_Error_Kun_Core_Results_Error\_}

```csharp
public static bool operator ==(Error? left, Error? right)
```

#### Parameters

`left` [Error](/sdk/api/kun/kun-core-results/error.md)?

`right` [Error](/sdk/api/kun/kun-core-results/error.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(Error?, Error?) \{#Kun_Core_Results_Error_op_Inequality_Kun_Core_Results_Error_Kun_Core_Results_Error\_}

```csharp
public static bool operator !=(Error? left, Error? right)
```

#### Parameters

`left` [Error](/sdk/api/kun/kun-core-results/error.md)?

`right` [Error](/sdk/api/kun/kun-core-results/error.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
