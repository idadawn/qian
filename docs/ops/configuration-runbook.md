# 配置开发、运行与故障处置（S1-CFG-001 / 002）

配置源顺序与引导项见 [Qian.Config.Apollo](../../packages/Qian.Config.Apollo/README.md)。本手册描述已实现客户端的行为；L0–L4 为配置运维状态，与权限模型分层无关。

## 开发配置分工

| 配置类别 | 存放与读取位置 | 边界 |
|---|---|---|
| 部署级引导 | Kustomize / 宿主环境 | Apollo 端点、AppId、环境等；Apollo 不能改写自身引导 |
| 平台 / 服务级非敏感参数 | Apollo | 发布前扫描；运行期每份原始快照与合并结果都校验，失败保留旧值 |
| 密钥 | K8s Secret 投射文件 | 配置只保存公开 secretRef，使用时经声明与审批取交集后解析 |
| 租户级在线设置 | ITenantSettings | 不进入 Apollo；能力随 TEN 任务交付 |
| 前端公开运行配置 | window.__APP_CONF__ | 只允许浏览器可公开的数据，不能放原始密钥或后端解析能力 |

密钥解析、审计端口与内存边界见 [密钥 Provider 开发说明](../../packages/Qian.Config.Secrets/README.md)，提交发布前使用 [配置扫描门禁](../../tools/Qian.Secrets.Tool/README.md)。`ApolloConfigurationSchema` 由宿主传入合成配置 Schema，敏感属性使用 `x-qian-sensitive`；Schema 自动收集仍属后续 CFG 任务。属性名与 `patternProperties` 的键都和配置键一样不区分大小写；模式按 ECMA-262 可移植子集解释，超出子集或规模上限即判 Schema 无效，键名含各方言判断不同的字符时按敏感处理。

## 降级矩阵

| 状态 | 当前行为 | 处置 |
|---|---|---|
| L0 正常 | 拉取和长轮询成功，IOptionsMonitor 随配置根更新 | 读取最终生效值；Secret 文件、环境变量、命令行仍可覆盖 Apollo |
| L1 运行期失联 | 保留当前内存配置，告警并重试，业务宿主不重启 | 恢复网络/服务，再核对实际值与修订；不能只看发布成功 |
| L2 离线启动且缓存有效 | 网络/IO 类失败时读取同环境、端点、应用、集群、namespace 和标签的有效缓存 | 保护缓存文件，恢复后回读新值；缓存只承载非敏感配置和引用 |
| L3 拒绝新快照或访问被拒 | 运行期保留旧值并重试；启动 401/403 或访问密钥缺失直接拒绝，不回退缓存 | 修正权限或候选配置；不要用删除缓存和反复重启掩盖原因 |
| L4 无可用基线 | 无网络且无有效缓存，或引导/必需项无效时启动失败 | 恢复有效配置来源；禁止用空配置启动 |

## 排查顺序

1. 确认 AppId、UAT/PRO、Cluster、ConfigServer 地址和 namespace 顺序。引导来自宿主源，Apollo 不能修改自己的访问地址。
2. 确认高优先级源是否覆盖了同名键，尤其是 Secret 文件、环境变量和命令行。配置发布成功不等于最终生效值发生变化。
3. 检查网络、DNS 与长轮询超时；客户端超时必须大于 60 秒。默认客户端关闭自动重定向。
4. 401/403 检查访问密钥引用与授权；只检查引用名和解析是否成功，不输出值。签名前每次解析引用，缺失不发送未签名请求。
5. 检查缓存所属环境、目录权限与完整性；UAT/PRO 缓存在不同子目录，快照还按端点、应用、集群、namespace 顺序和标签分隔。

## 部署步骤：平台宿主装配

platform profile 宿主（包括自建的组合包）在登记 `ISurfaceReader` 与 `IToolContractHasher` 之后，必须调用 `services.AddQianCapabilities()`。它装配能力契约的登记期检查：组件登记能力时就生成目录条目、预编译输入校验器，不合格的契约让组件挂载为 Failed，见 [Qian.Capabilities](../../packages/Qian.Capabilities/README.md)「登记期契约检查」。

漏掉这一步宿主照常启动，但检查退化为首次读目录或调用时才编译：不合格的能力不会在挂载时暴露，目录排除它并记一次告警（「能力 … 的契约不合格（规则），位置 …；该能力不登记、不进入目录。」），执行入口对它返回 404。预发环境上线后检查宿主日志：出现这条告警说明有组件带了不合格的契约；该组件的挂载状态仍是 Ready 而不是 Failed，说明组合包漏了这一步。

## 部署步骤：数据库迁移

带迁移的组件上下文（如 `qian.capabilities` 的菜单偏好与幂等回执表）随宿主镜像交付迁移，每次发布按下面的顺序上线：

1. 以 `--migrate-only` 启动迁移任务（K8s Job），用迁移属主连接执行全部待应用迁移。属主同样必须是 NOSUPERUSER / NOBYPASSRLS。
2. 用 [Qian.Data](../../packages/Qian.Data/README.md) 「迁移执行与 RLS 核查」一节的 SQL 核对：每张租户表都强制 RLS，且恰有 `qian_tenant_access` 与 `qian_tenant_guard` 两条策略。不要用 `dotnet ef database update` 代替第 1 步：RLS 由 `AddQianData<T>` 登记的生成器在迁移执行期产出，`dotnet ef` 的设计时入口没有经 `AddQianData<T>` 装配时用的是 Npgsql 默认生成器，建出的租户表没有 RLS 与策略。设计时入口经 `AddQianData<T>` 装配时，`dotnet ef migrations script` 同样带 RLS，可以用来审阅将要执行的 SQL。
3. 再滚动在线副本。启动检查发现未应用的迁移，或迁移生成器不是 Qian 的，会拒绝启动；按第 1 步处理，不要绕过。

## 过期幂等回执清理

通用执行入口的幂等回执（`plugin_qian_capabilities.invoke_receipts`）过期后不再回放。物理删除靠本租户后续带键写调用登记时的惰性清理，每次最多 100 行；长期没有带键写调用的租户，过期回执会一直留在表里，其中可能有 `public` / `internal` 能力的响应原文。按需定期用下面的 SQL 清理，删除条件与惰性清理相同（`expires_at <= now()`）。执行中的登记租约未到期，不会被删。

有 BYPASSRLS 的维护角色（如数据库管理员）一次清理全部租户，分批删除以免长时间持锁，重复执行到返回 `DELETE 0`：

```sql
DELETE FROM plugin_qian_capabilities.invoke_receipts
WHERE ctid = ANY (ARRAY(SELECT ctid FROM plugin_qian_capabilities.invoke_receipts WHERE expires_at <= now() LIMIT 10000));
```

迁移属主与在线业务角色都是 NOBYPASSRLS，表又强制 RLS，它们只看得到会话变量 `app.current_tenant_id` 指定的租户，要逐个租户执行（把 `<租户 ID>` 换成租户主档里的 ID）：

```sql
BEGIN;
SELECT set_config('app.current_tenant_id', '<租户 ID>', true);
DELETE FROM plugin_qian_capabilities.invoke_receipts WHERE expires_at <= now();
COMMIT;
```

## 生命周期和验证

配置根由创建者释放，释放会停止文件监视和 Apollo 长轮询。配置插件借用宿主根，卸载时撤销自己的 Config 服务与错误码登记，不释放其它独立消费者共用的根。

快速协议回归使用共享 ApolloFixture；真实验收使用共享 RealApolloFixture 的 Apollo 2.5.2 ConfigService/AdminService 与 MySQL 8.0.43。通过真实 AdminService 修改并发布，检查同一 IOptionsMonitor 在 60 秒内从 10 更新到 25，然后停止服务验证缓存启动和无缓存失败。CI 测试制品中的 evidence/apollo-real-reload.json 保存更新耗时。

真实发布回归还覆盖敏感字段明文：拒绝候选后，内存仍保留原引用、缓存不包含被拒值；再发布有效引用，长轮询能恢复更新。证据文件同时保存 `sensitiveCandidateRejected`、`cacheContainsRejectedValue` 和 `recoveredAfterRejection`，不能仅凭服务健康状态推断这些行为成立。

强类型配置的最后有效值使用 Kun.Configuration；失败事件、治理审计和配置 Schema 管理由后续 CFG 任务完成。生产高可用、Portal OIDC 与托管 MySQL 按部署任务验收。
