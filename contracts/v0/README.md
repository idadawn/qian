# contracts/v0：Qian 的冻结契约（draft）

> 依据：[PRD §07 7.18](../../docs/prd/07-s1-foundation.md) 第 2、3、5、9 项与技术落点；[ADR-016](../../docs/adr/ADR-016-iclaw-boundary.md) D16.3 / D16.12；[ADR-018](../../docs/adr/ADR-018-stages-s1-scope.md) D18.6。任务 S1-AGT-001。
>
> 真相源是本目录的 JSON Schema 2020-12、数据文件与 fixture；PRD 与 ADR 里的字段列表是摘要，二者不一致时以本目录为准，并回头修正文档。与 iClaw 的差异见 [iclaw-alignment.md](iclaw-alignment.md)。

## 1 文件

| 文件 | 内容 | 主要消费方 |
|---|---|---|
| [error-detail.schema.json](error-detail.schema.json) | 错误响应体 `ErrorDetail`：RFC 9457 problem+json 的 Qian 配置文件 | 全部 HTTP 端点、`qian` CLI、校验器（7.2.2）、yue、Agent |
| [error-catalog.schema.json](error-catalog.schema.json) | 错误码目录的 JSON 导出格式：类别、状态码、归属组件、多语言文案 | 文档站、yue 文案、各组件的错误码目录导出（7.5 等） |
| [error-codes.json](error-codes.json) | 平台内置错误码目录（数据文件，由 `PlatformErrors` 导出，测试比对防漂移） | 同上 |
| [s2s-claims.schema.json](s2s-claims.schema.json) | 服务间 JWT 的 payload | `qian.gateway` 签发（7.9）、各服务校验、`qian.identity`（7.4） |
| [event-envelope.schema.json](event-envelope.schema.json) | 跨进程集成事件的信封 | `qian.events`（7.10）、审计（7.11）、S4 触发器 |
| [capability-entry.schema.json](capability-entry.schema.json) | 能力目录中一条命令 / 查询的条目，含全部 Agent 字段 | `qian.capabilities`（7.2.1）、`tools/<name>.json`（tool/v2）、iClaw ToolPolicy |
| `fixtures/<schema>.valid.json` | 正例：过 Schema 且满足语义规则 | 契约测试、各语言实现 |
| `fixtures/<schema>.invalid.json` | 反例：Schema 必须拒绝 | 同上 |
| `fixtures/<schema>.semantic-invalid.json` | 过 Schema、但违反第 3–6 节的语义规则 | 同上（实现语义校验时用） |

fixture 文件形状统一为 `[{ "name": "<这条样例说明什么>", "data": <实例> }]`。

## 2 通用约定与演进

- 全部 Schema 为 JSON Schema 2020-12，`$id` 前缀 `https://qian.dev/contracts/v0/`（标识符，不要求可解引用）。跨文件引用：`error-catalog` → `error-detail`，`event-envelope` → `s2s-claims`，`capability-entry` → 2020-12 元 Schema。
- 声明了 `properties` 的 object 一律 `additionalProperties: false`。开放容器只有三处，都由别处的 Schema 约束内容：事件 `payload`（事件目录中该 `type` 的 Schema）、能力的 `inputSchema` / `outputSchema`（它们本身是 Schema 文档）、多语言文案（语言标签 → 文本）。
- 状态 `x-status: draft`：允许破坏性修订，但改 Schema 必须同时改 fixture、`Qian.Runtime.Errors` 与已知消费方，并在 PR 中写明。第 8 节的默认裁决经 [S1-DEC-009](../../pm/tasks/S1-DEC-009.md) 确认后去掉 `draft`，此后只做向后兼容的增量；破坏性变更开 `contracts/v1/`，不改 v0。
- 线上成员名：错误体、S2S claim、事件信封用 snake_case（与 iClaw 同形）；能力条目用 camelCase（与 `plugin.json` 和 tool/v2 一致）。
- 契约格式（7.18）：HTTP 接口 OpenAPI 3.1 + `operationId`；数据 JSON Schema 2020-12 + `additionalProperties: false`。

## 3 ErrorDetail

### 3.1 形状

错误响应的媒体类型为 `application/problem+json`，响应体是带 Qian 扩展成员的 RFC 9457 对象：

| 成员 | 必填 | 说明 |
|---|---|---|
| `type` | 是 | `https://qian.dev/problems/<protocol\|business>/<code>`，末段等于 `code` |
| `title` | 是 | 问题类型摘要，按请求语言本地化，同一 `code` 不随出现而变 |
| `status` | 是 | 与 HTTP 状态行相同 |
| `detail` | 否 | 出现时与 `message` 相同，供只认 RFC 9457 的通用客户端读取 |
| `instance` | 否 | 出错的请求路径 |
| `code` | 是 | 稳定错误码，见 3.3 |
| `message` | 是 | 由错误码目录的文案模板按请求语言渲染 |
| `trace_id` | 是 | W3C trace-id（32 位小写十六进制），与日志、审计、事件 `trace_parent` 中的 trace-id 同值；请求没有活动 trace 时由宿主生成一个 |
| `pointer` | 否 | 出错的输入位置，见 3.4 |
| `allowed` | 否 | 该位置允许的取值（1–100 个任意 JSON 值） |
| `errors` | 否 | 逐项字段错误 `{ code, message, pointer, allowed? }`，1–100 项 |

不在表里的成员一律不允许出现：ASP.NET Core 默认附加的 `traceId`、异常堆栈、iClaw 的自由成员 `details` 都会被宿主剔除（3.8）。

### 3.2 协议错误与业务错误

| 类别 | 含义 | 状态码 | `type` 段 |
|---|---|---|---|
| 协议错误 `protocol` | 请求未进入业务语义即被拒：解析失败、不符合输入 Schema、未认证、无权、寻址不到、方法不对、限流、服务端故障 | 4xx / 5xx 中**除 409、422 以外**的值 | `/problems/protocol/` |
| 业务错误 `business` | 请求合法且已授权，业务规则拒绝 | **只用** 409（状态冲突）或 422（规则不满足） | `/problems/business/` |

因此只看状态码或只看 `type` 都能区分两类。对 Agent 的含义：协议错误按 `code` 修正请求本身（补字段、换令牌、等待重试）；业务错误要改变业务意图或先处理冲突。输入 Schema 能表达的约束（必填、类型、枚举、长度、格式）违反时是协议错误 `INPUT_INVALID`；跨字段、依赖数据状态的规则违反时是业务错误。404 一律用 `NOT_FOUND`，未挂载、无权可见与不存在三种情况响应相同（R32）。

平台内置错误码（冻结在 [error-codes.json](error-codes.json)，归属 `qian.runtime`）：

| 类别 | 错误码（状态码） |
|---|---|
| 协议 | `BAD_REQUEST`(400)、`INPUT_INVALID`(400)、`UNAUTHENTICATED`(401)、`AUTHZ_DENIED`(403)、`INSUFFICIENT_SCOPE`(403)、`NOT_FOUND`(404)、`METHOD_NOT_ALLOWED`(405)、`NOT_ACCEPTABLE`(406)、`REQUEST_TIMEOUT`(408)、`PRECONDITION_FAILED`(412)、`PAYLOAD_TOO_LARGE`(413)、`UNSUPPORTED_MEDIA_TYPE`(415)、`RATE_LIMITED`(429)、`INTERNAL`(500)、`BAD_GATEWAY`(502)、`UNAVAILABLE`(503)、`GATEWAY_TIMEOUT`(504) |
| 业务（通用兜底） | `CONFLICT`(409)、`BUSINESS_RULE_VIOLATED`(422) |
| 字段级（只用于 `errors[]`） | `REQUIRED`、`TYPE_MISMATCH`、`VALUE_NOT_ALLOWED`、`OUT_OF_RANGE`、`PATTERN_MISMATCH`、`FORMAT_INVALID`、`UNKNOWN_PROPERTY`（对应 JSON Schema 的 required / type / enum·const / 范围与长度 / pattern / format / additionalProperties） |

宿主框架自己产生、没有带错误码的错误按状态码兜底（`PlatformErrors.ForStatus`）：上表有专门错误码的状态码取之；其余 4xx 取 `BAD_REQUEST`、5xx 取 `INTERNAL`，响应状态码保持原值。

### 3.3 错误码

- 形如 `QUOTA_EXCEEDED`：大写字母开头，大写字母、数字、单下划线分隔，最长 64（`^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$`）。
- 全局唯一。每个错误码有且只有一个归属组件（`owner`），在该组件的 Definition 包（`Qian.<X>.Abstractions`）里以 `static readonly ErrorCode` 定义，挂载时登记到错误码目录；同名不同定义登记即失败，别的组件要用就引用同一个定义。
- 一经发布不改名、不改类别与状态码；文案可以改。
- 每个错误码在目录里有 `title` 与 `message` 的多语言文本，`zh-CN` 必填；`message` 可含 `{name}` 占位符，各语言占位符集合必须相同，出错方按名提供参数。
- `retryable` 表示以同一幂等键原样重试是否可能成功，是错误码的属性，不逐次给出。

### 3.4 pointer、allowed 与 errors

- `pointer` 是 RFC 6901 JSON Pointer 的 URI 片段形式，可带文档前缀：请求输入写 `#/items/0/qty`（指向能力的输入文档：请求体，或按 `inputSchema` 由查询参数组装的输入对象）；文件写 `<相对路径>#/<pointer>`，如清单校验的 `.qian-plugin/plugin.json#/runtime/nuget/0`（7.2.2 校验器的「文件路径 + JSON Pointer」即此形式）。段内 `~` 写 `~0`、`/` 写 `~1`，其余按 URI 规则百分号编码（`ErrorPointer` 负责）。
- 只有一个出错位置的业务错误直接给顶层 `pointer`（与 `allowed`）。
- 有多个出错位置时给 `errors[]`；此时顶层 `pointer` 必填且等于 `errors[0].pointer`，顶层 `allowed` 等于 `errors[0].allowed`（便于只读顶层的客户端）。
- `errors[]` 的项用字段级或业务错误码，不用协议错误码。

### 3.5 Schema 表达不了的规则

1. `type` 末段等于 `code`；`kind` 与错误码目录一致。
2. `detail` 出现时等于 `message`。
3. 有 `errors` 时顶层 `pointer` / `allowed` 等于 `errors[0]` 的对应值。
4. `status` 等于 HTTP 状态行；对目录里的错误码，等于目录中的 `status`（兜底情形见 3.2 末段）。
5. `code` 必须已登记在错误码目录。

`tests/Qian.Runtime.Errors.Tests` 的 `Contracts.CheckSemantics` 是这些规则的参考实现；`error-detail.semantic-invalid.json` 给出反例。

### 3.6 .NET 用法

包 `Qian.Runtime.Errors`（无 ASP.NET Core 依赖，Definition 包登记错误码只需它）与 `Qian.Runtime.Errors.AspNetCore`（HTTP 映射）。

```csharp
// Definition 包：定义本组件的错误码
public static class FilesErrors
{
    public static readonly ErrorCode QuotaExceeded = ErrorCode.Business("QUOTA_EXCEEDED", 422, "qian.files",
        title:   new Dictionary<string, string> { ["zh-CN"] = "超出配额", ["en"] = "Quota exceeded" },
        message: new Dictionary<string, string> { ["zh-CN"] = "文件大小超出租户配额（剩余 {remaining} 字节）。",
                                                  ["en"] = "The file exceeds the tenant quota ({remaining} bytes left)." });
}

// 宿主装配
builder.Services.AddQianErrors(catalog => catalog.Register(FilesErrors.QuotaExceeded));
app.UseRequestLocalization();   // 放在 UseQianErrors 之前，错误文案才会按请求语言渲染
app.UseQianErrors();            // = UseExceptionHandler() + UseStatusCodePages()

// 业务代码：抛出或返回
throw new QianErrorException(new QianError(FilesErrors.QuotaExceeded, new Dictionary<string, string> { ["remaining"] = "1024" })
{
    Pointer = ErrorPointer.Input("sizeBytes"),
});
return QianResults.Error(new QianError(PlatformErrors.InputInvalid)
{
    Errors = [new QianFieldError(PlatformErrors.ValueNotAllowed, ErrorPointer.Input("purpose")) { Allowed = AllowedValues.Of("attachment", "avatar") }],
});
```

插件挂载时调用 `catalog.Register(...)` 得到的释放函数交给 `ctx.Effect`，卸载即撤销；同一定义重复登记按引用计数共享。错误码与文案导出：`catalog.ExportJson()`，格式即 [error-catalog.schema.json](error-catalog.schema.json)。

### 3.7 与 Kun 的衔接

`Kun.AspNetCore` 用 ASP.NET Core 的 `AddProblemDetails()`；`AddQianErrors` 在同一个 `IProblemDetailsService` 上做后置规范化，不另起一套。规范化规则：扩展成员 `code` 是已登记、非字段级、状态码一致且无占位符的错误码时保留，否则按状态码兜底；`title` / `detail` / `message` 一律取错误码目录的文案，其余扩展成员丢弃。

| Kun `ErrorType` | Kun 状态码 | Qian 错误码 | 类别 |
|---|---|---|---|
| `Validation` | 400 | `INPUT_INVALID`（`errors[]` 由 Kun 的 `property` 转为 pointer） | 协议 |
| `NotFound` | 404 | `NOT_FOUND` | 协议 |
| `Conflict` | 409 | `CONFLICT` 或组件登记的 409 业务码 | 业务 |
| `Unauthorized` | 401 | `UNAUTHENTICATED` | 协议 |
| `Forbidden` | 403 | `AUTHZ_DENIED` | 协议 |
| `Unavailable` | 503 | `UNAVAILABLE` | 协议 |
| `Failure` | 500 | `INTERNAL` | 协议 |
| `DomainException` | 400 | 组件登记的 422 业务码，缺省 `BUSINESS_RULE_VIOLATED` | 业务 |

过渡态（2026-10-07）：仓库尚未引用 `Kun.*` 包（Kun 包源接入随首个 Kun 任务）；`KunExceptionHandler` 直接 `WriteAsJsonAsync` 写 ProblemDetails，绕过了 `IProblemDetailsService`，它产生的错误体不会被规范化，且 Kun 错误码（如 `order.notFound`）不是 Qian 错误码。桥接（Kun `Error` → `QianError`、`ValidationException.Errors` → `errors[]`、上游改用 `IProblemDetailsService`）在引入 Kun 包时完成。

## 4 S2S claim

字段见 [s2s-claims.schema.json](s2s-claims.schema.json)：iClaw 同形的 `iss / aud[] / sub / tenant_id / project_id? / roles[] / scope / session_id / iat / nbf / exp / jti`，加 Qian 的 `principal_kind`、`membership_id`、`identity_revision`、`plugin_id`，预留 `act`。Schema 表达不了的规则：

1. `0 < exp − iat ≤ 300`（网关换签默认 60 秒），`iat ≤ nbf < exp`。
2. `principal_kind = service` 时没有交互会话，`session_id` 取本令牌的 `jti`。
3. `tenant_id` 必须出现；`null` 表示平台级主体，租户域端点见 `null` 一律拒绝（fail-closed）。
4. `roles[]` 只放粗粒度角色代码，服务不得仅凭它放行（R24）；权限点集合、`email` 等用户属性不进令牌。
5. `act` 为 S4 预留（RFC 8693 委托链），S1 不签发。
6. 新能力授权 = 增加 scope 串，不新增 claim。

## 5 事件信封

字段见 [event-envelope.schema.json](event-envelope.schema.json)。Schema 表达不了的规则：

1. `type` = `subject` + `.v<N>`，即 `[IntegrationEventId]` 的值；`subject` 是 `<domain>.<entity>.<verb>`（允许两段及连字符，以容纳既有事件名 `identity.session_revoked`、`lifecycle.tenant-plugin.upgraded`）。
2. `payload` 按事件目录（`GET /api/capabilities/events`）中该 `type` 的 Schema 校验；信封只保证是 object。
3. `tenant_id` 必须出现；平台级事件为 `null`。消费端据此回灌租户上下文。
4. `actor.kind` 与 S2S `principal_kind`、审计 `actor_type` 同值；定时作业等无人触发的事件以执行它的 service 主体记。
5. `trace_parent` 中的 trace-id 与 ErrorDetail `trace_id`、审计 `trace_id` 同值。

## 6 能力条目

字段见 [capability-entry.schema.json](capability-entry.schema.json)。`kind` 为 `command` / `query`（事件与实体条目不用本 Schema）。Schema 已强制：`sideEffect ≠ read` ⇒ `idempotent = true` 且 `inputSchema` 有必填的 `idempotencyKey`；`query` ⇒ `sideEffect = read`；`inputSchema` 顶层为关闭的 object 且无租户属性；`description` 至少 10 个字符。Schema 表达不了的规则：

1. `id` = `<plugin>/<name>`（R23）。
2. `schemaSha256` = SHA-256（小写十六进制）of UTF-8( RFC 8785 JCS( `{ "description": …, "inputSchema": …, "name": … }` ) )。`qian pack`、`qian.capabilities` 落库与 iClaw 运行授权快照必须用同一算法；fixture 中的值可作测试向量。
3. `inputSchema` 嵌套层也不得有租户字段（由 7.2.2 的语义校验器检查）。

与 tool/v2（`tools/<name>.json`，组件模型规格 §4.6）的关系：tool/v2 = 本条目去掉 `id` / `plugin` / `kind` / `operationId`，加 `$schema` 与 `binding`；tool/v2 的 Schema（S1-RT-008）应 `$ref` 本文件的 `$defs`（`SideEffect`、`PolicyHint`、`DataClassification`、`CostTier`、`SchemaSha256`、`AgentExposure`、`TenantFieldName`），不另抄一份枚举。

## 7 校验与测试

`tests/Qian.Runtime.Errors.Tests`（随 `dotnet test --solution Qian.slnx` 运行）：

- 每份 Schema 通过 2020-12 元 Schema 校验，带 `$id`、`title`、`description`、`x-status: draft`；声明了 `properties` 的 object 全部关闭。
- 每条 fixture：正例过 Schema 与语义规则，反例被 Schema 拒绝，语义反例过 Schema 但违反语义规则。
- `error-codes.json` 与 `PlatformErrors` 的导出逐字节一致；改了内置错误码后以 `QIAN_UPDATE_CONTRACTS=1` 运行该测试重新生成。
- 内存宿主（TestServer）中的真实响应：业务错误（抛出与返回）、输入校验、未认证、无权、404、405、415、请求体无法解析、未处理异常（不泄露异常信息）、第三方 ProblemDetails，全部符合 `error-detail.schema.json` 与语义规则，协议 / 业务按 `type` 与状态码区分，文案随 `Accept-Language`，`trace_id` 为请求的 W3C trace-id。

新增样例：在对应 `fixtures/*.json` 里追加 `{ "name", "data" }` 即可，测试自动发现。

## 8 默认裁决（待 S1-DEC-009 确认）

下列各点是写本契约时取的默认值，`draft` 期内可改；确认后去掉 `x-status: draft`。

1. 错误码一律大写下划线（与 7.5、7.7、§08 的 `AUTHZ_DENIED`、`QUOTA_EXCEEDED`、`RECORD_CONFLICT` 等一致）。R32 文中的 `plugin_not_mounted` 读作 `PLUGIN_NOT_MOUNTED`；`insufficient_scope` 仍是 `WWW-Authenticate` 头里的 OAuth `error` 值，响应体 `code` 为 `INSUFFICIENT_SCOPE`。
2. 业务错误只用 409 / 422，其余状态码都是协议错误；404 一律 `NOT_FOUND`。
3. `AUTHZ_DENIED` 归平台内置（框架默认 403 与 `qian.authz` 的 PEP 共用一个码），`qian.authz` 引用、不重复登记。
4. ErrorDetail 是封闭对象，不设 iClaw 的自由成员 `details`。由此 R32 的 403「附原因（`not_entitled` / `not_mounted` / `restricted`）」暂无结构化承载位，候选：每个原因一个错误码，或增加按错误码在目录里声明 Schema 的 `details` 成员。须在 S1-RT-004 实现前定。
5. S2S claim 与事件信封的 `tenant_id` 可为 `null`（平台级），iClaw 要求非空。
6. 字段级错误码同为大写下划线并配 `pointer`；ADR-011 D11.5 的 `{aggregate}.{field}.{constraint}` 形式在 S2 落地时对齐（`pointer` 已表达字段）。
7. `schemaSha256` 采用 RFC 8785 JCS 规范化（第 6 节）。
8. 7.4.1 / 7.5 提到的 `humanOnly` 在能力条目里没有字段，v0 未收录。
