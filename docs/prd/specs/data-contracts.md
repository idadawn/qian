# 数据过滤与记录扩展契约 v1

本文是 S1-DATA-004 的权威规格，落实 ADR-011 D11.7 与 §07 7.6；供数据过滤、高级查询、数据权限、按钮启用规则、流程条件五处共用。源文件位于 `contracts/data/v1/`，C# 类型、嵌入 Schema 与目录随 `Qian.Data.Abstractions` 发布。

## 1. 组件与交付边界

- 所属 seam 为 Data，角色为 Service Definition；文件夹提供 C# 类型、JSON Schema、运算符矩阵、系统变量目录与使用示例。
- 所有 .NET profile 可引用 Definition；组件安装入口沿用 `qian add data`，契约不单独启动 Provider。现有数据组件的装配与安装验收见 [Data 主文档](../../../packages/Qian.Data/README.md)。
- S1 交付可编译、可校验、可分发的契约。S2 的 `qian.dynamic-data` 负责 SQL/条件求值、Hook 调度、事务、Outbox 消费及元数据 HTTP 端点。

### 冻结与修订

data/v1 的冻结标记于 2026-10-15 随 PR #95 落盘，按 [R80](../../decisions/key-decisions-v0.md#验收复核裁决-r80s1-qa-010-复核2026-10-15) 显式使用 `x-status: frozen`；`$id` 统一为 `https://qian.dev/contracts/data/v1/<文件名>`。冻结后只允许兼容增量，破坏性变更新开 v2，并同步 Schema、fixture、C# 契约与已知消费方。零代码消费方即冻结以及引用 v0 草稿信封的两条例外、结构门禁和复核时点，以 [data/v1 冻结与修订](../../../contracts/data/v1/README.md#冻结与修订) 为准；本轮不提前冻结 v0。

## 2. Filter AST 与检查顺序

```json
{
  "logic": "and",
  "groups": [
    { "logic": "or", "conditions": [
      { "field": "age", "op": "between", "value": [18, 65] },
      { "field": "owner_id", "op": "in", "sysVar": "currentUserAndAllSubordinates" }
    ] }
  ]
}
```

根与组的 logic 仅为 `and` / `or`，固定两层；组内至少一条条件。根 `groups: []` 的 and 为真、or 为假。它们仅表达业务过滤条件，平台强制注入的租户、软删和权限谓词仍独立生效。

消费方先按 [filter-ast.schema.json](../../../contracts/data/v1/filter-ast.schema.json) 校验原始 JSON，再以源生成 `DataContractJson.FilterTypeInfo` 反序列化，最后调用 `FilterContracts.Validate(expression, compiledFieldTypes)`。字段映射必须来自已发布模型的编译产物，不能来自查询请求；返回错误路径为 `#/groups/0/conditions/0/op` 等 JSON Pointer，可映射为统一 ErrorDetail 的 `INPUT_INVALID` 字段错误。

校验器拒绝未知字段、未登记类型、SQL 片段和 `tenant_id` / `tenantId`（含路径段与大小写变体）。字段存在于映射中并不允许用户覆写租户条件。S2 SQL 生成只能从该映射取得标识符，每个值都绑定参数；不得把 AST 中的字段名或 value 直接拼入 SQL。S1 不生成或执行 SQL。

非空值运算符必须且只能含 `value` / `sysVar` 之一。`isNull` / `notNull` 均不携带操作数；`eq: null` 等形态无效。`in` / `notIn` 接受非空、类型一致的数组；`between` 接受两个同类型边界，表示闭区间，反向边界的求值结果为假。普通比较不把 null 当作普通值；需要匹配 null 时使用专用运算符。

## 3. 字段类型与运算符矩阵

[filter-operators.json](../../../contracts/data/v1/filter-operators.json) 是运行时与前端可消费的矩阵。C# `FilterContracts.Operators` 直接读取此目录；[矩阵 Schema](../../../contracts/data/v1/filter-operators.schema.json) 固定同一内容，测试要求完全一致。

| 类型 | 运算符 |
|---|---|
| int / bigint / decimal / float / datetime | eq、ne、isNull、notNull、in、notIn、ge、gt、le、lt、between |
| varchar / text | eq、ne、isNull、notNull、in、notIn、like、notLike |
| bool | eq、ne、isNull、notNull |
| uuid | eq、ne、isNull、notNull、in、notIn |
| json | eq、ne、isNull、notNull |

int / bigint 分别限制在有符号 32 / 64 位范围，decimal 在 .NET decimal 范围，float 为有限 double，datetime 为带时区的 RFC 3339 字符串，uuid 为规范带连字符形式。JSON 比较采用结构相等语义；对象键序不参与相等性。`like` / `notLike` 表达文本包含 / 不包含，值作为文本参数，S2 编译器须转义 `%`、`_` 与转义字符，不把用户文本当作 SQL 通配模板。

## 4. 系统变量目录

[system-variables.json](../../../contracts/data/v1/system-variables.json) 为 15 个变量提供中文说明、标量/集合基数与可比较字段类型。`FilterContracts.SystemVariables` 复用该目录：

- 标量：currentUser、currentOrg、currentPosition、formId、keyword、flowId、taskId、launchUserId。
- 集合：currentUserAndSubordinates、currentUserAndAllSubordinates、currentOrgAndChildren、currentOrgAndDescendants、currentPositionAndChildren、currentPositionAndDescendants、currentManagedOrgs。

身份/组织/流程标识仅与 uuid、varchar、text 比较；keyword 仅与 varchar、text 比较。集合仅进入 `in` / `notIn`，标量进入匹配类型的普通运算符；between 使用显式两个边界。宿主根据已认证身份与当前业务上下文解析变量，缺少上下文时拒绝求值，不回退为空过滤或扩大数据范围。S1 不实现组织树展开和变量解析器。

## 5. 数据规则策略

`DataRulePolicy` 与 [data-rule-policy.schema.json](../../../contracts/data/v1/data-rule-policy.schema.json) 定义：

| 字段 | 语义 |
|---|---|
| primaryKeyStrategy | uuidV7 或 snowflake，首次保存后不可修改 |
| optimisticConcurrency | 启用原子并发版本检查；S1 静态 EF 使用 xmin，S2 动态表使用 row_version |
| softDelete | 启用逻辑删除与对应系统列；安全谓词由引擎强制合入 |
| recordHistory | 保留字段级修改记录，随同业务事务提交 |
| businessKey | 有序、去重的业务字段列表；空列表表示没有额外业务主键，非空时联合唯一且导入共用 |

主键策略不可变、字段存在性、唯一约束与策略执行由 S2 发布校验/引擎实现。Schema 只校验单份定义，不声称可以独立比较两次发布。

## 6. Record Hook 与变更事件

`IRecordHook.BeforeAsync` 接受可信 `RecordHookContext`：租户、主体、实体、记录标识、create/update/delete 操作、可选 before 和 proposed 快照。它在当前业务事务内运行：返回 `RecordHookResult.Accept()` 沿用载荷，传入对象可改写载荷，`Reject(QianError)` 或异常使当前事务回滚。改写后必须重新执行字段权限、必填、业务主键和模型校验；不得改写租户、记录身份或已冻结的主键策略。before 不执行不可回滚的外部副作用。导入按实际新增或编辑路径执行 before。

`IRecordHook.AfterAsync` 仅在提交成功后由 Outbox 异步驱动，接收 `RecordChangedEvent`：消费端把收到的信封交给 `RecordChangedEvent.FromEnvelope`，得到解析后的 `QianEventEnvelope` 与类型化的 `RecordChangedPayload`（信封类型来自 Events seam 的 Definition `Qian.Events.Abstractions`）。至少一次投递，消费者以 EventId（即信封 event_id）幂等，失败重试不能撤销已经提交的业务数据。S1 定义两个端口与结果类型，调度次序、持久化重试与 `NpgsqlTransaction` Outbox Provider 在 S2 落地。`RecordHookResult` 克隆替换快照，原 JsonDocument 释放后结果仍可读取；记录快照不进入默认 ToString 日志。

事件固定为 `qian.record.changed.v1`，按 [Qian.Events](../../../packages/Qian.Events/README.md) 的模型发布：集成事件类型就是载荷。原 Kun 稳定事件标记 `[IntegrationEventId]` 标在 `RecordChangedPayload` 上，`RecordEventRegistration.RegisterRecordEvents` 以 `DataContractJson.RecordChangedPayloadTypeInfo` 源生成注册，事件目录的 payload Schema 由它导出。信封只有一层，由写入端在入队时生成 event_id、occurred_at、producer、actor、trace_parent 与因果链；`RecordChangedEvent` 是消费端视图，不登记、不单独序列化。完整 [信封 Schema](../../../contracts/data/v1/record-changed-envelope.schema.json) 引用冻结的 [v0 信封](../../../contracts/v0/event-envelope.schema.json)，不另建基础信封格式；[payload Schema](../../../contracts/data/v1/qian.record.changed.v1.schema.json) 包含 entityCode、recordId、created/updated/deleted/imported、tenantId、actor、changedFields 与可选 before/after 对象，成员与必填项和目录导出的 Schema 一致。快照可因权限或敏感字段策略省略；软删除可以同时包含 before 与 after。

生产者在命令 handler 的事件作用域内把载荷交给数据会话的 Outbox 写入端（`IOutboxWriter.Enqueue`），不自己拼信封，事件与业务写入同事务。载荷的 tenantId 与 actor 重复了信封字段，必须取自写入端所用的同一可信来源：当前租户上下文与 `EventEnvelopeScope.Current.Actor`（用 `RecordActor.From` 换算）。入队前调用 `payload.EnsureConsistentContext(tenant)` 核对；消费端 `FromEnvelope` 再核对一次，type 不符、平台级信封（tenant_id 为 null）、载荷不符合契约，或租户、主体与信封不一致时一律拒绝。before/after 和 changedFields 必须先按数据授权与敏感字段策略裁剪。载荷使用 camelCase，冻结信封保留 snake_case，actor 的 on_behalf_of 与 v0 一致。NuGet 同时嵌入并打包引用所需的两份 v0 Schema，消费者可离线建立 Schema 注册表。

## 7. 验证与后续接入

独立 `Qian.Data.Abstractions.Tests` 覆盖示例往返、字段矩阵拒绝 bool/between、操作数基数/类型、系统变量、未知/租户字段、Schema 注入、矩阵一致性、v0 信封与载荷登记、视图派生与上下文核对、Hook 改写生命周期和统一错误类型。`Qian.Events.Tests` 用真实写入端验证记录载荷只生成一层信封并通过记录信封 Schema；`Qian.Sample.Crm.Tests` 的事件目录用例比对导出的 payload Schema 与手写 Schema。PublicAPI 分析器登记全部公开类型；原 Data 真库安全装配测试继续保留。

S2 的五处消费方引用同一份 Schema 和矩阵，不重建自己的运算符列表。鉴权 `DataScopeRule.filter`、高级查询、启用规则与流程条件的接入点见 [§07](../07-s1-foundation.md#75-qianauthz-鉴权引擎p0--s1) 和 [§08](../08-s2-lowcode.md#82-动态数据引擎p0--s2)。

## 8. 基准类型、方言与数据连接

本节由 S1-DATA-003 补充，源文件同在 `contracts/data/v1/`，实现与装配见 [Qian.Data](../../../packages/Qian.Data/README.md)。

**基准类型**。[base-data-types.json](../../../contracts/data/v1/base-data-types.json) 是 10 个系统类型的种子，代码与 §3 运算符矩阵的字段类型一一对应。每个类型列出可携带的约束（facet）：

| 类型 | facet |
|---|---|
| int / bigint / float | range |
| decimal | precision、scale、range |
| datetime | precision（秒的小数位数） |
| varchar | length |
| text / bool / json / uuid | 无 |

系统类型不可删除、不可改名；S1 没有自定义类型。

**方言（qian.data.dialect/v1）**。方言组件提供 [data-types.schema.json](../../../contracts/data/v1/data-types.schema.json) `$defs/dbDialect` 形状的描述数据：代码、名称、驱动、缺省端口、只作展示的连接串模板，以及每个基准类型恰好一条映射（原生类型 + 与 facet 对应的约束）。注册时拒绝以下数据：

- 漏掉系统类型或重复映射；
- 约束与类型的 facet 不一致；
- 区间为负、下限大于上限，或缺省值不在区间内。

**字段定义校验**。`$defs/dataFieldDefinition` 由类型与可选的 length、precision、scale、minimum、maximum 组成，上下限以十进制字符串表示。按方言映射校验：

- 省略的长度、精度、小数位取缺省值；只给精度时，缺省小数位不超过精度；
- 长度、精度、小数位须在映射区间内，小数位不超过精度；
- 上下限须在原生类型可表示的范围内。decimal 由精度 p 与小数位 s 推出 ±(10^(p−s) − 10^−s)，上下限的小数位不超过 s；整数类型不接受小数；
- 最小值不大于最大值；
- 类型不接受的约束一律拒绝。

违反时返回 DATA_FIELD_DEFINITION_INVALID（422），errors[] 逐项指向 `#/length` 等成员。

**数据连接**。[data-connection.schema.json](../../../contracts/data/v1/data-connection.schema.json) 定义请求与响应。口令只以租户作用域的 secretRef 给出，请求中出现 password 等未声明成员即拒绝，任何响应都不含口令。连通测试先做出网检查，再在使用点解析凭据：目标主机解析出的每个地址都须通过宿主配置的拒绝 / 放行网段，连接只用检查过的地址。成功返回服务端版本与耗时；失败返回以下错误之一：
- DATA_CONNECTION_TARGET_DENIED：目标不在允许的出网范围内，此时不连接也不读取密钥；
- DATA_CONNECTION_UNREACHABLE：可重试；
- DATA_CONNECTION_REJECTED：reason 为 authentication、database、schema、permission、parameter 或 server；
- DATA_CONNECTION_CREDENTIAL_INVALID：密钥内容不能用作口令。

该操作会从平台出网，Agent 契约标 external。完整的出口代理与 SSRF 基线按 PRD 7.19 归 S2。
