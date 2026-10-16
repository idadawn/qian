> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class KunRateLimitOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.RateLimit` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.RateLimit`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.RateLimit)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.RateLimit.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/index.md)\
Assembly: Kun.RateLimit.dll

Root configuration for `AddKunRateLimit`. Callers register one
or more named windows — typically `"api"` for bulk read / write
traffic and `"auth"` for stricter login paths — and optionally
supply a custom [KunRateLimitOptions.OnRejected](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/kunratelimitoptions.md#Kun_RateLimit_Abstractions_KunRateLimitOptions_OnRejected) hook that runs after the
default 429 response is written.

```csharp
public sealed class KunRateLimitOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunRateLimitOptions](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/kunratelimitoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunRateLimitOptions() \{#Kun_RateLimit_Abstractions_KunRateLimitOptions__ctor}

```csharp
public KunRateLimitOptions()
```

## Properties

### OnRejected \{#Kun_RateLimit_Abstractions_KunRateLimitOptions_OnRejected}

Optional hook invoked after the default 429 / `Retry-After`
response is written. Ideal for structured logging or metrics
emission; do **not** mutate the response from here — the
response has already been committed.

```csharp
public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; set; }
```

#### Property Value

[Func](https://learn.microsoft.com/dotnet/api/system.func-3)\<[OnRejectedContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.ratelimiting.onrejectedcontext), [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken), [ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask)>?

### Windows \{#Kun_RateLimit_Abstractions_KunRateLimitOptions_Windows}

Named rate-limit windows. Keys double as the policy names that
consumers attach via `.RequireRateLimiting("api")`.

```csharp
public Dictionary<string, WindowRule> Windows { get; }
```

#### Property Value

[Dictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [WindowRule](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/windowrule.md)>

## Methods

### AddWindow(string, Action\<WindowRule>) \{#Kun_RateLimit_Abstractions_KunRateLimitOptions_AddWindow_System_String_System_Action_Kun_RateLimit_Abstractions_WindowRule\_\_}

Registers a new window under `name`. The builder
returns [this](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/this) so callers can chain multiple
`AddWindow` calls in a single options callback.

```csharp
public KunRateLimitOptions AddWindow(string name, Action<WindowRule> configure)
```

#### Parameters

`name` [string](https://learn.microsoft.com/dotnet/api/system.string)

Policy name — used as the argument to `RequireRateLimiting`.

`configure` [Action](https://learn.microsoft.com/dotnet/api/system.action-1)\<[WindowRule](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/windowrule.md)>

Callback to populate the window's [WindowRule](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/windowrule.md).

#### Returns

[KunRateLimitOptions](/pr-preview/pr-181/sdk/api/kun/kun-ratelimit-abstractions/kunratelimitoptions.md)
