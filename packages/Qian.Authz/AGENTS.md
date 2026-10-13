# Authz Provider

- 权威设计是 ADR-006、组件规格 §6.3 与 S1 §7.5；权限不从令牌权限集合推导。
- Definition 为 Qian.Authz.Abstractions，主文档为 README.md；平台经 Registry 的预装/发布元数据登记，独立模式经同语义的属性声明登记。
- 权限点以组件 id 加资源、动作短名派生；角色模板只能引用本组件已登记的权限，通配符先展开，不能扩大为未知或外部权限。
- 元数据登记不等于租户角色已授予；真实角色持久化、求值、缓存、审计、接口都按任务验收，不以替身关闭。
- 数据库实现只依赖 Data Definition 并继承 QianDbContext；租户读写用受限角色及 RLS。
