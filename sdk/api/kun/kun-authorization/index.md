> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# Namespace Kun.Authorization

:::info 生成页
本页由 DocFX 从 nuget.org 上 `Kun.Authorization` 2026.10.14 的 XML 注释生成，源码在 kun 仓库 [`src/Kun.Authorization`](https://github.com/idadawn/kun/tree/v2026.10.14/src/Kun.Authorization)（`v2026.10.14`）。Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship.md)）；Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian.md)。
:::

### Classes

[AuthorizationSubject](/sdk/api/kun/kun-authorization/authorizationsubject.md)

统一授权主体；权限集合不从令牌直接推导。

[DataPermissionPredicate](/sdk/api/kun/kun-authorization/datapermissionpredicate.md)

可序列化的字段谓词 AST，SQL 编译与参数绑定由 Provider 实现。

[FieldDecision](/sdk/api/kun/kun-authorization/fielddecision.md)

字段可读、可写与需要脱敏的集合。

[PermissionDecision](/sdk/api/kun/kun-authorization/permissiondecision.md)

可解释判定；Allow=false 是缺省语义。

[PermissionRequest](/sdk/api/kun/kun-authorization/permissionrequest.md)

权限点与可选资源实例；Permission 使用稳定的资源动作编码。

[RequiresPermissionAttribute](/sdk/api/kun/kun-authorization/requirespermissionattribute.md)

HTTP/gRPC 端点的静态权限元数据。

### Interfaces

[IDataPermissionFilter](/sdk/api/kun/kun-authorization/idatapermissionfilter.md)

本地数据过滤端口，不依赖 EF 包或具体数据库。

[IFieldPolicy](/sdk/api/kun/kun-authorization/ifieldpolicy.md)

字段权限端口；输出给服务端投影与写入白名单。

[IPermissionChecker](/sdk/api/kun/kun-authorization/ipermissionchecker.md)

权限判定端口；框架不提供 RBAC、ABAC 或持久化引擎。

[IPermissionProtectedRequest](/sdk/api/kun/kun-authorization/ipermissionprotectedrequest.md)

命令在编译期声明全部所需权限，避免从用户提交的 JSON 改写权限点。
