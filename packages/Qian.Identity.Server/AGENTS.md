# Identity 协议 Provider

- ADR-005、PRD 7.4.1 与 S1-IDP-002 是边界；账号、密码因子、会话中心仍由后续任务交付。
- OpenIddict 保持持久令牌记录、PKCE、非对称签名与 HTTPS 校验，不使用 degraded 模式。
- 在线存储继承 QianDbContext，不用超级用户、不记录密码、令牌或原始 sid。
- 协议错误由 OpenIddict 按 OIDC/OAuth2 输出；平台管理接口使用 ErrorDetail。
- 只依赖 Runtime、Definition 与第三方协议库，宿主组合 Data Provider。
