> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Class ResultExtensions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Core` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Core`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Core)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Core.Results](/pr-preview/pr-122/sdk/api/kun/kun-core-results/index.md)\
Assembly: Kun.Core.dll

Synchronous railway-oriented extensions for [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md) and
[Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md). Follows ADR-011.

```csharp
public static class ResultExtensions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[ResultExtensions](/pr-preview/pr-122/sdk/api/kun/kun-core-results/resultextensions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Every method short-circuits on failure inputs: the supplied delegate is
**not** invoked and the original [Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md) is carried through
unchanged. See `docs/architecture/ADR-011-Result-async-extensions.md`
for normative semantics, naming rationale, and async counterparts on
`Task<Result>` / `ValueTask<Result>` sources.

## Methods

### Bind(Result, Func\<Result>) \{#Kun_Core_Results_ResultExtensions_Bind_Kun_Core_Results_Result_System_Func_Kun_Core_Results_Result\_\_}

Chains a follow-up step that itself returns a [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md).
If the input is failed, the supplied delegate is not invoked and
the original error is returned.

```csharp
public static Result Bind(this Result result, Func<Result> next)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

`next` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)>

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

### Bind\<T, TOut>(Result\<T>, Func\<T, Result\<TOut>>) \{#Kun_Core_Results_ResultExtensions_Bind__2_Kun_Core_Results_Result___0__System_Func___0_Kun_Core_Results_Result___1\_\_\_}

Chains a follow-up step whose output is itself a
[Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md). Failure inputs short-circuit; the
supplied delegate only runs on success.

```csharp
public static Result<TOut> Bind<T, TOut>(this Result<T> result, Func<T, Result<TOut>> bind)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<TOut>>

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<TOut>

#### Type Parameters

`T`

`TOut`

### Bind\<T>(Result\<T>, Func\<T, Result>) \{#Kun_Core_Results_ResultExtensions_Bind__1_Kun_Core_Results_Result___0__System_Func___0_Kun_Core_Results_Result\_\_}

Chains a follow-up step whose output is a non-generic
[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md). Useful for "do this side-effectful op next"
that doesn't produce a new value.

```csharp
public static Result Bind<T>(this Result<T> result, Func<T, Result> bind)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

`bind` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)>

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

#### Type Parameters

`T`

### Ensure(Result, Func\<bool>, Error) \{#Kun_Core_Results_ResultExtensions_Ensure_Kun_Core_Results_Result_System_Func_System_Boolean__Kun_Core_Results_Error\_}

Adds a post-condition check to a successful result. When
`predicate` returns [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), the
result becomes a failure carrying `error`. Failure
inputs short-circuit and are returned unchanged.

```csharp
public static Result Ensure(this Result result, Func<bool> predicate, Error error)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<[bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

`error` [Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md)

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

### Ensure\<T>(Result\<T>, Func\<T, bool>, Error) \{#Kun_Core_Results_ResultExtensions_Ensure__1_Kun_Core_Results_Result___0__System_Func___0_System_Boolean__Kun_Core_Results_Error\_}

Adds a post-condition check on a successful [Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md).
When `predicate` returns [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool),
the result becomes a failure carrying `error`.

```csharp
public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

`predicate` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, [bool](https://learn.microsoft.com/dotnet/api/system.boolean)>

`error` [Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md)

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

#### Type Parameters

`T`

### Map\<T, TOut>(Result\<T>, Func\<T, TOut>) \{#Kun_Core_Results_ResultExtensions_Map__2_Kun_Core_Results_Result___0__System_Func___0___1\_\_}

Transforms the success value via `map`. Failure
inputs short-circuit and the original [Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md) is
preserved in the returned [Result\<T>](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md).

```csharp
public static Result<TOut> Map<T, TOut>(this Result<T> result, Func<T, TOut> map)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

`map` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, TOut>

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<TOut>

#### Type Parameters

`T`

`TOut`

### Match\<TOut>(Result, Func\<TOut>, Func\<Error, TOut>) \{#Kun_Core_Results_ResultExtensions_Match__1_Kun_Core_Results_Result_System_Func___0__System_Func_Kun_Core_Results_Error___0\_\_}

Terminal fold. Invokes exactly one of `onSuccess` or
`onFailure` depending on the result state and
returns the produced value.

```csharp
public static TOut Match<TOut>(this Result result, Func<TOut> onSuccess, Func<Error, TOut> onFailure)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-1)\<TOut>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md), TOut>

#### Returns

TOut

#### Type Parameters

`TOut`

### Match\<T, TOut>(Result\<T>, Func\<T, TOut>, Func\<Error, TOut>) \{#Kun_Core_Results_ResultExtensions_Match__2_Kun_Core_Results_Result___0__System_Func___0___1__System_Func_Kun_Core_Results_Error___1\_\_}

Terminal fold. Invokes exactly one of `onSuccess`
(with the success value) or `onFailure` (with the
carried [Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md)) and returns the produced value.

```csharp
public static TOut Match<T, TOut>(this Result<T> result, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

`onSuccess` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<T, TOut>

`onFailure` [Func](https://learn.microsoft.com/dotnet/api/system.func-2)\<[Error](/pr-preview/pr-122/sdk/api/kun/kun-core-results/error.md), TOut>

#### Returns

TOut

#### Type Parameters

`T`

`TOut`

### Tap(Result, Action) \{#Kun_Core_Results_ResultExtensions_Tap_Kun_Core_Results_Result_System_Action\_}

Invokes `onSuccess` only when the result is
successful; returns the input result unchanged. Common use: side
effects such as logging or metric increments that must not mutate
the pipeline value.

```csharp
public static Result Tap(this Result result, Action onSuccess)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

`onSuccess` [Action](https://learn.microsoft.com/dotnet/api/system.action)

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result.md)

### Tap\<T>(Result\<T>, Action\<T>) \{#Kun_Core_Results_ResultExtensions_Tap__1_Kun_Core_Results_Result___0__System_Action___0\_\_}

Invokes `onSuccess` with the success value. Returns
the input result unchanged; failures short-circuit without calling
the delegate.

```csharp
public static Result<T> Tap<T>(this Result<T> result, Action<T> onSuccess)
```

#### Parameters

`result` [Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

`onSuccess` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<T>

#### Returns

[Result](/pr-preview/pr-122/sdk/api/kun/kun-core-results/result-1.md)\<T>

#### Type Parameters

`T`
