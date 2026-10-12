# Qian.Registry

注册表属于 `Registry` seam 的 Provider，只进入 `platform` 的 base 组合包；Definition 为 `Qian.Registry.Abstractions`。分发表面位于 `plugins/qian.registry`，当前仍是配置骨架，消费者的 standalone profile 不独立安装这项平台插件（R34）。本任务尚未交付完整 `RegistryPlugin`、清单制品注册与管理面接口，不能以内部数据库代码宣称组件已经可安装。

## 已实现的持久化基础

`RegistryDbContext` 继承 `QianDbContext`，由宿主以 `AddQianData<RegistryDbContext>()` 装配，使用 `plugin_qian_registry`。迁移复用 Data 的 SQL 生成器，为 `scope_patches` 自动生成强制 RLS；应用连接必须使用非超级用户且无 BYPASSRLS 的角色，管理员仅执行迁移。上下文不会在启动时建表或迁移。

内部 `PostgresPatchRepository` 只提供数据库原语，尚不向消费者暴露写入服务。每次调用使用独立 Data 会话，要求有效租户上下文；tenant scope 必须与当前租户一致。app / agent-session 行同样以当前租户作 RLS 键，所属关系和运营授权仍必须由后续注册表服务检查。`patch/v2` 的语法、重复键与 Schema 校验直接复用 `ISurfaceReader`，路由身份不能被请求体覆盖；完整写入使用内部 `RegistryPatchWriter`：在同一事务内锁定租户/scope 并用共用装配器校验组件配置、依赖和 required；真实权益、鉴权与制品信任仍需在公开服务层接入。

写入使用条件修订：0 表示创建，已有条目必须带当前 revision；相同修订下提交相同 patch 不增加版本。数据库唯一键与并发列负责拒绝同时创建和同时更新的竞争，不把后一个写入静默覆盖前一个。撤销写内部墓碑、清除配置和 note 并增加修订；重建从保留的修订继续增长，旧客户端无法利用版本重置覆盖新条目。墓碑不进入有效 patch 列表，不产生另一个 Enabled / Disabled 状态机。

`disabled` 的显式 false 与字段缺省分开保存，保证文件 patch 的继承语义；`x-` 扩展保持原样。业务层收到的 `ScopePatchSnapshot.Patch` 仍是原有 patch 对象，revision、主体和时间在快照外层。快照的默认 `ToString` 不展开配置与 note。

每次成功变更在同一 PostgreSQL 事务中提交 `pg_notify`，通知只含租户、scope、条目、修订和撤销标记；配置和 note 不广播。通知语句失败则数据写入也回滚。监听、失联恢复及运行时重合入仍未接入，不能以已发通知宣称 ≤5 秒装载验收通过。

## 数据库层合入与写前校验

`RegistryProfileComposer` 只依赖 `IProfileLoader`，不引用 Profiles Provider。它读取当前租户 scope 的有效行，作为文件层之后的 `ScopePatchLayer` 传入共用装配器；不生成临时配置文件，不把租户数据写回项目目录。省略 plugin/config 的局部 patch 可沿用同 id 的全局条目，显式 config 整段替换并重新应用 Schema 默认值。禁用项在 `DisabledEntries` 中保留 scope/id/来源，供后续运行时安装 restriction，避免禁用后回退到同名全局服务。

`RegistryPatchWriter` 的准入与写入处于同一数据库事务，使用租户/scope 维度的 PostgreSQL 事务锁，跨副本串行读取并校验最终配置。两个不同条目不能各自通过预检后共同引入重复 Provider 或破坏依赖；失败不增加修订、不留下记录、不发送通知。该内部入口也不等于已实现完整安装权限。

真实测试覆盖受限角色隔离、独立会话竞争、撤销重建的旧修订拒绝、通知失败整笔回滚、配置准入失败保留原修订，以及两个独立宿主并发提交冲突 Provider。文件和内存 scope 层的 .NET / TS 装配规则由同一套 profile TCK 精确比较。

## 未完成门禁

S1-RT-004 仍为 doing：清单制品与修订目录、完整安装准入、真实权限/权益检查、scope 视图与 404/403 隐藏语义、跨实例变更消费和运行时重合入、`/api/kernel/*` 兼容别名均需继续实现与验收。`plugins/qian.registry` 的骨架标记保留。完整任务验收不因这层持久化可用而提前勾选。
