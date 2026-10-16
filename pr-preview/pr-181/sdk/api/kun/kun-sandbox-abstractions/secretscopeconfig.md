> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# Class SecretScopeConfig

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Sandbox` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Sandbox`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Sandbox)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-181/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-181/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Sandbox.Abstractions](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/index.md)\
Assembly: Kun.Sandbox.dll

Secret-scope sub-object of [NeuroagentSandboxExtension](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/neuroagentsandboxextension.md).

```csharp
public sealed class SecretScopeConfig
```

#### Inheritance

[object](https://learn.microsoft.com/dotnet/api/system.object) ←
[SecretScopeConfig](/pr-preview/pr-181/sdk/api/kun/kun-sandbox-abstractions/secretscopeconfig.md)

#### Inherited Members

[object.GetType()](https://learn.microsoft.com/dotnet/api/system.object.gettype),
[object.ToString()](https://learn.microsoft.com/dotnet/api/system.object.tostring),
[object.Equals(object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object\)),
[object.Equals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.equals#system-object-equals\(system-object-system-object\)),
[object.ReferenceEquals(object?, object?)](https://learn.microsoft.com/dotnet/api/system.object.referenceequals),
[object.GetHashCode()](https://learn.microsoft.com/dotnet/api/system.object.gethashcode)

## Constructors

### SecretScopeConfig() \{#Kun_Sandbox_Abstractions_SecretScopeConfig__ctor}

```csharp
public SecretScopeConfig()
```

## Properties

### Scopes \{#Kun_Sandbox_Abstractions_SecretScopeConfig_Scopes}

Scope names (e.g. `github_read`, `company_crm_read`).

```csharp
[JsonPropertyName("scopes")]
public IReadOnlyList<string> Scopes { get; init; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>
