# 注册表 Provider

- 主文档为 README.md；只进 platform，不把注册表放进特权运行时。
- 只依赖 Runtime 和 Definition 包，Data/Surfaces/Profiles 的 Provider 由宿主组装。
- 在线上下文必须继承 QianDbContext，迁移复用 Data 的强制 RLS 生成器；应用角色不得拥有超级用户或 BYPASSRLS。
- 内部 patch 仓储不等于权限、权益或完整组件准入，未接通服务层前不开放公共写入接口。
- 修订要覆盖创建、更新、撤销与重建；真实并发不能依靠单进程锁。
- 通知与数据库写入同事务，不广播配置或 note。监听成功不等于运行时已经重合入。
- R48 管理面原因码归 Definition；业务/模型表面仍统一隐藏为 404，不新增 ErrorDetail 自由字段。
