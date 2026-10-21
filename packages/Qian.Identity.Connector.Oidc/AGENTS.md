# 通用 OIDC 连接器约定

- 只依赖 `Qian.Identity.Abstractions`、`Qian.Config.Abstractions` 与 IdentityModel 的令牌库；不引用 `Qian.Identity.Server` 或任何 Provider 包（依赖图门禁把关）。
- 连接器无状态：state、nonce、PKCE verifier 由身份服务生成并核对关联，这里只按协议使用；不要在连接器里保存流程状态或签发平台令牌。
- 客户端密钥只经 secretRef 现取现用，不缓存明文、不写日志、不进异常消息；配置与清单的 `ToString` 不打印内容。
- id_token 校验（签名算法族、iss、aud、azp、exp、nonce、RFC 9207 的 iss 参数）任何一项都不能放宽；改动这些校验要配篡改用例，并做变异验证确认用例能拦住。
- 配置 Schema 的唯一来源是组件文件夹的 `schemas/config.schema.json`（嵌入程序集）；改配置成员要同步 `OidcConnectorOptions`、Schema 与 README。
- 不复制其他产品的身份代码；iClaw 的连接器只借鉴「连接器只回断言、绑定归本体」的模型，它不验 id_token 签名的做法不要沿用。
