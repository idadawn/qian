> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SandboxCreateOptions

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Sandbox create options use the portable E2B v2 fields plus explicitly named provider-owned extension data.

```csharp
public sealed class SandboxCreateOptions
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SandboxCreateOptions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/sandboxcreateoptions.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SandboxCreateOptions() \{#Kun_Sandbox_Abstractions_SandboxCreateOptions__ctor}

```csharp
public SandboxCreateOptions()
```

## Properties

### CpuCount \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_CpuCount}

vCPU count requested for the sandbox.

```csharp
[JsonPropertyName("cpuCount")]
public int CpuCount { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### DiskMB \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_DiskMB}

Disk limit in megabytes (optional; when null, backend default applies).

```csharp
[JsonPropertyName("diskMB")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public int? DiskMB { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)?

### Envs \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_Envs}

Environment variables to inject into the sandbox. Treated as "low trust" — prefer [NeuroagentSandboxExtension.SecretScope](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md#Kun_Sandbox_Abstractions_NeuroagentSandboxExtension_SecretScope) for credentials.

```csharp
[JsonPropertyName("envs")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public IReadOnlyDictionary<string, string>? Envs { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

### MemoryMB \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_MemoryMB}

Memory limit in megabytes.

```csharp
[JsonPropertyName("memoryMB")]
public int MemoryMB { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)

### Metadata \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_Metadata}

Free-form application metadata, such as `workspaceId`, `threadId`,
`tenantId`, `agentDefId` for audit correlation.

```csharp
[JsonPropertyName("metadata")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public IReadOnlyDictionary<string, string>? Metadata { get; init; }
```

#### Property Value

[IReadOnlyDictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlydictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [string](https://learn.microsoft.com/dotnet/api/system.string)>?

### NeuroagentExtension \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_NeuroagentExtension}

Legacy explicit wire field. Retained for existing consumers; never emitted by default.

```csharp
[JsonPropertyName("neuroagent")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
[Obsolete("Use ProviderExtensions with an explicitly selected provider-owned key.")]
public NeuroagentSandboxExtension? NeuroagentExtension { get; init; }
```

#### Property Value

[NeuroagentSandboxExtension](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md)?

### ProviderExtensions \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_ProviderExtensions}

Provider-owned top-level JSON members. The caller explicitly chooses keys and values; core fields cannot be overridden.

```csharp
[JsonExtensionData]
public Dictionary<string, JsonElement>? ProviderExtensions { get; set; }
```

#### Property Value

[Dictionary](https://learn.microsoft.com/dotnet/api/system.collections.generic.dictionary-2)\<[string](https://learn.microsoft.com/dotnet/api/system.string), [JsonElement](https://learn.microsoft.com/dotnet/api/system.text.json.jsonelement)>?

### TemplateId \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_TemplateId}

Template id (e.g. `kun-default`).

```csharp
[JsonPropertyName("templateId")]
public string TemplateId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)

### TimeoutMs \{#Kun_Sandbox_Abstractions_SandboxCreateOptions_TimeoutMs}

Hard timeout in milliseconds. The sandbox is torn down after this wall-clock interval.

```csharp
[JsonPropertyName("timeoutMs")]
public int TimeoutMs { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)
