# 平台能力目录

本目录是 Capabilities seam 的 Provider；Definition 在 `Qian.Capabilities.Abstractions`，HTTP 适配器在 `Qian.Capabilities.AspNetCore`。平台声明式表面位于 `plugins/qian.capabilities`，只装入 `platform`。按 R34，它不是消费者 standalone 中独立安装的组件；插件目录的清单由 S1-RT-014 合并生成，完整平台安装与私有包发布归 S1-RT-005 / S1-RT-013。

组件配置（`plugins/qian.capabilities/schemas/config.schema.json`）有两项，宿主未经 profile 装载时 Provider 按同一规则校验并取默认值：

- `catalogTtlMs`：目录响应的 `ttlMs`，整数 0–3000，默认 1000。上限与 PRD 7.5「授权变更 ≤ 3 秒生效」一致；它只是客户端缓存提示，执行入口每次重新判定。
- `receiptMaxBytes`：通用执行入口的幂等回执保存响应体的上限（UTF-8 字节），整数 0–1048576，默认 65536，0 表示从不保存。超限时只记「已执行」，见下文「回执策略」。

## 当前实现

`CapabilitiesPlugin` 在 global scope 提供 `ctx.Capabilities()`。它依赖实际 Registry、租户主档和租户/授权/审计策略服务键；装载器撤销必要依赖后，该服务随生命周期撤销。

**目录怎么枚举**：不是收集 Kun Mediator 的 `AddRequestHandler` 注册（Kun 的 `ICapabilityRegistry` 只描述进程内处理器，不知道组件、scope 与挂载状态）。来源是两个集合的交集：一是 SDK 具名绑定——组件在 Apply 中调用 `ctx.ProvideCapability<TRequest, TResponse>()`，以可逆注册把 `IExecutableCapability` 登记到 `ExecutableCapabilities` 具名集合，绑定携带源生成的 `CapabilityContract`；二是 Registry 的 scope 视图——`GetScopeEntriesAsync` 返回当前租户 scope 中处于 Ready、且属于已安装制品的条目。只有两边都有的能力才出现，预装声明不当成可调用代码，卸载、禁用或 restrict 后的绑定也不会因为 Mediator 里还有处理器而出现。

目录、提示词和调用每次共享同一个判定：已认证主体与当前租户一致、主档为 active/trial、scope 中的组件可见、OAuth scope 满足、真实 RBAC 允许该权限。令牌中的 roles 不授予权限。被隐藏、禁用、撤权或不存在的能力在调用表面统一为 `NOT_FOUND`，不会返回 Registry 的管理原因。

**主体类型**：目录、OpenAPI 与执行入口接受 User、Service、Agent 三类主体，带 `act` 的委托主体一律按不可见处理；Agent 主体只看到 `agent.expose: true` 的能力（R52），提示词同样只含这些能力。菜单与偏好（`Navigation`）只接受 User、Service：菜单是给人看的界面，偏好按真人或服务账号保存，Agent 不展示菜单也没有个人偏好；而能力目录在 S4 直接成为 Agent 的发现入口，S1 先让 Agent 主体按同一判定走通（PRD 7.2.1「Agent 契约」）。S1 的 RBAC 不授予 Agent 权限，`qian.authz` 的命令策略也拒绝 Agent 执行，所以 S1 里 Agent 实际看到的是空目录；`agent.expose` 过滤在授权开放前就已生效，不依赖 RBAC 兜底。

条目来自 SDK 生成的不可变 `CapabilityContract`，包括实际 JsonTypeInfo 派生 Schema、敏感字段、Agent 字段和共用 JCS/SHA-256 摘要。输出通过冻结 `capability-entry` 校验并检查嵌套输入租户字段，不包含内部 binding、配置或服务对象；条目在能力登记时生成并缓存（见下文「登记期契约检查」）。目录信封 `{ items, ttlMs, cacheScope }` 按 `contracts/v0/capability-catalog.schema.json` 以具名类型 `CapabilityCatalog` 输出。当前条目只有 command / query 两类：事件类目录归 S1-EVT-003，实体类目录待产品负责人分配（R77），交付前 `?kind=event` 与 `?kind=entity` 返回空集合。

`InvokeAsync` 调用同一绑定的真实 Mediator 请求，继续经过租户、权限与审计策略以及业务事务；目录返回值不是可缓存的执行授权。Provider 撤销会取消正在执行的请求，绑定本身也在卸载或 restrict 后拒绝缓存引用。宿主仍需按业务幂等规则处理可重试事务失败；通用绑定尚未复用 CRM 专用入口的整笔重试策略。

## HTTP 装配

宿主先安装经过验证的身份、租户与 ErrorDetail 中间件，再调用 `MapQianCapabilities`，提供从当前可信租户解析实际 Runtime 视图的函数。身份从请求服务的 `ICurrentUser` 获取，HTTP 输入不能指定主体或租户。注册身份适配器必须早于数据服务，防止匿名兜底占住框架身份端口。

- `GET /api/capabilities?kind=&q=`：返回 `{ items, ttlMs, cacheScope: "private" }`，同时设置私有且不存储的 HTTP 缓存指令。`kind` 只接受 `command` / `query` / `event` / `entity`，`q` 最长 200 字符；不符时返回 400 `INPUT_INVALID`，`errors[]` 分别为 `VALUE_NOT_ALLOWED #/kind`（带 `allowed`）与 `OUT_OF_RANGE #/q`。
- `GET /api/capabilities/openapi.json`：按同一权限过滤生成 OpenAPI 3.1，仅包含当前实际可调用绑定的执行路径和输入/输出 Schema；递归 Schema 引用重定位到各自组件。
- `GET /api/kernel/capabilities`、`GET /api/kernel/capabilities/openapi.json`：同一实现的 S1 别名（R33），响应带 `Deprecation: @1791331200`（RFC 9745，2026-10-07）与 `Link: </api/capabilities…>; rel="successor-version"`，后继路径带宿主的 PathBase 并保留原查询串（URI 不允许的字符按 RFC 3986 百分号编码）；S2 移除。
- `POST /api/capabilities/{plugin}/{name}/invoke`：通用执行入口（R75），规则见下节。

未认证请求在以上端点都返回 401 ErrorDetail（`UNAUTHENTICATED`）。提示词是服务端 `ComposePromptAsync` 端口，未新增生产 HTTP 提示词接口。测试宿主的 `/api/acceptance/prompt` 仅作为该生产端口的真实认证入口胶水。

## 通用执行入口

`POST /api/capabilities/{plugin}/{name}/invoke`（规格 §9.5、PRD 7.2.1，R75）只在 `platform` 存在，按下面的顺序处理：

1. 重做与目录相同的判定；不可见与不存在一样返回 404 `NOT_FOUND`，不可见的能力也不暴露其输入约束。
2. 请求体按条目 `inputSchema`（JSON Schema 2020-12）校验。不符返回 400 `INPUT_INVALID`，`errors[]` 每项是字段级错误码与输入 JSON Pointer：缺必填 `REQUIRED`、未声明成员 `UNKNOWN_PROPERTY`、类型不符 `TYPE_MISMATCH`、枚举 / 常量 / 组合不满足 `VALUE_NOT_ALLOWED`（带 `allowed`）、长度、范围与 `minContains` / `maxContains` 计数 `OUT_OF_RANGE`、正则 `PATTERN_MISMATCH`、格式 `FORMAT_INVALID`；同一位置只报一项，最多 100 项。JSON 无法解析、含重复成员、含孤立的 UTF-16 代理项（如 `\ud800` 转义），或任何位置有绝对值超过 decimal 范围（约 7.9e28，如 `1e29`、`1e400`）的数，返回 400 `BAD_REQUEST`：JsonSchema.Net 9.4 按 decimal 比较 `enum`、`const` 与 `uniqueItems` 的取值，这类数会让求值抛异常，所以不论 Schema 是否用到都在求值前拒绝；`decimal.MaxValue` 本身与下溢为 0 的小数照常校验。细则：
   - `format` 只强制 `uuid` 与 `date`，与 System.Text.Json 的解析口径一致；`date-time`、`time`、`uri` 等只作注解，交给绑定反序列化（System.Text.Json 接受不带时区偏移的时间与相对 URI，按 RFC 3339 / 3986 校验会拒绝绑定本来能处理的输入）。
   - `anyOf` / `oneOf` 全部分支失败时只报最接近的分支：先排除本位置类型就不符的分支（如可空类型的 `null` 分支），再取直接成员的判别字段（`const` / `enum`，如多态类型的 `$type`）不符最少者，其次违反项最少。并列分支在同一判别字段上都不符时只报该字段并合并各分支的允许值；并列分支结论不同、或 `oneOf` 同时匹配多个分支时，只在该位置报 `VALUE_NOT_ALLOWED`，不把各分支的要求混在一起。
   - `if` / `not` / `contains` 子 Schema 内部的失败只是逻辑测试，不算输入错误；`contains` 没有任何元素满足时报 `VALUE_NOT_ALLOWED`。
   - `$ref` 可指向本文档、带 `$id` 的内嵌资源与 `$anchor`，外部引用禁用。pointer 超过 contracts/v0 的 1024 字符上限（如超长属性名）时退到最近的合法上级位置。
   - `patternProperties` / `dependentSchemas` 的键不能含 `/` 或 `~`：JsonSchema.Net 9.4 对前者每次求值都抛异常、对后者静默跳过，含此类键的能力在登记阶段就被拒绝（规则 `unsupported-key`），不放过约束，执行入口也不会因此返回 500。
   - 编译好的校验器按 `schemaSha256` 缓存，最多 1024 个；满了只淘汰最久没用的一个，被淘汰的在下次执行时按同一份 Schema 重新编译。已知会让库在求值时抛异常的写法都在登记期（Schema）与上一条的输入检查里拒绝；其余意外的求值异常记一条带组件 ID 与能力名的错误日志，再交给 ErrorDetail 中间件按 500 `INTERNAL` 返回。
3. 经 SDK 绑定走真实 Mediator 管道执行，租户、授权、审计策略照常生效。

`Idempotency-Key` 请求头可选，取 UUID 文本（可按结构化字段带引号）；多值、空值、全零或非 UUID 返回 400 `BAD_REQUEST`。只读能力（`sideEffect: read`）重复执行没有副作用，忽略该键、不存回执。写类能力带键时，同一租户、同一主体、同一能力、同一键在 24 小时内的重复请求不再执行：回执保存了响应体时返回首次成功响应（同样的响应体，加 `Idempotent-Replayed: true`），按回执策略没有保存时返回 409 `CAPABILITY_REPLAY_UNAVAILABLE`；键相同而输入不同、或同键请求仍在执行，返回 409 `CONFLICT`。语义与 Kun 的幂等消费一致（R63），分三段：

1. 短事务登记：同键请求经事务级 advisory lock 串行，读到未过期的完成回执即回放（没有响应体的回执返回 409）；否则登记「执行中」（响应为空，5 分钟租约）后立即提交。
2. 执行：不占数据库连接与锁，执行耗时不影响回执表。
3. 收尾：成功时按回执策略写入原样响应或不保存的原因，并把过期时间延到 24 小时；失败撤销本次登记，不留回执。收尾用不可取消的令牌，请求在执行后取消也会完成；收尾本身失败时记录告警，照常返回执行结果或异常，标记在租约到期后失效，期间同键请求得到 409。

**回执策略**（S1-RT-020）：只有条目 `dataClassification` 为 `public` 或 `internal`、且响应体不超过 `receiptMaxBytes`（UTF-8 字节）时保存响应体。`pii`、`finance`、`secret` 与未知分级一律不保存，超限的响应也不保存；这时回执只记「已执行」与原因（`withheld` 列：`data_classification` 或 `response_too_large`），回放窗口内同键请求返回 409 `CAPABILITY_REPLAY_UNAVAILABLE`（业务类错误码，由 `CapabilitiesErrors` 登记），ErrorDetail 的说明带原因，不再执行。调用方据此通过查询确认结果，不要换新键重试——换键会再执行一次。这样敏感数据不以明文留在回执表里，大响应也不会撑大回执表。表上另有两条检查约束兜住写入前检查之外的写入：响应体不超过 1 MiB（与配置上限一致）；`withheld` 只取上述两值，且与响应体互斥。前者在迁移里以 `NOT VALID` 添加，只约束新写入：此前没有上限，存量回执可能超限，这些行会让带校验的约束失败，而迁移属主在 FORCE RLS 下删不掉它们；DDL 校验本身不经 RLS，照样会扫到这些行。

**按数据分级标注能力**：回执是否保存响应体只看条目的 `dataClassification`。SDK `[Capability]` 的 `DataClassification` 默认 `internal`，也就是默认把响应体明文存进回执表、保留到回放窗口结束；响应含个人信息、财务或机密数据的能力要显式标为 `pii`、`finance` 或 `secret`。参考应用的 `customer.create`、`customer.list`、`customer.get` 响应含客户名称，标为 `pii`：`customer.create` 带键重放得到 409 `CAPABILITY_REPLAY_UNAVAILABLE`（`data_classification`），调用方按业务查询确认结果。

登记后、写完成前进程崩溃时，租约到期后的重试会再执行一次，写类能力仍须按 `inputSchema` 中的 `idempotencyKey` 做业务幂等（tool/v2 的强制要求）。回放前照样重做第 1 步的判定（可见性、OAuth scope、RBAC），撤权后不能靠旧回执取回结果。

回执存 `plugin_qian_capabilities.invoke_receipts`（租户 RLS，保存请求指纹，以及按回执策略保存的首次响应原文）。回执过期后不再回放；物理删除靠本租户后续带键写调用登记时的惰性清理，每次最多 100 行，用 `FOR UPDATE SKIP LOCKED` 跳过其他请求正在清理的行。长期没有带键写调用的租户，过期回执会一直留在表里，由运维按[运维手册](../../docs/ops/configuration-runbook.md)「过期幂等回执清理」一节的 SQL 清理。没有用 R63 的 Redis 幂等键：Kun 的 `IProcessedMessageStore` 只记录「已处理」标记，HTTP 重放要原样返回首次响应，且回执要和租户行级隔离同库。未修改 Kun，不需要 KUN 任务。

ApiExplorer 不列这条通用模板：它只能描述静态、与调用者无关的路由，而这里的请求与响应形状随能力而变，可见集合随调用者而变。`/api/capabilities/openapi.json` 按同一判定为每个可见能力列出具体路径（`/api/capabilities/<plugin>/<name>/invoke`）、真实输入 / 输出 Schema；写类能力另列 `Idempotency-Key` 参数与 `Idempotent-Replayed` 响应头，只读能力不列。

## 登记期契约检查

平台宿主调用 `services.AddQianCapabilities()`（须已登记 `ISurfaceReader` 与 `IToolContractHasher`）后，组件 `Apply` 里的 `ctx.ProvideCapability<TRequest, TResponse>()` 在登记前检查契约：生成目录条目、预编译输入校验器，结果按契约实例缓存，目录与执行入口直接复用，不在首次调用时编译。不合格的契约抛 SDK 的 `CapabilityContractException`，组件 Apply 失败、挂载为 Failed，已登记的能力随作用域一起撤销。异常带组件 ID、能力短名、以能力 ID 为文件的 JSON Pointer（如 `qian.sample.crm/customer.tenant-probe#/inputSchema/properties/tenantId`）与规则名，`Data["reasonCode"]` 为 `INPUT_INVALID`；Registry 的 scope 装载失败（`RegistryScopeApplyException`）沿用这个原因码。

| 规则 | 含义 |
|---|---|
| `tenant-field` | 输入 Schema 任何层级含租户字段（`tenant`、`tenantId`、`tenant_id`、`tenant-id`，不分大小写）；租户只来自令牌 |
| `tool-invalid` / `entry-invalid` | 声明或生成的条目不符合 tool/v2 或 `contracts/v0/capability-entry`，位置指向条目内出错的字段 |
| `unsupported-key` | `patternProperties` / `dependentSchemas` 的键含 `/` 或 `~` |
| `unsupported-number` | `enum` / `const` 的取值（含嵌套的对象与数组）里有绝对值超过 decimal 范围的数；JsonSchema.Net 9.4 按 decimal 比较取值，遇到这类数，任何数值输入都会让求值抛异常 |
| `unresolved-reference` | `$ref` / `$dynamicRef` 在本文档、带 `$id` 的内嵌资源与锚点里都找不到目标（外部引用禁用） |
| `schema-invalid` | Schema 无法编译，如关键字取值非法、正则无法解析 |
| `contract-invalid` | 其余无法生成条目的情况 |

SDK 生成的契约由 System.Text.Json 导出 Schema，不会出现后四类问题，这几条是兜底。

**platform profile 宿主必须调用 `AddQianCapabilities()`**，[运维手册](../../docs/ops/configuration-runbook.md)的部署步骤同样要求。漏掉时 `CapabilitiesComponent` / `CapabilitiesPlugin` 在挂载阶段拒绝，异常明确指出缺失的装配；不再退化为首次请求才检查。独立宿主没有通用执行入口时不需要调用。TCK 平台组合包宿主断言容器里的 `ICapabilityContractCheck` 是宿主单例，不合格契约在登记期被拒。

## 验收与保留项

`tests/Qian.Sample.Crm.Tests/CapabilityCatalogTests.cs` 与 `CapabilityInvokeTests.cs` 使用真实 Kestrel、签名 S2S、受限 PostgreSQL、RBAC、Registry、SDK 绑定与客户业务。验证目录信封与条目契约（`schemaSha256` 与生产实现 `ToolContractHash` 一致，该实现由 `Qian.Surfaces.Tests` 按 RFC 8785 的示例与附录 B 样本验证）、别名的 `Deprecation` / `Link` 头、参数校验与匿名 401 的 ErrorDetail、缺权限/scope、restrict、卸载、数据库 scope patch、撤权与冻结、执行入口的输入校验 / 幂等重放（含执行中 409、失败不留回执、只读能力忽略幂等键）/ 回执策略（按分级与上限不存响应体时同键 409、表上的硬上限、非默认组件配置）/ Agent 主体、登记期拒绝（组件挂载失败，目录与执行入口没有该能力），并检查实际客户、Outbox 和审计。测试组合使用必要的实际 Provider，不声称已经覆盖默认 platform 全部 Provider；测试签发器也不替代真实 Identity 登录验收。Agent 用例另在测试宿主打开「授权预览」，把 Agent 主体按同 ID 服务主体求值，以单独观察 `agent.expose` 过滤；预览默认关闭，不是生产判定。

OpenAPI 聚合已覆盖 SDK 真实调用入口及宿主标注绑定的原生 HTTP 路由，实际 gRPC 契约经独立反射探测；平台挂载路径的契约准入已由 S1-RT-005 接入（R76，见「平台挂载准入」）。菜单贡献及偏好已接入，范围与验证见下节。扩展点按 ADR-020 D20.3 和组件规格 §3.6 使用既有事件定义及可逆服务注册，不另建旧版独立扩展点注册表。platform / minimal 双宿主等价与表面热重载的运行时语义由 S1-QA-002 的 TCK（T19、T23）覆盖。本组件的交付与复核记录见 S1-RT-006 与 S1-RT-019。

## 契约兼容检查

`IOpenApiCompatibilityProbe` 由 `OasDiffOpenApiProbe` 实现，使用固定的 [oasdiff 1.33.0](https://github.com/oasdiff/oasdiff/releases/tag/v1.33.0)，不以手写字段比较替代真实 OpenAPI 差异检测。下载脚本 `scripts/prepare_contract_tools.py --destination <工具目录>` 按仓库锁文件校验发行归档 SHA-256，只提取检查器普通文件。运行宿主需预装对应架构的二进制并把绝对路径交给 Probe；运行中不自动安装或更新。工具采用 Apache-2.0 许可证。

探测对基线和候选文档先做 OpenAPI 3.1 解析，再运行固定参数的兼容检查。外部 `$ref` 明确禁用；不调用 shell，不继承环境配置，使用独立空配置、受限输出和 30 秒超时。取消会终止子进程，临时契约文件在退出后删除。检测失败、版本不符和报告异常都抛错，不能视为兼容。

`OpenApiContractPlugin` 是平台装配器使用的普通包装，保持原业务插件依赖，在其 Apply 之前探测带 `/vN` 的同组件命名空间契约；破坏性变更导致挂载 Failed，业务注册不会公开。它不修改 Runtime，也不是调用者可绕过的请求参数。生产装配必须从已验证的版本化制品取得完整基线与候选，不能使用当前用户权限过滤后的 OpenAPI 返回值。宿主可用 FromRepository 选择已捕获的精确版本契约，候选版本必须与实际装载条目一致。平台挂载路径由 Registry scope Reconciler 经契约准入强制套用此包装（R76），宿主不再自行决定是否采用。

测试通过 `QIAN_OASDIFF_PATH` 指向实际锁定工具；CI 会先校验下载，缺失工具不会跳过测试。相关测试覆盖说明与兼容响应扩展、输入类型破坏、路径删除、外部引用拒绝，以及真实客户组件挂载被拒/兼容恢复后实际执行。

OpenAPI 文档格式按 [OpenAPI 3.1.1 规范](https://spec.openapis.org/oas/v3.1.1.html) 生成，使用 Microsoft.OpenApi 3.10.2 解析验证。平台发行镜像携带检查器与完整契约制品归 S1-DEP-003。

## 版本化契约仓库

Capabilities 同时提供 `Contracts` 服务键（`ctx.Contracts()` / `IContractRepository`）。仓库只读取实际 Registry 预装集合中由 `FromArtifact` 核验并捕获的文件。组件的 metadata 表面以 `contracts.json` 声明契约索引；索引引用原有文件，不形成第二份插件清单：

```json
[
  { "id": "qian.sample.crm.api/v1", "kind": "openapi", "path": "./contracts/http-v1.openapi.json" },
  { "id": "qian.sample.crm.wire/v1", "kind": "proto", "path": "./contracts/customer-v1.proto" },
  { "id": "qian.sample.crm.item/v1", "kind": "json-schema", "path": "./schemas/customer-v1.schema.json" }
]
```

契约 ID 必须属于本组件命名空间并带 `/vN`；文件必须在同一制品内，不能越界、缺失或重复声明同一 ID。OpenAPI 支持共用读取器的 JSON/YAML 语法并验证 3.1 文档；JSON Schema 使用 2020-12 元 Schema 校验；proto 原始文件按制品形成封闭依赖集合；Provider 在公开契约仓库前执行真实编译，缺少检查器、语法错误或导入缺失均失败。实际服务还须经过带认证的反射一致性探测。

按组件 ID、精确制品版本和契约 ID 查询，返回原始内容的副本、文件 SHA-256 与制品目录摘要。仓库不按业务用户裁剪契约，也没有匿名 HTTP 读取入口；卸载后旧服务引用失效，在途兼容检查会取消。`CheckOpenApiAsync` 只比较两个完整版本制品中的同族 OpenAPI，缺基线、候选或检查器均拒绝。

`CapabilityContractCompiler.CreateOpenApi` 在构建期从一个组件的完整生成契约派生文档；目录和该编译器复用同一条目校验及哈希实现。真实客户验收宿主已使用生成契约构建三个有摘要的版本制品，再经 `FromRepository` 验证破坏性修订拒绝与兼容修订恢复；不再从用户过滤后的接口响应构造基线。这些是测试宿主生成的版本制品，不等于正式 NuGet/OCI 发布：CLI 产出契约制品与签名发布归 S1-RT-013，挂载准入由 S1-RT-005 接入，默认平台的组件装配归 S1-RT-014。

## 菜单贡献、资源引用与个人偏好

`NavigationManifestPlugin` 从 `RegistryComponentBinding.FromArtifact` 捕获的 UI 文件读取 `pages[].route / expose / permission / nav`，与实际业务插件共同 Apply，并把菜单登记到 `NavigationItems` 具名集合。它是平台装配器使用的普通包装，不修改 Runtime，也不把清单里的声明自动当成已挂载状态。此包装验证自己消费的菜单字段，不代替完整 MF2 制品、UI 脚本或前端渲染验收。

`Navigation` 服务先读取真实 Registry Ready 视图，再应用 scope 遮蔽、restriction 交集、租户状态和真实 RBAC。业务插件卸载或被租户禁用后，菜单不会从预装清单、个人偏好或被遮蔽的全局项恢复。菜单资源引用采用 `qian:{tenant}:{app}:page:{id}`；本期 tenant 菜单的 app 为组件 ID，id 是规范化编码后的路由，完整引用最多 512 字符。当前提供页面菜单；外壳的页面渲染、外链类型与 App scope 归属仍由相应外壳/应用能力接入。

认证 HTTP 入口：

- `GET /api/capabilities/navigation` 返回当前实际可见的菜单，缓存声明为 private；隐藏偏好在权限过滤之后应用。
- `GET /api/capabilities/navigation/item?resourceRef=...` 解析当前有权限的资源，未知、无权限与未挂载统一 404。个人隐藏是展示偏好，不影响此访问判定。
- `PUT /api/capabilities/navigation/preference?resourceRef=...` 只接受 `{ hidden, order? }`，修改当前主体的偏好。租户、主体类型和主体 ID 来自已验证的端口，不能从请求体指定。

偏好保存在 `CapabilitiesDbContext` 的 `plugin_qian_capabilities.navigation_preferences`。表强制租户 RLS，服务查询另按 `principal_kind + sub` 过滤；这也让同名用户与服务拥有独立空间。偏好行不会授予权限，另一租户的资源引用不能写入。

`CapabilitiesDbContext` 的表（`navigation_preferences`、`invoke_receipts` 与 Kun 基类的 `outbox_messages`）随包以 EF 迁移交付（`Migrations/`），迁移由 `Qian.Data` 的迁移生成器自动生成两张业务表的 ENABLE / FORCE RLS 与 `qian_tenant_access` / `qian_tenant_guard` 策略。宿主用 `AddQianData<CapabilitiesDbContext>` 注册在线连接（受限应用角色），用 `AddQianDataMigrations` 为迁移指定属主角色连接；部署时由 `--migrate-only` 迁移任务（S1-DEP-004）调用 `IDataMigrator` 在 advisory lock 内执行，在线副本启动时只校验、发现未应用的迁移即拒绝启动。没有迁移的上下文会被迁移器跳过，所以宿主不能再用 `GenerateCreateScript` 代替。

偏好更新与 `qian.capabilities/navigation.preference` 审计在同一个 PostgreSQL Serializable 事务中提交。审计只写已知非敏感输入的规范化哈希，字段为 hidden、order 和 ASCII 资源引用，payload 不复制输入。审计失败时偏好回滚；序列化、死锁及目标键冲突会重新验证资格并重试整笔更新，耗尽后返回 CONFLICT。宿主需给本上下文的受限应用角色配置既有审计事务函数权限，不授予审计历史 DML。

真实 Kestrel/PostgreSQL 用例覆盖两个租户、两个服务主体的展示隔离，版本化租户菜单替换、禁用不回退、restriction、卸载、偏好恢复、审计失败回滚和并发更新单行。参考宿主用 UI 声明验证后端目录；没有把它当成前端远程模块已部署或真实用户交互登录的证据。

## proto 与实际 gRPC 服务探测

`IProtoCompatibilityProbe` 由 `BufProtoCompatibilityProbe` 实现，使用锁定版本 [Buf 1.73.0](https://github.com/bufbuild/buf/releases/tag/v1.73.0)。同一安装脚本按 `proto-tools.lock.json` 校验原始二进制 SHA-256。运行环境用 `QIAN_BUF_PATH` 指向工具；CI 实际安装并执行，工具缺失不会跳过测试。

`ProtoContractSet` 只保存同一制品中的 proto 文件，复制字节并沿用制品路径、文件数和总大小限制。编译使用独立目录、固定配置、禁止符号链接、清空继承环境及受控缓存，不读取制品中的 Buf 配置、网络依赖或生成器。输出包含依赖的标准 FileDescriptorSet。版本升级按 FILE 规则检测源码、JSON 和 wire 破坏性变化；语法错误、缺少导入、检查异常和超时均关闭准入。

破坏性规则只从检查器的标准输出取，且只认形如 `{"type":"<规则 ID>",…}` 的合法 JSON 行；标准输出里的警告、进度、无效 JSON 等其余行，与标准错误分开处理：两路各最多 50 行、每行截断到 500 字符，经宿主传入的 `ILogger` 分别记为警告，失败时分别附在异常的 `Data["contractDiagnostics"]`（标准输出）与 `Data["contractErrors"]`（标准错误）。标准错误不参与规则解析，形似规则的行也不算；不会因为混入一行非 JSON 而抛异常。判定只看退出码：0 兼容；100 且至少有一条规则为不兼容；100 却没有任何规则、或其他退出码，都拒绝准入。

`GrpcReflectionContractProbe` 使用宿主提供的 CallInvoker 和认证元数据连接真实 HTTP/2 reflection 服务，复制凭据且不写入诊断。它查询声明中的每个服务，收集文件和依赖，核验冲突、大小及 RPC 最终状态，再按 PACKAGE 规则检查 RPC、消息与字段；允许服务构建的 ProtoRoot 文件前缀不同，不因仅有文件布局差异误拒。两类规则依据 [Buf 兼容规则说明](https://buf.build/docs/breaking/rules/)。宿主负责端点、TLS、出网范围和探测授权；探测入口不接受客户端 URL 或可信标记。服务端在读请求前就结束调用时（如认证拒绝后立即回 401 并重置流），写请求可能先于调用状态就绪而失败：Grpc.Net.Client 会报 `Cancelled`，或直接抛传输层 `IOException`。调用方没有取消时，探测器改读响应流，按调用的最终状态报错（如 `Unauthenticated`）；调用方自己取消、截止时间到期仍分别报 `Cancelled`、`DeadlineExceeded`（S1-RT-028）。

`GrpcContractPlugin` 在业务 Apply 前先检查精确版本制品，再探测实际服务；任一失败都不能公开业务注册。测试使用实际 protobuf Echo 调用、官方 reflection 实现和 RS256 认证，验证正确协议、类型漂移、匿名拒绝、挂载失败与认证恢复后的真实声明式表面登记，并验证平台挂载准入对 proto 契约的选择（见下节）。此证明不代替生产服务部署验收。

## 平台挂载准入（R76）

`CapabilitiesPlugin` 以 `CapabilitiesDefinition.AdmissionKey` 提供 `IContractAdmission`。Registry 的 scope Reconciler 合入声明契约索引的组件版本时，经它包装解析出的插件；Lifecycle 的 `Mount` 与 `Upgrade` 走同一合入路径。准入服务不可见时，Reconciler 让该条目 Apply 失败，不会跳过检查。

`Admit(plugin, pluginId, version)` 的规则：

- 基线：同一组件已预装的版本中，低于候选、且声明同一契约 ID 与同一种类的最高版本。契约族的首个版本没有基线，不做比较。
- OpenAPI 契约套用 `OpenApiContractPlugin.FromRepository`，从预装制品取完整基线与候选。
- proto 契约套用 `GrpcContractPlugin`，反射对端由宿主经 DI 注册的 `IGrpcContractPeers` 按组件与精确版本给出。没有对端时 Apply 以 `PRECONDITION_FAILED` 失败，pointer 指向 `contracts/<契约 ID>`，不当作兼容。
- json-schema 契约随制品登记，挂载时不比较。

不兼容、比较无法完成或缺对端时条目为 Failed，业务注册不公开；失败原因经 `/api/lifecycle/diagnostics` 给出，见 [Qian.Lifecycle](../Qian.Lifecycle/README.md)。`GrpcReflectionTests` 验证 proto 的首个版本不比较、无对端 fail-closed、有对端时兼容版本 Ready；OpenAPI 的挂载、Reconciler 与升级三条路径由 `tests/Qian.Sample.Crm.Tests` 的 `LifecycleAcceptanceTests` 验证。

## 实际 HTTP 端点聚合

平台宿主注册 `AddQianHttpContracts()` 后，`IHttpContractSource` 从原生 ApiExplorer 读取实际 Minimal API/MVC 路由、参数、请求体和声明的响应类型。端点通过 SDK 的 `PluginEndpointBinding` 标注实际安装条目及服务/能力绑定，未标注的宿主路由不被猜测为组件接口。运行时先核对 Registry Ready 视图，再执行端点本身的 ASP.NET 授权策略；命中后的模型才导出到文档，避免无权限 Schema 残留。

源生成能力的具体调用路径与原生 HTTP 文档共同组成 OpenAPI 3.1。宿主需提供准确的 `Produces` 等响应元数据；无法正确描述的输入来源会失败，不输出伪造 Schema。本期已覆盖 JSON 请求体、路径、查询与请求头参数，使用 S1 后端 S2S Bearer 认证口径。路由约束会转换为 OpenAPI 路径参数，JSON Schema 的递归引用共用 SDK 的重定位实现，命名冲突拒绝覆盖。

`CompileDocumentAsync(pluginId)` 是可信构建流程的完整组件导出端口，不依赖当前 HTTP 用户或运行时权限裁剪，也没有公开 HTTP 路由。客户宿主的版本制品现已同时包含生成能力契约及实际 REST 路由契约；有限权限用户看不到创建接口，但发布基线仍保留完整操作，删掉原生路径会在挂载前被拒绝。

`MapQianCapabilities` 的端点绑定默认为 capabilities 条目，可用重载指定真实条目。宿主必须先注册并安装 SDK 插件端点中间件；目录端点过滤器会拒绝缺失的中间件证据。业务宿主需使用同一绑定与中间件保护实际入口，不能只在文档里隐藏路径。参考客户 REST 路由已绑定到实际可执行能力，因此 restrict 后文档和直接访问都拒绝。

平台运营端点可使用 Definition 的 PlatformHttpContract 标注归属，CompileDocumentAsync 将其纳入完整制品 OpenAPI；它不授予运行权限，也不进入租户可见的 GetDocumentAsync。租户业务端点仍使用 PluginEndpointBinding 与实际 Registry 挂载门禁（TEN-006）。

原生HTTP契约导出保留实际参数默认值与Range约束、DTO的AllowedValues及非空写入约束（包括DisallowNull），不以原始宽泛类型冒充接口已接受的输入。PlatformHttpContract标记优先于业务绑定，在租户可见聚合中直接排除；完整构建契约仍包含运营端点。Tenancy真宿主用例验证此区分与破坏性契约准入，schema租户字段遍历门禁落在其契约测试。

TEN-011 收口：原生 HTTP 导出同时读取字符串 MinLength/MaxLength/RegularExpression，查询取值来自 AllowedValues；与 CLR int 类型边界相同的 Range 上限不重复写成业务 maximum。DisallowNull/IsSetNullable 的去空变换仅适用于请求体，输入与输出 Schema 分开缓存，避免同一 DTO 的写入限制污染响应。动态 JsonElement 响应由 Definition 的 HttpResponseSchema 元数据提供完整 Schema；它只描述响应，不授予权限或改变执行。

TenantInputContract 是能力 inputSchema 与 HTTP 契约检查共用的租户字段规则，解析本地引用并跳过实例注解，HTTP 调用方另外按同一词表检查参数名。平台排除用例同时提供正常绑定与双标记端点；删除生产排除分支时用例必须失败（R112）。本次没有放宽 oasdiff、准入探针或失败等级。

## 清单入口与代码派生

`plugins/qian.capabilities/.qian-plugin/plugin.json` 的稳定入口是 `CapabilitiesComponent`，允许 global scope，依赖 `Data`、`Audit`、`Registry`、`Tenancy` 及三种命令策略键，提供 能力目录、契约仓库、契约准入与导航的四个 Definition 服务键。固定委托无参 `CapabilitiesPlugin`，宿主必须先调用 `AddQianCapabilities()`。OpenAPI / gRPC / 导航包装器和准入失败占位是实现构件，不是独立组件。

入口的 `Inject`、独立服务声明与 `ComponentConfiguration.Schema` 是清单代码字段及配置 Schema 的来源；通过 `qian-plugin sync` 更新组件目录，再用 `check` 验证漂移。元数据补齐不替代真实安装验收，本组件继续保留 `x-skeleton`。
