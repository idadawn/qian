> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# §14 质量与评估

> 本页由构建脚本镜像自仓库 [`docs/prd/14-quality.md`](https://github.com/idadawn/qian/blob/main/docs/prd/14-quality.md)，请修改源文件，不要改本页。

> 状态：草案 v0（2026-10-06）。依据：ADR-018（阶段与退出标准）、ADR-001（Kun 缺口与消费方式）、ADR-002（TCK 与清单门禁）、ADR-020（组件模型 v2：运行时语义 TCK、文档新鲜度与 `dump-config` 门禁）、ADR-019（组件库依赖边界）、ADR-004 / ADR-006 / ADR-009（多租户、鉴权、审计的可验证性）、ADR-013（前端工程化基线）。本章规定「怎样证明做到了」：测试层级、机器可执行用例、CI 门禁、阶段退出准则与验收纪律。各功能规格章与专题 spec 末尾的验收 checkbox 必须能映射到本章的某一层级与门禁。

***

## 14.1 为什么 S1 就要做

1. **多租户与安全缺陷无法后补。** 跨租户串读、权限进令牌、审计断链这三类缺陷一旦进入 S2 的元数据运行时与 S3 的市场生态，修复面是「所有插件 × 所有租户」。参考平台的教训是：接口层鉴权恒为真、租户解析 fail-open、会话不可吊销，这些都不是功能缺失，而是无法用补丁修复的结构性缺陷。
2. **Kun 缺口在多副本 + 多租户下必然触发。** 领域事件静默丢失、消费端 at-most-once、后台链路丢租户、Outbox / Saga 多副本无锁——单副本开发环境永远复现不了，只有带真实 PostgreSQL / RabbitMQ / Redis 的集成测试在 CI 里跑多副本才能证明修好了（ADR-001）。
3. **自建身份核心的安全责任全在自己。** `qian.identity` 是 OpenIddict 之上的自建 issuer，OIDC 一致性测试、渗透测试、依赖补丁节奏必须从 S1 进预算，而不是上线前「做一次安全测试」。
4. **S4 防返工的设计只能靠门禁守住。** 带 Schema 的写命令、`inputSchema` 禁租户字段、能力目录经鉴权、事件契约化、清单与代码不漂移——这些约定靠评审守不住，靠 lint 和契约测试才守得住。
5. **插件机制的正确性是乘法。** 一个平台插件（原称系统插件）「独立出修订 → 逐租户迁移 → 回切」是 S1 退出标准；TCK 不先于第一个插件存在，`platform` profile base 组合包里的其余平台插件就会各自解释运行时语义与生命周期。v2 的运行时语义（可逆副作用、scope 过滤、五种分发）是自研的，.NET 没有参考实现可对照，只能用 TCK 固化——语义以 ADR-020 与 specs/component-model.md 为准，不以某个实现为准（ADR-020 §5）。

***

## 14.2 测试金字塔与门禁

| 层级                   | 覆盖对象                                                                                                                                                                                                                                                                                                                                                                                                                                                          | 工具与约束                                                                                         | 阻断级别                 | 阶段      |
| -------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------- | -------------------- | ------- |
| 单元测试                 | 领域规则、权限求值、Filter AST 编译、表达式解释器、令牌对比度                                                                                                                                                                                                                                                                                                                                                                                                                          | xUnit / Vitest；覆盖率阈值按包设定（`Qian.Runtime` 与 `qian.authz` ≥ 85%，其余 ≥ 70%；建议值，待研发负责人校准）           | PR 阻断                | S1      |
| 属性测试                 | 流程逆向语义交叉（退回 / 撤回 / 撤销 / 拒绝续流 × 子流程 / 加签 / 计时器）、Schema Migrator 的「扩展 → 迁移 → 收缩」可重放性                                                                                                                                                                                                                                                                                                                                                                            | FsCheck / fast-check；只追加流转记录为不变量                                                              | PR 阻断                | S2      |
| 集成测试（Testcontainers） | PostgreSQL 16 + RLS、RabbitMQ、Redis、Apollo（mock server）；**多副本**（同一消费者 / Saga 执行器 / Outbox 发布器起 2–3 个实例）；**至少一次**（重投、乱序、重复）；**租户回灌**（消息 / Saga / 作业中的租户作用域）                                                                                                                                                                                                                                                                                                     | 每个平台插件至少一组；跑真库而非 InMemory；断言含 `tenant_id` 行计数与审计条目                                            | PR 阻断                | S1      |
| 契约测试                 | OpenAPI 3.1 diff（additive-only，`x-status: draft` 期例外）、proto breaking（buf）、AsyncAPI diff、JSON Schema 2020-12 兼容性、yue 的 FormSchema / ListSchema / 控件 manifest 版本兼容                                                                                                                                                                                                                                                                                              | 契约文件为真相源；前端类型从 OpenAPI 生成并作快照                                                                 | PR 阻断                | S1      |
| 跨租户渗透测试样例            | HTTP / gRPC 流式 / 消息 / 文件票据 / 数据引擎 Filter AST / 直连 DB 角色六类通道的越权探测                                                                                                                                                                                                                                                                                                                                                                                              | 作为集成测试的固定子集（§14.3 示例 1），每次 PR 跑；外部渗透测试每阶段至少一次                                                 | PR 阻断 / 阶段阻断         | S1      |
| TCK（三套，ADR-020）      | ① 运行时语义套件：`Inject` / `Apply` 装载顺序与缺失键报错、可逆副作用按逆序撤销、scope 遮蔽与 `restrict` 交集、五种分发模式、profile / 组合包 / patch 层叠与 `dump-config` 一致性（.NET 与 TS 同一组 fixture，以 `minimal` profile 为基准宿主）；② 独立模式套件：组件在 `standalone` 宿主（消费者自己的 ASP.NET Core 项目，不装 `qian.registry` / `qian.lifecycle` / `qian.capabilities`）里 `qian add` 后可用，未显式选 `SingleTenant` 且无宿主 `ITenantContext` 即拒绝启动；③ 组件一致性套件：清单校验、契约一致性、生命周期钩子幂等性（重复调用 `tenantProvision / tenantUpgrade`）、跨租户越权探测、性能基线、「未挂载」三处拦截 | `qian` CLI 一键运行；上架闸门 = 三套 TCK 全绿 + can-i-deploy；官方与第三方同一套                                     | 发布阻断                 | S1      |
| OIDC 一致性             | `qian.identity` 的授权码 + PKCE、refresh、后通道登出、discovery、`resource` 参数                                                                                                                                                                                                                                                                                                                                                                                             | OpenID 官方一致性套件（Basic / Back-Channel Logout profile）                                           | 阶段阻断                 | S1      |
| E2E                  | 开发者写一个多租户应用的完整路径（S1）；建应用 → 表单 / 列表 → 审批 → 授权（S2）；市场选购 → 开通 → 自助配 IdP（S3）                                                                                                                                                                                                                                                                                                                                                                                      | Playwright，chromium + webkit；在 staging 跑；`data-testid` 寻址                                     | 阶段阻断（夜间）             | S1      |
| 无障碍                  | yue 全部组件与关键页面                                                                                                                                                                                                                                                                                                                                                                                                                                                 | 独立 Vitest 浏览器测试与 Rspress 静态页面 Playwright + axe，critical / serious = 0；令牌对比度单测 WCAG AA ≥ 4.5:1 | PR 阻断（组件）/ 夜间（页面）    | S1      |
| 性能预算                 | 首屏 script 体积（error 级）、Lighthouse perf / a11y / bp；API p95 基线；Schema Migrator 单租户迁移时长                                                                                                                                                                                                                                                                                                                                                                          | Lighthouse CI；k6 基线脚本                                                                         | PR 阻断（体积）/ 阶段阻断（p95） | S1      |
| 视觉回归                 | 每个 yue 组件默认 / 悬停 / 禁用 / 错误 / 暗色                                                                                                                                                                                                                                                                                                                                                                                                                               | Playwright 截图，固定字体与容器镜像，基线入库                                                                  | S1 可选 / S2 PR 阻断     | S1 / S2 |
| 安全扫描                 | 依赖漏洞、容器镜像、SAST、密钥形态扫描、许可证                                                                                                                                                                                                                                                                                                                                                                                                                                     | Trivy / CodeQL / gitleaks；许可证白名单（排除 AGPL 与不允许多租户 PaaS 的条款）                                    | PR 阻断                | S1      |
| 混沌与恢复                | Pod 驱逐、RabbitMQ 断连、PG 主从切换、Apollo 不可达时的降级矩阵；备份恢复演练                                                                                                                                                                                                                                                                                                                                                                                                            | staging 每阶段一次；记录 RTO / RPO                                                                    | 阶段阻断                 | S1      |

首屏 script 体积预算（PR 阻断，error 级）：外壳首屏 script 压缩后传输体积 ≤ 250 KB，由 Lighthouse CI 的 `resource-summary:script:size` 断言；S1-YUE-003 的基线是验证门靶场 `apps/yue-playground`（React 19 + Base UI 五原语 + Tree）约 151 KB，`@yue/shell` 建立后改为对外壳首屏测。改预算先改这里，再改 `apps/yue-playground/lighthouserc.cjs`。

***

### 14.2.1 门禁运行节奏与环境

| 节奏   | 运行什么                                                                | 环境                                        | 时限                     |
| ---- | ------------------------------------------------------------------- | ----------------------------------------- | ---------------------- |
| PR 级 | 单测、属性测试、集成测试（Testcontainers）、契约测试、跨租户渗透样例、安全扫描、a11y、性能预算、分层与契约 lint | 自托管 runner（需 Docker-in-Docker 或远程 Docker） | ≤ 20 分钟，超时即优化测试而不是放宽门禁 |
| 夜间   | E2E 全量、视觉回归全量、Lighthouse 全页、k6 基线                                   | staging                                   | 失败次日站会第一项              |
| 发布级  | TCK、SBOM / 签名校验、can-i-deploy、迁移脚本演练                                 | 制品库 + staging                             | 阻断发布                   |
| 阶段级  | OIDC 一致性、外部渗透、混沌与恢复演练、退出准则汇总                                        | staging / prod-smoke                      | 阻断阶段退出                 |

多副本的模拟方式统一：Testcontainers 启动一套依赖，被测服务以进程内多宿主（2–3 个 `WebApplicationFactory` 或 Generic Host 实例共享同一数据库与消息总线）运行；禁止用「单实例 + 顺序调用」冒充多副本。租户夹具由统一的测试数据工厂生成（至少两个租户、每租户至少两个主体），所有集成测试默认在双租户上下文下运行，单租户测试需显式声明理由。

测试数据不含真实个人信息；凭据只来自种子与示例配置文件。安全用例的 `flaky_budget` 为 0：一次失败就是失败，不允许重试通过。

## 14.3 机器可执行用例格式

每个阶段级与安全级用例以 YAML 落在 `tests/cases/<stage>/<id>.yaml`，由测试运行器解析执行；字段含义：

| 字段                    | 含义                                                                                                                                                            |
| --------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `id`                  | 稳定编号 `QA-<stage>-<domain>-<nnn>`，被 PRD 验收 checkbox 与阶段退出准则引用                                                                                                  |
| `stage`               | S1 / S2 / S3 / S4                                                                                                                                             |
| `tier`                | unit / integration / contract / e2e / pentest / tck                                                                                                           |
| `preconditions`       | 环境与数据前置，可由运行器自动准备                                                                                                                                             |
| `steps`               | 以主体身份（`as`）执行的动作序列                                                                                                                                            |
| `expected_behavior`   | 行为断言列表，每条可独立判 PASS / FAIL                                                                                                                                     |
| `acceptance_criteria` | `max_duration_seconds`、`artifact_required`（必须产出的证据文件）、`must_pass_in`（ci / staging / prod-smoke）、`zero_tolerance`（任一断言失败即整体失败）、`flaky_budget`（允许的重试次数，安全用例为 0） |
| `judge`               | `deterministic`（HTTP 状态 + JSON Path + SQL 计数 + 日志匹配）或 `rubric`（人工评审项，仅用于体验类）                                                                                  |

运行器的行为约定：`preconditions` 由运行器幂等准备（重复运行不产生重复租户或用户）；`steps` 中的 `as` 必须是夹具里存在的主体，运行器为其签发真实令牌而不是绕过网关；每条 `expected_behavior` 独立判定并在报告中逐条列出 PASS / FAIL 与证据指针；`artifact_required` 列出的文件缺一即判 FAIL；`judge: deterministic` 的断言只允许 HTTP 状态、JSON Path、SQL 计数、日志 / 审计匹配四类，不允许「人工目测」。用例 id 与 PRD 验收 checkbox 双向引用：checkbox 引用不存在的 id、或用例未被任何 checkbox 引用，都由 C17 门禁报告。

### 示例 1 · S1 跨租户越权探测

```yaml
id: QA-S1-TENANT-001
title: 跨租户越权探测（HTTP / Filter AST / 写入 / 消息 / 文件票据 / 直连 DB 六通道）
stage: S1
tier: integration
owner: qian.tenancy
preconditions:
  - 租户 t-a、t-b 已开通（shared + RLS），各一名普通用户 u-a、u-b
  - u-a 拥有 demo.order 的 read / write；u-b 拥有同样权限但只在 t-b
  - 消费者 demo.order-projector 以 2 个副本运行
steps:
  - as: u-a
    do: POST /api/demo/orders            # 返回 id = O1
  - as: u-b
    do: GET /api/demo/orders/O1
  - as: u-b
    do: POST /api/demo/orders:query
    body: { filter: { logic: and, groups: [{ logic: and, conditions: [{ field: tenant_id, op: "==", value: "t-a" }] }] } }
  - as: u-b
    do: PATCH /api/demo/orders/O1
    body: { tenant_id: "t-b" }
  - as: system
    do: publish event { subject: demo.order.created.v1, tenant_id: t-a, payload: { id: O1 } } to replica 2
  - as: u-b
    do: GET /files/preview?ticket=<伪造或过期票据，指向 t-a 的文件>
  - as: db-role(demo-service)
    do: SELECT count(*) FROM demo.orders   # 不设置 app.current_tenant_id
expected_behavior:
  - u-b 读取 O1 返回 404（不泄露存在性），审计 outcome=denied
  - Filter AST 含租户字段 → 400，error_code=CONTRACT_TENANT_FIELD_FORBIDDEN
  - 写他租户 id → 被 RLS WITH CHECK 拒绝，返回 403，审计 outcome=denied
  - 副本 2 在 t-a 作用域处理事件，投影行 tenant_id=t-a；若作用域缺失则 fail-closed 进死信并告警，不得落到默认租户
  - 伪造 / 过期票据 → 401，审计记录含 channel=api
  - 未设置 app.current_tenant_id 的查询返回 0 行
acceptance_criteria:
  max_duration_seconds: 120
  artifact_required: [audit-entries.json, rls-policies.sql, dead-letter.json]
  must_pass_in: [ci, staging]
  zero_tolerance: true
  flaky_budget: 0
judge:
  type: deterministic
```

### 示例 2 · S1 消费端重投不丢

```yaml
id: QA-S1-EVENTS-002
title: Outbox → RabbitMQ → 消费端：至少一次、幂等、多副本不丢不重
stage: S1
tier: integration
owner: qian.events
preconditions:
  - demo.order 命令 handler 在同一 scope 内发出 demo.order.created.v1（Outbox 同事务写入）
  - 消费者 demo.notify 2 副本；Redis 幂等存储可用；重试退避 3 次后进死信
steps:
  - as: u-a
    do: 连续创建 500 条订单（并发 20）
  - as: chaos
    do: 第 100–150 条处理期间杀掉消费者副本 1
  - as: chaos
    do: 对第 200–220 条由 broker 重投一次（模拟 ack 丢失）
  - as: chaos
    do: 第 300 条消费时 Outbox 发布器两副本同时运行 30 秒
  - as: chaos
    do: 第 400 条处理时注入持续失败，验证退避与死信
expected_behavior:
  - Outbox 表最终 0 条 pending；所有 500 条事件至少投递一次
  - demo.notify 对 500 个 event_id 各产生恰好 1 条副作用记录（幂等生效）
  - 重投的 21 条被幂等存储去重，无重复副作用
  - 消费端采用「处理成功后 ack」：被杀副本未完成的消息被副本 2 接管处理
  - Outbox 两副本并存期间无重复发布（锁生效），无顺序错乱导致的丢失
  - 第 400 条经 3 次退避后进死信，死信带 tenant_id、event_id、最后一次错误与 trace_id
  - 全程 tenant_id 在消费端作用域中可读，审计 actor_type=service
acceptance_criteria:
  max_duration_seconds: 300
  artifact_required: [outbox-final.csv, consumer-side-effects.csv, dead-letter.json, trace-sample.json]
  must_pass_in: [ci, staging]
  zero_tolerance: true
  flaky_budget: 0
judge:
  type: deterministic
```

### 示例 3 · S2 应用包双租户独立升级

```yaml
id: QA-S2-BUNDLE-003
title: 同一 managed 应用包在两个租户分别安装、各自配置、独立升级互不影响
stage: S2
tier: tck
owner: qian.entitlement
preconditions:
  - 组合包 demo.crm@1.0.0（原 bundle；managed，含 package 元数据、ui、迁移脚本、默认角色模板）代码部分已签名并推入 Harbor，marketplace 条目已登记
  - 租户 t-a、t-b 均获授权（Entitled）
steps:
  - as: tenant-admin(t-a)
    do: 安装 demo.crm@1.0.0，批准反向权限 { data: crm.*, platform: [user:read] }
  - as: tenant-admin(t-b)
    do: 安装 demo.crm@1.0.0，批准同样权限
  - as: builder(t-a)
    do: 在 tenantEditable 的表单布局上新增字段 ext_note；修改列表列顺序
  - as: publisher
    do: 发布 demo.crm@1.1.0（新增字段 priority、修改 locked 的模型字段长度、新增反向权限 notify:send）
  - as: tenant-admin(t-a)
    do: 升级到 1.1.0（先 dryRun 取冲突清单，再执行）
  - as: builder(t-b)
    do: 不升级，继续使用 1.0.0 并新建 10 条记录
  - as: tenant-admin(t-a)
    do: 回切到 1.0.0 修订
expected_behavior:
  - 两租户安装后各自拥有独立的 Installation、独立的物理表（同名不同 tenant_id 数据）与独立配置
  - t-a 升级 dryRun 返回三方比较结果：ext_note 保留（租户定制在 tenantEditable 路径）、priority 新增、字段长度变更为 protected 路径冲突项并列入清单
  - 升级因新增反向权限 notify:send 要求重新批准，未批准前不执行
  - 迁移按「扩展 → 迁移数据 → 收缩」执行并记录；t-a 的升级期间 t-b 的 10 次写入全部成功、无锁等待超时
  - t-b 仍运行 1.0.0，其 Records API 与菜单不变
  - t-a 回切后数据不丢（收缩步骤未执行或可逆），`ext_note` 字段与数据仍在
  - 两租户的权限点、菜单、Apollo namespace 均按租户隔离，t-a 的升级不改变 t-b 的 Entitled / Enabled 状态
acceptance_criteria:
  max_duration_seconds: 900
  artifact_required: [dry-run-diff.json, migration-plan.json, install-state-t-a.json, install-state-t-b.json]
  must_pass_in: [staging]
  zero_tolerance: true
  flaky_budget: 0
judge:
  type: deterministic
```

***

## 14.4 CI 门禁清单

| #   | 门禁               | 检查内容                                                                                                                                                                                                                                                     | 失败即           | 阶段 |
| --- | ---------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------- | -- |
| C1  | PublicAPI        | Kun、`Qian.Runtime` 与各 `Qian.<X>.Abstractions` 公共 API 变更必须更新 `PublicAPI.Shipped/Unshipped.txt`；破坏性变更需 ADR 引用                                                                                                                                              | PR 阻断         | S1 |
| C2  | 分层边界 lint        | 后端四层依赖（Kun 约定）；yue 六层依赖方向（eslint-plugin-boundaries / dependency-cruiser）；横切包只被上层消费                                                                                                                                                                       | PR 阻断         | S1 |
| C3  | 清单与代码漂移          | 源生成器重新生成 `.qian-plugin/plugin.json` 的代码部分字段并与仓库中的清单比对（只有一份清单，不存在手写 YAML 与派生文件并存）；端点权限点声明与 OpenAPI `x-qian-*` 扩展一致                                                                                                                                        | PR 阻断         | S1 |
| C4  | 契约 lint          | 写入端点必须有 JSON Schema；`inputSchema` 禁止 `tenant_id / tenantId` 等租户字段；命令元数据必须含 `sideEffect`；事件必须有稳定 ID 与 Schema                                                                                                                                              | PR 阻断         | S1 |
| C5  | 品牌字样闸门           | 仓库文本（文档、注释、提交信息、i18n 文案）不得出现参考平台品牌名、公司名、域名或源码路径，不得出现已作废项目名                                                                                                                                                                                               | PR 阻断         | S1 |
| C6  | 密钥形态扫描           | gitleaks + 自定义规则（Apollo 导出、Kustomize、清单中的疑似密钥键名与值）；配置 Schema 中 `sensitive` 字段只能为 secretRef                                                                                                                                                               | PR 阻断         | S1 |
| C7  | SBOM / 签名        | 每个 profile 镜像与组件代码制品产出 SBOM（SPDX）并 cosign 签名；市场文件 `.qian/marketplace.json` 分离签名；部署前校验签名与 digest；许可证白名单                                                                                                                                                   | 发布阻断          | S1 |
| C8  | 多租户基类检查          | 在线 DbContext 必须继承多租户基类并启用 RLS 拦截器；新表必须有 `tenant_id` 与 RLS 策略（平台表白名单例外）                                                                                                                                                                                   | PR 阻断         | S1 |
| C9  | 迁移脚本检查           | 迁移脚本幂等（可重复执行）；收缩步骤单独标记；不出现 `DROP TABLE` 于非收缩步骤                                                                                                                                                                                                           | PR 阻断         | S1 |
| C10 | 测试门禁             | 单测覆盖率阈值；集成测试（Testcontainers）必须跑；安全用例 `flaky_budget: 0`                                                                                                                                                                                                   | PR 阻断         | S1 |
| C11 | 契约兼容             | OpenAPI diff additive-only、proto breaking、AsyncAPI diff；前端类型快照                                                                                                                                                                                           | PR 阻断         | S1 |
| C12 | i18n 硬编码         | yue 与各前端面不得出现硬编码界面文案；i18n key 表作为契约发布                                                                                                                                                                                                                    | PR 阻断         | S1 |
| C13 | a11y 与对比度        | axe critical = 0；令牌对比度单测；jsx-a11y                                                                                                                                                                                                                        | PR 阻断         | S1 |
| C14 | 性能预算             | Lighthouse script 体积 error 级；publint；循环依赖                                                                                                                                                                                                                | PR 阻断         | S1 |
| C15 | 视觉回归             | 组件截图基线比对                                                                                                                                                                                                                                                 | S2 起 PR 阻断    | S2 |
| C16 | TCK              | 组件发布前三套 TCK（运行时语义 / 独立模式 / 组件一致性）全绿；can-i-deploy                                                                                                                                                                                                         | 发布阻断          | S1 |
| C17 | 文档一致性            | PRD 验收 checkbox 引用的用例 id 必须存在；ADR 状态与 PRD 引用一致                                                                                                                                                                                                           | PR 警告 → S2 阻断 | S1 |
| C18 | 文档新鲜度            | 配置目录、模块图、事件图（生产方与消费方映射）、能力 seam 图由 `qian docs generate` 从代码生成，CI 重新生成并与提交内容比对，漂移即失败（ADR-020 D20.9）                                                                                                                                                       | PR 阻断         | S1 |
| C19 | 依赖图断言            | 组件库（`Qian.<X>.Abstractions` 与 Provider 包）只依赖 `Qian.Runtime`，不依赖 `qian.registry` / `qian.lifecycle` / `qian.capabilities` 等插件（取代 ADR-019 的「不依赖 `Qian.Kernel`」断言）；扩展插件只依赖 Service Definition、不依赖具体 Provider；事件分发模式声明与调用点一致（ADR-020 D20.3 / D20.4 / D20.10） | PR 阻断         | S1 |
| C20 | `dump-config` 漂移 | 每个发行 profile（`platform` / `gateway` / `identity` / `standalone` / `minimal`）的 `qian --profile <p> --dump-config` 输出与仓库内基线快照比对，条目增删或 config 变化须随 PR 更新快照并说明；patch 不得移除 profile 标记为不可移除的强制执行点（租户、鉴权、审计监听器）                                                 | PR 阻断         | S1 |

***

## 14.5 阶段退出准则

每条准则对应 ADR-018 的退出标准并量化；「证据」指必须留存的产物。退出评审只认证据，不认口头。

| 阶段     | 准则                           | 量化指标                                                                                                                                                                                             | 证据                                            |
| ------ | ---------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | --------------------------------------------- |
| **S1** | 开发人员用代码写出多租户应用，八项技术组件全部由平台提供 | 示范应用 E2E（登录 / SSO / 鉴权 / 存储 / 文件 / 配置 / 网关 / 事件 / 审计）100% 通过；从零到可运行 ≤ 2 人日                                                                                                                       | E2E 报告、示范应用仓库                                 |
| S1     | 一键安装（ADR-020 §6）             | 空白 ASP.NET Core 项目 `qian add identity authz files` 后 ≤ 30 分钟跑通登录、权限检查与上传，不部署 Qian 平台；由未参与运行时开发的开发者计时；独立模式 TCK 通过                                                                                 | 计时记录、独立模式 TCK 报告、`samples/standalone-minimal` |
| S1     | 多租户隔离                        | 跨租户渗透用例（§14.3 示例 1 及其变体）0 漏洞；外部渗透测试高危 0、中危已修复                                                                                                                                                    | 渗透报告、用例结果                                     |
| S1     | 消息与事件                        | 至少一次 + 幂等用例 0 丢失 0 重复副作用；Outbox 最终 pending = 0；死信可追溯                                                                                                                                             | 用例产物                                          |
| S1     | 审计                           | 哈希链 100% 可验证；每个写命令 100% 有审计条目；`actor_type / channel` 填充率 100%                                                                                                                                    | 链校验报告                                         |
| S1     | 身份                           | OIDC 一致性套件 Basic + Back-Channel Logout 通过；会话失效清单 9 项全部有用例                                                                                                                                        | 一致性报告                                         |
| S1     | 鉴权                           | 100% 端点声明权限点（扫描）；缺省拒绝用例通过；`identity_revision` 失效 ≤ 3 秒                                                                                                                                           | 端点扫描报告                                        |
| S1     | 组件机制                         | 任一平台插件「出修订 → 逐租户迁移 → 回切」演示通过；`platform` profile base 组合包内全部平台插件三套 TCK 全绿；未签名安装失败；`qian --profile platform --dump-config` 打印整棵插件树且任一条目可被 patch 替换；文档新鲜度（C18）、依赖图断言（C19）、`dump-config` 漂移（C20）门禁启用 | TCK 报告、演示录屏、`dump-config` 基线快照                |
| S1     | 可观测                          | trace 覆盖 ≥ 95%（含消息与作业；建议值，待研发负责人校准）；每服务 `/healthz /readyz`；Alertmanager 告警可达                                                                                                                     | 采样报告                                          |
| S1     | 稳定性                          | staging 7 天 crash（容器重启）≤ 3%（建议值，待研发负责人校准）；p95 基线记录                                                                                                                                               | 监控快照                                          |
| S1     | 前端                           | yue S1 最小集 Rspress 页面与共用状态示例覆盖 100%；axe critical / serious = 0；对比度单测通过；Lighthouse 预算通过                                                                                                           | CI 报告                                         |
| S1     | 部署                           | default-deny + ResourceQuota + Secret 加密已启用；备份恢复演练 1 次成功                                                                                                                                         | 集群配置快照、演练记录                                   |
| S1     | 过渡态清零                        | §13.14 的 G1–G14 全部关闭或有合入期限的补丁包                                                                                                                                                                   | 缺口跟踪表                                         |
| **S2** | 跑通建应用 → 表单 / 列表 → 审批 → 授权    | E2E 100%；非开发人员按手册 ≤ 1 人日完成示范应用                                                                                                                                                                   | E2E 报告、可用性测试记录                                |
| S2     | 应用包双租户独立升级                   | §14.3 示例 3 通过；100 租户批量迁移单批失败可恢复、无半升级态残留                                                                                                                                                          | 用例产物                                          |
| S2     | 数据引擎安全                       | SQL 注入与标识符注入用例 0 漏洞；数据权限作用于读 / 改 / 删 / 导出 100% 路径；字段权限服务端为准                                                                                                                                      | 渗透报告                                          |
| S2     | 元数据                          | 版本可 diff、可回滚；Schema 校验器前后端共用，Agent 生成样例走同一校验                                                                                                                                                     | 用例产物                                          |
| S2     | 流程                           | 逆向语义属性测试通过；会签停滞 100% 产生 `node.stalled`；兜底不落全局 admin                                                                                                                                              | 测试报告                                          |
| S2     | 前端                           | 三大设计器 E2E；视觉回归基线建立；控件 manifest 全量导出                                                                                                                                                              | CI 报告                                         |
| **S3** | 租户自助选购、开通、配 IdP 与品牌          | E2E 100%；ISV 上架闸门 = TCK + 签名 + 审核                                                                                                                                                                | E2E 报告                                        |
| S3     | 不可信 UI 沙箱                    | 沙箱逃逸用例 0 漏洞；CSP 由清单生成 100%                                                                                                                                                                       | 渗透报告                                          |
| S3     | 计量与成本                        | Meter 与账单对账误差 ≤ 0.5%；预算告警可达                                                                                                                                                                      | 对账报告                                          |
| S3     | 高隔离档                         | 独立库租户开通 ≤ 30 分钟；备份独立                                                                                                                                                                             | 演练记录                                          |
| **S4** | Agent 发现、调用、编排、生成            | 每租户 MCP 门面通过 MCP 一致性测试；工具列表按权限过滤 100%；生成 `package` 经校验 + TCK 后落库                                                                                                                                 | 测试报告                                          |
| S4     | Agent 安全                     | `humanOnly` 服务端拒绝 100%；`act` 链进审计 100%；急停可在 ≤ 10 秒内生效                                                                                                                                            | 用例产物                                          |

***

## 14.6 「PR 合并 ≠ done」的验收纪律

每个任务（功能项、缺口修复、插件修订）必须有一份任务级验收记录，三段：

1. **任务级目标**：一句话写清交付什么、对哪个阶段退出准则有贡献（引用 §14.5 行号或用例 id）。
2. **验收 checkbox**：每条可观察、可判 PASS / FAIL，注明谁来测、在哪测。写法示例：
   - `- [ ] QA-S1-TENANT-001 在 ci 与 staging 均 PASS`
   - `- [ ] 平台插件 qian.files 从 r3 回切到 r2 后，t-a 的文件列表与 r2 快照一致（staging，由平台开发者 B 验证）`
   - `- [ ] 安全门槛写成「≤ 基线定值」：p95 ≤ 300 ms，而不是不可证的「性能良好」`
3. **证据四件事**：只回答 **PR、命令、环境、结果**——哪个 PR（含 digest）、跑了什么命令或用例、在哪个环境（ci / staging / prod-smoke）、结果产物在哪（链接到 `artifact_required` 的文件）。

规则：

- PR 合并只代表代码进入主干；任务状态只有在证据四件事齐全后才能标 done。
- 缺真实环境验收用 `blocked` 而不是 `done`；`blocked` 必须写明缺什么环境或数据。
- 安全类与多租户类用例不允许「本地通过」作为证据。
- 验收记录随 PR 合入仓库（`ACCEPTANCE.md` 或任务目录），由阶段退出评审汇总。
- 被推翻的验收条目用删除线保留原文再接新结论，不得静默删除。

一份合格的验收记录示例（节选）：

```
目标：qian.events 消费端改为「处理成功后 ack」，支撑 S1 退出准则「消息与事件」行。
验收：
- [x] QA-S1-EVENTS-002 在 ci PASS（2 副本）
- [x] QA-S1-EVENTS-002 在 staging PASS（3 副本，含杀副本与重投）
- [ ] 死信告警在 Alertmanager 可达 —— blocked：staging 尚未部署 Alertmanager（ADR-017 G13），期限 S1-迭代 6
证据：
- PR：#214（镜像 qian-platform@sha256:…）
- 命令：qian test run QA-S1-EVENTS-002 --env staging --replicas 3
- 环境：staging（K3s v1.35，RabbitMQ 4.x，PG 16）
- 结果：reports/2026-10-xx/QA-S1-EVENTS-002/{outbox-final.csv, consumer-side-effects.csv, dead-letter.json}
```

***

## 14.7 质量度量

度量只用于发现趋势与阻断退出，不用于考核个人。每个阶段退出评审展示下列指标的阶段内曲线：

| 指标           | 定义                                           | 阶段目标                                   |
| ------------ | -------------------------------------------- | -------------------------------------- |
| 安全用例通过率      | 跨租户、鉴权、密钥、审计类用例在 ci + staging 的通过率           | 100%，且 flaky = 0                       |
| 缺陷逃逸率        | staging 或生产发现的 P0 / P1 中，没有被任何自动化用例覆盖的比例     | S1 ≤ 30%，S2 ≤ 20%，S3 ≤ 10%；每个逃逸缺陷必须补用例 |
| Flaky 率      | 夜间与 PR 级用例中需要重试才通过的比例                        | ≤ 2%；安全用例为 0                           |
| 契约破坏次数       | 被 C11 拦截的 OpenAPI / proto / AsyncAPI 破坏性变更次数 | 趋势下降；定稿契约为 0                           |
| 生成文档与配置树漂移次数 | 被 C18（文档新鲜度）与 C20（`dump-config`）拦截的次数        | 趋势下降；发布级为 0                            |
| TCK 首次通过率    | 插件修订首次运行 TCK 即全绿的比例                          | S1 ≥ 60%，S2 ≥ 80%（衡量 SDK 与模板质量）        |
| 平均修复时长（MTTR） | P0 从发现到修复合入的中位时长                             | P0 ≤ 24 小时                             |
| 过渡态关闭进度      | §13.14 缺口已关闭数 / 总数                           | S1 退出时 100%                            |
| 门禁耗时         | PR 级门禁中位耗时                                   | ≤ 20 分钟                                |

## 14.8 缺陷与安全事件分级

| 级别    | 定义                                | 响应                                      |
| ----- | --------------------------------- | --------------------------------------- |
| P0 安全 | 跨租户数据可读 / 可写；令牌或密钥泄露；审计链断裂；鉴权缺省放行 | 立即止血（急停相关能力或租户），24 小时内修复并补用例，事后复盘入本章用例库 |
| P1    | 数据丢失或重复副作用；会话无法吊销；插件升级导致半升级态      | 下一发布前修复；阶段退出阻断                          |
| P2    | 功能缺陷不涉及隔离与安全                      | 常规排期                                    |
| P3    | 体验与文案                             | 常规排期                                    |

任何 P0 / P1 必须新增至少一条机器可执行用例覆盖该路径，并写入 §14.3 用例库。

***

## 14.9 待确认

1. 覆盖率阈值（`Qian.Runtime` 与 `qian.authz` ≥ 85%、其余 ≥ 70%）为本章建议值，需研发负责人确认。
2. S1「7 天 crash ≤ 3%」「trace ≥ 95%」借鉴了团队既有阶段准则的量级，需结合 Qian 的 staging 规模校准。
3. 外部渗透测试的频次（每阶段一次）与供应商预算需产品负责人确认（ADR-001 风险 1 要求显式列安全预算）。
4. 契约 lint 对「`x-status: draft` 期允许破坏性修订」的放行窗口长度需与 iClaw 对齐（ADR-016）。
5. 品牌字样闸门（C5）的具体词表由本章维护但不写入正文，需确认存放位置（建议 CI 配置而非文档）。
6. 示例 3 要求「回切后数据不丢」，前提是收缩步骤默认延后执行；与 ADR-011 的 Schema Migrator 收缩策略需核对。
7. TCK 三套的归属与基准宿主（运行时语义套件以 `minimal` profile 为基准、独立模式套件以 `standalone` 为载体）需研发负责人确认；`dump-config` 基线快照（C20）的存放位置与更新纪律待定（建议随 profile 文件同目录入库）。
