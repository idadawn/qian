# Qian.Authz.Abstractions

- 只放授权端口和元数据；复用 Kun.Authorization，不能在 Definition 实现 RBAC、数据库或入口鉴权算法；命令注册桥接只委托 Kun 既有行为。
- 权限点按 R9 从组件 id、资源短名和动作拼接，不接受客户端把已有权限集合放进主体。
- AUTHZ_DENIED 引用 PlatformErrors 的原对象，其余错误码在本包定义并由 Provider 挂载登记。
- 变更公共契约须同步 PublicAPI、命名/错误目录测试以及本包 README。
