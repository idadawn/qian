# Qian.Tenancy.Abstractions

租户 seam 的稳定 Definition。TenancyDefinition.Key 与 ctx.Tenancy() 提供 ITenantDirectory；TenantDescriptor 仅包含租户 UUID、不可变租户号、名称、隔离策略和状态，不包含凭据或管理员资料。

CreateTenant 不接收 tenantId；UpdateTenant 只接收名称与并发版本，不能改租户号和隔离策略。ITenantContextPropagator 统一 JSON 与文本元数据的 tenant_id 传播，消费端必须另外提供可信来源所授权的目标租户；集成事件消费端没有独立令牌，是唯一的例外（R122），口径见 Provider 文档。具体安全边界、组件表面、profile 与剩余安装门见 [Provider 文档](../Qian.Tenancy/README.md)。

开通 Definition 在 Provisioning.cs：TenantProvisioning 服务键、ITenantProvisioning（创建/续跑/步骤诊断）、TenantAdministrator 指针输入，以及 Identity/Notify 的可选适配端口。真实实现、跳过及授权限制见 Qian.Tenancy 主文档。

Settings.cs 定义当前租户设置读模型、已授权运营门面、TenantQuotas、真实 Identity 人数统计端口与配额判定；不建立独立租户配置表。Get<T> 是 R97 固定名称，返回 ValueTask。用量缺席显式标识为不可用；成员创建的计数锁和事务由 Identity 负责。
