> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Struct ValidationResult

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Validation` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Validation`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Validation)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Validation](/sdk/api/kun/kun-validation/index.md)\
Assembly: Kun.Validation.dll

Result of running a validator against an instance. [ValidationResult.IsValid](/sdk/api/kun/kun-validation/validationresult.md#Kun_Validation_ValidationResult_IsValid) is true iff
[ValidationResult.Errors](/sdk/api/kun/kun-validation/validationresult.md#Kun_Validation_ValidationResult_Errors) is empty.

```csharp
public readonly struct ValidationResult : IEquatable<ValidationResult>
```

#### Implements

[IEquatable\<ValidationResult>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Properties

### Errors \{#Kun_Validation_ValidationResult_Errors}

Errors accumulated by the validator. Empty collection means valid.

```csharp
public IReadOnlyList<ValidationError> Errors { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ValidationError](/sdk/api/kun/kun-validation/validationerror.md)>

### IsValid \{#Kun_Validation_ValidationResult_IsValid}

True iff [ValidationResult.Errors](/sdk/api/kun/kun-validation/validationresult.md#Kun_Validation_ValidationResult_Errors) is empty.

```csharp
public bool IsValid { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Success \{#Kun_Validation_ValidationResult_Success}

A successful validation with no errors.

```csharp
public static ValidationResult Success { get; }
```

#### Property Value

[ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

## Methods

### Equals(object) \{#Kun_Validation_ValidationResult_Equals_System_Object\_}

```csharp
public override bool Equals(object obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(ValidationResult) \{#Kun_Validation_ValidationResult_Equals_Kun_Validation_ValidationResult\_}

```csharp
public bool Equals(ValidationResult other)
```

#### Parameters

`other` [ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Failure(IReadOnlyList\<ValidationError>) \{#Kun_Validation_ValidationResult_Failure_System_Collections_Generic_IReadOnlyList_Kun_Validation_ValidationError\_\_}

Create a failing validation result from the given errors.

```csharp
public static ValidationResult Failure(IReadOnlyList<ValidationError> errors)
```

#### Parameters

`errors` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[ValidationError](/sdk/api/kun/kun-validation/validationerror.md)>

#### Returns

[ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

### GetHashCode() \{#Kun_Validation_ValidationResult_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### ToString() \{#Kun_Validation_ValidationResult_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(ValidationResult, ValidationResult) \{#Kun_Validation_ValidationResult_op_Equality_Kun_Validation_ValidationResult_Kun_Validation_ValidationResult\_}

```csharp
public static bool operator ==(ValidationResult left, ValidationResult right)
```

#### Parameters

`left` [ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

`right` [ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### operator !=(ValidationResult, ValidationResult) \{#Kun_Validation_ValidationResult_op_Inequality_Kun_Validation_ValidationResult_Kun_Validation_ValidationResult\_}

```csharp
public static bool operator !=(ValidationResult left, ValidationResult right)
```

#### Parameters

`left` [ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

`right` [ValidationResult](/sdk/api/kun/kun-validation/validationresult.md)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
