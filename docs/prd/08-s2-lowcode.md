# §08 S2 低代码构建

> 本章粒度：范围清楚、关键模型与契约定型，比 §07（S1 技术底座）粗一级，但功能清单必须完整。决策引用写 `ADR-0xx`（ADR-011 元数据与动态数据、ADR-012 流程、ADR-013 前端与 yue、ADR-014 市场、ADR-006 鉴权、ADR-002 插件模型、ADR-018 阶段），基线与参考平台只作依据不作规范。决策清单主表的待拍板问题写 `Q1–Q18`，补充裁决（基线汇编编号）写 `补充裁决 Qxx`，两套编号互不混用。被借鉴的商业低代码产品一律称「参考平台」。
>
> 优先级口径：**P0** = S2 退出标准关键路径，不做则 S2 不能验收；**P1** = S2 必做但不在关键路径，可在 S2 内后置；**P2** = S2 可选，可滑到 S3。阶段归属以 ADR-018 与补充裁决为准，与基线冲突处在 8.16 注明。
>
> 功能项固定小节：设计理念 / 范围与边界 / 技术落点 / 插件化定位 / Agent 契约 / 验收标准。验收标准写可观察行为与测法，供 TCK 与验收脚本直接引用。

### 8.0 目标与退出标准

#### 8.0.1 S2 的一句话目标

把 S1 交付给开发人员的技术底座，变成**不写代码的搭建者**也能用的低代码构建能力：元数据驱动运行时为主轨（ADR-011），搭建产物是可打包、可安装到多个租户、可独立升级的应用包（ADR-014）。

#### 8.0.2 退出标准（ADR-018）

S2 结束时，以下两条必须在预发环境被第三方按脚本复现：

1. **主链路**：租户管理员或开发者在平台上「建应用 → 建表单与列表 → 走审批 → 授权给租户用户使用」，全程不写代码；租户普通用户登录后能在被授权的菜单下新增记录、发起审批、审批人在工作台处理、发起人看到结果。
2. **多租户独立性**：同一应用包（bundle v1）在两个租户分别安装，各自修改表单布局与审批人配置，其中一个租户升级到应用新版本后，另一个租户的行为与数据不受任何影响；两个租户的升级都可回滚。

退出标准之外、但 S2 必须「做对否则 S4 返工」的设计见 8.14。

#### 8.0.3 S2 新增可部署单元

S1 的 5 个可部署单元（`qian-gateway`、`qian-identity`、`qian-platform`、`qian-model-gateway-adapter`、`qian-shell`，见 §07）不变。S2 新增 3 个必做单元与 1 个可选单元，仍遵守 ADR-017 的 K3s/ArgoCD/Kustomize 约定与「每服务 `/healthz` `/readyz`、default-deny NetworkPolicy、ResourceQuota」。

| 单元 | 形态 | 承载的插件 | 为什么单独成单元 | 级别 |
|---|---|---|---|---|
| `qian-lowcode` | service，JIT 宿主（Kun 允许的「非 AOT 孤岛」） | `qian.metadata`、`qian.dynamic-data`、`qian.schema-migrator`、`qian.expression`、`qian.forms`（服务端部分）、`qian.lists`（服务端部分）、`qian.widgets`（Field Type Provider 部分）、`qian.data-model`、`qian.data-api`、`qian.print`（服务端数据组装） | 动态 SQL 生成、反射式 JSON、运行时元数据编译都不能进 `qian-platform` 的 AOT 边界；低代码查询是热点，需要独立水平扩展与故障域 | P0 |
| `qian-workflow` | service | `qian.workflow` 全部（定义、引擎、运行态、解析器、通知绑定、SLA、子流程） | 审批域体量大（基线估 12–15k 行）、计时器与令牌推进有自己的伸缩曲线；与表单运行时解耦便于单独回滚 | P0 |
| `qian-file-preview` | service（第三方预览进程 + Qian 薄插件） | `qian.file-preview` 的提供者实现 | 预览后端是可替换的外部进程，Helm 子 chart 钉版本随插件安装（补充裁决 Q34） | P1 |
| `qian-wasm-runtime` | service（可选） | `function` 型插件宿主 | ABI（Extism 或 WIT 组件）在 S2 中期拍板，之前公式与规则先用 JSON AST 解释器（补充裁决「Wasm function 运行时」） | P2 |

前端不新增可部署单元：`qian-shell` 以 Module Federation 2 远程模块形式新增 `@qian/designer`（三大设计器）、`@qian/lowcode-runtime`（表单/列表渲染器与控件集）、`@qian/workflow-ui`（工作台与审批详情），静态站随 `qian-shell` 发布。

#### 8.0.4 S2 新增与升级的插件

命名以 ADR-003 的 `qian.` 前缀与决策清单用名为准（决策写 `qian.dynamic-data`，P 基线附录 B 中的 `qian.data-engine` 视为同一插件的别名）。全部 `tier: system`，形态标注见 ADR-002。

| 插件 | 形态 | S2 职责一句话 | 级别 | 详见 |
|---|---|---|---|---|
| `qian.metadata` | module@`qian-lowcode` | 元数据注册表：每类元数据的 JSON Schema、语义校验、草稿/发布/版本/回滚、patch/diff、包层/租户层分层与 manageability、template/managed、发布编排、字段敏感度 | P0 | 8.1、8.7、8.10 |
| `qian.dynamic-data` | module@`qian-lowcode` | 动态数据引擎：Records API、Filter AST → SQL、唯一入口三段式鉴权、混合存储、系统列、乐观锁、服务端 Hook、信封事件、导入导出接入、批量 Resolver | P0 | 8.2 |
| `qian.schema-migrator` | module@`qian-lowcode` | 元数据 diff → 迁移计划 → 审核 → 逐租户分批执行 → 记录；表名规范校验；dry-run | P0 | 8.3 |
| `qian.widgets` | ui + module | 控件注册表（单一事实源）、S2 控件集、每控件配对的 Field Type Provider | P0 | 8.4 |
| `qian.expression` | module@`qian-lowcode` | 表达式/公式 JSON AST 解释器、函数注册表、运算符矩阵、系统变量目录、试算端点、执行预算 | P0 | 8.5、8.8 |
| `qian.forms` | module + ui | 表单引擎：四段 Schema、校验器（前后端共用）、三态渲染器、子表、联动三层策略、声明式规则、引用关系图、从表生成 | P0 | 8.5 |
| `qian.lists` | module + ui | 列表引擎：布局提供者、查询/列表字段、动作编排、标准操作集、权限四开关产出可授权项、一份语义 + 端覆盖 | P0 | 8.6 |
| `qian.designer` | ui（MF2 远程） | 表单、列表、审批流三大设计器与设计器基础设施（嵌套拖拽、撤销重做、Schema 驱动属性面板、校验定位） | P0 | 8.5、8.6、8.8、8.13 |
| `qian.workflow` | service@`qian-workflow` | 自研 .NET 轻量审批内核 + 审批域最小集（含子流程、超时催办） | P0 | 8.8 |
| `qian.authz`（S2 增量） | module@`qian-platform` | 数据权限编译器、字段权限、授权界面（含字段/数据维度）、创建者自动 Owner、数据权限方案自动生成 | P0 | 8.9 |
| `qian.navigation`（S2 增量；S1 为 `qian.kernel` 内「菜单由清单贡献 + URN 目录 + 用户偏好」的逻辑名，S2 允许拆出，R26） | module + ui | 应用菜单管理界面、多端维度、菜单类型扩展点接入低代码功能、跨应用引用挂菜单 | P0 | 8.10 |
| `qian.identity` 组织目录子模块（S2 增量；§07 7.4.3 把组织目录最小集放在 `qian.identity`，若独立为 `qian.directory` 本章随之改名） | module | 责任岗位/责任人/主管解析契约与「第 N 级责任人」查询；导入向导接入 | P0 | 8.8 |
| `qian.operations` | module@`qian-platform` | Operation/Task 资源：长任务与人工步骤资源化，`input_required`，对齐 MCP Tasks | P0 | 8.12 |
| `qian.entitlement`（S2 增量） | module | 应用包安装到租户时的权益检查、升级同意规则（新增反向权限需重批） | P1 | 8.10 |
| `qian.print` | module + ui | 打印模板实体、版本、组件 Schema、数据绑定、挂载到列表按钮与流程节点、浏览器打印；打印设计器 S2 后期 | P1 | 8.11 |
| `qian.file-preview` | module + service | 预览票据、提供者接口、格式清单、来源白名单 | P1 | 8.11 |
| `qian.data-model` | module + ui | 数据建模器：可视化建表/字段/主键、浏览数据、控件映射与基准字段 UI、模型导入导出序列化器 | P1 | 8.11 |
| `qian.data-api` | module + ui | 接口设计器：SQL（高级权限）/API/静态三类执行器、参数与返回字段、分页、值翻译、执行日志；S1 契约见 §07 | P1 | 8.11 |
| `qian.notify`（S2 增量） | module + ui | 短信通道 UI、群机器人（出站 Webhook）、第三方模板同步、监控 UI | P1 | 8.11 |
| `qian.scheduler`（S2 增量） | module + ui | 数据接口型任务、任务管理 UI、作业定义与触发器状态拆分 | P1 | 8.11 |
| `qian.realtime`（S2 增量） | module | Redis/MQ 背板替代进程内连接池；在线状态；事件类型目录 | P1 | 8.11 |
| `qian.identity.connector.dingtalk` / `.feishu` / `.wechat-web` | connector | 钉钉、飞书扫码与工作台免登（S2 P0）；微信网站扫码（S2 P1，R4） | P0 / P1 | §07 连接器 SPI |
| `qian.model-gateway`（S2 增量） | service adapter | 模型目录与按用途路由的**契约**（逻辑模型名、能力标签、用途槽位），为 §10.5 预留；租户可见在 S3 | P2 | §10.5 |
| `qian.wasm-runtime` | service | `function` 型 Wasm 运行时 | P2 | 8.5、8.15 |
| `qian.automation` | module@`qian-workflow` | 任务流程（事件/定时/WebHook 触发 + 数据四动作 + 接口 + 消息 + 发起审批）| P2（S3 必做，补充裁决 Q28） | 8.8 |
| `qian.portal`（配置路径型） / `qian.report`（子集） | module + ui | 固定模板 + 配置的应用首页；列表 + 分组汇总 + 导出 XLSX | P2（补充裁决 Q29） | 8.11 |
| `qian.codegen` | 开发者工具 | 调用运行时 SDK 的薄壳脚手架导出 | P2，不早于 S2 后期（Q11） | 8.15 |
| `qian.script-runtime` | ui | QuickJS 前端沙箱（白名单 API、超时、内存上限、不暴露令牌） | P2（补充裁决 Q18） | 8.5 |

同期交付、但不属于低代码域的 S2 增量（归 §07 各插件，本章只列不展开）：`qian.gateway` 多 Pod 共享限流计数与密钥热轮换（P 基线 B.2 标 S2 必做）；`qian.files` 分片上传与用途目录；`qian.identity` 个人中心扩展（我的组织/身份/偏好/消息接收）；`qian.i18n` 在线翻译管理（可选）；`qian.plugin-sdk` 开发者租户与远程调试通道（可选）。

#### 8.0.5 S2 的横切约束（从 S1 继承、在 S2 首次被低代码运行时大规模消费）

- 租户上下文只来自已校验令牌，`inputSchema` 禁止出现租户字段；动态数据引擎的每条 SQL 强制注入 `tenant_id` 谓词并叠加 PostgreSQL RLS（ADR-004、ADR-006）。
- 所有写操作走带 Schema 的命令，携带 `sideEffect / idempotencyKey / dryRun`，UI 也不例外（ADR-018「S4 防返工」清单第 1 条）。
- 结构化错误：`ProblemDetails` 扩展 + 错误码 + JSON Pointer 定位，设计器浮层与 Agent 消费同一份。
- 审计含主体链（user / service / agent）、`capability_id`、`contract_version`、来源通道（UI / API / MCP）（ADR-009）。
- 一份契约多处生成：控件注册表、Field Type Catalog、Filter AST、流程节点 Schema 既是设计器数据源，也是 `GET /registry/*` 的响应，也是 S4 工具描述的来源。

### 8.1 元数据注册表与版本模型（P0 · S2）

#### 设计理念

元数据是 Qian 的一等资产：版本化、可校验、可 diff、可回滚的**文档**，而不是散落在若干 JSON 文本列里的隐式契约（参考平台 77 个无 Schema 的 JSON 列是反例）。注册表是所有 `package` 型插件的地基（ADR-002、ADR-011），表单、列表、流程、打印、菜单、数据模型全部走同一套「草稿 → 不可变发布快照 → 当前生效指针」心智；S4 让 Agent 改元数据时，校验、预览、回滚必须已经存在（ADR-018 防返工第 12 条）。

#### 范围与边界

- ✅ **元数据类型注册**：每类元数据（`FormSchema`、`ListSchema`、`DataModel`、`FlowDefinition`、`PrintTemplate`、`MenuSet`、`RoleTemplate`、`App`、`CapabilityManifest`）在内核登记一份带 `$schema` 与 `schemaVersion` 的 JSON Schema 2020-12，加一个语义校验器（引用存在性、命名规范、依赖闭包）。保存时服务端校验，拒绝一切不过校验的写入。
- ✅ **三态 + 不可变快照**：保留参考平台「未发布 / 已发布 / 已修改」的用户心智，底层升级为每次发布一条不可变 `Release` 快照、版本号递增、变更摘要、发布人、发布日志；运行时除设计器预览外一律读当前生效版本（F 基线裁决 C-5）。
- ✅ **回滚到任意版本并可 diff**：两个版本间输出结构化 JSON Patch（RFC 6902）与人读摘要；回滚 = 把生效指针指回旧快照 + 触发 8.3 的前向兼容迁移评估，不做自动数据回滚。
- ✅ **patch 式写入**：设计器与 Agent 都以 JSON Patch 提交增量；每个 patch 记来源（人 / Agent / 导入）、时间、审批记录（S2 人用即「保存人」，S4 扩为审批链）。
- ✅ **依赖图与删除保护**：引用登记（表单引用数据接口、流程引用表单、菜单引用功能、打印引用表单）由内核统一查询；被引用对象不可删，提示解除路径。
- ✅ **分层与 manageability**：有效元数据 = 系统基础层 → 按依赖拓扑排序的包层 → 租户定制层；每个组件与属性带 `manageability: locked | developerEditable | tenantEditable | protected`，由 bundle 清单声明、平台强制；容器型组件（表单、列表、菜单）做结构化合并（以子元素稳定 ID 为键），其余上层优先。
- ✅ **template / managed 两种分发**（ADR-014，默认 `managed`，`template` 可选）：`template` 拷贝即拥有，升级只能同源导入（合并或覆盖）；`managed` 锁定命名空间、可升级、租户只能改 `tenantEditable` 项。
- ✅ **字段敏感度标签**：`dataClassification: public | internal | pii | finance | secret` 进入字段元数据，供 8.9 脱敏与导出策略消费。
- ✅ **元数据即能力**：实体 CRUD、表单提交、流程动作在发布时自动登记进能力目录（命令/查询/事件/实体四类），能力按租户安装状态可见。
- ❌ 用户个性化层（S3 可选）；三方合并冲突预检报告与升级前快照（S3 必做，见 §09）；在线多人协同编辑（本期不做，设计器用乐观锁 + 冲突提示）。
- ❌ 元数据存业务库：元数据用普通 EF 聚合存在平台库 `metadata` schema，按 ADR-004 schema-per-service；动态业务表才走 8.2。

#### 技术落点

- 核心对象：`MetadataType`（类型注册）、`Asset`（稳定 ID、命名空间化编码 `tenant.app.local`、类型、所属应用、层级、manageability）、`Draft`、`Release`（不可变快照、版本号、`compiledModelRef`）、`ActivePointer`、`Patch`（JSON Patch + 来源 + 审批）、`Reference`（依赖边）、`Layer`（系统 / 包 / 租户）。
- **编译产物（Compiled Model）**：发布时把 FormSchema/ListSchema/DataModel 编译成运行时直接消费的紧凑模型（字段投影、标识符白名单、规则表、权限资源清单），按「租户 + 版本指纹」缓存；8.2 的 SQL 生成只信任编译产物中的标识符。
- 编码规则：名称应用内唯一、编码全局唯一；未填编码时自动「前缀 + YYYYMMDD + 六位序列」（序号服务见 §07）；复制加「.副本 + 五位随机」且默认未发布；跨应用导入用命名空间化稳定编码 + id 重映射表（补充裁决 Q20）。
- 事件：`qian.metadata.released.v1`、`qian.metadata.rolledback.v1`、`qian.metadata.patched.v1`，信封对齐 ADR-008。

#### 插件化定位

`qian.metadata` 是 `tier: system` 的 module 插件，住在 `qian-lowcode`。内核只保留契约（类型注册接口、校验器接口、Release/Pointer 语义、引用登记）；各低代码插件通过 SPI 贡献自己的元数据类型与序列化器；`package` 插件安装 = 把包内元数据实例化为该租户的包层记录。

#### Agent 契约

- `GET /metadata/types` 返回每类元数据的 JSON Schema 与示例；`POST /metadata/{type}/{id}:validate` 返回结构化错误（错误码 + JSON Pointer + 允许值）。
- `POST /metadata/{type}/{id}/patches`（带 `idempotencyKey`、`dryRun`）→ 返回校验结果与预览 diff；`POST …:release` 与 `…:rollback` 均为带副作用等级 `write` 的命令。
- `GET /metadata/{type}/{id}/releases/{a}/diff/{b}` 返回 JSON Patch 与人读摘要，供 Agent 解释「为什么某字段不存在了」。
- 生成格式 = 导入格式：Agent 生成的元数据与导入包用同一 Schema、同一校验器。

#### 验收标准

- [ ] 任一类元数据提交不满足 Schema 或语义校验的内容，服务端返回 422，`errors[]` 含错误码与 JSON Pointer；设计器浮层能据此定位到控件。**测法**：TCK 用 20 个坏样例逐类打接口。
- [ ] 同一功能连续发布 3 次后，`releases` 列表有 3 条不可变记录；回滚到第 1 版后运行时读到第 1 版布局，第 2、3 版快照仍可查。**测法**：脚本发布 → 改字段 → 发布 → 回滚 → 比对 `GET /functions/{id}/compiled` 与快照。
- [ ] 两个版本 diff 输出的 JSON Patch 回放到旧版等于新版（幂等重放）。**测法**：属性测试 200 次随机编辑。
- [ ] `managed` 包中 `locked` 属性在租户层写入被拒绝并返回 `METADATA_LOCKED`；`tenantEditable` 属性写入成功并在包升级后保留。**测法**：两租户安装同一包，分别改一个 locked 与一个 tenantEditable 属性，升级后比对。
- [ ] 被流程引用的表单删除返回 `METADATA_REFERENCED` 并列出引用方。**测法**：接口测试。
- [ ] 发布后能力目录出现该功能的 `records.*` 能力条目，卸载后消失。**测法**：查 `GET /capabilities?app=…`。

### 8.2 动态数据引擎（P0 · S2）

#### 设计理念

元数据驱动运行时的核心是一组**住在非 AOT 孤岛里的 Kun 消费方模块**：不走 EF 模型，按实体编码读写 JSON 行，用统一 Filter AST 生成参数化 SQL，所有通道（PC、移动、API、流程回调、导入、Agent）共用一个入口并在入口做「资源 + 动作 + 行级」三段式鉴权（ADR-011、ADR-006）。参考平台 47 处拼 SQL、鉴权散落控制器、按 id 读改删不过数据权限、全量用户表进缓存，全部是反向约束。

#### 范围与边界

- ✅ **Records API**（每个已发布功能自动获得）：`list`（Filter AST + 排序 + 分页 + 关键词）、`get`、`create`、`update`（乐观锁）、`delete`（按策略逻辑/物理）、`batchDelete`、`batchSave`、`import`、`export`、`startFlow`（委托 8.8）、`relatedQuery`（关联查询，P2）。注册为少量泛型命令 `CreateRecord / UpdateRecord / DeleteRecord / QueryRecords`，内部按元数据分派，Pipeline 行为（校验/UoW/审计/追踪）照常复用。
- ✅ **统一 Filter AST**：`{logic, groups[{logic, conditions[{field, op, value | sysVar}]}]}`，13 个运算符（eq ne like notLike isNull notNull in notIn ge gt le lt between）× 字段类型矩阵机器可读；系统变量目录（currentUser 及下属/子孙、currentOrg 及子/子孙、currentPosition 及子/子孙、currentManagedOrgs、formId、keyword、flowId、taskId、launchUserId）。数据过滤、高级查询、数据权限、启用规则、流程条件五处共用（S1 定义，见 §07）。
- ✅ **唯一入口三段式鉴权**：资源（功能/菜单）→ 动作（标准操作或自定义动作）→ 行级（8.9 编译出的谓词）；字段权限在读投影与写白名单处执行；鉴权不依赖客户端传入的 `menuId / flowIds`，也不依赖设计者是否勾选「权限开关」。内部调用（流程回调、调度）走服务身份 + `on_behalf_of`。
- ✅ **混合存储模型**：每个实体一张物理主表（副表一对一、子表一对多各自成表，外键 `f_foreign_id`）；多值字段（多选、人员多选、文件列表）用原生 `jsonb` 列并建 GIN 索引，或在 Field Type Provider 声明 `multiValueStrategy: relationTable` 时走关联表。
- ✅ **系统列约定**：`id`、`tenant_id`（联合主键）、`created_at/by`、`updated_at/by`、`is_deleted/deleted_at/deleted_by`（按开关）、`row_version`（乐观锁，整型自增）、`flow_task_id/flow_id/flow_state`（主表、流程表单）、`org_snapshot_id/position_snapshot_id`（新建时快照）。列名沿用 Kun 约定，RLS 策略随建表写入。
- ✅ **乐观锁**：原子 `UPDATE … WHERE id=@id AND row_version=@v`，影响行数为 0 返回 `RECORD_CONFLICT`，不做「先查后改」。
- ✅ **记录生命周期**（F 基线 §3.3 升级）：新建 = 字段权限裁剪 → 必填 → 唯一/业务主键 → 系统字段填充（单据号、创建人、快照、租户、版本=0、雪花 id）→ 主副子表同事务写入 → `before` Hook → 提交 → `qian.record.changed.v1`；编辑 = 乐观锁 → 子表按 id 比对增删改（删除策略 cascade / restrict / keep）→ 字段级数据日志 → 事件；删除 = 有在途流程则拒绝 → 逻辑或物理删除 → 事件。
- ✅ **服务端 Hook**：`before.create/update/delete`（可拒绝、可改写载荷，同事务）与 `after.*`（事务提交后经 Outbox 异步）；Hook 实现只能是：声明式规则、已注册的平台能力（如调用数据接口、发起流程）、S2 可选的 `function` Wasm；不允许进程内任意脚本。
- ✅ **信封事件** `qian.record.changed.v1`：`{entityCode, recordId, op: created|updated|deleted|imported, tenantId, actor, before?, after?, changedFields[]}`，静态注册一次，Outbox 同事务投递（需要面向 `NpgsqlTransaction` 的 `IOutboxWriter` 实现）。
- ✅ **按需批量 Resolver**：字典、组织、用户、关联表单、数据集的 id→名称翻译按请求批量解析、租户维度分布式缓存，不做全量预热。
- ✅ **导入导出接入**：复用 §07 的导入导出引擎——模板派生（必填强制勾选、不可导入控件黑名单）、名称→存储值转义、预览 dry-run、错误报告与模板同构、仅新增/更新或新增（先判业务主键再判唯一）、流程表单导入后待提交；导出当前页/全部/当前选择 + 字段勾选，分批流式生成不先取全量。
- ✅ **数据日志**：字段级差异（新建/编辑/删除/导入都覆盖），走 ADR-009 实体级审计同事务写入。
- ✅ **唯一性与业务主键**：单字段唯一（空值不校验、主副表按库表查重且数据过滤隐藏的记录也参与、子表仅同主记录内）；业务主键多字段联合、导入同校验。
- ❌ 匿名外链填单/公开查询（S2 可选 → S3，用服务端签发的能力令牌重做；不照搬读草稿、不校验的实现）；租户自带业务库连接（待 ADR-004 拍板，本期不做）；MySQL/国产库方言（ADR-004）；跨库数据同步（本期不做）。

#### 技术落点

- `IDynamicRecordStore`（按 `entityCode` 读写 `JsonObject` 行）、`IQueryDsl → SqlBuilder`（参数化 + 标识符白名单，白名单只来自编译产物）、`IMetadataValidator(entityDef, payload)`（字段级错误码 `{entity}.{field}.{constraint}`）、`IRecordHookRegistry`、`IValueResolver`（Field Type Provider 提供）。
- 连接打开时设置 RLS GUC（Dapper 版租户拦截器），强制 `tenant_id = @tenant AND is_deleted = false`；复用 Kun 双 SQL 分页。
- 分页上限、导出行数上限、`in` 列表长度上限、查询超时为租户级配置（§07 配置 Schema）。
- 多副本：雪花 WorkerId 分配沿用 §07；单据序号原子取号沿用 §07 序号服务。

#### 插件化定位

`qian.dynamic-data`（module@`qian-lowcode`，`tier: system`）。扩展点：Field Type Provider（8.4）、Hook 实现（能力引用）、数据源提供者（静态/字典/数据集/组织树）。不在内核：内核只持有 Filter AST Schema、运算符矩阵、系统变量目录与鉴权判定端口。

#### Agent 契约

- 每个已发布功能自动产出：JSON Schema（字段、类型、枚举/字典、必填、唯一、关联、敏感度）+ OpenAPI 3.1（`records.*` 端点，查询参数 = Filter AST Schema）+ 语义描述（label、描述、示例）。
- `records.*` 的 `inputSchema` 不含租户字段；`create/update/delete` 带 `idempotencyKey` 与 `dryRun`；`sideEffect` 分别为 `write / write / destructive`，`delete` 默认 `policyHint: confirm`。
- 错误枚举化：`RECORD_CONFLICT`、`RECORD_REFERENCED_BY_FLOW`、`BUSINESS_KEY_DUPLICATE`、`SUBTABLE_ROW_LIMIT`、`FIELD_FORBIDDEN`、`ROW_SCOPE_DENIED`，附字段 JSON Pointer。
- `qian.record.changed.v1` 可被 Agent 订阅；Hook 可注册为「调用能力」，S4 时能力可以是 Agent 工具。

#### 验收标准

- [ ] 任意组合的 Filter AST 生成的 SQL 全部参数化，标识符不在编译产物白名单时返回 `FILTER_FIELD_UNKNOWN`。**测法**：模糊测试 10k 随机 AST + SQL 文本断言无字面量拼接。
- [ ] 以租户 A 的令牌访问租户 B 的记录 id：`get/update/delete` 均 404/403，且即使绕过应用层谓词，RLS 仍阻断。**测法**：跨租户越权探测（TCK 内置）+ 直接 SQL 旁路验证。
- [ ] 不带 `menuId` 的请求、移动端通道、流程回调通道对同一记录的读写结果与 PC 通道一致（都受行级与字段权限约束）。**测法**：同一用户四通道回归。
- [ ] 两客户端并发更新同一记录，后提交者收到 `RECORD_CONFLICT`，数据库 `row_version` 只递增一次。**测法**：并发测试 100 轮。
- [ ] 新建含 50 行子表的记录后，`qian.record.changed.v1` 恰好一条进入 Outbox 并被消费一次；消费端故障重启后不丢不重（至少一次 + 幂等）。**测法**：注入消费端崩溃。
- [ ] 10 万行主表、列表页含 5 个 id→名称字段时，一次 `list` 对翻译源的查询次数 ≤ 5（无 N+1）。**测法**：SQL 计数断言。
- [ ] 导入 1 万行，其中 100 行违反业务主键：返回与模板同构的错误报告，成功行可见，失败行可修正重导。**测法**：导入脚本。

### 8.3 Schema Migrator（P0 · S2）

#### 设计理念

参考平台「改表 = 删表重建、只允许空表、业务列不迁移」是本领域最大实现差距的来源。Qian 的迁移是正式管道：**元数据 diff → 迁移计划 → 审核 → 逐租户分批执行 → 记录**，遵守「扩展 → 迁移数据 → 收缩」且不承诺自动回滚（ADR-011、P 基线 §4.3）。

#### 范围与边界

- ✅ 自动建表（无表模式）：发布时按 DataModel 建主/副/子表，带系统列、RLS 策略、外键与索引；表名可自定义或默认「前缀 + 功能稳定编码」。
- ✅ 手动选表（有表模式）：从数据建模（8.11）已有表勾选主/子表，发布时校验主键存在、系统列齐备（缺失则由计划补齐）。
- ✅ 表名与字段名规范校验服务：字母开头、字母数字下划线、长度上限、非保留字、不重复；前端实时 + 后端发布时双层，错误带「去修改」定位。
- ✅ 迁移计划：`steps[{op: createTable|addColumn|alterColumn|dropColumn|addIndex|dropIndex|addRlsPolicy, table, column?, from?, to?, dataLoss: bool, lockLevel}]`，`tenantsAffected`，预估耗时；`dryRun` 只出计划不执行。
- ✅ 审核门：`dataLoss=true` 的步骤（删列、缩短度、类型不兼容）必须人工批准；默认策略是「先加新列、后台迁移数据、旧列标记弃用，N 天后收缩」。
- ✅ 逐租户分批执行：共享库 + RLS 下一次 DDL 即全租户生效；SchemaPerTenant / 独立库（S3）下循环所有租户 schema，批次大小、并发上限、失败重试、半升级状态可观测、人工介入点（继续/跳过/回退本批）。
- ✅ `CREATE INDEX CONCURRENTLY`、`ALTER TABLE … ADD COLUMN` 等低锁操作优先；高锁操作标记并建议维护窗口。
- ✅ `schema_change_log`：每步执行记录（租户、版本、SQL、耗时、结果、操作者），按「租户 × 版本」幂等——重放已执行步骤为 no-op。
- ✅ 方言子插件：S2 只有 PostgreSQL 16（ADR-004）；方言接口预留。
- ❌ 自动数据回滚；跨库事务；在线把共享库租户迁到独立库（S3 评估是否作服务）；MySQL/国产库方言。

#### 技术落点

`ISchemaMigrator.Plan(fromRelease, toDraft) → MigrationPlan`、`Approve`、`Apply(plan, tenantBatch)`、`Status`。DDL 经 K8s 迁移 Job 先行（ADR-017），应用发布的「切流」步骤等待迁移完成。字段类型与长度精度来自 8.4 的 Field Type Catalog。

#### 插件化定位

`qian.schema-migrator`（module@`qian-lowcode`，`tier: system`），被 `qian.metadata` 的发布编排调用（8.7），也被 `qian.data-model` 的建模器调用；对 `qian.kernel` 暴露「逐租户批量迁移」执行器，供 `package` 插件的租户级钩子复用。

#### Agent 契约

`POST /functions/{id}:publish?dryRun=true` 返回迁移计划与影响面；计划的 `dataLoss` 步骤带人话解释；`GET /migrations/{planId}` 暴露执行状态机。Agent 只能提交 dry-run 与请求审批，不能直接 `Apply` `dataLoss=true` 的计划（`policyHint: confirm`）。

#### 验收标准

- [ ] 表单新增控件 → 发布：目标表新增列，已有数据不变；删除控件 → 发布：计划含 `dropColumn(dataLoss=true)`，未审批不执行，旧数据仍可查。**测法**：发布脚本 + 表结构比对。
- [ ] 数字字段精度从 2 改为 4：计划为 `alterColumn` 无数据丢失，执行后原值精度保留。**测法**：数据比对。
- [ ] 1000 个租户（SchemaPerTenant 模拟）分 20 批迁移，第 7 批人为失败：状态显示 6 批完成、第 7 批失败、其余未开始；修复后「继续」只跑剩余批次。**测法**：故障注入。
- [ ] 同一迁移计划重复 `Apply` 两次，第二次全部 no-op，`schema_change_log` 不新增成功记录。**测法**：幂等重放。
- [ ] 非法表名（保留字、超长、重名）在设计器实时提示并在发布时被后端拒绝。**测法**：接口与 UI 双测。

### 8.4 Field Type Catalog 与控件注册表（P0 · S2）

#### 设计理念

控件目录是**数据而不是代码**：一份注册表同时是设计器左栏、`GET /registry/widgets` 响应、运行时三态渲染来源、Field Type Provider 的类型映射来源，也是 S4 Agent 的词典（参考平台把 23 个控件名硬编码进提示词是反例）。每个控件包 = `meta + Runtime + Designer 面板 + toSearchControl + toColumnRenderer + 服务端 Field Type Provider`，前后端同名同版本发布（ADR-013）。

#### 范围与边界

- ✅ **Field Type Catalog**（S1 契约，S2 全量）：条目 `{key, displayName, dbType, defaultLength, defaultPrecision, indexable, multiValueStrategy: json|relationTable|none, provider: {toStorage, fromStorage, toQuery, toDisplay, validate}}`；控件 → 字段类型映射只在此一处声明，无代码 switch 兜底；基准类型集（int/float/decimal/datetime/varchar/text/bigint/jsonb/bool/uuid）与 PostgreSQL 方言映射归 `qian.data`（§07）。
- ✅ **控件注册表** `registerWidget(type, {meta, Runtime, Designer, toSearchControl, toCellRenderer})`，`meta = {type, name, category, description(一句话用途与场景), props: JSONSchema(默认值与枚举), valueType, events, devices[], supports: {pc, mobile, subtable, listColumn, query, group, condition, linkage, import, export, inlineEdit, anonymous}, acceptsDataSources[], defaultOperators, authAware, i18nKeys, compatWith[]}`。能力矩阵由引擎与 Agent 机器判定，不写在文档里。
- ✅ **三态渲染一次声明**：`edit / readonly / cell`，不再出现独立的详情渲染器。
- ✅ **查询用控件**：日期区间、时间区间、关键词属于列表引擎的查询控件，不是表单控件。
- ✅ **控件分级表**（下表）；P0 控件集 = S2 退出标准主链路所需，P1 = S2 内交付的常用控件，P2 = S2 可选或 S3。
- ❌ OCR 控件库（9 种，S4，经模型服务网关）；签章/签名/定位（S3 可选，合规与移动场景）；H5 专属渲染器（补充裁决 Q13：S2 只做 PC 响应式，`devices[]` 与端覆盖预留）。

**控件分级表**

| 类别 | P0（S2 必做，关键路径） | P1（S2 必做） | P2（S2 可选 / S3） |
|---|---|---|---|
| 基础 · 输入 | 单行输入、多行输入、数字输入、开关 | — | — |
| 基础 · 选择 | 单选框组、多选框组、下拉选择 | 级联选择 | — |
| 基础 · 日期时间 | 日期选择、时间选择 | — | — |
| 基础 · 上传 | 文件上传、图片上传（策略对象归 `qian.files`） | — | — |
| 基础 · 杂项 | — | — | 颜色选择、评分、滑块、富文本（选型调研后，补充裁决 Q39） |
| 基础 · 展示 | 文本、提示 | 链接 | 按钮（优先挂声明式动作）、轮播图 |
| 基础 · 码类 | — | — | 二维码、条形码（不建列） |
| 高级 · 组织类 | 用户选择、组织选择 | 岗位选择、角色选择、分组选择 | — |
| 高级 · 主从 | 设计子表（表格布局、合计、条数限制、删除策略、底部按钮） | 子表平铺布局、「选择数据」批量回填 | — |
| 高级 · 数据源选择 | 下拉树形 | 弹窗选择 + 弹窗选择属性 | 下拉表格、下拉补全（`@keyword` 契约） |
| 高级 · 关联 | 关联表单 | 关联表单属性 | 关联查询（与页签管理合并设计） |
| 高级 · 计算 | 数字公式、日期公式（服务端复算） | — | — |
| 高级 · 其他 | — | — | 省市区域（行政区划数据包）、Iframe（URL 白名单 + CSP）；签章/签名/定位 S3 |
| 系统 | 创建人员、创建时间、修改人员、修改时间、所属组织、所属岗位、单据组件 | — | — |
| 布局 | 分组标题、分割线、栅格容器、卡片容器 | 折叠面板、标签面板 | 步骤条、表格容器（仅 PC） |
| 查询专用 | 日期区间、时间区间、关键词（列表引擎） | — | — |
| OCR | — | — | S4 全部 |

分级依据：F 基线附录 A 的 59 + 9 控件目录为基线口径；P0/P1 的划分以「退出标准脚本需要什么」为唯一标准（建请假单：输入/选择/日期/人员/子表/公式/系统列/布局；建列表：查询控件）。

#### 技术落点

- 控件包目录结构与 `qian plugin pack` 的 TCK 检查：meta 必须有 props Schema、三态实现、Field Type Provider 同名；`supports` 与实际实现一致性由 TCK 渲染探针验证。
- 注册表持久化为数据（含版本），设计器与 `GET /registry/widgets` 都读它；平台升级新增控件 = 发布新控件包，不改引擎。
- 第三方控件（S3，`partner/private` tier）只允许 `ui` 形态走 Worker 远程渲染白名单；服务端 Field Type Provider 只能由 `system/official` 提供，第三方控件复用已有 Field Type。

#### 插件化定位

`qian.widgets`（`ui + module`，`tier: system`）持有注册表与官方控件集；每个控件分组是一个子包（`widgets-input/choice/datetime/upload/display/org/subtable/datasource/relation/formula/system/layout`）。扩展点 `qian.form.field-type/v1` 面向第三方。

#### Agent 契约

- `GET /registry/widgets` 返回 type、用途、props Schema、能力矩阵、可接受数据源、默认运算符、示例；`GET /registry/field-types` 返回 Catalog。
- 控件 meta 的 `description` 面向 Agent 写成「何时使用/何时不用」；`compatWith` 与 `supports` 让 Agent 在生成前自检而非撞错。
- S4 的 `generateFormFromTable` 与 `insertFields` 工具只能产出注册表内的控件类型。

#### 验收标准

- [ ] 注册表中每个控件都能在设计器左栏出现、能被拖入画布、能以 edit/readonly/cell 三态渲染，且与 `GET /registry/widgets` 返回一致。**测法**：TCK 渲染探针遍历全部控件。
- [ ] 声明 `supports.query=false` 的控件不出现在查询字段候选；`supports.subtable=false` 的控件拖入子表被拒绝并提示。**测法**：UI 自动化。
- [ ] 数字控件精度 4 建表得到 `numeric(?,4)`；多选控件默认建 `jsonb` 列并有 GIN 索引；Catalog 改映射后新建表随之变化而旧表不变。**测法**：表结构断言。
- [ ] 新增一个官方控件包并发布，不重启 `qian-lowcode` 即可在设计器使用（控件包是 `ui` + Field Type 数据，不是进程内代码热插）。**测法**：发布控件包后刷新设计器。

### 8.5 表单引擎与设计器（P0 · S2）

#### 设计理念

一套表单引擎贯穿 S1 后台 CRUD 与 S2 低代码运行时（ADR-013），表单是四段 Schema 的文档：**Data**（字段与约束）、**UI**（布局与呈现）、**Rule**（声明式行为）、**Binding**（物理绑定与数据源）。设计器只是 Schema 的编辑器之一，Agent 是另一个；两者产物过同一个校验器。声明式优先于脚本（补充裁决 Q18）。

#### 范围与边界

- ✅ **四段 Schema**（F 基线附录 B.3 为轮廓，定稿于本章）：
  - `dataSchema`：`fields[{name, type: FieldType, label, required, unique, precision?, length?, enum?/dictionary?/dataset?, defaultValue, validations[], dataClassification}]`、`businessKey[]`、`subTables[{name, fields, maxRows, deletePolicy}]`。数据部分对齐 JSON Schema 2020-12。
  - `uiSchema`：`appearance{size, labelAlign, labelWidth, labelSuffix, gutter, popupType: center|drawer|fullscreen, style: default|official, className}`、`tree[WidgetNode{widgetType, fieldRef?, props, style, channels{pc, mobile: bool|override}, children?}]`、`detailTabs?`（P2）。
  - `ruleSchema`：`formRules[{scene: create|edit, action: disable|require|hide, fields}]`、`linkages[{when: FilterExpression, then: Action[]}]`、`formulas[{field, expr: AST, mode: display|store}]`、`buttons{confirm, cancel, print?, continueGroup, custom[≤5]}`、`hooks{onLoad?, beforeSubmit?, afterSubmit?}`（引用，可空）。
  - `bindingSchema`：`primaryKeyPolicy: snowflake|identity`、`concurrencyLock`、`logicalDelete`、`dataLog: bool`、`tables[{table, role: main|aux|sub, foreignKey?, fieldMap}]`、`dataSources[{id, kind: static|dictionary|dataset|org, params[], valueField, labelField, cache}]`。
- ✅ **纯表单 / 列表表单两种模式**，可互切；向导式三步「基础设置 → 表单设计 → 列表设计」，进入下一步有前置校验。
- ✅ **三栏设计器**：左控件库（分组）+ 大纲树 + 模板套件（P2）；中画布，PC/移动一键切换外观但绑定**同一份**控件树（一份 Schema + 端可见性/端覆盖，F 基线裁决 C-3）；右侧 Schema 驱动的属性面板（组件属性 / 组件样式 / 表单属性），面板由控件 `props` Schema 与 `visibleWhen` 自动生成。
- ✅ **撤销/重做覆盖属性变更**（参考平台只记控件增减）、复制/删除、清空、预览（PC 全屏，复用运行时渲染器，禁止落库）。
- ✅ **校验器前后端共用**：保存/下一步/发布时校验字段名规范、必填配置、绑定完整性、容器守则（空容器不可存、仅剩一个子面板不可删），浮层列出控件与原因并「去填写」定位；后端返回同构的结构化错误。
- ✅ **三态渲染器**：`schema → React 树`，edit / readonly / cell 由注册表提供；布局容器（栅格/卡片/折叠/标签/步骤/表格）；系统字段默认值；i18n 替换；按 8.9 字段权限裁剪；`formApi = {values, setValues, setVisible, setRequired, setDisabled, utils}`。
- ✅ **子表**：行内编辑、增删复制、批量删除、合计（单行/数字/存储型公式）、条数限制默认 1000、校验汇总、删除策略；主表引用子表公式先求和。
- ✅ **联动三层策略**：① 声明式动作（设值/显隐/必填/禁用/调用数据服务/打开表单/发起流程）→ ② 可序列化表达式 DSL（JSON AST，用于条件、公式、`ifShow`、`dynamicRules`）→ ③ QuickJS 沙箱脚本（P2 兜底：白名单 API、超时、内存上限、不暴露令牌）。优先级：脚本 > 表单规则 > 控件属性。
- ✅ **公式**：数字（精度、千位、大写金额、四种舍入、日期间隔）与日期（`+1y-1M+2d`，单位大小写敏感，源空则空，溢出进位）；`store` 型可被引用、入库并由服务端复算防篡改；`display` 型只前端。函数起步集 `SUM / SUBTRACT / PRODUCT / DIVIDE / COUNT` + 日期函数，目录机器可读。
- ✅ **数据源三选一**（静态 / 字典 / 数据集）面板被全部选择类复用；数据集必须设回显；参数来源「字段 / 自定义 / 系统变量 / 为空」。
- ✅ **表单间引用**：关联表单（当前应用或跨应用白名单内已发布表单、填充规则主副→主副、子→子、被引用方数据过滤生效）；引用关系图独立机器可读。
- ✅ **从数据表生成表单**（确定性生成，最适合先暴露给 Agent）；插入字段不覆盖已有。
- ✅ **表单按钮与自定义按钮**：确定/取消改名、打印（依赖 8.11）、「确定并继续」组；自定义按钮 ≤5，类型弹窗 / 接口 / 发起审批 走 8.6 的动作契约，自由 JS 按钮不做。
- ✅ **数据规则透传**：主键策略保存后不可改、乐观锁、逻辑删除、修改记录、业务主键四开关落到 bindingSchema，由 8.2 执行。
- ❌ 控件级/表单级自由 JS 事件（S2 声明式规则必做，脚本沙箱 P2）；OCR、签章、定位；页签管理（P2，与关联查询合并）；H5 渲染器（Q13）；AI 建表/生成字段（S4，S2 只保证 Schema 可生成可校验）。

#### 技术落点

- 前端：`@yue/form`（react-hook-form + Ajv）为底座，`@qian/lowcode-runtime` 的表单渲染器只做叶子与容器编排，不新建引擎；设计器基于 dnd-kit 嵌套拖拽，撤销栈以 JSON Patch 记录。
- 后端：`qian.forms` 在 `qian-lowcode` 提供校验器（与前端共用同一 Schema 包 `@qian/metadata-contracts` 生成 TS 与 C# 类型）、规则编译（声明式规则编译进 Compiled Model）、公式服务端复算、引用图维护、`generateFromTable`。
- 服务端 Hook 与前端联动的边界：校验与计算以服务端为准，前端只做体验。

#### 插件化定位

`qian.forms`（module + ui）+ `qian.designer`（ui）。扩展点：控件类型（8.4）、动作类型（8.6）、数据源提供者、公式函数包。模板套件、数据浏览器、脚本沙箱为可选子插件。

#### Agent 契约

- 工具最小集（S4 落地，S2 契约就位）：`listWidgets()`、`listDataSources()`、`generateFormFromTable(tableId, options)`、`insertFields(formId, fields[])`、`validateForm(schema)`、`previewForm(formId)`、`forms.references(formId)`。
- 每个 Schema 带 `$schema` 与版本；错误枚举化（`WIDGET_NOT_ALLOWED_IN_SUBTABLE`、`CONTAINER_EMPTY`、`FIELD_NAME_INVALID`、`DATASOURCE_ECHO_REQUIRED` 等）+ JSON Pointer。
- 脚本（若启用）标记为不透明并附结构化描述（用途、读写字段）。

#### 验收标准

- [ ] 用设计器搭出「请假单」（含人员选择、日期、数字公式、子表、折叠面板），保存 → 发布 → 运行时新增一条记录，服务端复算的 `store` 公式值与前端一致。**测法**：端到端脚本。
- [ ] 同一表单在 PC 与移动端预览时渲染同一份控件树，仅按 `channels` 显隐与覆盖；修改一处字段标签两端同步。**测法**：UI 自动化比对 Schema 与 DOM。
- [ ] 以 Agent 方式提交（不经设计器）一份与设计器产物等价的 FormSchema，通过校验并发布后行为一致；提交 10 个坏样例均被拒并带 JSON Pointer。**测法**：接口测试。
- [ ] 撤销 20 步属性变更与控件增删后 Schema 与初始一致。**测法**：属性测试。
- [ ] 声明式联动「当请假类型 = 病假 则附件必填」在新增与编辑场景、PC 与移动端、服务端校验三处一致生效。**测法**：四路回归。
- [ ] 关联表单引用一个已发布表单，被引用方「数据过滤」在选择弹窗生效；被引用表单删除被拒。**测法**：接口与 UI。

### 8.6 列表引擎与设计器（P0 · S2）

#### 设计理念

列表是「一份语义配置 + 端覆盖」（`base + overrides.mobile`），不是 PC/移动两份完整配置；布局、数据来源、动作、权限资源种类都是扩展点而非写死枚举。视图（数据集驱动的纯列表）归入列表引擎的「只读数据源」形态，不单独立项（补充裁决 Q17）。

#### 范围与边界

- ✅ **ListSchema**：`base{layout{type, params}, table{page, pageSize, sort[], resizable, ellipsis, summary[], complexHeaders[], tabs?, subTableStyle}, filter: FilterExpression, search[{field, matchType: like|eq|range, defaultValue|sysVar, keyword, multiple, hidden}], columns[{field, label, width, align, fixed, sortable, filterable, visible}], buttons{standard[], custom[ActionRef]}, permissionFlags{button, column, form, data}, superQuery, scripts?}`、`overrides{mobile?: Partial<base>}`。
- ✅ **布局提供者**：普通表格（P0）；左侧树 + 表格、编辑表格（行内）、分组表格、树形表格（P2，各自声明能力矩阵：是否支持分页/合计/复杂表头/子表/头部按钮）；卡片（S3）。树形表格不得以清空数据权限换取全量（参考平台反例）。
- ✅ **表格配置**：数据过滤（设计期固定条件，Filter AST，运算符随控件类型，PC/移动分开）、默认排序（多字段，仅主表）、列宽拖拽与溢出省略、分页（默认开）、合计（P1）、复杂表头（P2，仅 PC）、标签面板（P2）、子表样式（P2）。
- ✅ **查询字段**：显示/顺序/改名（可翻译）/隐藏；条件类型模糊/等于/范围；默认值支持系统变量；关键词查询 ≤3 字段；多选查询。
- ✅ **列表字段**：排序（仅主表、多列按点击先后）、冻结（仅 PC、主副表）、对齐、宽度、表头内联筛选（同字段 OR、跨字段 AND）。
- ✅ **固定按钮 + 启用规则**：新增/编辑/删除/详情/导出/导入/批量删除/批量打印/审批，可改名、可配启用规则（Filter AST）；CRUD 由 8.2 统一实现。
- ✅ **动作编排契约**（自定义按钮，列内/头部）：`Action{id, label, type: openForm|callService|startFlow|setField|toggleVisible|setRequired|script, input[{param, source: field|const|sysVar|null, value}], confirm?, refresh, position: row|toolbar, enableWhen?, failures[]}`；弹窗（打开另一表单并赋值）、接口（调用数据接口）、发起审批 为 P0，脚本为 P2。
- ✅ **权限四开关**：按钮/列表/表单/数据开关只决定「设计器产出哪些可授权项清单」，不决定是否鉴权；鉴权恒在 8.2 唯一入口（F 基线 §3.10「双重门」升级）。
- ✅ **运行期高级查询**（P2，同一 Filter AST）；查询方案保存（S3，按「菜单 + 用户」私有）；个人视图（S3）。
- ✅ **流程状态 → 操作矩阵**由 8.8 下发，列表按矩阵置灰并给原因（「流程已受理，无法删除」），列表引擎与 Agent 共用。
- ✅ **视图形态**：数据源为数据集（8.11 数据接口），参数来源「自定义 / 系统变量 / 为空 / 作为查询字段」，数据集自带分页则真分页；视图同样是资源，走统一鉴权（无免鉴权资源类型）。P2。
- ✅ **数据管理页**（设计器侧对已发布列表表单的数据后台，P2）。
- ❌ 两份完整的 PC/移动配置；表格/行/单元格自由脚本（P2 沙箱兜底）；卡片布局（S3）。

#### 技术落点

`@yue/data` DataTable（TanStack Table v9 + Virtual）进阶：树表、列权限、行内编辑、复杂表头为 S2 必做；`SuperQuery`、`ConditionBuilder`、`ImportWizard`、`ExportDialog` 为公共交互组件（8.13）。服务端 `qian.lists` 把 ListSchema 编译为查询计划（投影、过滤、排序、分页、合计），交 8.2 执行；动作执行器 `IActionExecutor` 按 `type` 分派到表单、数据接口、流程。

#### 插件化定位

`qian.lists`（module + ui）+ `qian.designer`。扩展点：`layouts.register`（布局提供者）、动作类型、数据源提供者、权限资源类型。

#### Agent 契约

`validateList(schema)`、`invokeAction(actionId, input)`（受同一 ACL 约束，动作自带 `inputSchema / preconditions / sideEffect / idempotent / failures[]`）；布局能力矩阵、运算符矩阵、流程状态矩阵以元数据接口发布；S4 可注册「调用 Agent」动作类型。

#### 验收标准

- [ ] 请假单列表配置查询字段（申请人、日期范围、状态）、列表字段、固定按钮与「发起审批」自定义按钮，发布后租户用户按权限只看到被授权的按钮与列，且直接调 `records.list` 也只拿到被授权列。**测法**：UI + API 双路。
- [ ] 移动端覆盖只改列表字段子集，PC 配置不受影响；移动端 `overrides` 为空时完整继承 PC。**测法**：Schema 与渲染比对。
- [ ] 启用规则「状态 ≠ 进行中 才可编辑」在 UI 置灰且服务端执行同一规则（直接调接口返回 `ACTION_PRECONDITION_FAILED`）。**测法**：接口测试。
- [ ] 树形表格布局对无数据权限用户返回空而非全量。**测法**：权限回归。
- [ ] 视图绑定一个自带分页的数据集，翻页请求透传分页参数且总数正确。**测法**：接口计数。

### 8.7 功能发布向导（P0 · S2）

#### 设计理念

发布不是一次「保存到线上表」的写操作，而是四个可重放、幂等的步骤：**编译 → 迁移 → 切流 → 挂载**，全程有发布日志与 dry-run；参考平台发布跨十几张表无事务、建表 DDL 不可回滚是反例（ADR-011）。

#### 范围与边界

- ✅ 步骤：① 编译（校验 Schema，生成 Compiled Model 与权限资源清单）→ ② 迁移（8.3 计划与执行，可能等待审批）→ ③ 切流（生效指针指向新 Release，运行时缓存按版本指纹失效）→ ④ 挂载（生成/更新菜单 PC/移动、多上级；同步按钮/列/字段权限项；按创建人/所属组织/岗位控件自动生成数据权限方案「全部数据 + 六个相对范围」；授权提示）。
- ✅ 每步幂等、可重放；失败停在该步，修复后从该步继续；步骤状态进 Operation 资源（8.12）。
- ✅ `dryRun` 返回迁移计划、权限资源变更、菜单变更的影响面。
- ✅ 记忆型发布弹窗：记住上次勾选的端、展示已发布路径、二次发布确认覆盖；上级菜单被删则挂载步骤失败并提示。
- ✅ 回滚 = 8.1 指针回切 + 挂载步骤重放（菜单与权限项按旧快照补全）；迁移不自动回滚。
- ❌ 无表模式之外的「发布即建库」；跨租户灰度发布（S3 随 managed 包推送升级）。

#### 技术落点 / 插件化定位

`qian.metadata` 的发布编排器调用 `qian.schema-migrator`、`qian.navigation`、`qian.authz`；以 Saga 风格的 Operation 记录步骤，Outbox 发 `qian.function.published.v1`。

#### Agent 契约

`publish(functionId, {dryRun})` 返回计划与影响面；`mountMenu(functionId, parents[])` 独立可调；发布为 `sideEffect: write`、`policyHint: confirm`。

#### 验收标准

- [ ] 发布过程中在「迁移」步骤人为杀掉进程，重启后从迁移步骤继续，最终菜单、权限项、数据权限方案齐备且只有一份。**测法**：故障注入 + 幂等断言。
- [ ] 发布含删列的草稿：`dryRun` 列出 `dataLoss` 步骤；未审批时发布停在迁移步骤，运行时仍读旧版。**测法**：接口测试。
- [ ] 手工删除某个权限项后再次发布，权限项被补全而已有授权不丢。**测法**：数据库比对。

### 8.8 审批流程（P0 · S2）

#### 设计理念

自研 .NET 轻量审批内核（ADR-012）：节点树 JSON 定义保存时编译成内部有向图，令牌推进、单库单事务、条件走内存表达式引擎；「中国式审批」的工作量在审批域（或签/会签/依次/退回/转审/抄送/子流程/超时催办）而不在引擎。简单流程与标准流程**合并为一套 JSON 定义**，S2 只交付纵向节点树设计器（DOM 自绘，对 Agent 生成最友好，补充裁决 Q22），自由画布 S3 可选。参考平台 14 处做法（兜底全局 admin、会签停滞、转审改写记录、拼 SQL 判条件、V8 任意脚本、匿名回调等）全部修正。

#### 范围与边界

**定义与设计**
- ✅ 一套 JSON 流程定义：`FlowDefinition{code, name, category, type, visibility, launchScope, activeVersionId, appId}`；`FlowVersion{version, state: designing|active|archived, definitionJson, refs[]}`；`NodeDefinition{code, type, formRef, configJson}`；`Edge{from, to, name, condition: FilterExpression, isDefault}`；`GlobalProperties{titleTemplate, rejectPolicy, withdrawPolicy, fallbackPolicy, autoApproveRules[], flags, variables[]}`。
- ✅ **节点类型注册表** `NodeType{type, title, description, configSchema, uiMeta, constraints, executor}`：S2 内置 `start / approval / gateway(exclusive|inclusive|parallel) / subflow / end`；抄送是节点属性不单设节点；`task(办理)`、`choosePath`、`external`、`trigger`、`decision-call`、`free` 为扩展点预留（P2/S3）。每种节点类型发布 JSON Schema + `uiMeta`，属性面板按 Schema 生成，`:validate` 把全部拓扑约束（默认分支仅一条、并行连线不可配条件、选择分支前后不可加网关等）变成结构化错误。
- ✅ 版本三态**后端强制**：只有 `designing` 可写，对其他状态写入返回 `FLOW_VERSION_READONLY`；新增版本 = 复制为草稿、版本号递增；在途实例绑定发起时版本；有实例引用的版本不可删。
- ✅ 启用/停用（替代「上架/下架」，补充裁决 Q27）：停用最小语义 = 不能新发起，在途照常；「隐藏审批数据」模式 P2。
- ✅ 发布范围：公开 / 授权，授权走统一鉴权权限点 `qian.workflow:flow:launch`（按 §07 7.5 命名规范 `<plugin-id>:<resource>:<action>`，由 `qian.workflow` 清单声明；具体流程以资源引用 `flow/{code}` 传入 `Check`，R9）。
- ✅ 纵向节点树设计器：节点「+」只列合法后续节点类型；点空白进全局属性、点节点进多页签配置（基础/审批人/审批方式/表单权限/通知/超时/子流程）；保存/发布即校验并可定位；版本下拉驱动按钮；基础缩放/撤销重做。

**人员解析**
- ✅ 审批人四来源（`IAssigneeResolver`）：指定成员（用户/组织含子/岗位/角色/用户组五维度 + 范围过滤：同一公司/同一部门/同一岗位/发起人上级/发起人下属）、发起者本人、上级责任人（多级，按**岗位链**推导：岗位 → 上级责任岗位 → 现任责任人，依赖 §07 7.4.3 组织目录在 S2 补齐的解析契约）、表单变量（主副表人员类字段）。
- ✅ 兜底链（`IAssigneeFallbackPolicy`，补充裁决 Q25）：节点指定异常处理人 → 租户管理员角色 → 「无法提交」结构化错误 `FLOW_ASSIGNEE_UNRESOLVED`；不再兜底到全局管理员；解析结果与有效用户取交集；结果附来源链解释。
- ✅ 抄送：指定成员 / 发起人 / 表单变量 + 范围过滤；自选抄送人 P2。
- ❌ P2：流程环节、数据接口、逐级审批来源；候选人开关（按决策保持可选）；委托/代理（S3，grant 原语 S2 可选）。

**审批方式与动作**
- ✅ 或签（任一人**首个终结动作**结束本节点，其余作废）、会签（人数或百分比，实时计算；两阈值都不可达时发 `node.stalled` 并按租户策略转人工队列或自动判定，不得静默停滞）、依次（仅全为具体用户时可排序）。
- ✅ 动作最小集（`IWorkItemAction{id, label, inputSchema, preconditions, sideEffects, humanOnly, recordType}`）：同意、拒绝（`terminate` 策略；`continue` 枚举预留 S3）、退回（「重新审批」模式，目标发起/上一节点/指定节点；「从当前节点审批」模式 P2 需路径快照）、转审（单人、不可转本人、接收人不可再转；记录**只追加不改写**）、暂存、撤回（发起节点；下一节点发生任何动作即不可撤回）、管理员终止、管理员指派；审批意见（不启用/非必填/必填）；按钮由动作注册表 + 前置条件计算，可改显示名。
- ✅ 节点字段权限矩阵（查看/编辑/必填，一键全选）后端强制：审批保存时只写有编辑权限的字段。
- ✅ 实例标题模板（`{发起人}的{流程名}` + 字段）；全局变量 + 节点参数赋值；条件组（组间/组内 且/或；左值字段/公式；右值自定义/字段/系统变量/全局变量）复用 Filter AST 与 `qian.expression`。
- ❌ P2：会签延后计算、不同意规则、系统审批、自动审批去重、节点独立表单与数据传递、汇合归并、加签/减签（决策：保持可选）、自选抄送人、审批附件、拓展字段、评论；S3：协办、签收、撤销申请、暂停/恢复/复活、批量审批、签名、归档、节点打印、拒绝后继续、自由流程、决策。

**子流程（P0，按决策升为必做）**
- ✅ 同步子流程节点：只选当前应用内已发布且启用的流程；父 → 子字段映射；发起人来源复用审批人四来源；多发起人 → 多实例（同时）；发起权限校验；父子实例关联（`parentInstanceId + parentNodeCode`），子流程结束经事件推进父流程；子流程为末节点时父结论由最后一个同步子流程决定；子流程停用时父流程上节点提交被阻断并提示。
- ❌ 异步子流程、依次创建、自动提交 → P2。

**时效与催办（P0，按决策升为必做）**
- ✅ 限时：起算点接收时间/发起时间/表单日期字段；截止 + 提醒（首次 + 间隔，间隔 0 只一次；进入时已过首次提醒点立即补发）；超时通知（截止 + 超时时长首发，按间隔重复）。
- ✅ 催办：节点超时后发起人可多次催办，仅站内信，不改流转。
- ✅ 计时重置规则事件驱动：加签/指派/转审/退回/撤回重新生效；再次到达同一节点重新生效；停用的流程继续计时但不发通知。计时器走 §07 调度（按业务键取消/重置，多副本安全），不用进程内延时队列。
- ❌ 超时自动审批/自动转审（S3）。

**运行态与工作台**
- ✅ 对象：`FlowInstance`（状态：待提交/进行中/已通过/已拒绝/已终止/已撤回/已退回，其余枚举预留）、`NodeInstance`（含完成策略进度）、`WorkItem`（来源类型、父工作项、草稿、截止）、`OperationRecord`（只追加；`actor` 人/Agent/系统 + 委托人 + 解释信息）、`CcRecord`、`EdgeTrace`、`Timer`、`LaunchUserSnapshot`。全部带 `tenant_id` 走 RLS。
- ✅ 工作台四列表：待办、已办、我发起的（按状态出现撤回/催办）、抄送我的；审批详情页 = 表单（按字段权限）+ 动作区 + 意见 + 流转记录时间线（子流程可钻取）。
- ✅ 流程监控最小集（管理员）：实例列表、节点树着色、终止、指派。
- ✅ 通知六状态（待办/结束/同意/拒绝/退回/抄送）绑定消息模板，节点级四态继承（同步流程配置/自定义/默认/关闭），经 §07 消息中心发送；超时/提醒通知随时效能力。
- ✅ 领域事件（CloudEvents 风格、带租户与幂等键，经 Outbox）：`instance.started/ended/withdrawn/terminated/returnedToStart`、`node.entered/completed{reason}/returned/stalled`、`workitem.created/completed/transferred/returned/reminded/overdue/urged`、`assignee.unresolved`、`subflow.spawned/completed`。
- ✅ 流程状态 → 列表操作矩阵由本插件下发（8.6 消费）。
- ❌ 待签、批量审批、辅助信息侧栏、工作交接（S3）；移动端特性（本期不做）。

#### 技术落点

- `qian-workflow` 独立 service；引擎：令牌 + 持久化，节点级版本号做乐观并发替代「正在审批中」缓存锁；条件判定 = Filter AST + `qian.expression` 内存求值（执行预算）；表 7–8 张（借鉴开源轻量审批流表模型，不引入其代码）。
- 表单 × 流程接缝：流程绑定「数据模型」而非具体表单版本（可换表单视图）；字段元数据与类型族由 `qian.forms` 提供；流程状态回写记录的 `flow_*` 系统列经 8.2 的服务身份调用。
- Elsa 3 PoC 对照在 S1 末完成（§07），结论进 ADR-012；Flowable 只作「BPMN 合规」可选适配器，BPMN 导出不早于 S3。
- 逆向操作交叉语义（退回 × 子流程 × 计时重置）需场景回归集与属性测试。

#### 插件化定位

`qian.workflow` 内含 `workflow-core / designer / workbench / admin / assignee / notify / sla / node-subflow` 子模块（S2 必做），`data-mapping / actions-advanced / integration / automation` 为可选子模块。内核不内置通知、打印、归档、签名逻辑，只提供扩展点：`NodeType`、`IAssigneeResolver`、`IAssigneeFallbackPolicy`、`ICompletionPolicy`、`IAutoCompletePolicy`、`IWorkItemAction`、`GatewayStrategy`、`VariableSource`、`TimeoutHandler`、事件消费者、函数注册表。

#### Agent 契约

- 定义层：`GET/POST /workflow/definitions`、`:validate / :publish / :enable / :disable`；注册表 `GET /workflow/node-types | assignee-strategies | gateway-types | actions`；`POST /workflow/assignees:resolve`（试算审批人，返回来源链）。
- 运行层：`GET /workflow/workitems/{id}/actions → [{id, label, inputSchema, preconditions, humanOnly}]`（动作即工具）；`POST …/actions/{key}`（带 `idempotencyKey`）；`GET …/{id}/progress`（会签票数、是否可能停滞）；`GET /workflows?launchable=true` + `startFormSchema`。
- 「工作流即能力」：已发布流程自动登记为意图级能力（发起 = 一个工具；人工节点 = Operation `input_required`，见 8.12）。
- Agent 代办（补充裁决 Q26）：actor 三类；Agent 有效权限 = 委托人权限 ∩ 授予 scope；签名类动作 `humanOnly`；转审、协办、撤销对 Agent 禁止（S4 前）；生成的定义只能落 `designing` 并经 `:validate`。

#### 验收标准

- [ ] 用节点树设计器搭「请假审批」（发起 → 部门主管（上级责任人）→ 条件网关（天数 > 3 走总监会签 2/3，否则直达）→ 子流程「HR 备案」→ 结束），`:validate` 通过、发布、启用；租户用户在列表发起，各审批人在工作台处理，发起人看到「已通过」。**测法**：端到端脚本（S2 退出标准主链路）。
- [ ] 删除默认分支或给并行连线配条件，`:validate` 返回对应错误码与节点路径。**测法**：坏样例集。
- [ ] 对 `active` 版本发 PATCH 返回 `FLOW_VERSION_READONLY`。**测法**：接口测试。
- [ ] 上级责任人节点：发起人岗位无上级责任岗位时按兜底链走到租户管理员；租户管理员角色为空时发起返回 `FLOW_ASSIGNEE_UNRESOLVED`，不落到任何全局账号。**测法**：组织数据三组用例。
- [ ] 会签 2/3，三人中两人拒绝：节点以拒绝结束；配置「3/3 同意且 1 人拒绝」不可达时发 `node.stalled` 并进入人工队列。**测法**：接口模拟。
- [ ] 转审后流转记录保留原处理人与接收人两条，原记录不被改写。**测法**：记录比对。
- [ ] 节点限时 10 分钟、提醒 5 分钟：到点收到提醒与超时站内信各一次；多副本（3 个 `qian-workflow` 实例）下不重复发送；发起人催办后审批人收到催办站内信。**测法**：时钟注入 + 多副本。
- [ ] 子流程结束后父流程自动推进；子流程被停用时父流程上节点提交被拒并提示。**测法**：接口测试。
- [ ] 以 Agent 主体（委托某用户）调用 `actions/approve` 成功且记录 `actor=agent, onBehalfOf=user`；调用 `transfer` 返回 `ACTION_HUMAN_ONLY`。**测法**：S2S 令牌模拟。

### 8.9 数据权限与字段权限（P0 · S2）

#### 设计理念

权限七层（L0 租户隔离 → L6 ABAC，见 §07）中 S2 落地 L4 行级与 L5 字段级：数据权限 = 条件树 + 系统变量，编译为查询谓词在本地执行并与 RLS 叠加，不交给远程引擎；字段权限读投影/写白名单；合并语义固定「同角色 AND / 跨角色 OR / 任一全部数据则放开」，不可配置（补充裁决 Q19）。权限不进令牌，变更靠 `identity_revision` 即时失效（ADR-006）。

#### 范围与边界

- ✅ 数据权限方案 `DataAuthScheme{name, code, allData: bool, condition: FilterExpression, menuId}`；系统变量目录与 8.2 共用；相对范围条件（当前用户及下属等）不可用等于/不等于。
- ✅ 编译器 `IDataPermissionFilter`：主体有效角色 → 每角色的方案 AND → 角色间 OR → 任一 `allData` 放开 → 输出谓词 AST 并合入 8.2 查询；无方案 = 查不到数据；覆盖 list/get/update/delete/export/import 全部路径（参考平台只管列表是反例）。
- ✅ 字段权限 `FieldPolicy{resource, field, read: visible|masked|hidden, write: allow|deny}`：读时投影或按敏感度脱敏规则，写时白名单；yue 按元数据渲染但以服务端为准。
- ✅ 授权界面（多步向导：菜单 → 按钮 → 列表 → 表单字段 → 数据方案；大树勾选；授权人只能授出自己拥有的，其余置灰不隐藏）；资源管理页按菜单类型给出可配项与类型化空态。
- ✅ 创建者自动成为应用 Owner（资源实例级 ACL 简版：所有者 + 共享，内核表实现）。
- ✅ 数据权限方案自动生成（8.7 挂载步骤）；手删后再发布补全。
- ✅ 字段敏感度驱动默认脱敏（`pii` 字段对无读权限者返回掩码而非空）；导出遵守同一策略。
- ✅ 决策审计：每次 `Check` 记录命中规则，接入 ADR-009 哈希链。
- ❌ 组织角色/岗位角色/逐级递减/身份切换（S3 可选）；OpenFGA 资源实例共享（S3）；Explain 工具（S3，`ListPermissions` S1 已有）。

#### 技术落点 / 插件化定位

`qian.authz` S2 增量，住 `qian-platform`；编译器输出与 ORM 无关的谓词 AST，由 `qian.dynamic-data` 翻译为 SQL；主体权限按 `identity_revision` 缓存。

#### Agent 契约

`authz.check(action, resource)` 可预检；`ListAllowedActions` 过滤工具列表；数据权限 AST 可序列化并带自然语言描述，Agent 生成查询时作为强制过滤合入（而非由 Agent 自行拼条件）。

#### 验收标准

- [ ] 用户 A 有角色 R1（方案「本部门」+「本人创建」）与 R2（方案「全部数据」）：A 看到全部；移除 R2 后只看到「本部门 AND 本人创建」。**测法**：权限回归集 12 场景（含 F 基线 C-1 裁决）。
- [ ] 无任何方案的用户 `list` 返回空、`get` 返回 404、`export` 为空文件。**测法**：接口测试。
- [ ] 字段「身份证号」对无读权限者在 list/get/export 三处均为掩码；写入该字段返回 `FIELD_FORBIDDEN`。**测法**：接口测试。
- [ ] 授权变更后 ≤ 3 秒内（`identity_revision` 推送）该用户新请求按新权限执行，无需重新登录。**测法**：计时断言。
- [ ] 建应用者无需手工授权即可打开自己应用的全部菜单。**测法**：UI 自动化。

### 8.10 应用与菜单、系统表单式清单接入、应用包 bundle v1 与安装到租户（P0 · S2）

#### 设计理念

应用是租户内权限与元数据的作用域容器，一个应用 = 一个 bundle（ADR-014）；功能编码命名空间化（`tenant.app.function`）并只归属一个应用，其他应用通过「跨应用引用」挂菜单（补充裁决 Q20）；手写代码页面通过**插件清单**接入平台治理（替代参考平台手填的「系统表单登记」），与低代码功能享受同一套菜单、权限、流程引用。S2 必须跑通「同一包装进两个租户、各自配置、独立升级」。

#### 范围与边界

**应用与菜单**
- ✅ 应用实体（§07 契约）S2 升级为一等元数据对象：名称（可重复）、编码（全局唯一、不可改、可自动生成）、图标、排序、PC/移动首页（默认为应用默认首页 = 菜单 + 功能列表，可改指任一菜单或 8.11.7 配置路径型门户，R16）、负责人（可移交）、开发者（全部/名单）、使用授权、跨应用引用白名单。
- ✅ 应用菜单管理界面：PC/移动两棵树；菜单类型为扩展点（目录、页面、在线功能、代码页面、流程、报表、门户、字典、外链、视图……各类型注册运行时视图与配置面板）；路由应用内唯一；启用/禁用/隐藏（父禁用仍可挂子但授权不可见；所有菜单需授权才可访问，开发者不例外）；菜单与资源 URN 目录联动（见 §07 统一资源引用 URN）。
- ✅ 统一壳按「平台控制台 / 应用」上下文切换（补充裁决 Q37），不采用三面新窗口；应用切换器为 S2 必做。
- ✅ 跨应用引用：白名单允许引用其他应用「已发布」表单（关联表单、关联查询、子表选择数据生效）与把功能挂到本应用菜单（引用而非归属）。
- ❌ 应用中心入口体验（置顶/筛选/排序，S3）；应用级多身份切换（S3 可选）。

**系统表单式清单接入（代码页面）**
- ✅ 开发者用 SDK 注解在代码中声明 `CapabilityManifest{code, kind: page|api|action, routes{pc?, mobile?}, api?: OpenAPIRef, buttons[], formFields[], listFields[], flowForm, anonymous, permissionKinds[], dependsOn[]}`，由 `qian plugin pack` 生成清单片段并在部署时注册（§07 能力目录）；S2 增加：清单登记的页面可生成菜单、生成按钮/列/字段权限项、被流程作为流程表单引用、被打印引用。
- ✅ 数据权限对代码页面同样在数据引擎入口统一执行（代码页面通过运行时 SDK 访问数据，不自带 CRUD）。
- ❌ 手工填写的登记界面（清单只从代码派生）。

**应用包 bundle v1 与安装到租户**
- ✅ bundle = `qian-plugin.yaml`（`kind: bundle`，字段见 §07 清单 v1）+ `package`（数据模型、表单、列表、流程、菜单、角色模板、打印模板、数据接口、字典引用、种子数据）+ `ui`（可选 MF 远程）+ 可选 `function` + 迁移脚本 + 依赖清单 + **id 重映射表**。S1 的 v0 格式在 S2 定稿为 v1：补 `distribution`、`components.packages`、`marketplace.manageability`、`provides.resources`、`lifecycle.migrations.tenant`。
- ✅ 导出：从应用导出 bundle（含菜单、权限模板、字典引用、数据接口、表结构 DDL 计划——参考平台不含这些是反例）；复制「.副本 + 五位随机」默认未发布。
- ✅ 导入/安装到租户：解析依赖图（检测环与冲突）→ 展示所需反向权限（`permissions.platform/data/egress/secrets`）由租户管理员批准 → 8.3 逐租户迁移 → 实例化包层元数据 → 挂菜单与默认角色模板 → 发 `tenant.plugin.enabled`。导入策略「跳过 / 追加」，跨应用导入重生 id 并输出「待重连依赖清单」。
- ✅ `template` 与 `managed` 两种分发（默认 `managed`，补充裁决 Q10）；`managed` 包升级：平台级激活新修订 → 逐租户 `tenantUpgrade(fromVersion)` → 包层替换、租户层保留（8.1 分层）→ 可回切；无新增反向权限可自动推送，新增则需租户管理员重批（升级同意规则）。`template` 升级 = 同源导入合并/覆盖，可选升级前快照。
- ✅ 权益检查：安装前查询 `qian.entitlement`（S1 授权 v0：`tenant × plugin × plan` 手工授予）；Entitled（授权）与 Enabled（Apollo 开关）分开存，「未启用」在网关路由、SDK 入口、前端外壳三处拦截。
- ✅ 平台自营应用吃自己的狗粮：S2 至少一个官方业务应用（如「请假与考勤」示例应用）以 bundle 交付并走同一安装链路。
- ❌ 三方合并冲突预检报告（S3）；市场目录与安装向导 UI（S3，§09）；禁止导出 managed 应用（S3 可选）；用户个性化层（S3 可选）。

#### 技术落点

- 包内全部引用用命名空间化稳定编码，安装时经 id 重映射表落为租户内 id；`manageability` 由 8.1 强制。
- 安装编排走租户级状态机 `Available → Entitled → Provisioning → Enabled ⇄ Disabled → Upgrading → Enabled / Uninstalled → Purged`（§07），底层 `ITenantProvisioner` 钩子幂等。
- 制品：bundle 作 OCI artifact 推 Harbor（ADR-017），按 `@sha256:` 引用，强制验签。

#### 插件化定位

`qian.metadata`（分层、manageability、安装实例化、同源导入）+ `qian.kernel`（注册表、状态机、重映射）+ `qian.navigation`（菜单）+ `qian.entitlement`（权益）+ `qian.schema-migrator`（租户迁移）。

#### Agent 契约

`bundles.export(appId)`、`bundles.install(bundleRef, tenantId, {dryRun})` 返回依赖、反向权限、迁移计划；安装与升级 `policyHint: confirm`（付费与权限动作永远需要人）；`generatedBy / promptHash` 字段在 v1 清单预留（S4 Agent 生成包）。

#### 验收标准

- [ ] 从租户 A 的「请假与考勤」应用导出 bundle，在租户 B、C 分别安装：B 改表单布局（tenantEditable），C 改审批人；发布应用 v1.1（新增字段 + 新审批节点）→ 升级 B，C 不受影响；B 的布局修改保留且新字段出现；回滚 B 到 v1.0 后布局仍保留。**测法**：S2 退出标准第二条脚本。
- [ ] v1.2 新增 `permissions.egress`：推送升级对 B 停在「待管理员重批」，批准后继续。**测法**：接口测试。
- [ ] 包含循环依赖或缺失依赖的 bundle 安装被拒并列出依赖图。**测法**：坏样例。
- [ ] 一个代码页面（SDK 注解声明 2 个按钮、3 个表单字段）部署后：菜单可生成、权限项出现在资源管理、可被流程选为流程表单。**测法**：示例插件 + UI。
- [ ] 应用 X 引用应用 Y 的已发布表单挂菜单，Y 的表单改版发布后 X 处读到新版；取消白名单后 X 的引用失效并提示。**测法**：接口测试。

### 8.11 打印、文件预览、数据建模与接口设计器、消息模板与渠道 UI、任务管理 UI（P1 · S2）

> 本节六个功能项共用小节结构，各自给出范围与验收；横切规则（编码生成、唯一性、复制命名、导入策略、被引用保护、可发布元数据三态）全部复用 8.1 与 §07 内核框架，只实现一次。

#### 8.11.1 打印（P1，模板与浏览器打印；打印设计器 S2 后期）

- **范围** ✅ 打印模板实体（名称/分类/编码 `DY+日期+序列`、应用内唯一名、全局唯一编码）、版本生命周期（设计中 → 已发布锁定 → 新版本复制 → 旧版归档可启用）、模板 Schema（辅助线/矩形/圆、文本/长文本/二维码/条码/图片/图片组/表格、系统元素：打印时间/人员/审批记录/多联页码；纸型 A3–B5）、数据绑定（数据接口 + 查询设置把当前记录 id/当前用户传入；数据集名称模板内唯一）、数据转换（消费 §07 值翻译）、组件声明「可接受字段类型」、挂载扩展点（列表「批量打印」多模板、表单打印按钮、流程节点打印条件 S3）、预览 → 浏览器打印/导出 PDF（浏览器端）。多联 ≤2 P2。❌ 服务端 HTML→PDF（S3 可选，补充裁决 Q33）；打印文件与发布范围（S3）。
- **技术落点** 模板 Schema 由 Qian 定义（声明式格式化，禁函数字符串），前端设计器取开源 JSON 打印设计器的核心库或自研（React 栈，许可宽松者优先），条码/二维码用 MIT 库；服务端只组装数据。
- **Agent 契约** `print.validate(template)`、`print.render(templateId, recordIds) → html`、`print.explain`；模板 Schema 公开。
- **验收** - [ ] 为请假单建打印模板（文本 + 表格绑子表 + 审批记录），从列表勾选 3 行批量打印得到 3 页；模板发布新版本后旧版仍可启用。**测法**：UI + 快照比对。 - [ ] 绑定的数据接口未配查询条件时保存被拒。**测法**：接口测试。

#### 8.11.2 文件预览（P1）

- **范围** ✅ 预览票据（内核签发：单文件、单用户、短时效、签名；预览服务验签或经网关校验；禁止 URL 带登录令牌）、提供者接口（自建开源预览服务 / 文档服务器 / 商业云预览三选一可替换；首选 .NET 生态，Java 进程只作可选 sidecar——补充裁决 Q34）、`qian-file-preview` Helm 子 chart 随插件安装、配置入 Apollo、钉版本跟随上游、来源主机白名单、支持格式清单机器可读、按许可裁剪（关闭 CAD 与可能含 GPL 组件的视频转码）。❌ 在线编辑（本期不做）。
- **验收** - [ ] 上传控件的文件在详情页可预览，预览 URL 中无登录令牌，票据过期后 401。**测法**：抓包 + 时钟。 - [ ] 替换提供者实现（配置切换）后预览功能不变。**测法**：两提供者回归。

#### 8.11.3 数据建模（P1）

- **范围** ✅ 可视化建表/字段/主键/索引（经 8.3 迁移计划执行，不直接 DDL）、浏览数据（受数据权限）、复制表（仅结构/结构 + 数据，限同库）、基准字段（缺省/常用，内置不可删；系统列由 8.2 约定强制带入）、控件映射维护页（读写 8.4 Catalog）、模型导入导出并入统一元数据包格式。❌ 私有 `.bdb` 后缀；MySQL/国产库；跨库同步。
- **验收** - [ ] 建模器新建含 10 字段的表 → 表单「手动选表」绑定 → 发布运行；建模器再加一列后表单侧「从表生成」能看到新列。**测法**：端到端。

#### 8.11.4 接口设计器（数据接口，P1）

- **范围** ✅ 三类执行器：配置式查询（实体 + Filter AST，默认开放）、SQL 文本（**高级权限**：独立权限点 `qian.data-api:sql:author`，默认不在开发者角色内、由租户管理员显式授予具体人员；只读数据库账号由平台供给——平台 PG 中为每租户绑定 RLS 的只读角色、运行时按租户切换，租户自带连接的只读账号 S3 随 D4 评估；超时与行数上限、参数化 `${参数}`、审计、发布需租户管理员批准——补充裁决 Q32 / R11 / R25）、API（相对/绝对地址、Header/Query/Body 参数来源、返回字段与总数字段、异常验证、处理脚本需沙箱 P2）、静态/Mock（P2）；写接口（P2，叠加鉴权/审计/事务/危险级别/「需人工确认」）；参数（类型、默认值、必填、来源：自定义/系统变量/服务出参）、分页开关自动加入参、返回字段语义类型驱动值翻译、「服务出参」保存后不可改、启用/禁用、复制/导入导出、执行日志。消费侧统一叫「数据绑定」。❌ 任意 URL 代理携带用户令牌；匿名执行。
- **Agent 契约** 每个接口 `Describe(id) → JSON Schema`、`Query(id, params)`、`Execute(id, params)`，元数据含用途说明、读写与危险级别、幂等性、scope、限速——「数据接口 = 工具」，自动生成 OpenAPI，S4 映射为 MCP 工具。
- **验收** - [ ] 无 SQL 高级权限的开发者只能建配置式接口；有权限者建 SQL 接口，执行超过行数上限返回截断提示并记审计。**测法**：权限 + 接口。 - [ ] 下拉树形控件绑定一个数据集接口，参数来自表单字段，联动刷新。**测法**：UI。

#### 8.11.5 消息模板与渠道 UI（P1）

- **范围** ✅ 短信通道配置 UI（基于 §07 Provider：地域/参数、同步已审核签名与模板、被引用只能改次要字段）、群机器人出站 Webhook（钉钉/企微/飞书，加签，唯一默认不可删）、第三方模板同步（结果分成功/失败带原因，解析变量结构）、监控 UI（一次发送一条监控 + 每收件人一条记录、「发送中」态、按失败收件人定向重试、清空只清监控数据）、测试发送两步（返回待填参数 → 逐渠道发送并回报）。接收配置/用户订阅矩阵 P2；公告 S3 可选；钉钉/企微/飞书应用消息、公众号 S3。
- **验收** - [ ] 流程「待办」通知绑定站内信 + 群机器人模板，审批人收到两渠道消息，监控页两条记录；机器人地址错误时只重试该渠道。**测法**：Mock Webhook。

#### 8.11.6 任务管理 UI（P1）

- **范围** ✅ 作业定义（名称、cron/间隔/一次性、任务类型：数据接口 / 本地方法、参数、超时、重试、并发策略、执行身份为受限服务身份）与触发器状态（就绪/暂停/归档）两个聚合、启停/立即执行/查看日志、结构化执行记录（状态码、耗时、错误分类、业务结果回写）。❌ 脚本型任务（任意代码执行，不做）。
- **验收** - [ ] 创建每分钟调用某数据接口的任务，3 个副本下每分钟恰执行一次，业务失败在日志中可见。**测法**：多副本 + 计数。

#### 8.11.7 配置路径型门户与报表子集（P2，补充裁决 Q29/Q30）

> 已裁决（R16）：本节保持 S2 可选（P2）。S2 验收演示所需的「可用首页」由应用默认首页（菜单 + 功能列表，8.10）满足，不依赖本节门户；`@yue/charts` 维持 S2 可选。

- **范围** ✅ 门户配置路径型（固定模板 + 配置：指向页面/外链/功能，发布即挂菜单，作应用首页）；报表子集（列表 + 分组汇总 + 导出 XLSX，消费数据接口，服务端渲染）。❌ 门户设计型、交叉表/父子格/分栏、大屏（S3）。
- **验收** - [ ] 应用首页指向配置路径型门户后，租户用户登录进应用落在该页。**测法**：UI。

### 8.12 Operation/Task 资源与人在回路（P0 · S2）

#### 设计理念

长任务（发布、迁移、导入导出、批量迁移）与人工步骤（流程待办）统一为 **Operation/Task 资源**，状态机 `working / input_required / completed / failed / cancelled`，`input_required` 携带结构化输入请求；S2 的「待办」与 S4 Agent 的「中断」是同一个模型，不做两套（ADR-018 防返工第 13 条）。

#### 范围与边界

- ✅ `Operation{id, kind, status, progress{percent, message}, inputRequest?{schema, prompt, actions[]}, result?, error?, startedBy(actor 三类), tenantId, idempotencyKey, cancelable}`；`GET /operations/{id}`、`:cancel`、`:provideInput`；列表按租户/发起人过滤。
- ✅ 接入点：8.7 发布向导、8.3 批量迁移、8.2 导入导出、8.10 安装/升级、8.8 工作项（`WorkItem` 投影为 `input_required` 的 Operation，`actions` 即允许的输入动作）。
- ✅ 进度推送经 `qian.realtime`；完成/失败发事件。
- ✅ SDK：`StartOperation / ReportProgress / RequireInput / Complete`。
- ❌ MCP Tasks（`tasks/get/update/cancel`）与 A2A Task 的协议映射实现（S4，S2 只保证状态机字段对齐）。

#### 技术落点 / 插件化定位

`qian.operations`（module@`qian-platform`，`tier: system`），内核契约；持久化在平台库；长任务执行体在各自插件，Operation 只做状态与输入请求的权威记录。

#### Agent 契约

Operation 是 Agent 感知长任务与审批中断的唯一方式：`async: task` 的能力返回 `operationId`；`input_required` 的 `inputRequest.schema` 即 Agent 需要补充的输入；`humanOnly` 的输入动作对 Agent 不可用。

#### 验收标准

- [ ] 一次导入 5 万行作为 Operation 可查进度、可取消，取消后已导入行按策略保留或回滚并在结果中说明。**测法**：接口 + 计时。
- [ ] 流程待办在 `GET /operations?status=input_required` 中可见，其 `inputRequest.actions` 与 `GET /workflow/workitems/{id}/actions` 一致。**测法**：接口比对。
- [ ] 同一 `idempotencyKey` 重复发起 Operation 返回同一 `operationId`。**测法**：接口测试。

### 8.13 yue S2 交付（P0 · S2）

#### 设计理念

沿用 ADR-013：React 19 + Vite + Tailwind v4 + Base UI + shadcn 源码自有化，六层分层用 lint 强制（tokens → core → shell/data/form/charts/code-editor → biz → runtime/widgets → designer）；只保留一套表单/表格/弹层引擎；控件注册表与 yue 组件 props 的 JSON Schema 机器可读，设计器、远程渲染白名单、S4 Agent 生成 UI 共用一份契约。S1 首迭代的 Base UI 验证门结论（§07）决定 S2 是否整体切 Radix，不混用。

#### 范围与边界

- ✅ **控件集**（8.4 分级表 P0/P1 全部）：每控件 `meta + Runtime + Designer + toSearchControl + toCellRenderer`，`data-slot/data-testid` 齐备，端支持矩阵显式降级。
- ✅ **渲染器**：表单渲染器三态、子表（InputTable）、三层联动、公式控件；列表渲染器（普通表格 P0；左树/树形/行内编辑/分组 P2）、动作执行器；流程表单宿主（按节点字段权限渲染）；审批中心基础（四列表 + 详情页）。
- ✅ **三大设计器**：表单、列表、审批流（纵向节点树），共用设计器基础设施（dnd-kit 嵌套拖拽、撤销重做、Schema 驱动属性面板、校验浮层与定位、版本下拉、预览矩阵）。
- ✅ **通用交互组件**：`ConditionBuilder`（条件组构造器，被数据过滤/高级查询/数据权限/启用规则/流程条件五处复用）、`ParamBinding`（参数绑定表格，来源五选一）、`SuperQuery`、`ImportWizard` / `ExportDialog`、`PopupSelect` 族、`RelationForm`、`InterfaceSelect`、`PosSelect / GroupSelect / UsersSelect`、授权树族（菜单/按钮/列/字段/数据权限页签）、字段权限矩阵、人员选择器 + 范围过滤、「只列已发布资源」选择弹窗、字段映射表格、弹窗三件套。
- ✅ **`@yue/data` 进阶**：树表、列级权限、行内编辑、复杂表头、列宽拖拽与列序、虚拟滚动；合计与个人视图 P2。
- ✅ **`@yue/core` 补充**：Steps、Descriptions、Timeline 必做；Slider/Rate/ColorPicker/Watermark/Transfer/Command Palette 可选。
- ✅ **`@yue/code-editor`**：Monaco（桌面，SQL/表达式带白名单 `.d.ts` 补全）+ CodeMirror 6（轻量）、Cron 可视化；富文本/Markdown 选型调研后决定（补充裁决 Q39）。
- ✅ **元数据契约包** `@qian/metadata-contracts`：FormSchema / ListSchema / FlowDefinition / PrintTemplate 的 JSON Schema 定稿（S1 草案 → S2 定稿），生成 TS 与 C# 类型，校验器前后端共用。
- ✅ **组件 manifest 全量**：yue 组件目录（组件、props JSON Schema）机器可读，发布到私有 npm；应用切换器；视觉回归进 CI。
- ❌ `@yue/mobile` H5 渲染器（Q13）；`@yue/charts` 完整版（S2 可选，门户/报表 S3 必做）；门户/报表/打印/大屏设计器（S3）；不可信 UI 沙箱（S3）；bpmn-js（许可证要求水印可见）、Formily/amis 等自带体系的低代码引擎。

#### 验收标准

- [ ] eslint-plugin-boundaries / dependency-cruiser 在 CI 阻断任何反向依赖。**测法**：注入一条 `core → designer` 导入应失败。
- [ ] 平台控制台的 S1 后台 CRUD 表单与低代码运行时表单由同一 `SchemaForm` 渲染（无第二套表单引擎）。**测法**：代码审计脚本统计引擎入口数 = 1。
- [ ] 全部 P0/P1 控件通过 axe 无严重违规、Lighthouse 预算达标；三大设计器在 1366 宽度可用。**测法**：CI 报告。
- [ ] `ConditionBuilder` 在五处落点输出同一 Filter AST 并通过同一校验器。**测法**：快照比对。

### 8.14 S2 的 Agent 就绪项（P0 · S2）

S2 不做任何 Agent 功能（§10），但以下四项必须在 S2 做对，否则 S4 返工（ADR-018、ADR-016）：

| 项 | S2 交付（人用版） | S4 复用方式 | 归属 |
|---|---|---|---|
| **元数据生成闭环（人用版）** | patch → 校验 → 预览 → 人工批准 → 发布 → 可回滚（8.1、8.7）；设计器与导入都走它 | Agent 提交 patch 走同一门禁；生成产物记 `generatedBy / promptHash / 审核人` | `qian.metadata` |
| **字段敏感度标签** | `dataClassification` 进字段元数据，驱动 8.9 脱敏与导出策略 | 工具返回按敏感度脱敏；对外发送类工具默认需确认 | `qian.metadata` + `qian.authz` |
| **工作流即能力** | 已发布流程自动登记为意图级能力；`actions` 契约带 `inputSchema / preconditions / humanOnly`；待办 = Operation `input_required`（8.8、8.12） | 流程发起与审批动作直接成为 MCP 工具；人工节点映射 MCP Tasks | `qian.workflow` + `qian.operations` |
| **出网 SSRF 基线** | §13.8 已把平台自有出网点（OIDC 发现、企业微信、SMTP、短信、Apollo、对象存储）的出口代理与 SSRF 基线定为 S1 必做（R10）；S2 把低代码新增的出网点（数据接口 API 执行器、群机器人 Webhook、流程外部节点、第三方 `connector`）全部接入同一出口代理：私网与云元数据地址默认阻断、按插件清单 `permissions.egress` 白名单放行、不携带用户令牌外发、出网审计 | 连接器与 Agent 工具的出网走同一基线 | `qian.kernel` + K3s 基础设施（NetworkPolicy / 出口网关），规范见 §13.8 |

此外 S2 继续遵守 S1 起生效的防返工清单（8.0.5），并把控件目录、Field Type Catalog、运算符矩阵、系统变量目录、流程节点 Schema、布局能力矩阵全部以元数据接口发布（「目录是数据不是代码」）。

**验收标准**
- [ ] 任一出网点请求 `169.254.169.254`、`10.0.0.0/8` 或未在白名单的域名被阻断并记审计。**测法**：SSRF 探测用例。
- [ ] 发布一个流程后 `GET /capabilities` 出现 `workflow.<code>.launch` 能力（能力 ID 保持插件内短名，URN `qian.workflow/workflow.<code>.launch`；其权限点为 `qian.workflow:flow:launch`，R23）且带 `inputSchema`（= 发起表单 Schema）。**测法**：接口测试。
- [ ] 以非设计器通道提交 patch 的全部流程产物带 `source` 与审批记录。**测法**：审计查询。

### 8.15 S2 明确不做与理由

| 不做 | 理由 | 何时再议 |
|---|---|---|
| 参考平台形态的代码生成（完整 CRUD 源码 + 手工接入） | 制造第二份真相源与能力漂移；与微服务拆分冲突（Q11） | 「调用运行时 SDK 的薄壳脚手架」S2 后期可选 |
| 前端自由 JS 按钮 / 控件事件 / 表格脚本（`eval`） | 多租户安全面大、暴露令牌、Agent 不可发现（补充裁决 Q18） | QuickJS 沙箱 P2 兜底；声明式规则是首选 |
| 进程共享的服务端脚本引擎（V8/Jint 任意脚本） | 违反「第三方代码永不进共享进程」（ADR-002） | 重脚本走 `function` Wasm（ABI S2 中期拍板） |
| 匿名外链照搬（读草稿、不校验开关/密码、无有效期） | 安全缺陷 | S3 用服务端签发的能力令牌 + 网关匿名路由重做 |
| H5 表单渲染器与移动端专属配置 | S2 只做 PC 响应式（Q13）；两份配置是反例 | `devices[]` 与端覆盖预留，S3 评估 |
| 自由画布流程设计器、BPMN 导出、bpmn-js | 一套 DSL 两种视图，纵向节点树先行（补充裁决 Q22）；bpmn-js 许可证要求水印 | 自由画布 S3 可选（React Flow）；BPMN 只作导出器不早于 S3 |
| 加签/减签、候选人、拒绝后继续、委托代理、签收、撤销申请、协办 | 不在退出标准关键路径（Q14 / 补充裁决 Q24） | 加签减签与候选人 P2；其余 S3 |
| 定时器节点、嵌入式决策流程、简单→标准切换 | 参考平台亦未实现 / 与调用式重复 / 一套 DSL 无需切换 | 不做 |
| 树形表格以清空数据权限换全量 | 鉴权不得有旁路 | 不做 |
| 门户设计型、报表交叉表/分栏、大屏 | 阶段归 S3（补充裁决 Q29/Q30/Q31）；大屏不复用参考平台源码 | S3 |
| 服务端 HTML→PDF | 无头浏览器在 K3s 的成本未评估（补充裁决 Q33） | S3 可选 |
| MySQL/国产库方言、租户自带业务库 | ADR-004 PostgreSQL 唯一一等库；RLS 无等价物 | 本期不做 |
| 在途流程实例迁移到新版本 | 需求不明；版本隔离已足够 | 不做（本期） |
| 元数据多人实时协同编辑 | 乐观锁 + 冲突提示足够 | 不做 |

### 8.16 待确认

1. **`function` Wasm 运行时的阶段**：已裁决（R2）——统一为 S2 可选（P2），不计入 S2 核心交付，ABI 在 S2 中期拍板；本章 8.0.3 的 `qian-wasm-runtime` 可选单元与 8.2 / 8.5 的落点不变，服务端公式与规则先用 JSON AST 解释器。
2. **模型目录与按用途路由的阶段**：D 基线把「模型目录 / 上游凭证 / 按用途路由 / 默认参数 / 租户配额计量」标 S2 必做；P 基线与 ADR-015/018 把 `qian.model-gateway` 定为 S1 可选接线、S3 必做租户可见。本章按决策列 P2（只定契约），实现细节以 §10.5 为准。
3. **插件命名与 App / 导航归属**：已裁决（R18 / R1 / R26）——统一用 `qian.dynamic-data` 与 `qian.plugin-sdk`（P 基线别名说明仅保留在 8.0.4 一处）；组织目录作为 `qian.identity` 子模块、不独立成插件；App 实体契约由 `qian.kernel` 在 S1 预留（bundle 的租户内安装记录，持有默认首页、菜单根与权限点归属），本章 8.10 在 `qian.metadata` 升为一等元数据对象；`qian.navigation` 在 S1 是 `qian.kernel` 内「菜单由清单贡献 + URN 目录 + 用户偏好」的逻辑名（S1 菜单贡献走 `qian.shell.nav` 插槽、权限资源注册走 `qian.authz`），S2 允许拆出为独立插件。
4. **权限点命名**：已裁决（R9）——统一为 `<plugin-id>:<resource>:<action>`（§07 7.5）；本章 8.8 的流程发起权限点已改为 `qian.workflow:flow:launch`。
5. **数据接口 SQL 文本模式**：已裁决（R11 / R25）——高级权限为独立权限点 `qian.data-api:sql:author`，默认不在开发者角色内，由租户管理员显式授予具体人员；只读数据库账号由平台供给（平台 PG 中每租户绑定 RLS 的只读角色，运行时按租户切换），租户自带连接的只读账号 S3 随 D4 评估；发布需租户管理员批准。
6. **门户配置路径型与报表子集**：已裁决（R16）——保持 S2 可选（P2）；S2 验收演示的「可用首页」由应用默认首页（菜单 + 功能列表）满足，不依赖门户；`@yue/charts` 维持 S2 可选。
7. **富文本 / Markdown 编辑器选型**（补充裁决 Q39）：S2 前补选型调研，结论决定 8.4 富文本控件能否进 P1。
8. **S2 示例自营应用的选题**（本章假定「请假与考勤」）与其是否随 S2 对外交付，待产品负责人确认。
