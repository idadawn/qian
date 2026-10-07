# contracts/v0 与 iClaw 冻结契约的差异说明

> 依据：[ADR-016](../../docs/adr/ADR-016-iclaw-boundary.md) D16.3（六个对齐项）、D16.5（S2S 各签各的但 claim 同形）、D16.12（以 iClaw 冻结契约与代码为准）。
>
> 对照基准：iClaw 仓库 `7099a1b`（2026-09-28）的 `docs/contracts/v0/`——`s2s-jwt-claims.md`（2026-07-23）、`events-schema.json`（2026-07-14）、`openapi-gateway.yaml` 的 `ErrorDetail` / `ErrorResponse` / `ProblemDetails`（2026-07-30）、`browser-operator/confirm.schema.json` 的扩展 `ErrorDetail`；工具元数据取 `docs/services/python/tools/tool-protocol.md` 的 `ToolSpec` 与 `unified-content/run-grants.schema.json` 的 `ToolGrant`。iClaw 契约更新后按本表逐项复核。

## 1 总览

| D16.3 对齐项 | Qian 文件 | 结论 |
|---|---|---|
| (a) S2S JWT claim 形状 | [s2s-claims.schema.json](s2s-claims.schema.json) | 同形：iClaw 的必填 claim 除 `email` 外全部保留且语义相同；Qian 增加 4 个、预留 1 个 claim |
| (b) 事件信封 | [event-envelope.schema.json](event-envelope.schema.json) | 超集：iClaw 必填字段全部保留；Qian 增加 `type` / `actor` / `trace_parent` 并收紧为封闭对象；`subject` 命名空间与平台级 `tenant_id = null` 两处不兼容 |
| (c) 工具元数据 | [capability-entry.schema.json](capability-entry.schema.json) | 字段一一对应，命名改为 camelCase；Qian 多出鉴权、分级、计费与 Agent 暴露字段；`schemaSha256` 算法由 Qian 定义，待 iClaw 确认 |
| (d) 契约格式 | 全部 Schema | 相同：OpenAPI 3.1 + `operationId`、JSON Schema 2020-12 + `additionalProperties: false`、`x-status: draft` 期可破坏、定稿后 additive-only |
| (e) 错误体 `ErrorDetail` | [error-detail.schema.json](error-detail.schema.json) | 核心三成员 `code / message / trace_id` 同名同义；Qian 采用 RFC 9457 problem+json 外形，以 `pointer / allowed / errors` 取代自由的 `details` |
| (f) 注册表四层形状 | 不在本目录 | Package / Release / Publication / Installation 随 `qian.registry`（S1-RT-004）与市场（S3）定义，届时补入 `contracts/` |

运行授权快照（7.18 技术落点所列对齐项之一）同样不在 v0：S1 只保证能力条目带 `schemaSha256` 与 `contractVersion`，快照形状随 S4 对接定。

## 2 S2S claim

iClaw 只有文字规格（`s2s-jwt-claims.md`），没有 JSON Schema。S1-KUN-004、S1-GW-001 验收中的「通过 iClaw `s2s-jwt-claims` Schema 校验」以本目录 `s2s-claims.schema.json` 为准，并按下表理解与 iClaw 的关系。

| claim | iClaw | Qian | 差异 |
|---|---|---|---|
| `iss` | 必填，字面量 `neuroagent-edge` | 必填，网关规范 issuer（每环境一个，可配置） | 各签各的（D16.5），值不同、形状相同 |
| `aud` | 必填，数组，含校验方服务名 | 同 | 无 |
| `sub` | 必填，user UUID | 必填；user 为 person_id，service 为 client_id | Qian 允许服务主体（`client_credentials`，7.9） |
| `tenant_id` | 必填，UUID | 必填，UUID **或 `null`**（平台级主体） | Qian 放宽：平台运营者与平台级服务调用没有租户；租户域端点见 `null` 拒绝 |
| `project_id` | 可选，UUID | 同，只透传不解释（R24） | 无 |
| `email` | 必填 | **不签发**（不在 Schema 内，出现即不合法） | Qian 不把用户属性放进令牌（PRD §13.3.1）；审计关联用 `sub` |
| `roles` | 必填，数组 | 必填，数组，可为空；只放粗粒度角色代码 | 同形（R24 例外），服务不得仅凭它放行 |
| `scope` | 必填，空格分隔 | 同，可为空串 | 无 |
| `session_id` | 必填，UUID，吊销清单键 | 同；service 主体取 `jti` | 沿用 iClaw Desktop 委托令牌 `jti = session_id` 的先例 |
| `iat / nbf / exp` | 必填，`exp − iat ≤ 300` | 同；网关默认 60 秒 | 无 |
| `jti` | 必填，UUID | 同 | 无 |
| `principal_kind` | 无 | 必填：`user / service / agent` | Qian 增加（7.18 第 3 项统一主体模型） |
| `membership_id` | 无 | user 且有租户时必填；service 不得出现 | Qian 增加（ADR-010 D10.4 吸收） |
| `identity_revision` | 无 | user 必填 | Qian 增加（权限缓存失效，ADR-006 D6.7） |
| `plugin_id` | 无 | 可选，service 主体为插件时 | Qian 增加（PRD §13.3.1） |
| `act` | 无 | 可选，RFC 8693 委托链，S1 不签发 | Qian 预留（S4） |

iClaw 的校验方会拒绝 Qian 令牌（`iss` 不同），反之亦然：两边对齐的是形状，不是互信。若日后共用 S2S，需要先解决 `email` 与平台级 `tenant_id` 两处差异，以及内部服务自铸密钥的 `kid` 管理（ADR-016 §5）。

iClaw 校验失败返回 `401 {"error": {"code": "s2s_jwt_invalid", …}}`；Qian 返回 `401` ErrorDetail，`code = UNAUTHENTICATED`。

## 3 事件信封

| 字段 | iClaw `EventEnvelope` | Qian | 差异 |
|---|---|---|---|
| `version` | 必填，`v1`；语义是「事件 Schema 版本」 | 必填，`v1`；语义是**信封**版本 | payload 版本在 Qian 由 `type` 的 `.v<N>` 表达 |
| `event_id` | 必填，UUID | 同（生产方用 v7） | 无 |
| `subject` | 必填，`^(platform\|storage\|runtime\|identity)\.[a-z_]+\.[a-z_]+$` | 必填，`<domain>.<entity>.<verb>`，任意小写域名，允许两段及连字符 | **不兼容**：Qian 的业务域（`crm.customer.created` 等）不在 iClaw 的四个域名白名单内 |
| `type` | 无 | 必填，`<subject>.v<N>` | Qian 增加（稳定事件 ID，7.10） |
| `occurred_at` | 必填，date-time | 同，且必须带时区 | Qian 收紧 |
| `tenant_id` | 必填，UUID | 必填，UUID 或 `null`（平台级事件） | **不兼容**：平台级事件（如 `lifecycle.plugin.activated.v1`）没有租户 |
| `correlation_id` / `causation_id` | 可选，UUID | 同 | 无 |
| `producer` | 可选，服务名 | **必填**，组件 id | Qian 收紧 |
| `actor` | 无 | 必填 `{ kind, id, on_behalf_of? }` | Qian 增加（审计主体链，7.11.1） |
| `trace_parent` | 无 | 必填，W3C traceparent | Qian 增加（追踪贯通，7.18 第 8 项） |
| `payload` | 必填，object | 同 | 无 |
| 其余成员 | 允许（信封未关闭） | 不允许 | Qian 收紧 |

实测：Qian 的全部正例 fixture 拿 iClaw `events-schema.json#/$defs/EventEnvelope` 校验，只在 `subject` 白名单与 `tenant_id = null` 两处失败，Qian 多出的成员不冲突。因此 S1-EVT-001 验收「信封通过 iClaw `events-schema.json` 校验」按「对 iClaw `EventEnvelope` 去掉 `subject` 域名白名单后校验租户事件」执行；两个产品各用各的 vhost（7.10），不会互相投递。

## 4 工具元数据

| iClaw | Qian 能力条目 | 差异 |
|---|---|---|
| `ToolSpec.name` | `name`（插件内短名）+ `id`（`<plugin>/<name>` URN） | Qian 多全局 URN |
| `ToolSpec.schema` | `inputSchema`（顶层关闭的 object，无租户字段）、`outputSchema` | Qian 收紧 |
| `ToolSpec.side_effect` | `sideEffect`：`read / write / external / destructive` | 同值，命名改 camelCase |
| `ToolSpec.policy_hint` | `policyHint`：`allow / confirm / deny` | 同值；运行时权威仍是 iClaw ToolPolicy / RiskGate（D16.3 c） |
| `ToolSpec.required_secrets` | `requiredSecrets`（只写名字） | 同义 |
| `ToolGrant.toolContractVersion` | `contractVersion` | 同义 |
| `ToolGrant.toolSchemaSha256` | `schemaSha256` | 同为 64 位小写十六进制；**算法**由 Qian 定义为 JCS(`{description, inputSchema, name}`) 的 SHA-256（README 第 6 节），iClaw 未公开算法，需在对齐计划中确认 |
| `ToolSpec.category` / `capabilities` / `artifact_output` | 无 | Qian 不采用（描述性标签；Qian 以 `description` 与插件归属表达） |
| — | `kind`、`idempotent`、`async`、`requiredScopes`、`requiredPermission`、`dataClassification`、`costTier`、`audit`、`agent { expose, requiresConfirmation }`、`operationId` | Qian 增加；iClaw 据 `sideEffect / policyHint / requiredScopes / requiredSecrets` 生成默认 ToolPolicy |

## 5 错误体

| 方面 | iClaw | Qian |
|---|---|---|
| 外形 | 规范为 `ErrorResponse { error: ErrorDetail }`；实际同时存在 ProblemDetails、扁平 `{error}`、`ErrorResponse` 三种（iClaw DEBT-012） | 只有一种：RFC 9457 problem+json，`ErrorDetail` 成员平铺在问题对象上 |
| `code` | 必填；写法不统一（`s2s_jwt_invalid` 小写，browser-operator 大写） | 必填，大写下划线，全局唯一并登记在错误码目录 |
| `message` | 必填 | 必填，按请求语言本地化 |
| `trace_id` | 可选，OTel trace id；Unified Content 用 `traceId` | 必填，W3C trace-id，只用 `trace_id` |
| `details` | 可选，自由 object | 无；以 `pointer`（JSON Pointer）、`allowed`、`errors[]` 承载可被模型自纠正的信息 |
| 可恢复性 | browser-operator 扩展逐次给 `recoverable` + `suggestion` | 错误码目录的 `retryable`（属于错误码，不逐次给出）；`suggestion` 不采用，改为写进 `message` |
| 协议 / 业务区分 | 无 | `type` 段与状态码：业务只用 409 / 422 |

两向映射（仅在跨产品转发错误时使用，S4 MCP 门面是首个场景）：

- Qian → iClaw `ErrorResponse`：`{ "error": { "code": code, "message": message, "trace_id": trace_id, "details": { "type", "status", "pointer", "allowed", "errors" } } }`。
- iClaw → Qian：`code` 不在 Qian 错误码目录中时不透传，按 HTTP 状态码兜底（`BAD_GATEWAY` 等），原 `code` 与 `message` 只进日志。

## 6 待与 iClaw 对齐（进入 ADR-016 §5 的协议对齐计划）

1. `schemaSha256` 的规范化算法（第 4 节）。
2. iClaw 是否为 S2S claim 补 JSON Schema；若补，`email` 是否改为可选、`tenant_id` 是否允许平台级 `null`。
3. 事件 `subject` 的域名白名单：iClaw 若要消费 Qian 事件（S4 触发器），需放开白名单或由 adapter 改写。
4. 错误码写法：iClaw 新模块是否采用大写下划线。
