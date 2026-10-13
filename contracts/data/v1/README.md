# 数据契约 v1

规范与使用顺序见 [数据过滤与记录扩展契约](../../../docs/prd/specs/data-contracts.md)。本目录是 JSON Schema、字段运算符矩阵、系统变量目录和验收 fixture 的唯一内容源；Qian.Data.Abstractions 嵌入并打包根目录 JSON，测试直接验证 fixture 与发布类型。

完整记录事件引用 contracts/v0 的冻结信封与 UUID 定义，发布包包含这些引用的原始文件，支持离线解析。结构 Schema 与基于可信字段目录的语义校验都必须通过；bool/between 反例在结构上合法，但必须被矩阵校验拒绝。
