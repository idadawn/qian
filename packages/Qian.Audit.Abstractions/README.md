# Qian.Audit.Abstractions

审计记录与链验证 DTO 的 Definition。AuditRecord 是不可变快照，覆盖 Qian 的主体、委托、能力、版本、输入哈希、通道、资源、结果、追踪与补充元数据；默认字符串化不展开记录。AuditChainEntry 表示持久化序号、前序与内容哈希，AuditChainVerification 报告最早损坏的位置。

AuditErrors 定义 AUDIT_SCHEMA_INVALID，并提供可撤销的错误码登记；不回显原始 Schema 或输入值。

公共写入/读取 seam 仍须与真实能力事务及授权边界一起接线，不能用 DTO 可构造性代替写权限。组件目录、profile、算法与验收边界统一见 [Provider 文档](../Qian.Audit/README.md)。
