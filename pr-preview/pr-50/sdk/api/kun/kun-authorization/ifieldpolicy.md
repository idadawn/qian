> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# Interface IFieldPolicy

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.12-hotfix.3 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.12-hotfix.3/src/Kun.Authorization)（`v2026.10.12-hotfix.3`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/pr-preview/pr-50/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/pr-preview/pr-50/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/pr-preview/pr-50/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

字段权限端口；输出给服务端投影与写入白名单。

```csharp
public interface IFieldPolicy
```

## Methods

### EvaluateAsync(PermissionRequest, IReadOnlyList\<string>, CancellationToken) \{#Kun_Authorization_IFieldPolicy_EvaluateAsync_Kun_Authorization_PermissionRequest_System_Collections_Generic_IReadOnlyList_System_String__System_Threading_CancellationToken\_}

判定请求中的候选字段。

```csharp
ValueTask<FieldDecision> EvaluateAsync(PermissionRequest request, IReadOnlyList<string> fields, CancellationToken cancellationToken = default)
```

#### Parameters

`request` [PermissionRequest](/pr-preview/pr-50/sdk/api/kun/kun-authorization/permissionrequest.md)

`fields` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[FieldDecision](/pr-preview/pr-50/sdk/api/kun/kun-authorization/fielddecision.md)>
