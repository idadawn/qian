> For AI agents: the complete documentation index is available at /pr-preview/pr-122/llms.txt, the full documentation bundle is available at /pr-preview/pr-122/llms-full.txt.

# Interface IPermissionProtectedRequest

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-122/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-122/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-122/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

命令在编译期声明全部所需权限，避免从用户提交的 JSON 改写权限点。

```csharp
public interface IPermissionProtectedRequest
```

## Properties

### RequiredPermissions \{#Kun_Authorization_IPermissionProtectedRequest_RequiredPermissions}

所有声明均需允许才执行 handler。

```csharp
public static abstract IReadOnlyList<string> RequiredPermissions { get; }
```

#### Property Value

[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>
