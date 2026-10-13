# Tenancy Definition

- 本包声明租户目录与传播契约，具体实现和主文档在 Qian.Tenancy。
- 不重复定义 Kun 的 ITenantContext；租户号与租户 UUID 分开，输入 DTO 禁止 tenantId。
- 公共 API 变更同步 PublicAPI 文件；DTO 不返回数据库凭据、密码或管理员个人资料。
