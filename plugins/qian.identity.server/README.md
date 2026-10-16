# qian.identity.server

本目录仍是明确标记的配置骨架（x-skeleton: true），不宣称默认 profile 已包含账号登录页面或账号数据库。

[Qian.Identity.Server](../../packages/Qian.Identity.Server/README.md) 已提供可嵌入宿主的真实 OpenIddict 协议 Provider、令牌存储与后通道登出。该 Provider 只允许 global scope，每环境唯一 issuer；宿主须装配协议服务、Data 与受信账号 Cookie，并通过 PluginLoader 挂载，完整约定见包的主文档。

Person/Membership 与统一登录流水线由 S1-IDP-003 接入，会话中心由 S1-IDP-004 接入，外壳/RP 接入按各自任务验收。qian add identity 的完整产品安装路径在上述组装就位前仍不能当成已交付。
