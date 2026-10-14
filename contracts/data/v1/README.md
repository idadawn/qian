# 数据契约 v1

规范与使用顺序见 [数据过滤与记录扩展契约](../../../docs/prd/specs/data-contracts.md)。本目录是 JSON Schema、字段运算符矩阵、系统变量目录和验收 fixture 的唯一内容源；Qian.Data.Abstractions 嵌入并打包根目录 JSON，测试直接验证 fixture 与发布类型。

记录事件按 Qian.Events 的模型发布：登记为集成事件的是载荷（qian.record.changed.v1.schema.json，对应 RecordChangedPayload），信封只有写入端生成的那一层。record-changed-envelope.schema.json 约束整条消息，引用 contracts/v0 的冻结信封与 UUID 定义，发布包包含这些引用的原始文件，支持离线解析；fixtures/record-changed-envelope.json 即写入端产出的形状。结构 Schema 与基于可信字段目录的语义校验都必须通过；bool/between 反例在结构上合法，但必须被矩阵校验拒绝。

S1-DATA-003 在同目录增加三份文件，同样嵌入 Qian.Data.Abstractions：

- base-data-types.json：10 个系统基准类型的种子，与目录同形、不含方言；
- data-types.schema.json：GET /api/data/meta/types 的响应。`$defs/dbDialect` 是方言组件 qian.data.dialect/v1 数据的结构，`$defs/dataFieldDefinition` 是字段定义；
- data-connection.schema.json：/api/data/connections 的请求、详情、列表与连通测试结果；任何形状都没有口令成员。

规则见 [数据契约规格 §8](../../../docs/prd/specs/data-contracts.md#8-基准类型方言与数据连接)。

## 冻结与修订

全部 Schema 为 JSON Schema 2020-12，`$id` 保持 `https://qian.dev/contracts/data/v1/` 前缀，状态统一为 `x-status: frozen`（2026-10-14）。它们与发布的 C# 契约、矩阵和 fixture 共同构成 v1 的消费边界；`fixtures/field-types.json` 是过滤正反例使用的可信字段目录，测试直接读取。

参照 [通用契约的演进规则](../../v0/README.md#2-通用约定与演进)，冻结后只允许向后兼容的增量或不改变语义的说明修正。改动须在同一 PR 同步 Schema、fixture、Qian.Data.Abstractions 与已知消费方，说明兼容性并通过结构和语义校验；涉及错误响应时同时遵守 contracts/v0 的 ErrorDetail 约束。破坏性变更新开 `contracts/data/v2/`，不覆盖 v1；不得移除冻结标记恢复为 draft。引用的通用事件信封仍按 contracts/v0 自身的状态与规则演进。
