> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class TaskResultExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Core)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Results](/pr-preview/pr-50/sdk/api/kun/kun-core-results/index.md)\
Assembly: Kun.Core.dll

Asynchronous railway-oriented extensions for [`Task`](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)
receivers whose result type is [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md) or
[Result\<T>](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md). Follows ADR-011.

```csharp
public static class TaskResultExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[TaskResultExtensions](/pr-preview/pr-50/sdk/api/kun/kun-core-results/taskresultextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Each operation provides two delegate-shape overloads:

- a _sync-delegate_ variant (no [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken))
- an _async-delegate_ variant that accepts a [`CancellationToken`](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken) and forwards it to the delegate.

Failure inputs short-circuit without invoking the delegate. Faulted or
cancelled source tasks surface through `await` per normal
.NET semantics — neither a [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md) failure nor a wrapped
exception.

## Methods

### BindAsync(Task\<Result>, Func\<Result>) \{#Kun_Core_Results_TaskResultExtensions_BindAsync_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func_Kun_Core_Results_Result\_\_}

Awaits the source and chains a sync follow-up that itself returns [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md).

```csharp
public static Task<Result> BindAsync(this Task<Result> source, Func<Result> next)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`next` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

### BindAsync(Task\<Result>, Func\<CancellationToken, Task\<Result>>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_BindAsync_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task_Kun_Core_Results_Result___System_Threading_CancellationToken\_}

Awaits the source and chains an async follow-up returning [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md).

```csharp
public static Task<Result> BindAsync(this Task<Result> source, Func<CancellationToken, Task<Result>> next, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`next` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

### BindAsync\<T, TOut>(Task\<Result\<T>>, Func\<T, Result\<TOut>>) \{#Kun_Core_Results_TaskResultExtensions_BindAsync__2_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_Kun_Core_Results_Result___1\_\_\_}

Awaits the source and chains a sync follow-up returning [Result\<T>](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md).

```csharp
public static Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> source, Func<T, Result<TOut>> bind)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### BindAsync\<T, TOut>(Task\<Result\<T>>, Func\<T, CancellationToken, Task\<Result\<TOut>>>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_BindAsync__2_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_Task_Kun_Core_Results_Result___1____System_Threading_CancellationToken\_}

Awaits the source and chains an async follow-up returning [Result\<T>](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md).

```csharp
public static Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> source, Func<T, CancellationToken, Task<Result<TOut>>> bind, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### BindAsync\<T>(Task\<Result\<T>>, Func\<T, Result>) \{#Kun_Core_Results_TaskResultExtensions_BindAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_Kun_Core_Results_Result\_\_}

Awaits the source and chains a sync follow-up returning non-generic [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md).

```csharp
public static Task<Result> BindAsync<T>(this Task<Result<T>> source, Func<T, Result> bind)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

#### Type Parameters

`T`

### BindAsync\<T>(Task\<Result\<T>>, Func\<T, CancellationToken, Task\<Result>>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_BindAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_Task_Kun_Core_Results_Result___System_Threading_CancellationToken\_}

Awaits the source and chains an async follow-up returning non-generic [Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md).

```csharp
public static Task<Result> BindAsync<T>(this Task<Result<T>> source, Func<T, CancellationToken, Task<Result>> bind, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

#### Type Parameters

`T`

### EnsureAsync(Task\<Result>, Func\<bool>, Error) \{#Kun_Core_Results_TaskResultExtensions_EnsureAsync_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func_System_Boolean__Kun_Core_Results_Error\_}

Awaits the source and applies an [ResultExtensions.Ensure](/pr-preview/pr-50/sdk/api/kun/kun-core-results/resultextensions.md#Kun_Core_Results_ResultExtensions_Ensure_Kun_Core_Results_Result_System_Func_System_Boolean__Kun_Core_Results_Error_) check.

```csharp
public static Task<Result> EnsureAsync(this Task<Result> source, Func<bool> predicate, Error error)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

`error` [Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

### EnsureAsync(Task\<Result>, Func\<CancellationToken, Task\<bool>>, Error, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_EnsureAsync_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task_System_Boolean___Kun_Core_Results_Error_System_Threading_CancellationToken\_}

Awaits the source and applies an async predicate post-condition.

```csharp
public static Task<Result> EnsureAsync(this Task<Result> source, Func<CancellationToken, Task<bool>> predicate, Error error, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>

`error` [Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

### EnsureAsync\<T>(Task\<Result\<T>>, Func\<T, bool>, Error) \{#Kun_Core_Results_TaskResultExtensions_EnsureAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Boolean__Kun_Core_Results_Error\_}

Awaits the source and applies a sync post-condition check.

```csharp
public static Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> source, Func<T, bool> predicate, Error error)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

`error` [Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`

### EnsureAsync\<T>(Task\<Result\<T>>, Func\<T, CancellationToken, Task\<bool>>, Error, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_EnsureAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_Task_System_Boolean___Kun_Core_Results_Error_System_Threading_CancellationToken\_}

Awaits the source and applies an async post-condition check.

```csharp
public static Task<Result<T>> EnsureAsync<T>(this Task<Result<T>> source, Func<T, CancellationToken, Task<bool>> predicate, Error error, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>>

`error` [Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md)

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`

### MapAsync\<T, TOut>(Task\<Result\<T>>, Func\<T, TOut>) \{#Kun_Core_Results_TaskResultExtensions_MapAsync__2_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0___1\_\_}

Awaits the source and applies a sync transform to its success value.

```csharp
public static Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> source, Func<T, TOut> map)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`map` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, TOut>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### MapAsync\<T, TOut>(Task\<Result\<T>>, Func\<T, CancellationToken, Task\<TOut>>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_MapAsync__2_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_Task___1___System_Threading_CancellationToken\_}

Awaits the source and applies an async transform to its success value.

```csharp
public static Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> source, Func<T, CancellationToken, Task<TOut>> map, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`map` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Type Parameters

`T`

`TOut`

### MatchAsync\<TOut>(Task\<Result>, Func\<TOut>, Func\<Error, TOut>) \{#Kun_Core_Results_TaskResultExtensions_MatchAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func___0__System_Func_Kun_Core_Results_Error___0\_\_}

Awaits the source and folds via sync delegates.

```csharp
public static Task<TOut> MatchAsync<TOut>(this Task<Result> source, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<TOut>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md), TOut>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>

#### Type Parameters

`TOut`

### MatchAsync\<TOut>(Task\<Result>, Func\<CancellationToken, Task\<TOut>>, Func\<Error, CancellationToken, Task\<TOut>>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_MatchAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task___0___System_Func_Kun_Core_Results_Error_System_Threading_CancellationToken_System_Threading_Tasks_Task___0___System_Threading_CancellationToken\_}

Awaits the source and folds via async delegates, propagating `ct`.

```csharp
public static Task<TOut> MatchAsync<TOut>(this Task<Result> source, Func<CancellationToken, Task<TOut>> onSuccess, Func<Error, CancellationToken, Task<TOut>> onFailure, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>

#### Type Parameters

`TOut`

### MatchAsync\<T, TOut>(Task\<Result\<T>>, Func\<T, TOut>, Func\<Error, TOut>) \{#Kun_Core_Results_TaskResultExtensions_MatchAsync__2_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0___1__System_Func_Kun_Core_Results_Error___1\_\_}

Awaits the source and folds via sync delegates.

```csharp
public static Task<TOut> MatchAsync<T, TOut>(this Task<Result<T>> source, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, TOut>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md), TOut>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>

#### Type Parameters

`T`

`TOut`

### MatchAsync\<T, TOut>(Task\<Result\<T>>, Func\<T, CancellationToken, Task\<TOut>>, Func\<Error, CancellationToken, Task\<TOut>>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_MatchAsync__2_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_Task___1___System_Func_Kun_Core_Results_Error_System_Threading_CancellationToken_System_Threading_Tasks_Task___1___System_Threading_CancellationToken\_}

Awaits the source and folds via async delegates, propagating `ct`.

```csharp
public static Task<TOut> MatchAsync<T, TOut>(this Task<Result<T>> source, Func<T, CancellationToken, Task<TOut>> onSuccess, Func<Error, CancellationToken, Task<TOut>> onFailure, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[Error](/pr-preview/pr-50/sdk/api/kun/kun-core-results/error.md), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<TOut>

#### Type Parameters

`T`

`TOut`

### TapAsync(Task\<Result>, Action) \{#Kun_Core_Results_TaskResultExtensions_TapAsync_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Action\_}

Awaits the source and invokes `onSuccess` only when it succeeded.

```csharp
public static Task<Result> TapAsync(this Task<Result> source, Action onSuccess)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Action](https://learn.microsoft.com/dotnet/api/system.action)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

### TapAsync(Task\<Result>, Func\<CancellationToken, Task>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_TapAsync_System_Threading_Tasks_Task_Kun_Core_Results_Result__System_Func_System_Threading_CancellationToken_System_Threading_Tasks_Task__System_Threading_CancellationToken\_}

Awaits the source and invokes `onSuccess` only when it succeeded, propagating `ct`.

```csharp
public static Task<Result> TapAsync(this Task<Result> source, Func<CancellationToken, Task> onSuccess, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result.md)>

### TapAsync\<T>(Task\<Result\<T>>, Action\<T>) \{#Kun_Core_Results_TaskResultExtensions_TapAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Action___0\_\_}

Awaits the source and invokes a sync side-effect on success.

```csharp
public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> source, Action<T> onSuccess)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<T>

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`

### TapAsync\<T>(Task\<Result\<T>>, Func\<T, CancellationToken, Task>, CancellationToken) \{#Kun_Core_Results_TaskResultExtensions_TapAsync__1_System_Threading_Tasks_Task_Kun_Core_Results_Result___0___System_Func___0_System_Threading_CancellationToken_System_Threading_Tasks_Task__System_Threading_CancellationToken\_}

Awaits the source and invokes an async side-effect on success.

```csharp
public static Task<Result<T>> TapAsync<T>(this Task<Result<T>> source, Func<T, CancellationToken, Task> onSuccess, CancellationToken ct)
```

#### Parameters

`source` [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<T, [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task)>

`ct` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[Task](https://learn.microsoft.com/dotnet/api/system.threading.tasks.task-1)\<[Result](/pr-preview/pr-50/sdk/api/kun/kun-core-results/result-1.md)\<T>>

#### Type Parameters

`T`
