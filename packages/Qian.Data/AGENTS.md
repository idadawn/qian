# Qian.Data

- 宿主装配调用 AddQianData，业务组件只依赖 Qian.Data.Abstractions。
- 必须同时装配连接版、事务版 RLS 拦截器与租户模型缓存键。
- 启动只校验，不建表、不迁移；数据库角色与策略由宿主迁移负责。
- 真实数据库测试复用 Qian.Testing.Containers，按非超级用户角色验证。
- 数据会话必须将选定业务上下文、领域通知与 Outbox 缓冲绑定在同一 scope，不把非泛型 KunDbContext 指向最后登记的上下文。
- 按已批准的 S1-DEC-018，S1-DATA-001 先交付经真实参考应用验收的数据本体；SDK 源生成、私有 NuGet 锁步发布与真实 qian add 安装分别由 S1-RT-014 / S1-RT-011 回验。后续门未通过前保留组件骨架标记，不把本体完成写成完整组件交付。
