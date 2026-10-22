# qian.data

Data Provider 的实现位于 packages/Qian.Data，已提供数据会话、同上下文 Outbox、角色检查、RLS 迁移与软删审计。清单的代码字段（inject、provides、runtime.form / entry）由 Qian.Plugin.Tool 从实际程序集同步，CI 检查两者不漂移（S1-RT-014）；`runtime.nuget` 钉到私有源已发布的 `Qian.Data` 版本与摘要。S1-RT-011 已用 `qian add data` 从私有源真实安装并运行 Provider，本文件夹不再标记 x-skeleton。

配置契约已与实际 DataPlugin 对齐：dialect 固定 pg，rls.enforce 固定 true；可选 connection 为宿主显式绑定的 platform/dev secretRef，用于选择上下文集合。凭据不写入组件文件夹，按上下文创建时读取。

计划进入全部 .NET profile，角色为 Data seam 的 Provider。标准原生入口是 Qian.Data.DataPlugin，由 `qian add data` 交付：代码包按清单的精确版本从私有源还原，宿主按清单的 runtime.entry 挂载。库模式可通过 AddQianData 与 IDataService 独立使用。

S1-DATA-003 增加两项声明：

- `metadata/permissions.json` 与 `metadata/roles.json`：数据端点的权限点（基准类型 read / delete，数据连接 read / write / test）与租户默认角色模板。删除基准类型不授予任何租户角色。
- `policy.permissions.secrets`：数据连接的口令只解析租户作用域的 `data/*` 引用。

PostgreSQL 方言是独立组件 `qian.data.dialect.pg`，代码在 [Qian.Data.Dialect.Pg](../../packages/Qian.Data.Dialect.Pg/README.md)；其组件文件夹与真实 `PgDialectPlugin` 入口现已补齐，提供 `Data.Dialects:pg` 并在卸载时撤销目录登记，安装门仍单独验收。

配置 Schema 的真相源为 Provider 的 `ComponentConfiguration.Schema`，清单的 `runtime.configSchema` 与文件由工具同步，`check` 同时检查配置声明漂移。
