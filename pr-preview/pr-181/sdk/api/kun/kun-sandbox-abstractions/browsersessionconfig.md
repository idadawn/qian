> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class BrowserSessionConfig

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Browser configuration sub-object of [NeuroagentSandboxExtension](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md).

```csharp
public sealed class BrowserSessionConfig
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[BrowserSessionConfig](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/browsersessionconfig.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### BrowserSessionConfig() \{#Kun_Sandbox_Abstractions_BrowserSessionConfig__ctor}

```csharp
public BrowserSessionConfig()
```

## Properties

### Enabled \{#Kun_Sandbox_Abstractions_BrowserSessionConfig_Enabled}

Whether the browser subsystem should be available in the sandbox.

```csharp
[JsonPropertyName("enabled")]
public bool Enabled { get; init; }
```

#### Property Value

[bool](https://learn.microsoft.com/dotnet/api/system.boolean)

### ResumeSessionId \{#Kun_Sandbox_Abstractions_BrowserSessionConfig_ResumeSessionId}

Optional id of a prior browser session to resume.

```csharp
[JsonPropertyName("resumeSessionId")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? ResumeSessionId { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?
