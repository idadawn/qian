# §12 能力边界

> 状态：草案 v0（2026-10-06；组件模型 v2 修订中）。依据：`docs/decisions/key-decisions-v0.md`（D1–D18 与补充裁决，2026-10-06 全部获认可；D20 组件模型 v2 为同日方向修正新增，ADR-020 Proposed，其边界条目见 12.2 的 C 块与 12.9）。本章只回答「某个阶段结束时，平台承诺做到什么、长期不做什么」；功能细节在各功能规格章，安全规则在 §13，验收方法在 §14。
>
> 阅读约定：
> - 阶段口径见 ADR-018：S1 技术底座 → S2 低代码构建 → S3 面向客户 → S4 AI 原生 Agent 平台。
> - 「明确支持」每条写成**可判定的能力陈述**：阶段退出评审时能对它回答「是 / 否」，并能指出证据（§14 的用例、门禁或演示）。写不成这种句子的条目不进本章。
> - 「过渡态（2026-10）」标注的是 Kun 当前缺口造成的临时状态，不是产品承诺；过渡态的消除是 S1 退出条件之一（ADR-001）。
> - 被借鉴的商业低代码产品一律称「参考平台」，本章只引用其做法作为正反例，不引入其代码。

---

## 12.1 边界的三种语气

| 语气 | 含义 | 变更方式 |
|---|---|---|
| 明确支持（按阶段） | 该阶段退出前必须可验收；写作「能……」「必须……」 | 只能经阶段评审调整阶段归属，不能悄悄删除 |
| 安全底线（S1 起生效） | 原本可以放到后期的安全与多租户设计，被提前到 S1，因为后补代价不可接受 | 不可降级；新增需 ADR |
| 明确不支持 | 分「长期承诺不做」与「本期不做、可重议」两档，每条附理由 | 长期承诺的改变必须新开 ADR 并在本章记录被推翻的原文 |

---

## 12.2 S1 技术底座：明确支持

S1 退出标准（ADR-018；ADR-020 §6 新增末句）：开发人员用代码在 Qian 上写出一个多租户应用，登录/SSO、鉴权、数据存储、文件上传、配置、网关、事件、审计全部由平台提供；一个官方 module 插件能独立出修订、逐租户迁移、回切；在空白项目里 `qian add identity authz files` 后 30 分钟内跑通登录、权限检查与上传。

**租户与隔离（ADR-004、ADR-003）**
1. 运营人员能在运营后台创建租户、选择隔离策略（默认共享库 + `tenant_id` 行过滤 + PostgreSQL RLS 双保险）、设置有效期与用户配额、指定单个租户管理员；租户号与隔离策略创建后不可修改。
2. 租户具备最小状态机（待激活 / 正常 / 禁用 / 到期宽限），每次状态迁移发出事件并联动该租户会话吊销。
3. 租户上下文只来自已校验令牌中的 `tenant_id`；HTTP、消息消费、Saga、Outbox 发布器、定时作业五类入口都能正确恢复租户上下文；解析不到即拒绝。
4. 租户对插件 / 应用的开通是白名单（租户 → 插件 → 版本）；「未启用」在网关路由、SDK 入口、前端外壳三处同时被拦。
5. 平台表按 schema-per-service 划分；缓存键与存储路径带租户维度；在线 DbContext 必须继承多租户基类（CI 检查）。
6. 可选：schema-per-tenant 策略经真库集成测试验证（不作为 S1 退出条件）。

**身份与 SSO（ADR-005）**
7. `qian.identity`（OpenIddict）是平台唯一 issuer，提供 OIDC 授权码 + PKCE、refresh 轮换、client_credentials、后通道登出、JWKS 轮换；每环境一个规范 issuer。
8. 账号模型为 Person（全局自然人）+ Membership（租户成员）；一个人命中多个租户时先签 5 分钟选择令牌再换正式令牌；切换租户即重新签发令牌。
9. 登录方式：账号密码（Argon2、策略化锁定 / 延时、图形验证码按策略开关）、短信验证码（依赖 S1 必做的短信渠道）、TOTP 二次因子（Q18：S1 做）。
10. 上游连接器 SPI 可用，并交付两个连接器：通用 OIDC、企业微信（扫码与工作台免登）。第三方身份绑定需显式确认，同租户内唯一。
11. 会话中心：服务端会话表 + 用户 → 会话索引、强制下线、单端 / 并发上限（默认 5）、空闲与绝对超时、在线用户列表、登录审计；改密 / 停用 / 锁定 / 租户冻结 / 权限变更即时失效。
12. 密码策略（强度、锁定、初始密码强制修改）与 CSRF 防护上线即生效。
13. 平台与官方插件以内置 client 接入；外部系统只能用应用级客户端凭据（client_credentials），不存在用户级 AppSecret 与「公有」免鉴权接口（补充裁决 Q35）。

**鉴权（ADR-006）**
14. `Kun.Authorization` 端口（`IPermissionChecker` Check / BatchCheck / ListAllowedActions、`IDataPermissionFilter`、`IFieldPolicy`）与 `qian.authz` 内置实现可用；权限点由插件清单声明，自动登记为可授权项与 OAuth scope 目录。
15. 租户域 RBAC：内置角色（平台超管、租户 Owner / Admin / 开发者 / 使用者）+ 自定义角色；角色可授予用户、组织、岗位、用户组；插件自带默认角色模板。
16. 每个 API 端点声明所需权限点并在服务端强制校验，缺省拒绝；gRPC 一元与流式调用同样受检。
17. 权限不进令牌；令牌携带 `identity_revision`（网关只转发此 claim），权限变更经推送失效后 ≤ 3 秒内对新请求全部生效（R7）。
18. 每次判定可解释（命中规则 / 缺少权限）并入审计；平台超管旁路在策略中显式声明并审计。
19. 数据权限的契约（Filter AST、系统变量目录、「同角色 AND / 跨角色 OR / 任一全部数据则放开」）在 S1 定稿；编译器与字段权限在 S2 交付。

**数据、文件、配置（ADR-004、ADR-007、ADR-011）**
20. `qian.data`：PostgreSQL 16 为唯一一等数据库；RLS 拦截器与模型缓存键在装配时强制挂载（不再 opt-in）；基准数据类型与 PG 方言、数据连接（凭证只存引用）、数据接口 SQL 契约、值翻译、引用登记可用。
21. `qian.files`：文件上传（分片 + MD5 秒传）、存储路径策略、预览票据由内核签发并服务端校验；文件不以匿名 URL 直出。
22. `qian.config`：Apollo 作为平台 / 服务级非敏感配置权威，自研 .NET provider（长轮询、启动校验、失败保留上一份有效值并告警）；插件配置以 JSON Schema 声明，敏感项只能填 secretRef；租户级配置由 `qian.tenancy` 承载、不进 Apollo。
23. Filter AST JSON Schema 与「运算符 × 字段类型」矩阵定稿，供数据过滤、高级查询、数据权限、启用规则、流程条件五处共用。
24. 可发布元数据框架（草稿 / 发布 / 归档 + 当前生效指针 + 不可变快照）、导入导出框架（dry-run、逐行结果、异常导出）、编号规则 API、字典、i18n 资源机制可用。

**网关、事件、审计、可观测（ADR-008、ADR-009、ADR-010）**
25. 三层网关就位：Traefik（北向入口，不做鉴权）→ `qian.gateway`（强制认证、路由级限流、可信代理、私钥热轮换、Cookie 会话换 S2S、按 cluster 的 audience）→ Higress（只管模型与 MCP 流量）。
26. RabbitMQ 在 K3s 上运行；Outbox 不丢消息、消费端至少一次（处理成功后标记）、Redis 幂等、多副本锁默认开启、退避重试；事件信封字段 `version / event_id / subject / occurred_at / tenant_id / payload`；事件目录（稳定 ID + JSON Schema）是能力目录的一部分。
27. 审计只有一套：命令级 `AuditEntry` 哈希链持久化到 PostgreSQL；实体级 `AuditLog` 与业务同事务写入并经 Outbox 发事件；`actor_type` 含 `agent`，记录 `on_behalf_of`、`capability_id`、`contract_version`、来源通道；`IAuditReader` 可查。
28. 每个服务提供 `/healthz`、`/readyz`；traceparent 与 baggage（含 `tenant_id`）贯通 HTTP、gRPC、消息；计量带租户 / 插件 / 主体类型维度。

**插件机制、SDK、授权 v0（ADR-002、ADR-003、ADR-014）**
29. （清单在 v2 为唯一一份 `.qian-plugin/plugin.json`，文件名待 ADR-020 §7 拍板，见 C 块）`qian-plugin.yaml` 清单 v1 Schema + 校验器 + 源生成器：宿主不执行插件代码即可生成路由、权限点、菜单、Apollo namespace 骨架、OpenAPI 聚合与 NetworkPolicy 片段；CI 比对清单与代码，漂移即失败。
30. （租户级 Enabled ⇄ Disabled 经 ADR-020 改为租户 scope patch，见 C3；注册表与生命周期控制器为普通插件 `qian.registry` / `qian.lifecycle`）插件注册表与两层生命周期（平台级 Draft → … → Retired；租户级 Available → Entitled → Provisioning → Enabled ⇄ Disabled → Upgrading / Uninstalled → Purged）可用；租户级钩子逐租户、幂等、可重试、结果入库。
31. （「18 个」与「预装并按租户开关」经 ADR-020 修订，见 C2 / C3）18 个系统插件全部按 `tier: system` 清单交付，预装并按租户开关；其中任一 module 插件能独立出修订、逐租户迁移、回切到旧修订。
32. 制品按 digest 引用、全部签名、未签名直接安装失败；插件包作为 OCI artifact 推 Harbor。
33. SDK：源生成器 + CLI + TCK；TCK 覆盖清单校验、契约兼容性、生命周期钩子幂等性、跨租户越权探测、性能基线。
34. 授权 v0：`qian.entitlement` 存「是否买了」，Apollo 开关存「是否灰度开」，两者分开；bundle 包格式 v0、安装 / 升级引擎、id 重映射表定稿。
35. 能力目录（命令 / 查询 / 事件 / 实体四类注册表 + JSON Schema）经鉴权、按权限过滤后可查询；它不是公开 OpenAPI。
36. 所有写操作走带 JSON Schema 的命令，命令元数据含 `sideEffect`、`idempotencyKey`、`dryRun`；`inputSchema` 中不出现租户字段。

**组件模型 v2（ADR-020，Proposed；对上一块第 29–31、34 条的修订，条目编号 C 以别于 B / T / N）**
- C1. 组件库只依赖 `Qian.Runtime`（.NET）/ `@qian/runtime`（TS），不依赖 `qian.registry` / `qian.lifecycle` 等插件；CI 断言依赖图（取代 ADR-019 的「不依赖 `Qian.Kernel`」断言）。
- C2. 可部署单元 = profile：S1 随发行版交付 `platform` / `gateway` / `identity` / `standalone` / `minimal`；原 18 个系统插件成为 `platform` profile 的 base 组合包内容（规范术语「平台插件」，R30），各自按 seam 三角色拆包，数量由组合包定义而不是决策；`qian --profile platform --dump-config` 能打印整棵插件树，打印出的任何条目都能被 patch 替换。原可部署单元 ↔ profile / 组合包的定稿映射见 ROADMAP S1 核心交付与 prd/README 命名约定（R31：`qian-shell` 不是 profile，为 `platform` 的 `shell` 组合包；`qian-model-gateway-adapter` 为 `model-gateway` 组合包，默认挂 `platform`、可选独立 profile）。
- C3. 租户启用一个插件 = 在租户 scope 挂载其配置条目（patch，存数据库、与文件 patch 同格式同校验器）；`qian.entitlement` 的授权状态机只管「买没买」；二者分开存、分开审计（§11.1.6「授权与开关分开可见」不变）。
- C4. 声明式表面（skills / agents / commands / hooks / MCP、元数据、patch）S1 支持运行中重载；代码插件 S1 不承诺热装卸，走滚动重启（collectible `AssemblyLoadContext` 为 S2 可选，ADR-020 §7 第 5 条）。
- C5. 第三方代码不进共享进程保持不变（N1 / N2），映射到 `policy.tier` 与 `policy.sandbox`；`private / community` 组件只允许声明式表面与沙箱形态。
- C6. 一键安装与目录市场：`qian add / remove / list / doctor / dump-config / init --template` 可用；官方市场仓库与 `.qian/marketplace.json` 就绪；空白项目 `qian add identity authz files` 后 30 分钟内跑通登录、权限检查与上传。
- C7. skill / agent / command / hook / MCP 五种表面的解析器与校验器 S1 交付；模型可见的一切注册在 `ctx.Tools` 等注册表并受 scope 过滤；配置目录、模块图、事件图、seam 图由代码生成并设 CI 新鲜度门禁。

**前端 yue 与外壳（ADR-013；详见专题 spec《yue 组件库》）**
37. `@yue/tokens` 三层令牌、明暗两态、WCAG AA 对比度单测；`@yue/core` 约 30 个基础组件；`@yue/shell` 单一侧栏布局外壳（后端菜单 → 路由、`menuTypeRegistry`、租户切换器、兜底页）；DataTable 基础版、SchemaForm 基础版、Upload、组织 / 用户 / 角色选择器、认证页族可用。
38. 外壳以 Module Federation 2 运行时加载官方插件 UI，带版本号的插槽注册表；共享依赖 singleton。
39. 元数据契约草案（FormSchema / ListSchema，含 `device`、`auth` 字段）、控件注册表契约、SSE 事件 JSON Schema、权限码目录格式发布；分层依赖 lint 进 CI。

**部署（ADR-017）**
40. Qian 运行在独立命名空间 `qian-<env>`，专属 PostgreSQL / Redis；业务命名空间带 default-deny NetworkPolicy 与 ResourceQuota；Argo CD app-of-apps + Kustomize，按 digest 写回，迁移 Job 先行；K3s 开启 Secret 静态加密。
41. 生产与 iClaw 共用集群但独立节点池（Q16）；多租户拓扑默认共享，高隔离与私有化为可选档。

**模型服务网关（ADR-015，S1 可选）**
42. GPUStack 以 OpenAI 兼容上游注册进 Higress；Provider Adapter 完成模型发现、健康与生命周期；`model-capabilities` 契约填写；接入范围先 LLM + Embedding（Q17）。

---

## 12.3 S2 低代码构建：明确支持

S2 退出标准：跑通「建应用 → 建表单与列表 → 走审批 → 授权给租户用户使用」；同一应用包在两个租户分别安装、各自配置、独立升级互不影响。

**元数据与动态数据（ADR-011）**
1. 元数据（实体 / 字段 / 视图 / 表单 / 流程定义）以版本化聚合存储，草稿 / 发布双版本 + 不可变快照，可校验、可 diff、可回滚到任意版本。
2. `qian.dynamic-data`：每个实体一张物理主表（系统列 `tenant_id`、软删、审计列、乐观锁），多值 / 子表用 JSON 列或关联表；统一 Filter AST → 参数化 SQL + 标识符白名单；强制注入租户与软删谓词；不走 EF 模型。
3. Records API（列表 / 详情 / 新增 / 修改 / 删除 / 批量 / 导入导出 / 发起流程 / 关联查询）在**唯一入口**做「资源 + 动作 + 行级」三段式鉴权，PC、移动、API、流程回调、外链、Agent 六类通道共用。
4. Schema Migrator：元数据 diff → 迁移计划 → 审核 → 逐租户分批执行 → 记录；「扩展 → 迁移数据 → 收缩」三步，不承诺自动回滚；不出现「改表 = 删表重建」。
5. 泛型命令 CreateRecord / UpdateRecord / DeleteRecord / QueryRecords 注册一次、按元数据分派；通用信封事件 `qian.record.changed.v1`；服务端 before / after Hook。
6. Field Type Catalog 与控件注册表是单一事实源；值翻译用按需批量 Resolver，不做全量缓存。
7. 数据接口 SQL 文本模式开放，但作为高级权限：只读数据库账号、超时与行数上限、参数化、审计（Q32）。

**表单、列表、发布（ADR-011、ADR-013）**
8. 表单设计器（三栏、单一 Schema + `device` 可见性、结构化校验错误 + 定位）与表单运行时（编辑 / 只读 / 单元格三种呈现由注册表提供）交付 S2 控件集：基础输入、选择、日期、上传、组织类、子表、下拉树与弹窗选择、关联表单、公式、系统控件、六种布局容器。
9. 联动三层：声明式动作 → 可序列化表达式 DSL → 服务端 Hook；前端 QuickJS 沙箱为可选兜底；不提供自由 JS 按钮（补充裁决 Q18）。
10. 列表设计器与运行时：「一份语义 + 端覆盖」，普通表格 / 左树右表 / 树形表格必做，分组 / 卡片 / 行内编辑可选；查询字段派生、固定按钮、导入导出、审批按钮矩阵、权限四开关产出可授权项。
11. 功能发布拆成编译 → 迁移 → 切流 → 挂载的可重放幂等步骤并记发布日志；功能状态机 未发布 → 已发布 → 已修改 → 已发布；运行时只读当前生效版本。
12. 功能编码采用「租户.应用.功能」命名空间化稳定编码 + id 重映射表；一个功能只归属一个应用，其他应用经「跨应用引用」挂菜单（Q20）。
13. S2 只交付 PC 响应式渲染；Schema 与控件 manifest 预留端维度与显式降级（Q13）。
14. 打印：模板、版本、数据绑定、浏览器打印 S2 必做；打印设计器 S2 后期；服务端 PDF 不在 S2。
15. 可选（P2）：视图（只读数据源形态）、配置路径型门户（固定模板 + 配置）、报表子集（列表 + 分组汇总 + 导出 XLSX）；S2 应用的「可用首页」由应用默认首页（菜单 + 功能列表）满足，不依赖门户（R16）。

**权限深化（ADR-006）**
16. 数据权限编译器：条件树 + 系统变量编译为 EF / SQL 谓词在本地执行，与 RLS 叠加，作用于动态表的全部读写路径（不只列表）。
17. 字段权限：读投影或脱敏、写白名单，服务端为准；授权界面含菜单 / 按钮 / 列 / 字段 / 数据五维；创建者自动成为应用 Owner。
18. 责任岗位 / 责任人 / 直属主管 / 上级链解析契约可用（流程审批人依赖）。

**流程（ADR-012）**
19. 自研 .NET 审批内核最小集：一套 JSON 定义 + 节点 JSON Schema + `:validate`；版本三态后端强制只读；发起 / 审批 / 结束节点、排他 / 包容 / 并行网关、抄送；审批人四种来源 + 三档兜底（节点异常处理人 → 租户管理员角色 → 「无法提交」结构化错误）；或签、会签（人数 / 比例，两阈值不可达时发 `node.stalled` 转人工）、依次；动作 同意 / 拒绝 / 退回 / 转审 / 暂存 / 撤回 / 终止 / 指派；字段权限后端强制；工作台四列表 + 详情；通知六状态；领域事件。
20. Q14 升为必做：子流程、限时 / 超时 / 催办；加签 / 减签与候选人保持可选。
21. 审批设计器为纵向节点树（DOM 自绘）；流程层用「启用 / 停用」而非「上架 / 下架」。
22. `GET /workitems/{id}/actions` 返回带 `inputSchema / preconditions / humanOnly` 的动作列表，`POST` 带 `idempotencyKey`；流转记录只追加、`actor` 区分本人 / Agent / 系统。

**插件形态扩展（ADR-002、ADR-014）**
23. `package` 元数据插件可安装、逐租户批量迁移、`template` / `managed` 两种分发（默认 managed，Q10）；升级同意规则与三方比较冲突清单可用。
24. Operation / Task 统一长任务与人工步骤状态机（流程待办 = `input_required`）。
25. 字段敏感度标签（public / internal / pii / finance / secret）进入元数据，查询与导出据此脱敏。
26. 出网代理与 SSRF 基线扩展到低代码连接器与第三方出网点（数据接口 API 执行器、群机器人 Webhook、流程外部节点、第三方 `connector`），私网与云元数据地址默认阻断；平台自有出网点已在 S1 覆盖（B10，R10）。
27. 可选：Wasm `function` 运行时（公式与规则先用 JSON AST 解释器；ABI 在 S2 中期拍板）；`connector` 运行时；开发者租户与远程调试。

**集成、消息、调度（ADR-008）**
28. 钉钉、飞书连接器 S2 必做（P0），微信网站扫码连接器 S2 可选（P1，R4）；数据建模、接口设计器、短信通道 UI、群机器人、监控 UI、任务管理 UI、文件预览提供者可替换（首选 .NET 方案，Q34）、模型目录与按用途路由。
29. 自动化（任务流程）S2 可选，S3 必做；集合语义显式声明 `cardinality: single | each | batch`；破坏性动作声明 `destructive` 并提供试运行。

**前端 yue（ADR-013）**
30. DataTable 进阶（树表、列权限、行内编辑、复杂表头）、ImportWizard / ExportDialog、SuperQuery、ConditionBuilder、CodeEditor / Cron、PosSelect / GroupSelect、PopupSelect 族、RelationForm、授权树族；设计器基础设施（dnd-kit 封装、schema 驱动属性面板、撤销重做、校验定位）；表单 / 列表 / 审批流三大设计器；校验器与设计器共用；视觉回归进 CI。

---

## 12.4 S3 面向客户：明确支持

S3 退出标准：租户能从应用市场选购并开通应用，自助配置 IdP 与品牌。

1. **市场（ADR-014）**：私有目录型市场（目录 / 详情 / 安装向导 / 试用 / 升级中心 / 审核台）→ ISV 入驻、席位、Plan / Seat / Meter / Trial、评分；上架闸门 = TCK 全绿 + 签名 + 权限清单展示；工具描述哈希钉死；发布者撤销与黑名单自动卸载。
2. **ISV 可上架的形态**只有 package / function / connector / 受限 ui；第三方 `service`（任意代码容器）不上架（Q9）。
3. **租户自助（ADR-005）**：自助申请 → 审核 → 开通；多管理员与邀请；租户自助配 IdP（每租户多连接、按邮箱域 HRD、域名验证、「仅企业 IdP 登录」服务端强制）；SAML / CAS / LDAP 连接器（先代理 IdP 后原生）；Passkey；SCIM Server 与国内目录同步；应用安装即建 client；自助注销与数据导出。
4. **租户生命周期完整状态机**：待激活 → 试用 | 正常 → 宽限 → 只读 → 冻结 → 注销（数据销毁）；套餐 = 预定义白名单 + 配额；品牌与自定义登录域（只换登录页宿主，不改 issuer）。
5. **高隔离档**：独立库（DatabasePerTenant）作为付费选项；高隔离租户独立命名空间 + 独立 PG / Redis 由 ApplicationSet 生成；私有化客户整套 overlay 复制；不承诺隔离策略在线迁移。
6. **权限高级模式（可选、可开关）**：组织角色 / 岗位角色、逐级递减授权、身份（权限分区）、互斥 / 基数 / 先决约束；OpenFGA 插件处理资源实例共享与市场授权；`Explain` 工具。
7. **不可信 UI 沙箱**：`partner / private / community` 的 ui 走 Worker 远程渲染或 iframe；CSP 由清单生成。
8. **门户与报表**：门户设计器与运行时、表格报表（实体、版本、数据集、XLSX 导出）、通知抽屉、面向客户工作台；开放 API；分享链接与外链能力令牌（功能 + 动作 + 版本 + 过滤 + 有效期）。
9. **流程 S3**：拒绝后继续流转、归档、签收、委托 / 代理、撤销申请、协办、超时自动处置、决策流程（调用式）、自由流、批量审批；流程模板与决策表节点包作为市场商品；BPMN 只作导出器。
10. **计量与成本**：Plan / Seat / Meter；Higress token 限流与缓存；租户 / 应用预算与告警。
11. **动态路由**：`qian.gateway` 按租户 × 已安装应用动态路由；接线 Application 生成器。
12. 可选：大屏（只消费内核数据接口，不复用参考平台大屏源码）、服务端 HTML → PDF、多布局与偏好抽屉、H5 / PWA 工作台与审批、小程序与 App 以壳嵌 H5。

---

## 12.5 S4 AI 原生 Agent 平台：明确支持

S4 退出标准：Agent 能发现、调用、编排乃至生成平台能力与应用。本期（S1–S3）只做防返工设计，不交付 S4 功能。

1. 每租户一个 MCP 逻辑门面（Higress 托管），工具定义由插件清单 `capabilities` 机械生成，暴露经 `agent.expose` 策划，按调用者权限过滤工具列表。
2. Agent 主体（有归属人、可吊销）与委托链：RFC 8693 令牌交换带 `act`；有效权限 = 委托人权限 ∩ 授予 scope；`Check / ListAllowedActions / Explain` 以工具形式暴露。
3. Agent 生成应用 = 生成 `package`，经校验 + TCK + 差异审阅 + 人工确认后落库；代码只落 `function` 沙箱；记录 `generatedBy / promptHash`。
4. 与 iClaw 运行时经 MCP / HTTP 对接（ADR-016）；Qian 不写 Agent loop、沙箱、记忆、技能引擎；沙箱对接 CubeSandbox。
5. 元数据生成闭环：AI 建表 / 建流程只产出「设计中」草稿并走同一校验器；元数据操作 API 化并暴露状态机。
6. `@yue/agent`（ToolCallCard、ThinkingTimeline、Composer 等，只消费 S1 定义的 SSE 事件契约）；shadcn MCP Server；市场兼任私有 MCP registry。
7. 可选：A2A 端点与急停、MCP Apps 视图、WebMCP。

---

## 12.6 安全底线（从后期提前到 S1）

下列设计原本在参考平台或多数低代码产品中属于「上线后再补」，Qian 把它们**全部提前到 S1 生效**。理由只有一个：多租户隔离与可信计算基的缺陷无法事后修补——数据一旦串读、审计一旦断链、令牌一旦被当作权限载体，后续所有插件与应用都会建立在错误假设上。细则见 §13。

| # | 底线 | 原本阶段 | 提前理由 |
|---|---|---|---|
| B1 | 租户上下文只来自已校验令牌，fail-closed；生产禁用请求头兜底与 `default` 回退；非 HTTP 入口显式回灌 | Kun 现状为 opt-in | 跨租户串读是最高风险（ADR-004） |
| B2 | 权限不进令牌；令牌只含身份、`tenant_id`、`membership_id`、scope、`identity_revision` | 常见做法是把权限塞进 JWT | 权限变更即时失效与审计都依赖它（ADR-006） |
| B3 | API 层强制鉴权，缺省拒绝；前端裁剪只是体验层 | 参考平台恒 `true` | 鉴权分散到控制器后无法收口 |
| B4 | 所有写操作走带 JSON Schema 的命令，含 `sideEffect / idempotencyKey / dryRun` | S4 Agent 需要 | 事后补「后门写入」的权限与审计几乎不可能 |
| B5 | `inputSchema` 禁止租户字段；契约 lint 强制 | S4 | 跨租户越权的典型入口 |
| B6 | 统一主体模型 user / service / agent 与委托链字段 | S4 | 每个服务后补「这是不是 Agent 调的」 |
| B7 | 审计防篡改且含主体链、能力 ID、契约版本、输入哈希、来源通道 | S3/S4 | 无法回答「哪个 Agent 以谁的名义做了什么」（ADR-009） |
| B8 | 插件强制验签、按 digest 引用、反向权限安装即审批、升级新增即重批 | S3 市场 | 供应链信任链条必须从第一个系统插件开始 |
| B9 | 密钥只经 secretRef；Apollo / 清单 / 事件 / 日志不出现明文 | — | 一旦有明文路径，清理成本随插件数线性增长（ADR-007） |
| B10 | default-deny NetworkPolicy、ResourceQuota、K3s Secret 加密、出网 SSRF 基线对平台自有出网点（OIDC 发现、企业微信、SMTP、短信、Apollo、对象存储）生效；低代码连接器与第三方出网点 S2 | S2/S3 | 多租户上线前的集群治理，现网为零（ADR-017） |
| B11 | 前端三硬线：不 `eval` 执行租户脚本；令牌不进 URL / iframe；不在前端持有加密密钥或持久化明文令牌 | — | 参考平台脚本可拿令牌调任意接口（ADR-013） |
| B12 | 结构化、可自纠正的错误（错误码、字段、允许值）；事件契约化（稳定 ID + JSON Schema） | S4 | 接口一旦被消费，错误形状与事件形状就难以更改 |
| B13 | 能力目录经鉴权、按权限过滤，不等于公开 OpenAPI | S4 | 避免「发现即泄露」 |
| B14 | 跨租户渗透测试样例、至少一次消费、租户回灌集成测试进 CI | — | Kun 缺口在多副本 + 多租户下必然触发（§14） |
| B15 | gRPC 流式调用与一元调用同受鉴权与租户解析 | Kun 现状绕过 | 流式接口是数据导出与推送的主路径 |
| B16 | 可解释、可预检的权限判定 API（`Explain` 数据结构 S1 定，UI 可后置） | S3 | 排障与 Agent 盲调 403 的共同解 |

---

## 12.7 明确不支持

### 12.7.1 长期承诺不做

| # | 不做 | 理由 | 依据 |
|---|---|---|---|
| N1 | 为第三方代码与可 AOT 的边缘 profile（`gateway` 等）做运行时热插 DLL | Native AOT 不支持程序集动态加载；不可信代码无法安全载入受信进程；需要动态性的第三方能力走 `package` / `function` / 进程外 `service`。官方代码插件在 `platform` profile（非 AOT）的热装卸保持 S2 可选、默认进程重启（ADR-020 D20.7 / D20.11）。已裁决（R27）：本条由原「任何运行时热插 DLL」收窄为现表述 | ADR-002、ADR-020 |
| N2 | 第三方代码进入任何共享进程 | 信任等级（`policy.tier`）决定允许的 `runtime.form` 与 `policy.sandbox`：`partner / private / community` 只允许声明式表面、`package`、`function`、受限 `ui`，以及为某个 seam 提供 Provider 的组件（原 connector：`provides[]` 写服务键 + `policy.egress` 声明出网目标；其 `runtime.form` 不得为 `module`，按实现取进程外 `service` 或由平台连接器运行时托管）；`service` 即便允许也必须独立工作负载身份（R28 口径，字段细节以 specs/component-model.md 为准） | ADR-002、ADR-020 |
| N3 | 参考平台式匿名外链（读草稿、不校验开关 / 密码 / 条件、无有效期） | 匿名上下文只能访问「匿名可用」能力；外链一律经网关匿名路由 + 服务端签发的能力令牌（功能 + 动作 + 版本 + 过滤 + 有效期，S3） | ADR-010、§13 |
| N4 | 进程共享的服务端 JS 引擎（全局单例 V8 / 任意脚本） | 无锁、无超时、无沙箱、可拼 SQL 到租户库；用 JSON AST 表达式解释器 + Wasm `function` 替代 | ADR-002、ADR-012 |
| N5 | 自建 Agent 推理引擎、沙箱、记忆、技能引擎 | 复用 iClaw 运行时；Qian 只做能力提供方与业务主数据权威 | ADR-016 |
| N6 | 自研模型代理层（绕过 Higress） | 并入团队 MaaS / Higress 基础设施；Qian 只做 Provider Adapter 与契约 | ADR-015 |
| N7 | Dapr sidecar 作为默认依赖 | 三节点 K3s 每 Pod 一个 sidecar 代价高；与 Kun EventBus / Outbox、S2S、YARP 大面积重叠；只借鉴其组件声明方式 | ADR-002、ADR-017 |
| N8 | bpmn-js 作为流程画布 | 许可证要求水印始终可见；审批流用 DOM 节点树或 React Flow；BPMN 只在客户强制要求时做导出器，不作内部模型 | ADR-012、ADR-013 |
| N9 | Nacos 作为配置中心 | Apollo 是硬性要求；Nacos 收窄为 AI 资产 / MCP 注册投影，不再承担配置职责 | ADR-007 |
| N10 | 引入开源低代码 / AI 平台（NocoBase、Dify、Grafana、n8n 等）的代码 | 许可证分别禁止用于低代码 SaaS / PaaS、禁止未授权多租户、AGPL、Sustainable Use；只借鉴设计 | ADR-002 |
| N11 | 代码生成作为低代码主轨 | 双轨导致运行时与模板行为漂移；若保留只能是调用运行时 SDK 的薄壳，不早于 S2 后期（Q11） | ADR-011 |
| N12 | 参考平台式「外部程序集目录」启动期加载 | 一次性、不可卸载、不可热插；参考平台自己也没用 | ADR-002 |
| N13 | 接口体 AES 加密中间件 | 本质是混淆，密钥是前端常量；传输安全只靠 TLS | §13 |
| N14 | 全局 admin 作为审批人兜底 | 兜底链 = 节点异常处理人 → 租户管理员角色 → 「无法提交」结构化错误（Q25）；跨租户主体不得出现在业务流转中 | ADR-012 |
| N15 | 用户级 AppSecret 与「公有」免鉴权开放接口 | 统一为网关应用级客户端凭据（Q35） | ADR-010 |
| N16 | 自定义按钮任意 JS、`eval` / `new Function` 执行租户脚本 | 多租户安全与 Agent 不可发现；联动三层策略替代 | ADR-013 |
| N17 | 脚本型定时任务（任意代码执行） | 调度只管 Cron 与回调契约；任务处理器由插件注册，带参数 Schema 与幂等 | ADR-008 |
| N18 | 两套设计体系长期混用（如 antd 与 yue 并存） | 参考平台双轨反例；Base UI 验证门不过则整体切 Radix，不混用（Q12） | ADR-013 |
| N19 | 内置 IM 聊天、自研服务器监控、请求日志落库 + 一键清空 | 非平台主线；多副本下单机指标无意义；日志进可观测栈并按保留策略归档 | ADR-003 |
| N20 | 前端锁屏作为安全措施 | 密码前端比对仅 UI 遮罩 | ADR-013 |
| N21 | 租户层级多级隔离下沉到 RLS | 按需以 `project_id` / 组织树在 `qian.authz` 解决 | ADR-004 |
| N22 | 行级数据过滤交给远程 ReBAC 引擎 | 远程引擎有截止时间与条数上限，不能当行过滤器；OpenFGA 只处理资源实例共享 | ADR-006 |
| N23 | 复用参考平台大屏源码 | 许可证高风险；大屏若做只消费内核数据接口 | ADR-014 |

### 12.7.2 本期不做（可重议，变更需 ADR）

| # | 不做（本期） | 理由 | 重议条件 |
|---|---|---|---|
| T1 | MySQL / 国产数据库作为业务库 | Kun 的 RLS、schema 供给、PgBouncer 方案全部 PG 专属；MySQL 无 RLS 等价物、无任何引用与测试（Q5） | 出现需 RLS 等价能力的客户合同且愿意承担隔离降级 |
| T2 | per-tenant namespace 作为默认拓扑 | 三节点集群资源与运维面不可承受；高隔离档按需生成 | 集群规模与运维自动化成熟 |
| T3 | 第三方 `runtime.form: service`（任意代码容器）上架市场 | 审核与运维投入量级不可控（Q9）；专属部署场景的 `policy.tier: private` service 不在此列（R28 口径） | ISV 生态与审核能力到位 |
| T4 | 租户自带业务库连接（低代码业务库） | 与 Schema Migrator、RLS、备份边界冲突 | 高隔离档客户明确需求 |
| T5 | 每租户独立 issuer / 自定义登录域改 issuer | S1 单 issuer；自定义域只换登录页宿主（Q4） | 政企白标刚需 |
| T6 | 隔离策略在线迁移 | 复杂度与风险高；创建时一次性决定 | 作为付费迁移服务评估 |
| T7 | 小程序原生渲染（S1–S3）、原生 App | 同一 React 渲染器端适配；小程序与 App 以壳嵌 H5 | 客户面移动需求明确 |
| T8 | 服务端 PDF（S2）、跨库数据同步、在途流程实例迁移新版本 | 成本未评估 / 需求不明 | S3 评估 |
| T9 | 自由画布流程设计器（S2） | 纵向节点树对 Agent 生成最友好；React Flow S3 可选 | S3 |
| T10 | 商用密码（SM2 / SM3）签名算法 | 不做假设，需产品负责人确认（Q18） | 等保 / 商密要求明确 |

---

## 12.8 本期不规划的 AI 功能

本期（S1–S3）AI 范围**只有模型服务网关**（ADR-015）：GPUStack 作为 OpenAI 兼容上游注册进 Higress、Provider Adapter（模型发现 / 健康 / 生命周期）、`model-capabilities` 契约、S2 模型目录与按用途路由；租户 / 配额 / 计量在平台侧，Higress 对租户无感；接入范围先 LLM + Embedding，ASR / TTS / Rerank 待拍板。

下列 AI 功能**不在本期范围**，但其前置契约（§12.6 的 B4–B7、B12、B13，以及元数据版本化与校验器）必须在 S1–S2 做对，否则 S4 返工：

| 不做（本期） | 归属阶段 | S1–S3 必须预留的契约 |
|---|---|---|
| AI 建表 / AI 生成字段 | S4 | 元数据 JSON Schema + 校验器；生成物只落草稿 |
| AI 建流程 | S4 | 节点 JSON Schema、`:validate`、设计器「草稿导入」 |
| 智能体 / Agent 编排、A2A | S4（经 iClaw） | Operation / Task、能力目录、委托链字段 |
| 知识库 / RAG、提示词管理、MCP 管理台 | S4（AI 资产控制面归 iClaw） | 注册表 → MCP 映射约定 |
| OCR 控件库（证照识别回填） | S4 | 控件 manifest 预留 `category: ocr`；HTTP 契约由独立插件暴露 |
| AI 助手悬浮窗、划词翻译、`@yue/agent` 组件 | S4 | SSE 事件 JSON Schema（S1 定义） |
| Agent 作审批人解析器 / 外部审批者 / 自动化动作 | S4 | `IAssigneeResolver`、外部任务回调契约、`humanOnly` 标记 |
| Agent 生成 `package` 应用 | S4 | bundle 格式 v0、TCK、差异审阅 |
| AI 生成内容标识、模型路由按数据驻留 | S4 | 审计含 `createdBy.kind=agent`；模型目录含能力标签 |
| 接口即工具、模板 `validate / render / explain` | S4 | 能力注册表为唯一目录，UI / OpenAPI / MCP 三种消费同源 |

---

## 12.9 待确认

1. 已裁决（R2）：Wasm `function` 运行时统一为 S2 可选（P2），ADR-018 与决策清单 D18 已同步去掉其核心交付地位；本章 §12.3 第 27 条不变。
2. 已裁决（R4）：S1 连接器只有通用 OIDC 与企业微信（扫码与工作台免登），钉钉 / 飞书 S2 P0，微信网站扫码 S2 P1，ISV 第三方应用模式 S3；本章 §12.2 第 10 条与 §12.3 第 28 条与之一致。
3. TOTP：决策 Q18「S1 做」，S 域基线列 S1 可选；本章按决策写为 S1 必做（§12.2 第 9 条）。
4. 已裁决（R10）：出网 SSRF 基线分两步——平台自有出网点（OIDC 发现、企业微信、SMTP、短信、Apollo、对象存储）S1 必做（B10），低代码连接器与第三方出网点 S2（§12.3 第 26 条）。
5. 已裁决（R16）：配置路径型门户与报表子集保持 S2 可选（P2）；S2 验收演示的「可用首页」由应用默认首页（菜单 + 功能列表）满足，不依赖门户（§12.3 第 15 条）。
6. 模型服务网关底座接线列为 S1 可选（ADR-018 待拍板项），本章按可选写入 §12.2 第 42 条。
7. 本章对其他章节的引用（功能规格章编号）待 PRD 索引定稿后回填。
8. ADR-020 §7 第 1 条：TypeScript 侧（yue 外壳、`qian` CLI）直接采用现成的 MIT 运行时还是自研同语义的 `@qian/runtime`；.NET 侧确定自研。C1 的 `@qian/runtime` 依赖规则以此为准。
9. ADR-020 §7 第 2 条：清单文件名与格式（`.qian-plugin/plugin.json` 还是沿用 `qian-plugin.yaml`，默认前者）；本章第 29 条与 C 块按默认写。
10. ADR-020 §7 第 3 条：`qian` CLI 的实现语言（Node + `dotnet qian` 薄包装，还是 .NET global tool 为主，默认前者）；影响 C6 的 `npx` 一键体验。
11. ADR-020 §7 第 4 条：官方市场仓库的名称与可见性（默认同组织独立仓库，S1 私有、S3 评估公开）；影响 C6 与 §12.4。
12. 已裁决（R27）：N1 收窄为「不为第三方代码与可 AOT 的边缘 profile 做运行时热插 DLL」；官方代码插件在 `platform` profile 的热装卸保持 S2 可选、默认进程重启（ADR-020 D20.7 / D20.11），C4 按「S1 不承诺」写。ADR-020 §7 第 5 条中「是否进入 S2」据此回答为「S2 可选」，collectible `AssemblyLoadContext` 与 Kun AOT 约束的兼容验证归属仍待拍板。
13. ADR-020 §7 第 6 条：许可证与公开 feed（沿 ADR-019 §7）；影响 §12.2 第 33 条 SDK 的分发渠道。
14. 已裁决（R28）：`connector` 不是 `runtime.form`，而是「为某个 seam 提供 Provider 的组件」——清单上体现为 `provides[]` 写出服务键 + `policy.egress` 声明出网目标，`runtime.form` 按其实现取 `module` 或 `service`；`bundle` = 组合包（有 `config/qian.patch.yml`，可携带多个组件），应用 = 组合包 + marketplace 条目。T3 / N2 与 §03 3.4 已按 `runtime.form` / `policy.tier` / `provides` 口径改写；字段细节以 specs/component-model.md 为准。
