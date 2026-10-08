# Qian.Data

- 宿主装配调用 AddQianData，业务组件只依赖 Qian.Data.Abstractions。
- 必须同时装配连接版、事务版 RLS 拦截器与租户模型缓存键。
- 启动只校验，不建表、不迁移；数据库角色与策略由宿主迁移负责。
- 真实数据库测试复用 Qian.Testing.Containers，按非超级用户角色验证。
- 完整组件化范围归 S1-DATA-001；本任务仅交付安全基础。
