> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# 能力与函数

## 能力目录

组件把自己的能力登记进**能力目录**（[qian.capabilities](/components/platform/qian.capabilities.md)），分四类：命令、查询、事件、实体。

- 组件在 `Apply` 中用 `ctx.ProvideCapability<TRequest, TResponse>()` 登记能力，绑定携带源生成的契约，请求与响应的 JSON Schema 由类型信息派生；目录只列这些绑定里、当前 scope 已挂载且 Ready 的能力。
- platform profile 宿主必须调用 `services.AddQianCapabilities()`，登记时就生成目录条目并预编译输入校验器（漏掉时退化为首次读目录或调用时才编译）。不合格的契约（输入含 `tenantId` 等租户字段、Schema 无法可靠校验）抛 `CapabilityContractException`，带组件 ID、能力短名与 JSON Pointer（如 `qian.sample.crm/customer.export#/inputSchema/properties/tenantId`），组件挂载失败；问题在登记时暴露，不会留到第一次调用。
- `GET /api/capabilities?kind=command|query|event|entity&q=` 按调用者权限与当前 scope 的 restrict 过滤，被过滤的项不出现；响应信封 `{ items, ttlMs, cacheScope: "private" }` 的冻结 Schema 是 `contracts/v0/capability-catalog.schema.json`。
- `POST /api/capabilities/{plugin}/{name}/invoke` 是通用执行入口，规则见下节。
- `GET /api/capabilities/openapi.json` 聚合全部已挂载组件的 OpenAPI 3.1（需认证，不等于公开 OpenAPI）；写类能力的路径列出 `Idempotency-Key` 参数、`Idempotent-Replayed` 响应头与两种 409（`CONFLICT`、`CAPABILITY_REPLAY_UNAVAILABLE`）。
- Agent 用的工具**定义**由能力目录机械生成，保证 schema 不漂移；**暴露**哪些、怎么描述，经 `agent.expose` 单独策划（[ADR-016 D16.10](/design/adr/adr-016-iclaw-boundary.md)）。

## 代码与唯一清单

每个 `[QianPlugin]` 组件对应一份 `.qian-plugin/plugin.json` 和一个入口。同一程序集可包含多个不同组件 ID；`qian-plugin check|sync` 按目标清单 ID 选择，不合并不同组件的依赖。多组件程序集的 `[Capability]` 必须填写 `ComponentId`。契约包装器、动态表面适配器及宿主子插件通过仓库分类门记录归属，不各造一份清单。

| 已派生     | 代码来源与落点                                                                                              |
| ------- | ---------------------------------------------------------------------------------------------------- |
| 服务与具名贡献 | Inject / Provide、CommandPolicies 与按真实注册表符号绑定的 Contributes → inject / provides                        |
| 形态与入口   | QianPlugin → runtime.form / runtime.entry                                                            |
| 工具      | Capability 与源生成 JsonTypeInfo，或与实际绑定共用的静态 CapabilitySource → tools/\*.json                            |
| 配置契约    | QianConfigSchema 的完整代码声明 → runtime.configSchema 与对应 Schema 文件                                        |
| 权限与事件   | Permission / QianEvent → metadata/permissions.json、metadata/events.json；带清单侧的来源与派生指针，不放进 plugin.json |

id、版本、展示、策略、scope、包/镜像信息及表面路径仍手写；没有代码声明的权限文件与角色模板继续手写。动态 Inject、动态贡献名和未知上下文逃逸会报编译诊断，不生成空结果。需要不同构造模式的实现由固定薄入口明确依赖、委托目标与服务输出，真实装载负责验证提供项。

同程序集的多个 Provider 使用同一事件 Definition 时，由一个组件通过 MetadataTypes 拥有声明，其它组件以 SharedEvents 显式引用；生成的事件记录带 definitionComponent，保持名称、模式、载荷类型与来源同源。此机制只共享事件契约，不能共享权限，也不放宽未知上下文逃逸的诊断。

`sync` 沿既有表面目录写入，`check` 只读并报告文件与 JSON Pointer。清单保存权限、事件与配置文件的所属组件和内容哈希；手改这些文件后保留并报冲突，工具文件通过所属标记区分手写与派生。无组件注解、ID 不匹配、第二清单、手写覆盖、路径越界都有各自的 SDK 错误码及 `errors[]`。CI 自动发现所有代码入口并检查清单，同时拒绝漏分类的新实现。

R94 将 IQianPlugin 基类、客户端门面、生命周期钩子基类与 DI 注册代码生成推迟到 S2；S1 继续使用接口和 ctx.Inject。`[QianJob]` 属于 S1-SCH-001 的调度器交付，不在本次生成器补齐范围内。UI/低代码及 Wasm 表面按各自 S2 规划，代码热装卸尚未承诺。清单已经派生不代表真实安装验收完成，`x-skeleton` 仍按每个组件的安装门维护。

## 通用执行入口

`POST /api/capabilities/{plugin}/{name}/invoke` 先重做与目录相同的过滤（不可见与不存在一样 404），再按条目 `inputSchema` 校验请求体（不符返回 400 `INPUT_INVALID`，`errors[]` 带 JSON Pointer；请求体读不成确定的 JSON 值，如成员重名或有绝对值超过 decimal 范围约 7.9e28 的数，返回 400 `BAD_REQUEST`），然后走组件自己的命令管道，租户、授权与审计照常生效。

写类能力可以带幂等键，只读能力（`sideEffect: read`）忽略它、每次都执行：

| 头                           | 方向 | 含义                                                |
| --------------------------- | -- | ------------------------------------------------- |
| `Idempotency-Key`           | 请求 | UUID 文本，可带引号；多值、空值、全零或非 UUID 返回 400 `BAD_REQUEST` |
| `Idempotent-Replayed: true` | 响应 | 这次是回放：响应体与首次成功响应逐字节相同，没有再执行                       |

同一租户、同一主体、同一能力、同一键在 24 小时内的重复请求都不会再执行。409 分两种：

- `CONFLICT`：同键而输入不同，或同键请求仍在执行。改用新的键，或等前一个请求结束再重试。
- `CAPABILITY_REPLAY_UNAVAILABLE`：同键请求已经执行成功，但回执没有保存响应体，无法回放。只有 `dataClassification` 为 `public` 或 `internal` 的能力保存响应体：`pii`、`finance`、`secret` 不保存，未知分级也不保存；响应超过组件配置 `receiptMaxBytes`（默认 64 KiB）时同样不保存。应通过查询确认结果，不要换新键重试，换键会再执行一次。

`[Capability]` 的 `DataClassification` 默认 `internal`，也就是默认把响应体明文存进回执表，24 小时内同键请求原样回放。响应含个人信息、财务或机密数据的能力要显式标注，如 `DataClassification = "pii"`（`finance`、`secret` 同理）；参考应用响应含客户名称的 `customer.*` 能力都标为 `pii`。

执行失败不留回执，同一个键可以带正确的输入重试。服务端在执行后、写回执前崩溃时，5 分钟租约到期后的重试会再执行一次，所以写类能力仍要在 `inputSchema` 里带业务幂等键（`idempotencyKey`）。

## 函数

`function` 表面（Wasm 沙箱内运行的函数）是 S2 可选能力；Agent 生成的代码只落在 `function` 沙箱里（ADR-016 D16.11）。

详见 [PRD §7.2](/design/prd/07-s1-foundation.md)。
