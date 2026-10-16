# 客户参考应用：数据与命令入口

这是 qian.sample.crm 的数据与 SDK 命令参考宿主。列表验证来自 S1-DATA-001；创建命令用于验证真实身份、权限、租户、业务/Outbox/审计事务。独立 ASP.NET Core 宿主不依赖 Registry、Lifecycle 或 Capabilities Provider。

## 启动

配置来自标准 IConfiguration；可用环境变量提供下列键：

- ConnectionStrings__Crm：受限业务连接，不能使用超级用户或 BYPASSRLS。
- ConnectionStrings__Authz / ConnectionStrings__Tenancy / ConnectionStrings__Audit：各组件的受限连接；本参考宿主的事务接线使用同一个 PostgreSQL 数据库和独立 schema。
- S2S__WriterClientId：允许发起创建命令的内部服务主体；仍须真实数据库授权。
- S2S__Issuer / S2S__Audience：受信任签发方和当前服务受众。
- S2S__ReaderClientId：允许读取客户的内部服务主体。
- S2S__PublicJwks：仅含 RSA/EC 公钥的 JWKS JSON，不放私钥。
- ConnectionStrings__Events：可选，RabbitMQ 的 AMQP 地址。设置后宿主启动 Outbox 投递；未设置时只写 Outbox，适合没有 MQ 的本地开发。
- ConnectionStrings__Migrations：可选，迁移属主角色的连接（同样不能是超级用户或 BYPASSRLS），只在 `--migrate-only` 时使用。

```bash
dotnet run --project samples/Qian.Sample.Crm
```

默认入口启用真实命令宿主；原 DATA-001 只读宿主可用 --data-only 启动。库表由部署前迁移创建；应用启动只校验。`--migrate-only` 是 K8s 迁移 Job 的入口：按宿主登记的上下文在 advisory lock 下执行未应用的迁移后退出，不启动 HTTP 宿主。本应用的上下文目前没有包内迁移，这一入口在这里不执行 DDL。CrmDbContext 使用 plugin_qian_sample_crm schema，客户继承 Qian 的安全上下文约定；分页索引是 tenant_id + id。

## 调用与授权

`GET /api/customers?skip=0&take=100` 返回 total 与 items；`GET /api/customers/{id}` 查询单个客户。服务令牌使用冻结 S2S claim 形状，principal_kind=service、sub 匹配 ReaderClientId、scope 含 crm.read、tenant_id 为有效租户 UUID。Kun 验证签名、issuer、audience 和有效期；生产租户解析只从已验证身份取得 tenant_id。

端点经过 Kun 权限判定端口。本应用的只读策略仅允许已配置服务主体对当前租户执行 qian.sample.crm:customer:read，不根据 roles 数组推导权限。返回错误统一使用 ErrorDetail，跨租户记录按不存在返回 404。

## 真实验收

Qian.Sample.Crm.Tests 启动真实 Kestrel 与共享 PostgresFixture，使用受限应用角色。管理员只建表并为 A/B 各造 1000 行；测试签发真实 RS256 令牌，遍历 A 的 1000 行、验证 B 隔离、越租户 404、伪造令牌/租户头/错误客户端和 scope 被拒绝。

测试捕获 HTTP 列表实际发出的 SQL 和参数，使用同一应用角色及租户会话执行 EXPLAIN ANALYZE，要求命中 ix_customers_tenant_id_id。不会关闭顺序扫描或扩大数据集来制造结果。CI 把实际计划保存在 test-results 的 evidence/crm-customer-list-explain.json。

## 范围

默认宿主已有创建命令，--data-only 保留原只读验收入口。完整 CRM 的表单、附件、修订升级、审计展示和用户体验由 S1-QA-007 继续扩展；真实 Qian 身份服务的交互式登录由身份任务验收。本应用不会开放管理员造数接口或生成签名私钥。


## 权限声明与默认角色

`metadata/permissions.json` 是客户资源 read/write/export 的声明（export 为 high 风险、要求 step-up、限 web 终端），`metadata/roles.json` 定义 Owner 的本组件权限全集和使用者的 read。默认命令宿主的 authz 条目是完整 Provider `AuthzPlugin`，目录由 `CrmPermissions.Compile()` 从这两份文件编译后传入：启动时持久化并登记为 `qian.sample.crm:customer:<action>`，`tenant/provisioned` 到达时为新租户建内置角色并实例化默认策略，已有 Owner / 使用者随之得到对应权限。宿主自己也持有一份同内容的活动登记（与 Provider 共享租约），供端点的权限判定器使用：authz 组件卸载时业务端点按依赖缺失返回 404。这不是另一份仅存在于测试中的权限清单。

--data-only 使用上面的固定只读服务策略；默认命令宿主使用真实 RBAC。为具体主体授予角色仍需可信流程（开通编排的初始授权、授权管理 API）；应用启动不会给令牌中的 roles 自动授予权限。`GET /api/authz/me/permissions` 返回当前服务主体在本租户的有效权限及风险、step-up、终端元数据，主体与租户只取自令牌。完整用户管理、导出、组件打包和 QA-007 仍未完成。

## 创建命令与回滚

POST /api/customers 接收 idempotencyKey（UUID）和 name；租户来自验证后的 S2S 令牌。写客户端必须符合 WriterClientId、crm.write scope，并被真实角色授予 qian.sample.crm:customer:write。同租户同幂等键同内容返回原结果，不再新增客户或 Outbox；不同内容返回 CONFLICT。每次成功的命令调用各自入审计，包括成功的幂等重放。默认宿主的列表与详情同样经过 SDK 管道：列表按查询默认值不入审计，详情显式 Audit 后入链；两者要求 crm.read scope 和真实 read 权限，冻结租户拒绝，跨租户详情返回 404。

部署需为业务登录仅授予 CRM schema 权限，并用 AuditTransactionAccess 为独立 NOLOGIN 函数所有者与该业务登录配置受控审计入口；业务角色不获得审计表直接权限。SDK 的外层 SERIALIZABLE 事务覆盖客户、幂等回执、业务 Outbox 与审计链。争用时从新的 Mediator/Data scope 重试整笔命令，不在旧事务内单独重试审计。

客户名称在生成输入 Schema 中标为 sensitive，审计保存脱敏哈希。生产监听器读取真实租户状态、真实 RBAC 和真实 Audit 服务；业务 handler 延迟到强制校验后构造。验收用 Kestrel、RS256 和受限 PostgreSQL 角色验证成功、重复提交、冲突、审计失败整笔回滚、双租户并发及锁等待时卸载取消。Outbox 沿用 Kun 的服务级传输表，每行的 payload 是完整的 v1 事件信封，测试核验每条消息的 tenant_id 与实际客户归属；投递见下节。

这些是本机真实协议与数据库验收，未证明正式包安装、K3s 应用发布、真实 Qian Identity 管理员登录或完整 platform 已完成。

能力目录联调中，三个参考请求显式声明 agent.expose 元数据，用于验证提示词和目录过滤。命令宿主的身份适配器接受服务与 Agent 两类机器主体：Agent 只用来验证目录、提示词与通用执行入口的过滤，REST 端点仍只收服务主体，S1 的 RBAC 与命令策略也不授予 Agent 权限；带 `act` 的委托令牌一律在认证阶段拒绝。这不开放 S1 Agent 身份签发或执行。平台目录/Registry 依赖仅加入验收宿主，独立客户应用不因此引用这些平台 Provider。

## 集成事件

创建客户在同一事务里写入一条 `crm.customer.created.v1`（载荷 `customerId`）。事件类型由 `CrmEvents.RegisterCrmEvents` 连同源生成 JsonTypeInfo 一起登记，宿主与契约测试共用这一处。信封由 Qian.Events 补齐：producer 是 qian.sample.crm，actor 是发起命令的服务主体，causation_id 是本次命令调用，tenant_id 是令牌里的租户。运行时另挂 qian.events 组件，提供 Events 服务键。

真实验收在 CustomerCommandTests 中，使用 Kestrel、PostgreSQL 与 RabbitMQ 容器：

- 创建客户后 outbox_messages 恰好多一行，其 payload 通过 contracts/v0 的信封 Schema；
- 后台投递确认后，订阅 `crm.customer.created` 的 quorum 队列恰好收到一条，消息体与 Outbox 行逐字节相同。

IntegrationEventCatalogTests 扫描测试输出里全部 Qian 程序集的 `[IntegrationEventId]`，要求每一个都经组件登记入口带上 JsonTypeInfo，并能导出 payload Schema。

## profile 宿主装配与 T23

`ICrmRuntimeAssembly` 是宿主的装配入口：在首次 Mount 前接收上述同组真实业务 Provider，交由宿主的 ProfileLoader 与已安装组件解析器决定条目、配置及顺序；全部 Ready 后启动宿主监听。未注册该入口时仍使用默认独立装配。接口只依赖 Runtime，客户业务组件没有新增 Registry/Capabilities 依赖，也没有按运行模式分叉业务处理。

T23 的具体宿主装配在测试工程的 `ProfileModeAssembly`，复用真实 ProfileLoader、模块清单、制品摘要、Registry/Capabilities 与 PostgreSQL；minimal 与 platform 的边界和四次实际执行结果见 [运行时 TCK](../../tck/runtime/README.md#t23-的真实-profile-宿主对照)。这证明该运行时组合的行为等价，不宣称所有平台模块或 QA-003/QA-005 的独立安装验收已经完成。

## 生命周期钩子与平台宿主（S1-RT-005）

`CrmPlugin` 同时实现 `IPluginLifecycleHooks`。

- 只处理 `tenant-provision`：在 Lifecycle 已进入的租户数据上下文里，以钩子的幂等键为主键写一条示例客户。同一键已存在时不再写，结果记 `{ "seeded": false }`。
- 其余钩子返回 null，按无操作记为成功。

钩子不要求组件已挂载，也不读取请求头或令牌。

`CrmCommandApplication.Build` 新增两个可选参数 `events` 与 `mediator`。

- Kun 的事件类型表只认第一次 `AddKunEventBus`，中介者只认最后一次 `AddKunMediator`。嵌入 Registry、Lifecycle 等平台组件的宿主，经这两个参数把它们的事件类型与领域事件处理器并进参考宿主的同一次调用，不另行调用。
- 不传时行为与独立宿主相同。
- `CrmCommandRuntime.Source()` 优先取本租户 scope 的 `crm` 条目；没有时回到全局条目的 scope 视图。由 Lifecycle 装进租户 scope 的版本因此由各租户自己的条目处理。

真实验收在测试工程的 `LifecycleAcceptanceTests`。宿主只预装参考应用 1.0.0 / 1.0.1 / 1.1.0 三个版本、不挂全局条目，由 Lifecycle 完成开通、挂载、升级、回切与契约准入。证据与边界见 [Qian.Lifecycle](../../packages/Qian.Lifecycle/README.md#测试证据)。这不代表参考应用已经以正式包安装，正式安装验收仍归 S1-RT-011。

## 平台租户运营宿主

[TenantOperationsHost](TenantOperationsHost.cs) 提供可独立启动的真实Kestrel/JwtBearer宿主，复用调用方已装配的Runtime/Data/Authz/Audit，并实际映射MapQianTenancy。启动方调用Build，传入每次重新解析global qian.tenancy的函数、可信平台issuer/audience/RS256签名校验参数及监听地址配置，然后RunAsync。它要求签名、过期时间、issuer/audience/lifetime/signature完整验证；不经过业务TenantRequestBoundary。ICurrentUser从已验证的原始令牌捕获，并经PrincipalClaims核对，MapInboundClaims=false。

这与只接受机器业务请求的CrmCommandApplication是两个监听宿主；S1-GW-001探针应使用运营宿主的地址、真实平台超管令牌及已就绪的Registry/Lifecycle/Authz/Audit，不能把CRM业务端口当作运营端口。TenantManagementTests与六步用例直接调用同一Build工厂；测试密钥不替代正式Identity签发，部署由相应任务完成。具体HTTP契约与前置见[Tenancy主文档](../../packages/Qian.Tenancy/README.md#运营-http-契约与宿主s1-ten-008)。

本机想逐个调用这些运营接口，见 [本机租户运营手测环境](../../tools/Qian.Dev.TenantOps/README.md)（S1-QA-012）：一条命令起 PostgreSQL 并用同一个 Build 工厂启动运营宿主，不替代 qian dev（S1-RT-013）或 K3s 部署（S1-DEP-002）。

宿主读取 Tenancy:Resolver:Mode、Tenancy:Resolver:AllowHeaderFallback 和 Tenancy:InitializationCapabilities 配置节，并传入 tenancy 的同一 config。Resolver 仅接受 production/false，初始化清单须是完整能力 ID，不支持通配符；空清单不会授予开户 hook 例外。失败诊断先查 GetStepsAsync 的 04:provisioned Reason，再按 [Tenancy 主文档](../../packages/Qian.Tenancy/README.md) 区分重放、清单缺席和高风险能力拒绝。
