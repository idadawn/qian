> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# Class KunBaggageKeys

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Observability` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Observability`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Observability)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-202/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-202/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Observability.Enrichment](/pr-preview/pr-202/sdk/api/kun/kun-observability-enrichment/index.md)\
Assembly: Kun.Observability.dll

Canonical baggage key names the Kun activity enricher mirrors
onto Kun-sourced span tags at `Activity.Start` time. See
`docs/architecture/ADR-013-Kun.Observability-enricher.md`
for the contract, including why only these keys are whitelisted
and how consumers should populate them.

```csharp
public static class KunBaggageKeys
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[KunBaggageKeys](/pr-preview/pr-202/sdk/api/kun/kun-observability-enrichment/kunbaggagekeys.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.MemberwiseClone()](https://learn.microsoft.com/dotnet/api/system.object.memberwiseclone),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Remarks

Names follow OpenTelemetry semantic-convention style
(`namespace.attribute`) so that downstream exporters / UIs
can correlate them with existing OTel attributes.

## Fields

### All \{#Kun_Observability_Enrichment_KunBaggageKeys_All}

Every whitelisted key — iterate when building the mirror set.

```csharp
public static readonly string[] All
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)\[]

### OutboxMessageId \{#Kun_Observability_Enrichment_KunBaggageKeys_OutboxMessageId}

Outbox message identifier for spans emitted during outbox publishing.

```csharp
public const string OutboxMessageId = "outbox.message_id"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TenantId \{#Kun_Observability_Enrichment_KunBaggageKeys_TenantId}

Tenant identifier (typically the `tenant_id` claim from an S2S JWT).

```csharp
public const string TenantId = "tenant.id"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### UserId \{#Kun_Observability_Enrichment_KunBaggageKeys_UserId}

Authenticated user identifier (typically the `sub` claim).

```csharp
public const string UserId = "user.id"
```

#### Field Value

[string](https://learn.microsoft.com/dotnet/api/system.string)
