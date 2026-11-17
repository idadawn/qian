> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Interface IPermissionChecker

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.25 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.25/src/Kun.Authorization)（`v2026.10.25`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

Namespace: [Kun.Authorization](/sdk/api/kun/kun-authorization/index.md)\
Assembly: Kun.Authorization.dll

权限判定端口；框架不提供 RBAC、ABAC 或持久化引擎。

```csharp
public interface IPermissionChecker
```

## Methods

### BatchCheckAsync(IReadOnlyList\<PermissionRequest>, CancellationToken) \{#Kun_Authorization_IPermissionChecker_BatchCheckAsync_System_Collections_Generic_IReadOnlyList_Kun_Authorization_PermissionRequest__System_Threading_CancellationToken\_}

批量判定，结果与输入顺序一致。

```csharp
ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default)
```

#### Parameters

`requests` [IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[PermissionRequest](/sdk/api/kun/kun-authorization/permissionrequest.md)>

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[PermissionDecision](/sdk/api/kun/kun-authorization/permissiondecision.md)>>

### CheckAsync(PermissionRequest, CancellationToken) \{#Kun_Authorization_IPermissionChecker_CheckAsync_Kun_Authorization_PermissionRequest_System_Threading_CancellationToken\_}

判定单一动作。

```csharp
ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
```

#### Parameters

`request` [PermissionRequest](/sdk/api/kun/kun-authorization/permissionrequest.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[PermissionDecision](/sdk/api/kun/kun-authorization/permissiondecision.md)>

### ExplainAsync(PermissionRequest, CancellationToken) \{#Kun_Authorization_IPermissionChecker_ExplainAsync_Kun_Authorization_PermissionRequest_System_Threading_CancellationToken\_}

返回可解释判定。

```csharp
ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default)
```

#### Parameters

`request` [PermissionRequest](/sdk/api/kun/kun-authorization/permissionrequest.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[PermissionDecision](/sdk/api/kun/kun-authorization/permissiondecision.md)>

### ListAllowedActionsAsync(AuthorizationSubject, string, string?, CancellationToken) \{#Kun_Authorization_IPermissionChecker_ListAllowedActionsAsync_Kun_Authorization_AuthorizationSubject_System_String_System_String_System_Threading_CancellationToken\_}

列出指定资源的可执行动作。

```csharp
ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null, CancellationToken cancellationToken = default)
```

#### Parameters

`subject` [AuthorizationSubject](/sdk/api/kun/kun-authorization/authorizationsubject.md)

`resourceType` [string](https://learn.microsoft.com/dotnet/api/system.string)

`resourceId` [string](https://learn.microsoft.com/dotnet/api/system.string)?

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>>

### ListPermissionsAsync(AuthorizationSubject, CancellationToken) \{#Kun_Authorization_IPermissionChecker_ListPermissionsAsync_Kun_Authorization_AuthorizationSubject_System_Threading_CancellationToken\_}

查询主体权限点，供受控目录过滤使用。

```csharp
ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default)
```

#### Parameters

`subject` [AuthorizationSubject](/sdk/api/kun/kun-authorization/authorizationsubject.md)

`cancellationToken` [CancellationToken](https://learn.microsoft.com/dotnet/api/system.threading.cancellationtoken)

#### Returns

[ValueTask](https://learn.microsoft.com/dotnet/api/system.threading.tasks.valuetask-1)\<[IReadOnlyList](https://learn.microsoft.com/dotnet/api/system.collections.generic.ireadonlylist-1)\<[string](https://learn.microsoft.com/dotnet/api/system.string)>>
