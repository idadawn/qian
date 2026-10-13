# Qian.Data

- 宿主装配调用 AddQianData，业务组件只依赖 Qian.Data.Abstractions。
- 必须同时装配连接版、事务版 RLS 拦截器与租户模型缓存键。
- 启动只校验，不建表、不迁移；迁移只经 IDataMigrator 在 schema 级 advisory lock 下执行（K8s 迁移 Job 的 --migrate-only），属主角色同样不能是超级用户或 BYPASSRLS。
- 迁移用 dotnet ef 生成后不手改；序号取号保持单条 INSERT … ON CONFLICT DO UPDATE … RETURNING，雪花发号不得越过本地租约截止时刻。
- 真实数据库测试复用 Qian.Testing.Containers，按非超级用户角色验证。
- 基准类型与方言映射是数据：改系统类型改 contracts/data/v1/base-data-types.json，改映射改方言包的 JSON，不在代码里写类型分支；方言注册必须经 DbDialect.Validate。
- 数据连接只存租户作用域的 secretRef；口令只在 TestAsync 的使用点经宿主密钥策略解析、用后即释放，不得进入日志、错误、响应、审计或数据库。序号服务与数据连接共用 DataSystemContext 登记的系统上下文，不要再单独 AddQianData<DataSystemDbContext>。
- 数据会话必须将选定业务上下文、领域通知与 Outbox 缓冲绑定在同一 scope，不把非泛型 KunDbContext 指向最后登记的上下文。
- 按已批准的 S1-DEC-018，S1-DATA-001 先交付经真实参考应用验收的数据本体；SDK 源生成、私有 NuGet 锁步发布与真实 qian add 安装分别由 S1-RT-014 / S1-RT-011 回验。后续门未通过前保留组件骨架标记，不把本体完成写成完整组件交付。
