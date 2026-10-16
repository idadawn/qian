> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ValidationError

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation](/sdk/api/kun/kun-validation/index.md)\
Assembly: Kun.Validation.dll

A single validation failure on a specific property or at the object level.

```csharp
public sealed record ValidationError : IEquatable<ValidationError>
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ValidationError](/sdk/api/kun/kun-validation/validationerror.md)

#### Implements

[IEquatable\<ValidationError>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### ValidationError(string, string, string) \{#Kun_Validation_ValidationError__ctor_System_String_System_String_System_String\_}

A single validation failure on a specific property or at the object level.

```csharp
public ValidationError(string PropertyName, string Code, string Message)
```

#### Parameters

`PropertyName` [string](https://learn.microsoft.com/dotnet/api/system.string)

The name of the property that failed validation, or empty string for object-level errors.

`Code` [string](https://learn.microsoft.com/dotnet/api/system.string)

A stable, machine-readable error code.

`Message` [string](https://learn.microsoft.com/dotnet/api/system.string)

A human-readable description of the failure.

## Properties

### Code \{#Kun_Validation_ValidationError_Code}

A stable, machine-readable error code.

```csharp
public string Code { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### Message \{#Kun_Validation_ValidationError_Message}

A human-readable description of the failure.

```csharp
public string Message { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### PropertyName \{#Kun_Validation_ValidationError_PropertyName}

The name of the property that failed validation, or empty string for object-level errors.

```csharp
public string PropertyName { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Methods

### \<Clone>$() \{#Kun_Validation_ValidationError__Clone\_\_}

```csharp
public ValidationError <Clone>$()
```

#### Returns

[ValidationError](/sdk/api/kun/kun-validation/validationerror.md)

### Deconstruct(out string, out string, out string) \{#Kun_Validation_ValidationError_Deconstruct_System_String__System_String__System_String\_\_}

```csharp
public void Deconstruct(out string PropertyName, out string Code, out string Message)
```

#### Parameters

`PropertyName` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Code` [string](https://learn.microsoft.com/dotnet/api/system.string)

`Message` [string](https://learn.microsoft.com/dotnet/api/system.string)

### Equals(object?) \{#Kun_Validation_ValidationError_Equals_System_Object\_}

```csharp
public override bool Equals(object? obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ValidationError?) \{#Kun_Validation_ValidationError_Equals_Kun_Validation_ValidationError\_}

```csharp
public bool Equals(ValidationError? other)
```

#### Parameters

`other` [ValidationError](/sdk/api/kun/kun-validation/validationerror.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### GetHashCode() \{#Kun_Validation_ValidationError_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Validation_ValidationError_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(ValidationError?, ValidationError?) \{#Kun_Validation_ValidationError_op_Equality_Kun_Validation_ValidationError_Kun_Validation_ValidationError\_}

```csharp
public static bool operator ==(ValidationError? left, ValidationError? right)
```

#### Parameters

`left` [ValidationError](/sdk/api/kun/kun-validation/validationerror.md)?

`right` [ValidationError](/sdk/api/kun/kun-validation/validationerror.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ValidationError?, ValidationError?) \{#Kun_Validation_ValidationError_op_Inequality_Kun_Validation_ValidationError_Kun_Validation_ValidationError\_}

```csharp
public static bool operator !=(ValidationError? left, ValidationError? right)
```

#### Parameters

`left` [ValidationError](/sdk/api/kun/kun-validation/validationerror.md)?

`right` [ValidationError](/sdk/api/kun/kun-validation/validationerror.md)?

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
