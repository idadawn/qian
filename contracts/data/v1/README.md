# 数据契约 v1

规范与使用顺序见 [数据过滤与记录扩展契约](../../../docs/prd/specs/data-contracts.md)。本目录是 JSON Schema、字段运算符矩阵、系统变量目录和验收 fixture 的唯一内容源；Qian.Data.Abstractions 嵌入并打包根目录 JSON，测试直接验证 fixture 与发布类型。

记录事件按 Qian.Events 的模型发布：登记为集成事件的是载荷（qian.record.changed.v1.schema.json，对应 RecordChangedPayload），信封只有写入端生成的那一层。record-changed-envelope.schema.json 约束整条消息，引用 contracts/v0 的冻结信封与 UUID 定义，发布包包含这些引用的原始文件，支持离线解析；fixtures/record-changed-envelope.json 即写入端产出的形状。结构 Schema 与基于可信字段目录的语义校验都必须通过；bool/between 反例在结构上合法，但必须被矩阵校验拒绝。

S1-DATA-003 在同目录增加三份文件，同样嵌入 Qian.Data.Abstractions：

- base-data-types.json：10 个系统基准类型的种子，与目录同形、不含方言；
- data-types.schema.json：GET /api/data/meta/types 的响应。`$defs/dbDialect` 是方言组件 qian.data.dialect/v1 数据的结构，`$defs/dataFieldDefinition` 是字段定义；
- data-connection.schema.json：/api/data/connections 的请求、详情、列表与连通测试结果；任何形状都没有口令成员。

规则见 [数据契约规格 §8](../../../docs/prd/specs/data-contracts.md#8-基准类型方言与数据连接)。
