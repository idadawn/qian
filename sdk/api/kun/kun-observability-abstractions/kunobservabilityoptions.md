> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Class KunObservabilityOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Observability` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Observability`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Observability)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Observability.Abstractions](/sdk/api/kun/kun-observability-abstractions/index.md)\
Assembly: Kun.Observability.dll

Configuration for `AddKunObservability`. Every option has a safe
default; the zero-arg call yields a trace + metric + log pipeline with
ASP.NET Core + HttpClient + EF Core instrumentation enabled and OTLP
export pointed at `OTEL_EXPORTER_OTLP_ENDPOINT`.

```csharp
public sealed class KunObservabilityOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunObservabilityOptions](/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### KunObservabilityOptions() \{#Kun_Observability_Abstractions_KunObservabilityOptions__ctor}

```csharp
public KunObservabilityOptions()
```

## Properties

### EnrichActivitiesFromBaggage \{#Kun_Observability_Abstractions_KunObservabilityOptions_EnrichActivitiesFromBaggage}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), installs a
`BaseProcessor<Activity>` that mirrors whitelisted
OpenTelemetry baggage entries (see
`Kun.Observability.Enrichment.KunBaggageKeys`) onto span
tags for activities emitted by Kun-owned
[`ActivitySource`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activitysource)s. Default
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (opt-in per
`docs/architecture/ADR-013-Kun.Observability-enricher.md`
§Default posture). Has no effect when
[KunObservabilityOptions.TracingEnabled](/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md#Kun_Observability_Abstractions_KunObservabilityOptions_TracingEnabled) is [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool).

```csharp
public bool EnrichActivitiesFromBaggage { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### EnrichLogsFromBaggage \{#Kun_Observability_Abstractions_KunObservabilityOptions_EnrichLogsFromBaggage}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool), installs a
`BaseProcessor<LogRecord>` that mirrors whitelisted
OpenTelemetry baggage entries (from the ambient
[`Current`](https://learn.microsoft.com/dotnet/api/system.diagnostics.activity.current)) onto log
record attributes. The log statement's own attributes win
on conflict — the enricher only adds missing keys. Default
[false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (opt-in, mirrors
[KunObservabilityOptions.EnrichActivitiesFromBaggage](/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md#Kun_Observability_Abstractions_KunObservabilityOptions_EnrichActivitiesFromBaggage) posture per
ADR-013 §F1 resolution). Has no effect when
[KunObservabilityOptions.LoggingEnabled](/sdk/api/kun/kun-observability-abstractions/kunobservabilityoptions.md#Kun_Observability_Abstractions_KunObservabilityOptions_LoggingEnabled) is [false](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool).

```csharp
public bool EnrichLogsFromBaggage { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### IncludeAspNetCore \{#Kun_Observability_Abstractions_KunObservabilityOptions_IncludeAspNetCore}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), captures ASP.NET Core server traces and metrics.

```csharp
public bool IncludeAspNetCore { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### IncludeEfCore \{#Kun_Observability_Abstractions_KunObservabilityOptions_IncludeEfCore}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), captures EF Core SQL command traces.

```csharp
public bool IncludeEfCore { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### IncludeHttpClient \{#Kun_Observability_Abstractions_KunObservabilityOptions_IncludeHttpClient}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), captures outbound `HttpClient` traces and metrics.

```csharp
public bool IncludeHttpClient { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### IncludeRuntime \{#Kun_Observability_Abstractions_KunObservabilityOptions_IncludeRuntime}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), captures .NET runtime metrics (GC, threadpool, contention).

```csharp
public bool IncludeRuntime { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### LoggingEnabled \{#Kun_Observability_Abstractions_KunObservabilityOptions_LoggingEnabled}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), registers the OpenTelemetry logger provider and exports log records via OTLP.

```csharp
public bool LoggingEnabled { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### MetricsEnabled \{#Kun_Observability_Abstractions_KunObservabilityOptions_MetricsEnabled}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), registers the metrics pipeline.

```csharp
public bool MetricsEnabled { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### OtlpEndpoint \{#Kun_Observability_Abstractions_KunObservabilityOptions_OtlpEndpoint}

Override for the OTLP endpoint URL. When [null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null)
(default), the OpenTelemetry SDK reads
`OTEL_EXPORTER_OTLP_ENDPOINT` from the environment, matching
the .NET Aspire convention.

```csharp
public Uri? OtlpEndpoint { get; set; }
```

#### Property Value

Uri?

### ServiceName \{#Kun_Observability_Abstractions_KunObservabilityOptions_ServiceName}

Logical service name reported as the `service.name` resource
attribute. Falls back to the entry-assembly name when left as an
empty string; supply a stable value for production deployments.

```csharp
public string ServiceName { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### ServiceVersion \{#Kun_Observability_Abstractions_KunObservabilityOptions_ServiceVersion}

Service version reported as `service.version`. Leave
[null](https://learn.microsoft.com/dotnet/csharp/language-reference/keywords/null) to omit the attribute; otherwise pass a
semver / commit SHA / calendar version — whatever your deploy
pipeline stamps in.

```csharp
public string? ServiceVersion { get; set; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### TracingEnabled \{#Kun_Observability_Abstractions_KunObservabilityOptions_TracingEnabled}

When [true](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/bool) (default), registers the tracing pipeline and Kun ActivitySources.

```csharp
public bool TracingEnabled { get; set; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)
