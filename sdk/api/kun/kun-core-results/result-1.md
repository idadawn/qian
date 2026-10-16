> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Struct Result\<T>

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Results](/sdk/api/kun/kun-core-results/index.md)\
Assembly: Kun.Core.dll

带返回值的 Result。Handler 成功时携带 `T`，失败时携带 [Result\<T>.Error](/sdk/api/kun/kun-core-results/result-1.md#Kun_Core_Results_Result_1_Error)。

```csharp
public readonly struct Result<T> : IEquatable<Result<T>>
```

#### Type Parameters

`T`

#### Implements

[IEquatable\<Result\<T>>](https://learn.microsoft.com/dotnet/api/system.iequatable-1)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

#### Extension Methods

[ResultExtensions.Bind\<T, TOut>(Result\<T>, Func\<T, Result\<TOut>>)](/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Bind__2_Kun_Core_Results_Result___0__System_Func___0_Kun_Core_Results_Result___1___),
[ResultExtensions.Bind\<T>(Result\<T>, Func\<T, Result>)](/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Bind__1_Kun_Core_Results_Result___0__System_Func___0_Kun_Core_Results_Result__),
[ResultExtensions.Ensure\<T>(Result\<T>, Func\<T, bool>, Error)](/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Ensure__1_Kun_Core_Results_Result___0__System_Func___0_System_Boolean__Kun_Core_Results_Error_),
[ResultExtensions.Map\<T, TOut>(Result\<T>, Func\<T, TOut>)](/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Map__2_Kun_Core_Results_Result___0__System_Func___0___1__),
[ResultExtensions.Match\<T, TOut>(Result\<T>, Func\<T, TOut>, Func\<Error, TOut>)](/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Match__2_Kun_Core_Results_Result___0__System_Func___0___1__System_Func_Kun_Core_Results_Error___1__),
[ResultExtensions.Tap\<T>(Result\<T>, Action\<T>)](/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Tap__1_Kun_Core_Results_Result___0__System_Action___0__)

## Properties

### Error \{#Kun_Core_Results_Result_1_Error}

The error carried by a failure result; [Error.None](/sdk/api/kun/kun-core-results/error.md#Kun_Core_Results_Error_None) on success.

```csharp
public Error Error { get; }
```

#### Property Value

[Error](/sdk/api/kun/kun-core-results/error.md)

### IsFailure \{#Kun_Core_Results_Result_1_IsFailure}

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when the operation failed.

```csharp
public bool IsFailure { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### IsSuccess \{#Kun_Core_Results_Result_1_IsSuccess}

[true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) when the operation succeeded.

```csharp
public bool IsSuccess { get; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Value \{#Kun_Core_Results_Result_1_Value}

仅在 [Result\<T>.IsSuccess](/sdk/api/kun/kun-core-results/result-1.md#Kun_Core_Results_Result_1_IsSuccess) 为 [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) 时可访问。
失败 Result 调用此属性会抛异常——应先用 [Result\<T>.IsSuccess](/sdk/api/kun/kun-core-results/result-1.md#Kun_Core_Results_Result_1_IsSuccess) 判断。

```csharp
public T Value { get; }
```

#### Property Value

T

## Methods

### EnsureSuccess() \{#Kun_Core_Results_Result_1_EnsureSuccess}

在 Handler 直接返回给 gRPC 端点时使用，失败时抛出可被拦截器捕获的异常。

```csharp
public T EnsureSuccess()
```

#### Returns

T

### Equals(object) \{#Kun_Core_Results_Result_1_Equals_System_Object\_}

```csharp
public override bool Equals(object obj)
```

#### Parameters

`obj` [object](https://learn.microsoft.com/dotnet/api/system.object)

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Equals(Result\<T>) \{#Kun_Core_Results_Result_1_Equals_Kun_Core_Results_Result__0\_\_}

```csharp
public bool Equals(Result<T> other)
```

#### Parameters

`other` [Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### Fail(Error) \{#Kun_Core_Results_Result_1_Fail_Kun_Core_Results_Error\_}

Creates a failure result carrying `error`.

```csharp
public static Result<T> Fail(Error error)
```

#### Parameters

`error` [Error](/sdk/api/kun/kun-core-results/error.md)

#### Returns

[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

### GetHashCode() \{#Kun_Core_Results_Result_1_GetHashCode}

```csharp
public override int GetHashCode()
```

#### Returns

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Ok(T) \{#Kun_Core_Results_Result_1_Ok__0\_}

Creates a successful result carrying `value`.

```csharp
public static Result<T> Ok(T value)
```

#### Parameters

`value` T

#### Returns

[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

### ToString() \{#Kun_Core_Results_Result_1_ToString}

```csharp
public override string ToString()
```

#### Returns

[string](https://learn.microsoft.com/dotnet/api/system.string)

## Operators

### operator ==(Result\<T>, Result\<T>) \{#Kun_Core_Results_Result_1_op_Equality_Kun_Core_Results_Result__0__Kun_Core_Results_Result__0\_\_}

```csharp
public static bool operator ==(Result<T> left, Result<T> right)
```

#### Parameters

`left` [Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

`right` [Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### implicit operator Result\<T>(T) \{#Kun_Core_Results_Result_1_op_Implicit__0__Kun_Core_Results_Result__0\_}

Implicit conversion from `T` to a success result.

```csharp
public static implicit operator Result<T>(T value)
```

#### Parameters

`value` T

#### Returns

[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

### implicit operator Result\<T>(Error) \{#Kun_Core_Results_Result_1_op_Implicit_Kun_Core_Results_Error__Kun_Core_Results_Result__0\_}

Implicit conversion from [Result\<T>.Error](/sdk/api/kun/kun-core-results/result-1.md#Kun_Core_Results_Result_1_Error) to a failure result.

```csharp
public static implicit operator Result<T>(Error error)
```

#### Parameters

`error` [Error](/sdk/api/kun/kun-core-results/error.md)

#### Returns

[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

### operator !=(Result\<T>, Result\<T>) \{#Kun_Core_Results_Result_1_op_Inequality_Kun_Core_Results_Result__0__Kun_Core_Results_Result__0\_\_}

```csharp
public static bool operator !=(Result<T> left, Result<T> right)
```

#### Parameters

`left` [Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

`right` [Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>

#### Returns

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
