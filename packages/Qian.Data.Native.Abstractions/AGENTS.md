# 原生数据 Definition

- 服务键为 NativeData；禁止把原生连接对象登记到 EF 的 Data 键，禁止引入 EF 或具体 Provider。
- 租户连接、显式平台连接分开；平台入口需要真实且仍有效的 global 插件上下文，不使用 bool 绕过。
- 连接和事务由 session 拥有；外部事务校验不能提交、回滚或重试调用方的事务。
- 公共 API 与 PublicAPI.Unshipped.txt 同步；任何日志、错误或字符串化不得包含连接凭据。
