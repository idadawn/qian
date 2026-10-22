# NativeData PostgreSQL Provider

- 只提供 NativeData，不修改或伪装替代 EF Data；遵循 DEC-030 与 S1-DATA-008。
- 组件 JSON 只有宿主绑定名。凭据只经受信任工厂读取，不能进入配置快照、日志、异常或默认字符串化。
- 平台入口必须核验 owner/caller 的 Scope 与 Entry.Scope、无租户及有效服务引用；不能用 Scoped(Global) 或角色成员关系绕过。
- 表保护策略逐字比对固定迁移契约，不能自动学习现场表达式；不可禁用 RLS/FORCE 或放宽角色。
- 连接、命令、读取器与事务不提供解包逃生口；生命周期交接必须有清理范围，卸载等待在途打开完成。
- 提交取消/失败封存会话；清理不依赖原业务取消 token，失败则丢弃连接，不能泄漏事务或 advisory lock。
- NativeAOT 必须真实 Linux 发布和数据库运行，IL 警告不得抑制；只在证据通过后维护 x-aot。
