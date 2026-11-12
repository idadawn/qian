> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class ValueTaskResultExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Core)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Results](/sdk/api/kun/kun-core-results/index.md)\
Assembly: Kun.Core.dll

Asynchronous railway-oriented extensions for
[`ValueTask`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1) receivers whose result type is
[Result](/sdk/api/kun/kun-core-results/result.md) or [Result\<T>](/sdk/api/kun/kun-core-results/result-1.md). Follows ADR-011;
mirror of [TaskResultExtensions](/sdk/api/kun/kun-core-results/taskresultextensions.md) for hot paths that
avoid [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task) allocation (Mediator pipeline, Saga step
executors).

```csharp
public static class ValueTaskResultExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ValueTaskResultExtensions](/sdk/api/kun/kun-core-results/valuetaskresultextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Return types and async-delegate return types are
[`ValueTask`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask) / [`ValueTask`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1) to keep
the fluent chain on the ValueTask rail without
`.AsTask()` boundary friction. Callers holding
[`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1) sources should use
[TaskResultExtensions](/sdk/api/kun/kun-core-results/taskresultextensions.md) instead.

## Methods

### BindAsync(ValueTask\<Result>, Func\<Result>) \{#Kun_Core_Results_ValueTaskResultExtensions_BindAsync_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func_Kun_Core_Results_Result\_\_}

Awaits the source and chains a sync follow-up returning [Result](/sdk/api/kun/kun-core-results/result.md).

```csharp
public static ValueTask<Result> BindAsync(this ValueTask<Result> source, Func<Result> next)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`next` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

### BindAsync(ValueTask\<Result>, Func\<CancellationToken, ValueTask\<Result>>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_BindAsync_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___System_Threading_CancellationToken\_}

Awaits the source and chains an async follow-up returning [Result](/sdk/api/kun/kun-core-results/result.md).

```csharp
public static ValueTask<Result> BindAsync(this ValueTask<Result> source, Func<CancellationToken, ValueTask<Result>> next, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`next` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

### BindAsync\<T, TOut>(ValueTask\<Result\<T>>, Func\<T, Result\<TOut>>) \{#Kun_Core_Results_ValueTaskResultExtensions_BindAsync__2_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_Kun_Core_Results_Result___1\_\_\_}

Awaits the source and chains a sync follow-up returning [Result\<T>](/sdk/api/kun/kun-core-results/result-1.md).

```csharp
public static ValueTask<Result<TOut>> BindAsync<T, TOut>(this ValueTask<Result<T>> source, Func<T, Result<TOut>> bind)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [Result](/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### BindAsync\<T, TOut>(ValueTask\<Result\<T>>, Func\<T, CancellationToken, ValueTask\<Result\<TOut>>>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_BindAsync__2_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___1____System_Threading_CancellationToken\_}

Awaits the source and chains an async follow-up returning [Result\<T>](/sdk/api/kun/kun-core-results/result-1.md).

```csharp
public static ValueTask<Result<TOut>> BindAsync<T, TOut>(this ValueTask<Result<T>> source, Func<T, CancellationToken, ValueTask<Result<TOut>>> bind, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### BindAsync\<T>(ValueTask\<Result\<T>>, Func\<T, Result>) \{#Kun_Core_Results_ValueTaskResultExtensions_BindAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_Kun_Core_Results_Result\_\_}

Awaits the source and chains a sync follow-up returning non-generic [Result](/sdk/api/kun/kun-core-results/result.md).

```csharp
public static ValueTask<Result> BindAsync<T>(this ValueTask<Result<T>> source, Func<T, Result> bind)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [Result](/sdk/api/kun/kun-core-results/result.md)>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

#### Type Parameters

`T`

### BindAsync\<T>(ValueTask\<Result\<T>>, Func\<T, CancellationToken, ValueTask\<Result>>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_BindAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___System_Threading_CancellationToken\_}

Awaits the source and chains an async follow-up returning non-generic [Result](/sdk/api/kun/kun-core-results/result.md).

```csharp
public static ValueTask<Result> BindAsync<T>(this ValueTask<Result<T>> source, Func<T, CancellationToken, ValueTask<Result>> bind, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

#### Type Parameters

`T`

### EnsureAsync(ValueTask\<Result>, Func\<bool>, Error) \{#Kun_Core_Results_ValueTaskResultExtensions_EnsureAsync_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func_System_Boolean__Kun_Core_Results_Error\_}

Awaits the source and applies a sync post-condition check.

```csharp
public static ValueTask<Result> EnsureAsync(this ValueTask<Result> source, Func<bool> predicate, Error error)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

`error` [Error](/sdk/api/kun/kun-core-results/error.md)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

### EnsureAsync(ValueTask\<Result>, Func\<CancellationToken, ValueTask\<bool>>, Error, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_EnsureAsync_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask_System_Boolean___Kun_Core_Results_Error_System_Threading_CancellationToken\_}

Awaits the source and applies an async post-condition check.

```csharp
public static ValueTask<Result> EnsureAsync(this ValueTask<Result> source, Func<CancellationToken, ValueTask<bool>> predicate, Error error, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>

`error` [Error](/sdk/api/kun/kun-core-results/error.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

### EnsureAsync\<T>(ValueTask\<Result\<T>>, Func\<T, bool>, Error) \{#Kun_Core_Results_ValueTaskResultExtensions_EnsureAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Boolean__Kun_Core_Results_Error\_}

Awaits the source and applies a sync post-condition check.

```csharp
public static ValueTask<Result<T>> EnsureAsync<T>(this ValueTask<Result<T>> source, Func<T, bool> predicate, Error error)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

`error` [Error](/sdk/api/kun/kun-core-results/error.md)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`

### EnsureAsync\<T>(ValueTask\<Result\<T>>, Func\<T, CancellationToken, ValueTask\<bool>>, Error, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_EnsureAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask_System_Boolean___Kun_Core_Results_Error_System_Threading_CancellationToken\_}

Awaits the source and applies an async post-condition check.

```csharp
public static ValueTask<Result<T>> EnsureAsync<T>(this ValueTask<Result<T>> source, Func<T, CancellationToken, ValueTask<bool>> predicate, Error error, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>

`error` [Error](/sdk/api/kun/kun-core-results/error.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`

### MapAsync\<T, TOut>(ValueTask\<Result\<T>>, Func\<T, TOut>) \{#Kun_Core_Results_ValueTaskResultExtensions_MapAsync__2_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0___1\_\_}

Awaits the source and applies a sync transform to its success value.

```csharp
public static ValueTask<Result<TOut>> MapAsync<T, TOut>(this ValueTask<Result<T>> source, Func<T, TOut> map)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`map` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, TOut>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### MapAsync\<T, TOut>(ValueTask\<Result\<T>>, Func\<T, CancellationToken, ValueTask\<TOut>>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_MapAsync__2_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask___1___System_Threading_CancellationToken\_}

Awaits the source and applies an async transform to its success value.

```csharp
public static ValueTask<Result<TOut>> MapAsync<T, TOut>(this ValueTask<Result<T>> source, Func<T, CancellationToken, ValueTask<TOut>> map, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`map` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### MatchAsync\<TOut>(ValueTask\<Result>, Func\<TOut>, Func\<Error, TOut>) \{#Kun_Core_Results_ValueTaskResultExtensions_MatchAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func___0__System_Func_Kun_Core_Results_Error___0\_\_}

Awaits the source and folds via sync delegates.

```csharp
public static ValueTask<TOut> MatchAsync<TOut>(this ValueTask<Result> source, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<TOut>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[Error](/sdk/api/kun/kun-core-results/error.md), TOut>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>

#### Type Parameters

`TOut`

### MatchAsync\<TOut>(ValueTask\<Result>, Func\<CancellationToken, ValueTask\<TOut>>, Func\<Error, CancellationToken, ValueTask\<TOut>>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_MatchAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask___0___System_Func_Kun_Core_Results_Error_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask___0___System_Threading_CancellationToken\_}

Awaits the source and folds via async delegates.

```csharp
public static ValueTask<TOut> MatchAsync<TOut>(this ValueTask<Result> source, Func<CancellationToken, ValueTask<TOut>> onSuccess, Func<Error, CancellationToken, ValueTask<TOut>> onFailure, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[Error](/sdk/api/kun/kun-core-results/error.md), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>

#### Type Parameters

`TOut`

### MatchAsync\<T, TOut>(ValueTask\<Result\<T>>, Func\<T, TOut>, Func\<Error, TOut>) \{#Kun_Core_Results_ValueTaskResultExtensions_MatchAsync__2_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0___1__System_Func_Kun_Core_Results_Error___1\_\_}

Awaits the source and folds via sync delegates.

```csharp
public static ValueTask<TOut> MatchAsync<T, TOut>(this ValueTask<Result<T>> source, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, TOut>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[Error](/sdk/api/kun/kun-core-results/error.md), TOut>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>

#### Type Parameters

`T`

`TOut`

### MatchAsync\<T, TOut>(ValueTask\<Result\<T>>, Func\<T, CancellationToken, ValueTask\<TOut>>, Func\<Error, CancellationToken, ValueTask\<TOut>>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_MatchAsync__2_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask___1___System_Func_Kun_Core_Results_Error_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask___1___System_Threading_CancellationToken\_}

Awaits the source and folds via async delegates.

```csharp
public static ValueTask<TOut> MatchAsync<T, TOut>(this ValueTask<Result<T>> source, Func<T, CancellationToken, ValueTask<TOut>> onSuccess, Func<Error, CancellationToken, ValueTask<TOut>> onFailure, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[Error](/sdk/api/kun/kun-core-results/error.md), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<TOut>

#### Type Parameters

`T`

`TOut`

### TapAsync(ValueTask\<Result>, Action) \{#Kun_Core_Results_ValueTaskResultExtensions_TapAsync_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Action\_}

Awaits the source and invokes `onSuccess` only when it succeeded.

```csharp
public static ValueTask<Result> TapAsync(this ValueTask<Result> source, Action onSuccess)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Action](https://learn.microsoft.com/dotnet/api/system.action)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

### TapAsync(ValueTask\<Result>, Func\<CancellationToken, ValueTask>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_TapAsync_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask__System_Threading_CancellationToken\_}

Awaits the source and invokes an async side-effect on success, propagating `ct`.

```csharp
public static ValueTask<Result> TapAsync(this ValueTask<Result> source, Func<CancellationToken, ValueTask> onSuccess, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result.md)>

### TapAsync\<T>(ValueTask\<Result\<T>>, Action\<T>) \{#Kun_Core_Results_ValueTaskResultExtensions_TapAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Action___0\_\_}

Awaits the source and invokes a sync side-effect on success.

```csharp
public static ValueTask<Result<T>> TapAsync<T>(this ValueTask<Result<T>> source, Action<T> onSuccess)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<T>

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`

### TapAsync\<T>(ValueTask\<Result\<T>>, Func\<T, CancellationToken, ValueTask>, CancellationToken) \{#Kun_Core_Results_ValueTaskResultExtensions_TapAsync__1_System_Threading_Tasks_ValueTask_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_ValueTask__System_Threading_CancellationToken\_}

Awaits the source and invokes an async side-effect on success.

```csharp
public static ValueTask<Result<T>> TapAsync<T>(this ValueTask<Result<T>> source, Func<T, CancellationToken, ValueTask> onSuccess, CancellationToken ct)
```

#### Parameters

`source` [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[Result](/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`
