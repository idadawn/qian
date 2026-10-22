# Identity Client（依赖方 Provider）

- ADR-005、ADR-019、PRD 7.4.1 / 7.9、S1-IDP-005 是边界；外壳直接换码的令牌端点属于 auth-pages D5 的待定方案，裁定前不要加。
- 薄包装 Kun.Identity / Kun.Identity.AspNetCore / Kun.Security：登录事务、换码、id_token 校验、Cookie 会话、后通道登出都用 Kun 的公开类型组合，不复制其实现；Kun 缺什么先提 Kun 任务。只依赖 Runtime*、SDK、`*.Abstractions` 与 Kun，不引用 Qian.Identity.Server 等 Provider 包；SDK 是实际运行依赖，Generators 仅用于构建期分析。
- 密钥只经组件配置的 secretRef 在第一次换码时解析（`ctx.ResolveSecretAsync`），不进选项、日志、异常或响应；公开客户端不经 Config。
- 日志只记拒绝类别、错误类型与 sid 的 SHA-256，日志点只用 `[LoggerMessage]`；令牌、授权码、state、口令、原始 `qian.sid` 一律不出现。
- 规范身份只从已验签的访问令牌投影，`sid` → `session_id`，不透明 Cookie 永不当作会话标识；字段形状与冻结 S2S 契约一致，改动先对照 contracts/v0 §4 与 Kun 网关签发器的输入。
- 下游 S2S：`aud` 恰好是本服务，载荷闭合；禁止透传上一跳令牌（BannedSymbols 已禁 Kun 的透传处理器），不要为兼容放宽。
- 刷新必须保持单飞（进程内 + 跨副本的一次性标记），改动刷新路径要配并发测试：发行方的 refresh 轮换零容忍复用。
- 公共 API 变更同步 `PublicAPI.Unshipped.txt`；错误码登记在 `IdentityErrors`（Abstractions），对外一律 ErrorDetail。
- 唯一清单以 IdentityClientComponent 为稳定入口，委托现有 IdentityClientPlugin；configSchema 从 ComponentConfiguration.Schema 派生。独立宿主闭包保持精确白名单，公开客户端不依赖或启动其他 Provider。
- 不搬 iClaw 或其他产品的身份代码。
