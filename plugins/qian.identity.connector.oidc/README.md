# qian.identity.connector.oidc

组件清单位于 `.qian-plugin/plugin.json`；代码字段与配置 Schema 由 Provider 入口派生。完整能力边界、宿主装配、验证和安装限制统一维护在 [Qian.Identity.Connector.Oidc](../../packages/Qian.Identity.Connector.Oidc/README.md)。

清单保留 `x-skeleton`，补齐代码元数据不代表真实 `qian add` 安装验收已完成。

稳定入口只提供 `Identity.Connectors:oidc`；程序化自定义注册名见主文档。当前开发组合包使用 `https://idp.example.internal/` 和 `secretRef://platform/identity/oidc/client-secret` 示例，清单的出网目标和秘密声明与之对应。部署方须按实际 IdP 同步替换 issuer、`policy.egress` 与密钥引用；声明不等于已批准访问，密钥只接受 secretRef。
