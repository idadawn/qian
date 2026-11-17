> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Class NeuroagentSandboxExtension

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Sandbox)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Compatibility shape for the legacy explicit supervisor extension. New integrations choose their own provider extension contract.

```csharp
[Obsolete("Use provider-owned JSON extension data on SandboxCreateOptions.ProviderExtensions.")]
public sealed class NeuroagentSandboxExtension
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[NeuroagentSandboxExtension](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### NeuroagentSandboxExtension() \{#Kun_Sandbox_Abstractions_NeuroagentSandboxExtension__ctor}

```csharp
public NeuroagentSandboxExtension()
```

## Properties

### Browser \{#Kun_Sandbox_Abstractions_NeuroagentSandboxExtension_Browser}

Browser session configuration.

```csharp
[JsonPropertyName("browser")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public BrowserSessionConfig? Browser { get; init; }
```

#### Property Value

[BrowserSessionConfig](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/browsersessionconfig.md)?

### MaxCommands \{#Kun_Sandbox_Abstractions_NeuroagentSandboxExtension_MaxCommands}

Maximum number of commands permitted during the sandbox lifetime. Exceeding this aborts the sandbox.

```csharp
[JsonPropertyName("maxCommands")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public int? MaxCommands { get; init; }
```

#### Property Value

[int](https://learn.microsoft.com/dotnet/api/system.int32)?

### NetworkPolicy \{#Kun_Sandbox_Abstractions_NeuroagentSandboxExtension_NetworkPolicy}

Network egress policy label (e.g. `tenant-default`, `allow-all`, `deny-all`).

```csharp
[JsonPropertyName("networkPolicy")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? NetworkPolicy { get; init; }
```

#### Property Value

[string](https://learn.microsoft.com/dotnet/api/system.string)?

### SecretScope \{#Kun_Sandbox_Abstractions_NeuroagentSandboxExtension_SecretScope}

Secret scope configuration — names of secret bundles to be made available via the metadata service.

```csharp
[JsonPropertyName("secretScope")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public SecretScopeConfig? SecretScope { get; init; }
```

#### Property Value

[SecretScopeConfig](/pr-preview/pr-50/sdk/api/kun/kun-sandbox-abstractions/secretscopeconfig.md)?
