# 数据权限契约 v1

S1-AZ-006 冻结 DataScopeRule，资源使用组件 id:资源短名，例如 qian.sample.crm:customer。allData=true 时不携带 filter；allData=false 时必须携带 Filter AST；拒绝同时给出相互矛盾的两种范围。未知字段（含 tenantId）拒绝。

[规则 Schema](data-scope-rule.schema.json) 引用 [Data 的 Filter AST](../../data/v1/filter-ast.schema.json)，不创建第二份 AST、矩阵或系统变量。两份 [样例](fixtures/data-scope-filter.json) / [全部数据样例](fixtures/data-scope-all.json) 用于离线校验。

GET /api/authz/meta 在已认证、非 Agent/委托主体且当前 Authz Provider 可见时发布规则 Schema、原始 Filter AST、矩阵、15 个系统变量及固定合并说明；未认证 401，Provider 卸载后 404。解析器先登记返回的 filterAst，再编译 dataScopeRule，无需访问 qian.dev。NuGet 从同一批源文件嵌入与分发这些资源，Definition 不因此引入 EF 或 Data Provider。

合并固定为同角色 AND、不同角色 OR；同一角色分多份输入仍合并成一个 AND 分支。任意 allData=true 只放开该命名资源的数据范围，其他资源不受影响；没有规则或空角色集合为拒绝。租户隔离、业务权限、软删除等强制条件始终另外叠加。

Qian.Authz.DataScopeComposition 只返回结构化计划，保留每份原 Filter AST 的逻辑边界，不展平、不生成 SQL、不解析变量，也不替代权限判定。S2 编译器仍须用 Data 的 FilterContracts 与可信字段目录校验字段及变量上下文；S1 已有 IDataPermissionFilter/IFieldPolicy 继续 fail-closed。

权威规格见 [数据契约](../../../docs/prd/specs/data-contracts.md)，组件行为见 [Qian.Authz](../../../packages/Qian.Authz/README.md)。
