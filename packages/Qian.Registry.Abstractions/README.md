# Qian.Registry.Abstractions

注册表 Definition 提供 Registry 服务键、宿主运行时入口与预装组件的只读元数据，并拥有 R48 的四个 403 原因码、条件写入冲突和 scope patch 快照。当前 Provider 的实现进度、profile 边界与真实验收统一见 [Qian.Registry](../Qian.Registry/README.md)。本包只依赖 `Qian.Runtime`，不实现数据库访问或授权规则。
