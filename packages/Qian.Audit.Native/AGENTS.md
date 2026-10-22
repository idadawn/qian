# 原生 Audit Provider

- 权威范围为 S1-AUD-008、DEC-030、ADR-009；主文档为 README.md。
- 只引用 NativeData Definition，不引用 EF Provider。共享算法唯一位于 Audit Definition，旧 facade 保持兼容。
- 记录与链头同事务；外部事务不能由本组件提交、回滚或局部重试。
- Native 专用登录只追加；函数 owner 无 LOGIN、成员关系、schema CREATE。固定策略及函数期望来自源码，不从当前数据库学习。
- 保持旧 EF 授权与协议行为；不能把 AUD-007 其它范围顺带标完成。
- 迁移独立执行，在线只读校验；原生发布必须 IL 警告为错并运行真实 PostgreSQL。
