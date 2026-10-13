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

```bash
dotnet run --project samples/Qian.Sample.Crm
```

默认入口启用真实命令宿主；原 DATA-001 只读宿主可用 --data-only 启动。库表由部署前迁移创建；应用启动只校验。CrmDbContext 使用 plugin_qian_sample_crm schema，客户继承 Qian 的安全上下文约定；分页索引是 tenant_id + id。

## 调用与授权

`GET /api/customers?skip=0&take=100` 返回 total 与 items；`GET /api/customers/{id}` 查询单个客户。服务令牌使用冻结 S2S claim 形状，principal_kind=service、sub 匹配 ReaderClientId、scope 含 crm.read、tenant_id 为有效租户 UUID。Kun 验证签名、issuer、audience 和有效期；生产租户解析只从已验证身份取得 tenant_id。

端点经过 Kun 权限判定端口。本应用的只读策略仅允许已配置服务主体对当前租户执行 qian.sample.crm:customer:read，不根据 roles 数组推导权限。返回错误统一使用 ErrorDetail，跨租户记录按不存在返回 404。

## 真实验收

Qian.Sample.Crm.Tests 启动真实 Kestrel 与共享 PostgresFixture，使用受限应用角色。管理员只建表并为 A/B 各造 1000 行；测试签发真实 RS256 令牌，遍历 A 的 1000 行、验证 B 隔离、越租户 404、伪造令牌/租户头/错误客户端和 scope 被拒绝。

测试捕获 HTTP 列表实际发出的 SQL 和参数，使用同一应用角色及租户会话执行 EXPLAIN ANALYZE，要求命中 ix_customers_tenant_id_id。不会关闭顺序扫描或扩大数据集来制造结果。CI 把实际计划保存在 test-results 的 evidence/crm-customer-list-explain.json。

## 范围

默认宿主已有创建命令，--data-only 保留原只读验收入口。完整 CRM 的表单、附件、修订升级、审计展示和用户体验由 S1-QA-007 继续扩展；真实 Qian 身份服务的交互式登录由身份任务验收。本应用不会开放管理员造数接口或生成签名私钥。


## 权限声明与默认角色

`metadata/permissions.json` 是客户资源 read/write/export 的声明，`metadata/roles.json` 定义 Owner 的本组件权限全集和使用者的 read。Authz 真实数据库测试直接读取这两份文件，先建立租户角色与主体绑定，再安装策略包，验证新增授权自动作用于已有角色；这不是另一份仅存在于测试中的权限清单。

--data-only 使用上面的固定只读服务策略；默认命令宿主使用真实 RBAC。权限和角色模板需由可信安装流程存入 Authz，再为具体主体授予角色；应用启动不会给令牌中的 roles 自动授予权限。完整用户管理、me/permissions、导出、组件打包和 QA-007 仍未完成。

## 创建命令与回滚

POST /api/customers 接收 idempotencyKey（UUID）和 name；租户来自验证后的 S2S 令牌。写客户端必须符合 WriterClientId、crm.write scope，并被真实角色授予 qian.sample.crm:customer:write。同租户同幂等键同内容返回原结果，不再新增客户或 Outbox；不同内容返回 CONFLICT。每次成功的命令调用各自入审计，包括成功的幂等重放。默认宿主的列表与详情同样经过 SDK 管道：列表按查询默认值不入审计，详情显式 Audit 后入链；两者要求 crm.read scope 和真实 read 权限，冻结租户拒绝，跨租户详情返回 404。

部署需为业务登录仅授予 CRM schema 权限，并用 AuditTransactionAccess 为独立 NOLOGIN 函数所有者与该业务登录配置受控审计入口；业务角色不获得审计表直接权限。SDK 的外层 SERIALIZABLE 事务覆盖客户、幂等回执、业务 Outbox 与审计链。争用时从新的 Mediator/Data scope 重试整笔命令，不在旧事务内单独重试审计。

客户名称在生成输入 Schema 中标为 sensitive，审计保存脱敏哈希。生产监听器读取真实租户状态、真实 RBAC 和真实 Audit 服务；业务 handler 延迟到强制校验后构造。验收用 Kestrel、RS256 和受限 PostgreSQL 角色验证成功、重复提交、冲突、审计失败整笔回滚、双租户并发及锁等待时卸载取消。Outbox 沿用 Kun 的服务级传输表，测试核验每条消息的 tenant_id 与实际客户归属；后台发布与完整 Qian 事件信封仍由事件组件完成。

这些是本机真实协议与数据库验收，未证明正式包安装、K3s 应用发布、真实 Qian Identity 管理员登录或完整 platform 已完成。

能力目录联调中，三个参考请求显式声明 agent.expose 元数据，用于验证提示词和目录过滤；这不开放 S1 Agent 身份签发或执行。平台目录/Registry 依赖仅加入验收宿主，独立客户应用不因此引用这些平台 Provider。

## profile 宿主装配与 T23

`ICrmRuntimeAssembly` 是宿主的装配入口：在首次 Mount 前接收上述同组真实业务 Provider，交由宿主的 ProfileLoader 与已安装组件解析器决定条目、配置及顺序；全部 Ready 后启动宿主监听。未注册该入口时仍使用默认独立装配。接口只依赖 Runtime，客户业务组件没有新增 Registry/Capabilities 依赖，也没有按运行模式分叉业务处理。

T23 的具体宿主装配在测试工程的 `ProfileModeAssembly`，复用真实 ProfileLoader、模块清单、制品摘要、Registry/Capabilities 与 PostgreSQL；minimal 与 platform 的边界和四次实际执行结果见 [运行时 TCK](../../tck/runtime/README.md#t23-的真实-profile-宿主对照)。这证明该运行时组合的行为等价，不宣称所有平台模块或 QA-003/QA-005 的独立安装验收已经完成。
