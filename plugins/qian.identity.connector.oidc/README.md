# qian.identity.connector.oidc

此目录的清单仍是配置骨架（`x-skeleton: true`）：清单与配置结构可校验，但不宣称 `qian add` 可直接安装或已发布。

真实 Provider 已在 [Qian.Identity.Connector.Oidc](../../packages/Qian.Identity.Connector.Oidc/README.md) 交付（S1-IDP-007）：通用 OIDC 上游连接器，授权码 + PKCE，校验 id_token 签名、iss、aud 与 nonce，凭据只接受 secretRef。本目录的 [schemas/config.schema.json](schemas/config.schema.json) 是它唯一的配置 Schema，包里嵌入的清单就是这一份；`clientSecret` 标 `x-qian-sensitive` 且只接受 `secretRef://…`。

去骨架时清单应写 `inject: ["Config", "Identity"]`、`provides: ["Identity.Connectors:oidc"]` 与 `runtime.form: module`。目前不写进来的原因：身份 Provider 的组件清单本身还是骨架，identity profile 里找不到 `Identity` 的 Provider；SDK 生成器尚不支持具名 seam。`policy.egress` 取决于部署接的 IdP，由部署方按实际上游主机登记。
