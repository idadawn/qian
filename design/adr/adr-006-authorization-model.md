> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# ADR-006 鉴权模型

> 本页由构建脚本镜像自仓库 [`docs/adr/ADR-006-authorization-model.md`](https://github.com/idadawn/qian/blob/main/docs/adr/ADR-006-authorization-model.md)，请修改源文件，不要改本页。

## 1 背景与问题（Context）

Kun 对鉴权**全无**：`src/` 中没有任何 `IAuthorizationHandler`、策略或权限抽象；Mediator 没有授权行为；只有 `roles` claim 透传；`IPermissionChecker` 被 Kun 的 ADR 两次推迟（基线 K §2.3）。这是 S1 最大的单项工作量之一，且要同时服务 UI、API、MCP 三入口并与 RLS 协同（基线 P §10.1 第 4 条）。

参考平台的教训：框架层只保证「已登录」，细粒度权限散在业务服务里且可被客户端传参（`menuId / flowIds`）绕过，移动端控制器无校验，数据权限只对自生成查询生效、不是数据库级，权限无缓存每次查库，基数超限静默截断（基线 P 附录 A-14；基线 S §6.2、§9.3；基线 F §3.10）。可借鉴的是「数据权限方案化（菜单 → 条件 JSON 方案 → 角色授权）」与「按钮 / 列 / 字段双重门」（基线 F §3.10）。

基线 S 把权限分为 L0 租户隔离 → L1 角色 → L2 功能权限 → L3 资源实例 → L4 数据权限 → L5 字段 → L6 ABAC 七层（基线 S §9.3）。数据权限多方案合并语义经源码裁决为「同角色内 AND、跨角色 OR、任一方案为全部数据则整体放开」（基线 F 附录 C-1）。

## 2 决策（Decision）

- **D6.1 Kun 侧只新增 `Kun.Authorization` 端口**：`IPermissionChecker`（`Check / BatchCheck / ListAllowedActions / ListPermissions / Explain`）、`IDataPermissionFilter`（输出 EF 表达式或 SQL 谓词 AST）、`IFieldPolicy`，以及 ASP.NET Core 策略、Mediator Pipeline 行为、gRPC 拦截器集成；不放实现。
- **D6.2 `qian.authz` 为内置实现**（`tier: system, alwaysEnabled`）：权限点与默认角色模板由插件清单 `provides.permissions` 声明并登记；资源类型（应用、菜单、按钮、API、流程、打印…）由插件注册。权限点编码统一为 `<plugin-id>:<resource>:<action>`（如 `qian.workflow:flow:launch`、`qian.crm:customer:read`）：清单只声明 `resource` 与 `actions`，插件 ID 前缀由 `qian.authz` 登记时拼接。
- **D6.3 租户域 RBAC**：内置角色（平台超管；租户 Owner / Admin / 开发者 / 使用者）+ 自定义角色；主体可为用户、组织、岗位、用户组；有效角色 = 直接授予 ∪ 经组织 / 岗位 / 组获得；`Check` 缺省拒绝；平台超管旁路在策略中显式声明并审计。
- **D6.4 数据权限（L4）**：条件树 + 系统变量（`@userId @userAndSub @organizeAndProgeny @positionAndSub` 等，每个带自然语言描述写进契约），**编译成 SQL / EF 谓词在本地执行**，RLS 永远兜底；合并语义固定为「同角色 AND / 跨角色 OR / 任一全部数据则放开」，不做可配置（Q19）；开了数据权限但主体无任何方案 = 查不到数据；行级策略覆盖所有读写路径，不只列表。
- **D6.5 字段权限（L5）**：读投影或脱敏，写白名单；yue 按元数据渲染，以服务端为准。
- **D6.6 每次决策可解释**（`{allow|deny, reason, matchedPolicies}`）并入 `qian.audit` 哈希链。
- **D6.7 权限不进令牌**：令牌只含身份与会话类 claim（规范集 `sub`、`tenant_id`、`membership_id`、`scope`、`identity_revision`、`sid`、`amr`、`acr`，预留 `profile`、`act`），不含权限集合（S2S 令牌的 `roles[]` 例外见 ADR-010 D10.4 / R24）；有效权限按主体缓存，以 `identity_revision` 递增 + 推送刷新即时失效；网关 `ForwardedClaimTypes` 白名单只转发 `identity_revision`，不转发权限集合。（2026-10-22 S1-DEC-027：授予的增、改、撤提交后，授权组件在同一调用内经身份端口 `BumpIdentityRevisionAsync` 递增受影响 user 主体在该租户的修订，并把服务端修订下限同步抬到新值。这一做法只在同进程宿主里生效，身份组件不在本进程即跳过。瞬时故障退避重试，总时长不超过 3 秒；仍失败记警告与计数，不回滚授权变更。`authz.role.changed.v1` 不扇出。非 user 主体与无成员身份的主体跳过。陈旧令牌回 401（gRPC Unauthenticated），客户端据此换令牌。拆分部署（platform / identity 两个 Pod）由身份侧订阅 `authz.grant.changed.v1`，随 S1-EVT-002 闭环，见 S1-IDP-012。）
- **D6.8 鉴权唯一入口**：UI、API、MCP 三入口同一个判定服务；网关 + 服务端双层强制；「端点 → 权限点」写进 OpenAPI 扩展并与能力清单共用；低代码数据引擎在唯一入口做「资源 + 动作 + 行级」三段式校验，不依赖设计器布尔开关与客户端传参；内部调用走服务身份。
- **D6.9 S3 可选 OpenFGA 插件**（`qian.authz.openfga`）处理资源实例共享与市场授权（L3 ReBAC）；行级过滤不交给远程引擎（ListObjects 默认截断 1000 条 / 3 秒）；网关粗粒度 ABAC（OPA）S3 可选。
- **D6.10 高级权限模式**（身份切换、组织角色、逐级递减、互斥 / 基数 / 先决约束）为 S3 可选；S1 在令牌预留 `profile` claim，权限模型按 L0–L6 分层留扩展位（Q10 补充裁决）。基数超限直接拒绝并返回结构化错误。
- **D6.11 配额与可见性口径**：平台超管不计入租户用户配额，租户管理员计入；平台超管对租户不可见，但审计可见（Q12 补充裁决）。
- **D6.12 统一主体模型** `user / service / agent` + 委托链字段（RFC 8693 `act`）S1 设计到位；S4 启用时 Agent 有效权限 = 委托人权限 ∩ 授予 scope；权限点带风险级别与 step-up 标记；危险操作 Agent 只能发起申请。
- **D6.13 MFA**：TOTP S1 必做（见 ADR-005 D5.3）；商密要求不做假设。

## 3 理由（Rationale）

- 端口在 Kun、实现在 Qian，兑现 Kun ADR 留下的重议条件，又不违背其「不做权限模型」的定位（基线 K §2.3）。
- 权限不进令牌避免令牌膨胀与权限变更后的「旧令牌仍有效」窗口；代价（查询压力）由按主体缓存 + `identity_revision` 解决（基线 S §6.2 第 4 条）。
- 本地编译谓词叠加 RLS，是动态表场景下唯一既有性能又有兜底的方案；远程 ReBAC 引擎有截止时间与条数限制，不能当行过滤器（基线 S §6.2 第 5 条）。
- 合并语义不做可配置：源码裁决的语义已可覆盖参考平台全部用例，可配置会让策略引擎与 Agent 的可解释性复杂化。

## 4 备选方案与放弃原因（Alternatives）

- **权限进令牌**：令牌膨胀、变更不即时、权限集合经网关透传到下游不可控。放弃。
- **OpenFGA / OPA 作为主引擎**：行级过滤性能与截断限制不可接受，且与 RLS 叠加困难；仅作 S3 适配插件。放弃作主引擎。
- **Casbin 类库内嵌**：模型表达力与可解释性不足以覆盖数据权限编译，且需要再造管理面。放弃。
- **鉴权分散在控制器**（参考平台）：可被客户端参数绕过，三入口不一致。明确反例。

## 5 后果与代价（Consequences）

- 正面：三入口一致；每次决策可解释、可审计，S4 的 `Explain` 直接作为 Agent 工具；数据权限对动态表与手写表一视同仁。
- 负面：权限模型从零自建，S1 与身份核心互为前置；按主体缓存的跨服务一致性是工程难点；数据权限编译器依赖 S2 元数据，S1 只能落契约。
- 配套：权限管理界面（S2）、授权界面含字段 / 数据（S2）、创建者自动 Owner、yue 权限渲染协议、决策审计查询。

## 6 对各阶段的影响

- **S1**：端口 + 租户 RBAC + 权限点清单登记 + API 强制校验 + 决策审计 + `identity_revision` 失效 + 数据权限契约与系统变量目录。
- **S2**：数据权限编译器、字段权限、授权界面、数据权限方案随发布自动生成。
- **S3**：OpenFGA 插件、OPA、高级权限模式、应用中心 / 后台权限区分。
- **S4**：Agent 有效权限 = 用户权限 ∩ scope；`Check / ListAllowedActions / Explain` 作为 MCP 工具。

## 7 待确认事项

- 等保 2.0 / 商密要求（Q18：不做假设，需确认）。
- 按主体缓存有效权限的存储与失效推送方案（Redis 背板）需技术验证。
- `ListAllowedActions` 在大租户、多角色下的性能基线需测。

## 8 参考

- 相关 ADR：ADR-003、ADR-004、ADR-005、ADR-009、ADR-011、ADR-016。
- PRD：`PRD §07`、`PRD §13`、`PRD §10`（Agent）。
- 基线：K §2.3、§7.1 第 1 条；S §3.4、§6.2 第 1/4/5 条、§9.3、§10 第 7/11 条；F §3.9、§3.10、§6 第 4 条、附录 C-1；P §5 权限判定行、§8.3 第 3/4 条、附录 A-4/A-14。
