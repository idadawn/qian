# qian.data

Data Provider 的实现位于 packages/Qian.Data，已提供数据会话、同上下文 Outbox、角色检查、RLS 迁移与软删审计。本文件夹仍标记 x-skeleton：SDK 代码字段源生成和 CLI 代码包安装尚未完成，当前不能把配置骨架当作可执行组件安装。

配置契约已与实际 DataPlugin 对齐：dialect 固定 pg，rls.enforce 固定 true；可选 connection 为宿主显式绑定的 platform/dev secretRef，用于选择上下文集合。凭据不写入组件文件夹，按上下文创建时读取。

计划进入全部 .NET profile，角色为 Data seam 的 Provider。标准原生入口是 Qian.Data.DataPlugin；完成 S1-RT-014 的派生字段与 S1-RT-011 的安装事务后，由 qian add data 交付。库模式目前可通过 AddQianData 与 IDataService 独立使用。

S1-DATA-003 增加两项声明：

- `metadata/permissions.json` 与 `metadata/roles.json`：数据端点的权限点（基准类型 read / delete，数据连接 read / write / test）与租户默认角色模板。删除基准类型不授予任何租户角色。
- `policy.permissions.secrets`：数据连接的口令只解析租户作用域的 `data/*` 引用。

PostgreSQL 方言是独立组件 `qian.data.dialect.pg`，代码在 [Qian.Data.Dialect.Pg](../../packages/Qian.Data.Dialect.Pg/README.md)；它的组件文件夹要等装载器支持注册表键（`Data.Dialects:pg`）后补齐。
