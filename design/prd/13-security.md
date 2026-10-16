> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# §13 权限与安全

> 本页由构建脚本镜像自仓库 [`docs/prd/13-security.md`](https://github.com/idadawn/qian/blob/main/docs/prd/13-security.md)，请修改源文件，不要改本页。

> 状态：草案 v0（2026-10-06）。依据：ADR-004（数据库与隔离）、ADR-005（身份）、ADR-006（鉴权）、ADR-007（Apollo）、ADR-009（审计）、ADR-010（网关）、ADR-002（插件模型）、ADR-020（组件模型 v2）、ADR-016（iClaw 边界）、ADR-017（部署）、ADR-018（阶段）及补充裁决。本章是平台的**可信计算基**规范：描述目标态；Kun 当前缺口写成「过渡态（2026-10）」并集中在 §13.14。
>
> 四条总原则：
>
> 1. **身份 ≠ 授权**：身份记录结构上不携带授权载荷；授权来自判定端口 + 委托链 + 运行时授权快照。
> 2. **租户只来自令牌**：任何入口（HTTP、gRPC、消息、作业、Agent）都不信任请求头或默认值。
> 3. **缺省拒绝、确定性控制优先**：没有权限点声明的端点不可达；安全控制不依赖检测器或模型判断。
> 4. **一个权限引擎服务三个入口**：UI、API、MCP 使用同一判定服务；低代码数据引擎在唯一入口做三段式校验。

***

## 13.1 角色与主体模型

### 13.1.1 主体类型

| 主体类型      | 载体                                                                  | 凭据                                                         | 阶段              |
| --------- | ------------------------------------------------------------------- | ---------------------------------------------------------- | --------------- |
| `user`    | Person（全局自然人）+ Membership（租户成员）                                     | 密码 / 短信 / TOTP / 上游连接器断言                                   | S1              |
| `service` | 平台服务、平台插件（原称系统插件）、ISV `runtime.form: service` 组件、外部系统的 OAuth client | client_credentials；网关换签的 S2S 令牌；K8s ServiceAccount（工作负载身份） | S1              |
| `agent`   | 有归属人、可吊销的 Agent 客户端                                                 | RFC 8693 令牌交换，令牌带 `act` 链                                  | S1 字段预留 / S4 启用 |
| `system`  | 平台自身的定时任务、状态机迁移、迁移 Job                                              | 内部服务身份，审计 `actor_type=system`                              | S1              |

一个请求的审计主体链写法：`actor`（实际执行者）+ `on_behalf_of`（代表谁）+ `channel`（来源通道 UI / API / MCP / A2A / job / event）。人直接操作时 `actor = on_behalf_of`。

### 13.1.2 角色权限表

角色以租户为域（平台角色的租户为空）。「能做」是该角色内置权限点的上限，具体授权仍要经 §13.2 的 L1–L6 判定。

| 角色           | 域        | 能做                                                                                                  | 不能做                                                                  | 对租户的可见性                  | 阶段            |
| ------------ | -------- | --------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------- | ------------------------ | ------------- |
| 平台超管         | 平台       | 创建 / 冻结 / 注销租户；平台级配置与密钥引用；平台插件修订与回切；查看全平台审计；旁路租户权限（策略显式声明、每次审计）                                     | 以租户成员身份出现在业务数据中；绕过审计；读取密钥明文                                          | 对租户不可见，审计可见；不计入租户配额（Q12） | S1            |
| 平台运营         | 平台       | 租户主档 CRUD、开通白名单、套餐下发、管理员重置密码、运营看板、审核自助申请、市场审核台                                                      | 读取租户业务数据；修改平台插件；旁路租户权限                                               | 不可见                      | S1 最小 / S3 完整 |
| 平台开发者        | 平台       | 用 SDK 开发官方插件、出修订、跑 TCK、在开发者租户调试                                                                     | 直接向生产租户安装未签名制品；访问他人租户数据                                              | 仅开发者租户                   | S1            |
| ISV          | 平台（外部）   | 入驻、上传声明式表面、`package` / `function` / 受限 `ui` 与 seam Provider 组件（原 connector）、查看自己制品的审核与安装统计、席位与分成    | 上传任意代码容器（Q9）；访问安装方租户数据；修改已批准能力的描述或 Schema 而不触发复审                     | 不可见                      | S3            |
| 租户管理员        | 租户       | 组织 / 岗位 / 用户 / 角色 / 授权；挂载（租户 scope patch）已授权插件 / 应用并批准反向权限；租户级登录与密码策略、IdP 连接器（S3）；查看本租户审计；强制下线；工作交接 | 创建租户；跨租户操作；修改隔离策略；读取密钥明文；删除审计                                        | 本租户                      | S1            |
| 业务搭建者（租户开发者） | 租户       | 建应用、设计表单 / 列表 / 流程、发布、生成菜单与权限资源、配置数据接口（SQL 文本模式需额外高级权限）、打包导出 `template`                             | 授予自己没有的权限；绕过发布流程直接改线上元数据；配置出网域名（需管理员批准）                              | 本租户内其拥有 / 被授权的应用         | S2            |
| 终端用户         | 租户       | 使用被授权的应用、菜单、按钮、字段、数据范围；发起 / 处理审批；管理个人中心与偏好                                                          | 一切未授权项（缺省拒绝）                                                         | 本租户内授权范围                 | S1            |
| Agent 主体     | 租户（有归属人） | 委托人权限 ∩ 授予 scope 内的读与可逆写；发起申请类动作；查询 `Check / ListAllowedActions / Explain`                          | 标 `humanOnly` 的动作（签名、签章、转审、协办、撤销等 S4 前一律禁止）；破坏性动作（只能发起申请）；获取委托人以外的权限 | 继承委托人可见性                 | S4（字段 S1）     |
| 服务主体         | 平台 / 租户  | 清单声明的 `permissions.platform / data / egress / secrets` 范围；以自身工作负载身份调用其他能力                           | 直连其他插件的数据表；透传用户令牌；超出事件主题 ACL 发布 / 订阅                                 | 按租户上下文传播                 | S1            |

补充规则：

- 平台超管与租户管理员在策略中显式表达，不做代码 `IsAdministrator` 特判。
- 「使用应用」与「管理应用」是两类权限（S3 市场买家与管理员不同角色）。
- 创建者自动成为应用 Owner（L3 简版，S2）。
- 用户「删除」= 禁用 + 标记；SSO 同步删除只置禁用；基数超限直接拒绝并返回结构化错误，不静默截断。

***

## 13.2 权限七层模型 L0–L6

| 层                  | 对象                                           | 判定点（PEP）                                                                                                                                                 | 决策依据与存储                                                                 | 阶段                      |
| ------------------ | -------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------- | ----------------------- |
| **L0 租户隔离**        | 所有业务数据                                       | ① `qian.gateway` 验签后注入 `tenant_id`；② 每服务入口的租户解析中间件 / 消息订阅器 / Saga 执行器建作用域；③ EF 全局过滤 + 插入自动填充；④ PostgreSQL RLS（`FORCE ROW LEVEL SECURITY` + `WITH CHECK`） | 令牌 claim `tenant_id`；`tenancy.tenant`；每张业务表的 `tenant_id` 列与 RLS 策略      | S1                      |
| **L1 角色**          | 平台超管；租户 Owner / Admin / 开发者 / 使用者；自定义角色      | `qian.authz` 求值：有效角色 = 直接授予 ∪ 经组织 / 岗位 / 用户组获得                                                                                                           | `authz.role`、`authz.role_assignment`（主体类型 × 主体 id）                      | S1（职责分离约束 S3）           |
| **L2 功能权限**        | 应用、菜单、页面、按钮、API 端点、流程、打印等（资源类型由插件注册）         | 端点声明权限点 → ASP.NET Core 策略 / Mediator 行为 / gRPC 拦截器调用 `IPermissionChecker.Check`；缺省拒绝                                                                     | `authz.permission_definition`（清单派生）、`authz.grant`、`authz.resource_type` | S1                      |
| **L3 资源实例**        | 应用 / 表单 / 流程 / 报表 / 文件夹的所有者、编辑者、查看者、共享人；市场授权 | `qian.authz` 内置 ACL 表查询（S2）；OpenFGA 适配插件（S3，仅层级继承与共享，不做行过滤）                                                                                              | `authz.resource_acl`；OpenFGA 元组经 Outbox 同步                              | S2 可选 / S3              |
| **L4 数据权限（行级）**    | 列表、查询、导出、报表、API 的记录集                         | `IDataPermissionFilter` 把条件树 + 系统变量编译为 EF / SQL 谓词，在数据引擎唯一入口注入；与 L0 RLS 叠加；覆盖读、改、删、导出全部路径                                                                | `authz.data_scope_rule`（条件树 JSON、全部数据标志）；系统变量目录                         | S2（契约 S1）               |
| **L5 字段**          | 字段可见 / 可编辑 / 脱敏                              | 读：投影或按脱敏规则替换；写：白名单之外的字段拒绝；yue 按元数据渲染，以服务端为准                                                                                                              | `authz.field_policy`；字段敏感度标签                                            | S2                      |
| **L6 上下文条件（ABAC）** | IP、时间窗、端类型、套餐、数据密级、step-up、Agent 委派          | 作为条件挂在 L1–L4 规则上；Agent 有效权限 = 委托人权限 ∩ scope                                                                                                              | 规则 JSON 中的 `conditions[]`；令牌 `acr / amr / act / profile`                | S1 部分（IP、端、step-up）/ S4 |

### 13.2.1 求值规则

1. 功能权限：`Check(principal, action, resource)` 缺省拒绝；权限点 = 有效角色授权的并集。
2. 数据权限合并（补充裁决 Q19，不做可配置）：同一角色内多个方案 **AND**；不同角色之间 **OR**；任一方案为「全部数据」则整体放开；资源开启了数据权限但主体无任何方案 → 查不到数据；未开启 → 不过滤（仍受 L0）。
3. 多表：数据权限条件按表拆分条件组；主表直接字段名，副表 / 子表「表名.字段」。
4. 端维度：菜单与权限点带 `terminals`，PC 只读取含 PC 入口的授权，移动端同理。
5. 身份（权限分区，S3 可选）：有效对象 = 身份所挂对象 ∩ 自身拥有对象；当前身份进令牌 `profile`，不按终端持久化。
6. 逐级递减（S3 可选）：下级可授范围 ⊆ 上级授权 ∪ 上级角色授权；授权人只能授出自己拥有的，其余置灰不隐藏。
7. 「双重门」：按钮 / 列 / 字段既要在设计器勾选显示、又要被授权；但鉴权**不依赖**设计器勾选与客户端传参（`menuId / flowIds` 之类一律视为提示），资源化 ACL 在数据引擎唯一入口执行。
8. 判定结果缓存按主体，以 `identity_revision` 失效；跨服务一致。
9. 每次判定产出可解释结构 `{ allowed, matchedRules[], missingPermissions[], evaluatedAt, identityRevision }`，拒绝时随结构化错误返回给调用方（不泄露他人授权），并进审计。

***

### 13.2.2 判定端口语义

`Kun.Authorization` 只定义端口，`qian.authz` 提供内置实现，OpenFGA / OPA 为 S3 可选适配插件。端口语义如下，所有入口（ASP.NET Core 策略、Mediator 行为、gRPC 拦截器、数据引擎、MCP 门面）只能经它们判定，不得自行读取授权表：

| 方法                              | 输入                            | 输出                                                                    | 用途                     |
| ------------------------------- | ----------------------------- | --------------------------------------------------------------------- | ---------------------- |
| `Check`                         | 主体、动作、资源引用、上下文（端、IP、时间、`acr`） | `{ allowed, matchedRules[], missingPermissions[], identityRevision }` | 单次判定；拒绝时结构体随错误返回       |
| `BatchCheck`                    | 同上的数组                         | 逐项结果                                                                  | 列表页按钮矩阵、菜单裁剪，避免 N 次往返  |
| `ListAllowedActions`            | 主体、资源引用                       | 允许的动作集合                                                               | 前端按状态渲染合法按钮；Agent 先查后调 |
| `ListPermissions`               | 主体、可选 `profile`               | 有效权限点集合与来源                                                            | 权限透视；S1 可选             |
| `Explain`                       | 主体、动作、资源引用                    | 命中链（角色 → 授权 → 规则 → 条件）与缺失项                                            | 排障；S3 UI、S4 作为工具       |
| `IDataPermissionFilter.Compile` | 主体、资源引用、目标表集合                 | 谓词 AST 或 EF 表达式                                                       | 数据引擎唯一入口注入             |
| `IFieldPolicy.Resolve`          | 主体、资源引用、字段集合                  | 每字段 读 / 写 / 脱敏规则                                                      | 读投影与写白名单               |

缓存由实现负责：按主体缓存有效角色与权限点，以 `identity_revision` 作版本号；`Compile` 结果按（主体、资源、规则版本）缓存。端口调用必须带 `tenant_id` 作用域，跨租户判定（平台运营）走显式的平台作用域并审计。

***

## 13.3 令牌与会话

### 13.3.1 令牌内容

**用户访问令牌**（`qian.identity` 签发，非对称签名，短时）：

```
iss   规范 issuer（每环境一个）
sub   person_id
aud[] 资源标识（RFC 8707 resource）
tenant_id, membership_id
scope           OAuth scope 串
identity_revision  整数，权限 / 身份变更即递增
profile?        当前身份（S3 可选，S1 预留）
acr / amr       认证强度与方式（step-up 判定）
act?            委托链（S4，RFC 8693）
sid, iat, nbf, exp, jti
```

**不进令牌的东西**：权限点、角色列表（除 S2S 中的粗粒度 `roles[]`）、组织归属、任何业务字段。理由：权限变更即时失效、令牌体积、以及「令牌被当作权限载体」会让所有服务各自解释权限。

**选择令牌**：命中多个 Membership 时签发 5 分钟、只能换正式令牌的受限令牌。

**S2S 令牌**（`qian.gateway` 换签下行，与 iClaw 契约同形、各签各的，Q15）：

```
iss / aud[]（每服务一个）/ sub / tenant_id / project_id? / roles[]（粗粒度角色代码）/ scope / session_id / iat, nbf, exp, jti
exp − iat ≤ 300s（网关换签默认 60s）
plugin_id（服务主体为插件时）
```

新能力授权 = 增加 scope 串，不新增 claim。下游每一跳重新验签；禁止把上游令牌透传给下游（混淆代理防线）。`roles[]` 是与 iClaw 同形的例外：只放粗粒度角色代码，不是权限点集合；Qian 服务不得仅凭它放行，必须调用 `Kun.Authorization` 决策端口，时效性由 `identity_revision` 管；`project_id` 为可选 claim，Qian 只透传不解释（R24）。

### 13.3.2 会话

| 规则          | 内容                                                                           |
| ----------- | ---------------------------------------------------------------------------- |
| 两类会话        | SSO 会话（`qian.identity` 持有，sid cookie 不透明）与应用会话（各前端 / 插件）分离                   |
| refresh     | 设备级 refresh 令牌、轮换、重用检测即吊销整族                                                  |
| 索引          | 服务端会话表 + 用户 → 会话索引，可按用户、按租户、按设备吊销                                            |
| 登录策略（租户可覆盖） | 单端登录（同端类型后踢先，PC / 移动分别判定）或并发上限 2–10（默认 5）；空闲 / 绝对超时；IP 白名单（超管例外须策略显式）；上次登录提示 |
| 密码策略        | 强度与最小长度；锁定阈值与方式（锁账号 / 延时）；历史密码禁用最近 N 个；周期更新与站内信提前提醒；初始密码强制修改（不可取消）           |
| MFA         | TOTP S1；短信 / 邮箱 OTP、Passkey、恢复码 S3；租户级强制策略；敏感操作 step-up 以 `acr / amr` 判定     |
| 登录流水线       | 密码、第三方、扫码、企业 IdP 最终构造同一「登录输入」走同一签发路径，统一审计、限流与单端策略                            |
| 日志          | 只记 sid 哈希，不记明文 sid 与令牌                                                       |
| CSRF        | Cookie 方案上线即带 CSRF 防护与安全响应头                                                  |

### 13.3.3 会话失效触发清单

以下任一事件发生 → 服务端会话失效（或 `identity_revision` 递增）+ 向在线客户端推送刷新 / 登出：

1. 用户改密、重置密码（含管理员重置）
2. 用户被禁用、锁定、注销
3. 租户进入宽限 / 只读 / 冻结 / 注销，或被运营方 Suspend
4. 管理员对该用户 / 该租户执行强制下线
5. 用户的角色、授权、组织 / 岗位归属变更（`identity_revision` 递增并推送，≤ 3 秒内对新请求按新权限执行；令牌本身可继续用到刷新）
6. 第三方身份解绑、上游连接器被禁用（对应会话吊销）
7. refresh 令牌重用检测命中
8. 并发上限触发（踢最早会话）
9. 后通道登出：上游 IdP 发起（入站）或 Qian 发起向下游应用传播（出站），双向

***

## 13.4 纵深防御分层

```
┌──────────────────────────────────────────────────────────────────────────┐
│ 0. Traefik（北向公网入口）                                                  │
│    TLS 终止、HTTP 安全头、连接级限流、WAF 规则（可选）                        │
│    不做：鉴权、租户解析、业务路由                                            │
├──────────────────────────────────────────────────────────────────────────┤
│ 1. qian.gateway（Kun.Gateway 加固；业务 API 边界）                           │
│    强制认证（只信任 qian.identity 单 issuer）、aud 校验、identity_revision   │
│    白名单转发、路由级限流、可信代理、私钥热轮换、Cookie 会话换 60s S2S、      │
│    「插件未挂载」路由拦截（R32）、匿名路由只放行标「匿名可用」的能力（S3 外链）  │
│    不做：业务权限判定、行级过滤                                              │
│    旁路：Higress 只承载模型与 MCP 流量，对租户无感，不做 SSO                  │
├──────────────────────────────────────────────────────────────────────────┤
│ 2. 服务 PEP（每个 Kun 服务 / 平台插件入口）                                   │
│    重新验签 S2S、租户作用域建立（fail-closed）、端点权限点 → IPermissionChecker │
│    .Check、Mediator 授权行为、gRPC 一元 / 流式拦截器、命令 Schema 校验、       │
│    幂等键、dryRun；非 HTTP 入口（消费者 / Saga / Outbox / 作业）按消息或实例的  │
│    tenant_id 回灌作用域                                                     │
├──────────────────────────────────────────────────────────────────────────┤
│ 3. 数据引擎唯一入口（qian.dynamic-data，S2；S1 为 qian.data 的仓储层）         │
│    「资源 + 动作 + 行级」三段式：L2 资源动作 → L4 谓词注入 → L5 字段投影 /       │
│    写白名单；Filter AST → 参数化 SQL + 标识符白名单；强制租户与软删谓词；        │
│    值翻译按需批量；数据日志同事务                                             │
├──────────────────────────────────────────────────────────────────────────┤
│ 4. PostgreSQL RLS（最后一道）                                              │
│    FORCE ROW LEVEL SECURITY + USING / WITH CHECK (tenant_id =               │
│    current_setting('app.current_tenant_id', true))；                       │
│    每服务独立 schema 与数据库角色；                                       │
│    显式写入他租户 id 被 WITH CHECK 拒绝；事务外读返回空视为缺陷而非兜底         │
└──────────────────────────────────────────────────────────────────────────┘
```

设计意图：任一层被绕过，下一层仍拒绝。例如客户端省略 `menuId` 不会导致不裁列（第 3 层不信客户端），迁移脚本忘记租户过滤会被第 4 层挡住，消费者忘记建作用域会在第 2 层 fail-closed 而不是落到默认租户。

各层的失败模式与处置：

| 层              | 典型失败                                  | 处置                                         |
| -------------- | ------------------------------------- | ------------------------------------------ |
| Traefik        | 证书过期、安全头缺失                            | 可观测告警；不影响鉴权正确性                             |
| `qian.gateway` | 配置了多 issuer 过渡开关未关闭；`aud` 未校验；限流未挂路由  | 生产环境以「强制装配」替代 opt-in；配置项由 Apollo 校验器拒绝不安全值 |
| 服务 PEP         | 新端点忘记声明权限点；流式方法绕过拦截器                  | C3 / C4 门禁扫描 100% 端点；缺省拒绝使漏声明表现为 403 而不是放行 |
| 数据引擎           | Filter AST 透传客户端 `tenant_id`；标识符未走白名单 | 契约 lint 拒绝租户字段；SQL 生成只接受编译后模型中的标识符         |
| RLS            | 新表未建策略；事务外读到空集被当成「没数据」                | C8 门禁；事务外读在集成测试中断言为缺陷                      |

非 HTTP 入口单独说明：消息消费者按消息信封 `tenant_id` 建作用域，缺失即进死信并告警；Saga 执行器按实例 `TenantId`；定时作业按作业定义的租户范围逐租户执行；Agent 调用经 MCP 门面时租户来自令牌，`inputSchema` 中不存在租户字段。

***

## 13.5 需二次确认 / 人工审批的场景

「二次确认」= 同一操作者在 UI 再次确认（可要求 step-up）；「人工审批」= 另一主体批准后才执行；「confirm-required」= Agent 发起时平台返回 `input_required`，由人确认后继续；「禁止」= 对 Agent 返回结构化拒绝。

| 场景                                                        | 触发者          | 人操作                           | Agent 行为                                              | 阶段         |
| --------------------------------------------------------- | ------------ | ----------------------------- | ----------------------------------------------------- | ---------- |
| 冻结 / 注销租户、触发数据销毁                                          | 平台运营 / 超管    | 二次确认 + step-up + 人工审批（第二运营人员） | 禁止（只能发起申请）                                            | S1         |
| 修改租户隔离策略、租户号                                              | —            | 不允许（创建后不可改）                   | 禁止                                                    | S1         |
| 批量强制下线、清空缓存（影响全员）                                         | 管理员 / 运维     | 二次确认，标「影响全员」                  | confirm-required                                      | S1         |
| 重置他人密码、解绑他人第三方身份                                          | 租户管理员        | 二次确认；通知当事人                    | 禁止                                                    | S1         |
| 平台超管旁路租户权限                                                | 超管           | 策略显式 + 每次审计 + 二次确认            | 禁止                                                    | S1         |
| 安装 / 升级插件并批准反向权限（platform / data / egress / secrets / ui） | 租户管理员        | 逐项勾选批准；升级新增项即重批               | confirm-required（展示完整权限 diff）                         | S1         |
| 平台插件修订激活 / 回切                                             | 平台超管         | 二次确认 + 变更记录                   | 禁止                                                    | S1         |
| 修改 Apollo 公共 namespace、密钥引用指向                             | 平台运维         | 发布审核（Apollo 自带）+ 审计           | 禁止                                                    | S1         |
| 删除被引用的模板 / 字典 / 流程定义                                      | 搭建者          | 引用保护：列出引用方后方可强制               | confirm-required                                      | S1         |
| 导出全量数据、导出含 pii / finance 字段                               | 用户 / 管理员     | 二次确认；审计导出范围与行数                | confirm-required；对外发送类默认需确认                           | S2         |
| 数据接口 SQL 文本模式保存与发布                                        | 搭建者（高级权限）    | 只读账号、限额、审计；发布需管理员批准           | 禁止                                                    | S2         |
| Schema Migrator 执行迁移计划（含收缩步骤）                             | 搭建者 → 管理员    | 审核计划 → 分批执行；收缩步骤单独确认          | 禁止（只能产出计划）                                            | S2         |
| 破坏性命令（`sideEffect: destructive`，如清空表、批量删除、全表更新）           | 任何           | 二次确认 + 试运行 + 影响行数预估           | 二次确认或禁止（默认策略 deny）                                    | S1 分级 / S2 |
| 流程：转审、协办、撤销申请、终止、签名 / 签章                                  | 审批人          | 按动作前置条件；签名类 `humanOnly`       | `humanOnly` 一律禁止（Q26）                                 | S2         |
| 流程：同意 / 拒绝 / 退回（Agent 代办）                                 | 委托人授权的 Agent | —                             | confirm-required，流转记录 `actor=agent, on_behalf_of=委托人` | S4         |
| 向外部系统发送（邮件 / 短信 / Webhook / 连接器写）                         | 任何           | 按渠道策略                         | 对外发送类工具默认 confirm-required                            | S2         |
| 开放出网域名、新增连接器凭据                                            | 管理员          | 二次确认；凭据只填 secretRef           | 禁止                                                    | S1         |
| 租户自助申请审核、ISV 上架审核                                         | 运营           | 人工审批（可配自动规则）                  | 禁止                                                    | S3         |
| 费用与席位变更、套餐降级                                              | 租户 Owner     | 二次确认 + 账单预览                   | 禁止                                                    | S3         |

***

## 13.6 插件安全

### 13.6.1 信任等级 × 允许形态 × 沙箱 × 签名

| `policy.tier` | 谁                                        | 组合包 / 声明式表面 / 无代码组件              | 允许的 `runtime.form`（代码部分；原 `kind`）                                                                   | `policy.sandbox` 与 hooks `type`                                        | 审核与签名                          | 运行时身份                                                 |
| ------------- | ---------------------------------------- | -------------------------------- | --------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------- | ------------------------------ | ----------------------------------------------------- |
| `system`      | 平台团队（平台插件 = `platform` profile base 组合包） | 均可                               | `module` / `service` / `package` / `function` / `ui`（seam Provider 组件按实现取 `module` / `service`，R28） | `none`（MF2；`service` 为 `workload`）；hooks `command` / `http` / `plugin` | 平台密钥签名，随发行版（profile）发版         | 宿主进程身份；`policy.required: true`（原 `alwaysEnabled`，R34） |
| `official`    | 平台团队业务插件                                 | 均可                               | 同上                                                                                                  | 同上                                                                     | 同上                             | 同上或独立 `service`                                       |
| `partner`     | 认证 ISV                                   | 均可                               | `package` / `function` / `ui`；不允许 `module` 进共享进程；`service` 仅在 S3 沙箱策略另行开放（`workload`）               | `ui`：`worker`（审核后可 `none`）；`function`：`wasm`；hooks `http` / `plugin`   | 发布者认证 + 扫描 + TCK + 人工审核；cosign | 独立工作负载身份                                              |
| `private`     | 租户自建                                     | 均可                               | `package` / `function` / `ui`；不允许 `module`、`service`（`service` 仅在 S3 沙箱策略另行开放）                      | `ui`：`worker \| iframe`；`function`：`wasm`；hooks `http`                 | 租户内可见；自动 TCK；租户密钥签名            | 独立工作负载身份                                              |
| `community`   | 公开社区                                     | 均可；**S1 只接受无代码组件**（`runtime` 为空） | 同 `private`（S1 不接受任何代码部分）                                                                           | 同 `private`；hooks `http`                                               | 自动扫描 + TCK；「未认证」标识             | 不进任何共享进程                                              |

规则：

- **第三方代码永不进共享进程**；组合包与声明式表面对所有 tier 开放（应用 = 组合包 + marketplace 条目，ISV 应用即 `policy.tier: partner` 的组合包，R28；原「`bundle` 对所有 tier 开放」R22 据此改读），按 tier 收紧的是组合包内各组件的 `runtime.form` 与 `policy.sandbox`：`partner / private / community` 的组件不得使用 `module`，`service` 仅在 S3 沙箱策略另行开放；校验器与装载器（Resolve 阶段拒绝为非 `system / official` 组件加载程序集）两道检查互为冗余（specs/component-model.md §11.1）。
- **默认强制验签**：未签名或签名不符直接安装失败，不是警告。签名在清单外：代码制品 = 清单 + 文件摘要 + SBOM 打成 OCI 制品，cosign 出独立签名与证明；元数据包与 SKILL.md 用「文件 URI + SHA-256 + 字节数」清单 + 分离签名；市场文件 `.qian/marketplace.json` 用 Ed25519 分离签名，公钥须命中客户端信任根（specs/component-model.md §11.5）。
- **按 digest 引用**：清单里只接受 `@sha256:`；标签引用安装失败。
- **审核分级**：免费元数据模板快审；付费或带代码走安全审查；权限扩大、付费模式变更触发复审。
- **工具描述哈希钉死**（S3）：注册表保存已批准能力的「名称 + 描述 + 规范化 inputSchema」哈希，变更即下线待复审；`schemaSha256` 变即旧授权失效。
- **发布者撤销与黑名单**：命中即自动卸载并通知租户管理员。
- **未验签市场条目只允许无代码组件**（R40）：未签名市场或未命中信任根的条目需 `--allow-unverified` 才参与 `qian add` 解析，且只允许安装无代码组件（`runtime` 为空）、tier 最高 `community`；`qian list` 持续标记 `unverified`。
- **hooks `command` 类型仅限 `system / official`，argv 不经 shell**（R41）：`hooks.json` 的 `command` 是 argv 数组，不经 shell 解释（无管道、重定向、通配），在受限执行器中运行（工作目录为组件目录、环境变量白名单、默认超时 30s / 上限 600s、无网络除非 `policy.egress` 非空）；平台宿主默认关闭 `command` 执行，`platform` profile 需显式开启，`gateway` / `identity` profile 拒绝开启；`commands/*.md` 的内联 shell 遵守同一规则并须被 `allowed-tools` 逐条覆盖，`community / private` 组件出现内联 shell 即校验失败。
- **连接器 = 为某个 seam 注册 Provider 的组件**（R28 / R38）：`connector` 不是 `runtime.form`；清单上 `provides[]` 写「注册表键:名称」（如 `Identity.Connectors:oidc`）向该 seam 的注册表子服务按名称注册，`policy.egress[]` 声明允许的出网目标（为空 = 默认拒绝出网），`runtime.form` 按实现取 `module` 或 `service`；出网只经出口代理，白名单来自 `policy.egress`（§13.8）。

### 13.6.2 反向权限审批

清单 `policy.permissions` 段（原 `permissions`，R37）声明插件向平台申请什么，租户管理员挂载时逐项批准，升级新增即重新批准：

| 段                                                                                 | 内容                                                     | 运行时强制点                                                                           |
| --------------------------------------------------------------------------------- | ------------------------------------------------------ | -------------------------------------------------------------------------------- |
| `platform[]`                                                                      | 如 `tenant:read`、`user:read`、`file:write`、`notify:send` | 服务 PEP 以插件主体身份判定                                                                 |
| `data[]`                                                                          | `{ entity, access: r / rw }`                           | 数据引擎唯一入口；每插件独立 PG schema 与数据库角色，不得直连他人表                                          |
| `policy.egress[]`（原 `permissions.egress`，R28 升为 `policy` 顶层字段；seam Provider 组件必填） | 允许出网目标 `<host>[:<port>]`；为空 = 默认拒绝出网                   | 生成 NetworkPolicy / 出口网关规则；私网与云元数据地址默认阻断；`hooks.json`、`.mcp.json` 的 `url` 域名必须在其中 |
| `secrets[]`                                                                       | `{ name, scope }` 只有名字                                 | `ISecretResolver` 在使用点解析，永不回传明文                                                  |
| `ui.connectSrc[]`                                                                 | 前端允许连接的源                                               | 生成 CSP `connect-src`；MF2 远程只能从白名单加载                                              |

与之相对，`metadata/permissions.json`（原 `provides.permissions`，R37）是插件定义的、可被授予他人的权限点；两者都由 `qian.authz` 登记。

### 13.6.3 运行时强制点

- `runtime.form: service` 组件：独立 K8s ServiceAccount + 含 `plugin_id` 的 S2S 令牌；每插件独立 Deployment、ResourceQuota、default-deny NetworkPolicy + 清单派生的放行规则；健康 = K8s 状态 + `/health` 双条件。
- `function`（Wasm）：燃料、内存、超时限额来自清单 `runtime.limits`；无网络与文件系统能力，除非清单声明并被批准。
- 事件：发布 / 订阅主题 ACL 来自 `metadata/events.json`（原 `provides.events`，R37）；越权主题在总线层拒绝。
- 前端：`system / official` 以 MF2 挂载并共享 singleton 依赖；其余按 tier 走 Worker 远程渲染（白名单组件目录 = yue manifest）或 iframe（无 `allow-same-origin`，postMessage 握手）。
- 「未挂载」（租户 scope 未挂载，或被 `restrict` 过滤）在网关路由、SDK 入口、前端外壳三处同时拦截，一律 404、与不存在无法区分（管理面 403 原因码 `PLUGIN_NOT_MOUNTED` 等，R32 / R48）。

***

## 13.7 密钥与配置安全

| 规则         | 内容                                                                                                               | 阶段    |
| ---------- | ---------------------------------------------------------------------------------------------------------------- | ----- |
| 唯一来源       | 密钥只能来自 K8s Secret（或后续的 Infisical 运行时解析）；配置、清单、事件、日志、审计中只出现 `secretRef{ name, backend, version }`                 | S1    |
| Apollo 边界  | 只存平台 / 服务级非敏感配置与 secretRef；租户级配置不进 Apollo；Portal 接运维侧 Authentik OIDC、不公网；发布事件进 `qian.audit`；webhook 扫描拦截疑似密钥键名与值 | S1    |
| 配置 Schema  | 插件配置以 JSON Schema 2020-12 声明，`sensitive: true` 字段只能填 secretRef；取值范围进 Schema；运行期变更先校验、失败保留上一份有效值并告警               | S1    |
| K8s Secret | K3s 开启静态加密（`--secrets-encryption`）；仓库只存模板 / 键名 / secretRef；Secret 以文件形式挂载（KeyPerFile），不进环境变量明文日志                 | S1    |
| Infisical  | 现状为「纸面权威」；补 Operator / ESO 或应用内 `ISecretResolver` 解析后才算运行时权威 —— **过渡态（2026-10）**                                 | S1 评估 |
| 轮换         | `qian.identity` JWKS 轮换（新旧并存窗口）；`qian.gateway` S2S 私钥热轮换；连接器凭据按 `version` 轮换，旧版本保留一个过渡窗口                         | S1    |
| 凭据回传       | 任何详情接口不回传密钥 / 密码 / 连接串明文；参考平台「数据库连接密码可回传还原」是反例                                                                   | S1    |
| 服务间信任      | 控制面 ↔ 数据面走 S2S 令牌（mTLS 可选）；回调（调度、外部任务、Webhook）带签名或令牌 + 幂等键 + 超时 + 租户路由；不接受「请求头存在即信任」                             | S1    |
| 对称密钥       | 不使用共享对称密钥作跨服务信任；不存在带默认值的 AES Key                                                                                 | S1    |
| 开发环境       | 测试凭据只存项目种子 / 示例配置文件；mock 模式强制关代理                                                                                 | S1    |

***

## 13.8 数据安全

| 主题          | 规则                                                                                                                                                       | 阶段                                                              |
| ----------- | -------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------- |
| 字段敏感度       | 元数据字段带 `sensitivity: public / internal / pii / finance / secret`；清单 `resources[].sensitivity` 同义；查询、导出、日志、MCP resources 据此脱敏或拒绝                          | S2（标签）/ S1（契约）                                                  |
| 脱敏规则目录      | 姓名 / 手机 / 邮箱 / 身份证 / IP / 车牌 / 银行卡等规则可枚举、带 id 与参数 Schema；前后端共用一份；服务端脱敏为准，前端只负责展示                                                                         | S1                                                              |
| 导出审计        | 每次导出记录主体、资源、过滤条件、字段列表、行数、文件哈希；含 pii / finance 的导出需二次确认；导出文件走预览票据，不出匿名 URL                                                                                | S2                                                              |
| 数据保留与销毁     | 租户注销 = 软删 + 保留期（清单 `dataRetentionDays`，平台默认 30 天，合同可覆盖）+ 到期销毁任务 + 销毁记录；文案与行为一致（不把软删叫「永久删除」）                                                              | S1 最小 / S3 完整                                                   |
| 用户注销        | 注销 = 停用 + 脱敏 + 数据导出（PIPL）；管理员禁止自助注销；SSO 同步删除只置禁用                                                                                                         | S3                                                              |
| 软删与回收站      | 业务记录软删为默认；破坏性命令带试运行与影响行数预估；元数据版本可回滚                                                                                                                      | S1                                                              |
| 数据日志        | 新建 / 编辑 / 删除 / 导入写字段级差异，同事务；不覆盖删除与导入的做法是反例                                                                                                               | S1 schema / S2 接入                                               |
| 数据接口 SQL 文本 | 只读数据库账号由平台供给（平台 PG 中每租户绑定 RLS 的只读角色，运行时按租户切换）、语句超时、行数上限、参数化、禁止多语句、审计；高级权限 = 独立权限点 `qian.data-api:sql:author`（默认不在开发者角色内，租户管理员显式授予）；发布需租户管理员批准（R11 / R25） | S2                                                              |
| 出网与 SSRF    | 连接器 / Webhook / 任意 URL 代理一律经出口代理：域名白名单（清单 `egress`）、阻断私网与云元数据地址、不携带用户令牌外发                                                                                | S1（平台自有出网点：OIDC 发现、企业微信、SMTP、短信、Apollo、对象存储）/ S2（低代码连接器与第三方出网点） |
| 备份与恢复       | 专属 PG 的备份加密、恢复演练纳入阶段退出准则（§14）；高隔离档独立备份                                                                                                                   | S1                                                              |
| 多租户缓存 / 存储  | 缓存键与对象存储路径带租户前缀；共享 Redis 不与 L1 平台组件混用 `allkeys-lru` 实例                                                                                                   | S1                                                              |

***

## 13.9 审计

| 规则    | 内容                                                                                                                                                           |
| ----- | ------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| 只有一套  | 命令 / 能力级 `AuditEntry` 哈希链（`prev_hash → hash`），链状态持久化在 PostgreSQL；实体级 `AuditLog` 与业务同事务写入并经 Outbox 发事件；弃用事后不同事务的拦截器路径                                         |
| 主体链   | `actor_type ∈ { user, service, agent, system }`、`actor_id`、`on_behalf_of`（Membership / Person）、`act_chain`（S4）、`channel ∈ { ui, api, mcp, a2a, job, event }` |
| 能力定位  | `capability_id`、`contract_version`、`plugin_id`、`input_hash`（规范化输入 SHA-256，不存明文敏感参数）、`idempotency_key`、`dry_run`                                              |
| 结果    | `outcome ∈ { success, denied, failed, partial }`、`error_code`、受影响资源引用（URN）、行数                                                                                |
| 判定审计  | 每次 `Check` 的 `{ matchedRules, missingPermissions, identityRevision }`；超管旁路单独类型                                                                               |
| 登录审计  | 成功 / 失败、grant 类型、连接器、IP 归属、UA、设备；只记 sid 哈希                                                                                                                   |
| 来源自动化 | 由能力契约的 `audit.emits` 与命令元数据自动记录，不靠开发者手工加特性                                                                                                                   |
| 保留与查询 | 按租户与类型配置保留天数；**不提供「一键清空」**；`IAuditReader` 分平台 / 租户两种视角；跨租户只对平台超管可见                                                                                           |
| 完整性   | 哈希链 100% 可验证是阶段退出准则（§14）；断链告警                                                                                                                                |
| 可观测衔接 | `trace_id` 贯通；请求 / 异常日志进可观测栈，不落业务库                                                                                                                           |

***

## 13.10 前端安全硬线

1. **不 `eval`**：不用 `eval` / `new Function` 执行租户脚本；联动三层（声明式动作 → 可序列化表达式 DSL → QuickJS 沙箱，白名单 API、超时、内存上限、不暴露令牌）。
2. **令牌不进 URL / iframe**：嵌入页面、大屏、报表、外链、文件预览用一次性票据或 postMessage 握手；禁止 URL 带 token。
3. **CSP 由清单生成**：`connect-src / frame-src / script-src` 来自清单 `permissions.ui` 与 MF2 远程白名单；不可信 UI 走 iframe sandbox（无 `allow-same-origin`）或 Worker 远程渲染。
4. **前端裁剪只是体验层**：菜单 / 按钮 / 列 / 字段隐藏只影响体验；服务端缺省拒绝是唯一防线；`auth` 是引擎一等字段但不参与服务端判定。
5. **不在前端持有加密密钥**；不把接口体加密当安全。
6. **不持久化敏感字段**：不把 access token、锁屏密码写入 localStorage 明文；refresh 令牌只在安全存储（HttpOnly cookie 或设备安全区）。
7. **第三方 UI 零隔离即零信任**：`partner` 以下的 UI 不直接 MF 挂载。
8. **可寻址但不泄露**：`data-testid / data-slot` 不包含业务数据。
9. **依赖供应链**：锁定版本、publint、SBOM；重型物料独立包懒加载。

***

## 13.11 Agent 相关威胁与防线

| 威胁                     | 平台防线（确定性控制优先）                                                                                             | 阶段                    |
| ---------------------- | --------------------------------------------------------------------------------------------------------- | --------------------- |
| 提示注入（直接 / 间接，含工具描述藏指令） | 所有外部内容与工具返回视为不可信数据；摄入不可信内容后对后续动作降权（高风险动作强制人工确认）；不依赖检测器                                                    | S4（S1 起副作用分级就位）       |
| 工具投毒 / rug pull        | 已批准工具「名称 + 描述 + 规范化 schema」哈希钉死，变更即下线复审；Skills 清单摘要校验；`schemaSha256` 变即授权失效                               | S3                    |
| 越权 / 混淆代理              | 令牌受众绑定 + 禁止令牌透传；下游各跳重新验签；最小 scope 与逐步提权；按调用者权限过滤工具列表；句柄绑定主体；Agent 只拿委托人权限子集                               | S1（令牌与权限）/ S4（工具列表过滤） |
| 数据外泄                   | 沙箱与连接器出网白名单 / 出口代理；字段敏感度驱动脱敏；敏感参数不入 HTTP 头；对外发送类工具默认需确认                                                   | S2 / S4               |
| 破坏性操作 / 失控循环           | 副作用分级 + 默认策略（read 放行、write 确认、destructive 二次确认或禁止）；`dryRun`；软删与回收站；元数据版本回滚；调用频率上限；急停开关（按 Agent / 租户 / 工具） | S1（分级）/ S4（急停）        |
| 成本失控                   | Higress token 限流与缓存；租户 / 应用 / Agent 预算与告警；工具列表确定性排序提高缓存命中                                                 | S3                    |
| 供应链                    | 插件签名与哈希、市场条目验签（未验签只允许无代码组件，R40）、命名空间验证、安装时展示完整权限；沙箱默认最小权限；按 digest 引用                                     | S1                    |
| 第三方插件代码                | 永不进共享进程；独立工作负载身份；独立 schema 与数据库角色；事件主题 ACL；出口 NetworkPolicy；CSP 由清单生成；Wasm 限额                             | S1 规范 / S3 对第三方生效     |
| 身份冒用（Agent 冒充人）        | Agent 主体独立 client 类型、有归属人、可吊销；`act` 链进令牌；审计 `actor_type=agent`；`humanOnly` 动作服务端拒绝                        | S1 字段 / S4            |
| 合规                     | AI 生成内容显式 / 隐式标识；模型路由按数据驻留策略；审计留痕                                                                         | S4                    |

***

## 13.12 合规占位

| 项                     | 现状                     | 对设计的影响                                                             | 待确认人  |
| --------------------- | ---------------------- | ------------------------------------------------------------------ | ----- |
| 等保 2.0                | 未确认是否有定级要求             | 日志保留 ≥ 6 个月、登录策略、密码策略、审计不可删除——本章设计已满足常见三级要求的技术侧                    | 产品负责人 |
| 商用密码（SM2 / SM3 / SM4） | 不做假设（Q18）              | 若要求：令牌签名算法、哈希链算法、TLS 套件需支持国密；OpenIddict 需扩展签名提供方                   | 产品负责人 |
| PIPL / 个人信息保护         | 设计已含自助注销、数据导出、字段敏感度、脱敏 | 隐私政策链接进清单 `links.privacyPolicy`；数据驻留按租户标注                          | 法务    |
| AI 生成内容标识             | S4                     | 审计 `createdBy.kind=agent`；UI 显式标识；元数据记录 `generatedBy / promptHash` | S4 前  |
| 私有化 / 离线交付            | 假定有（Q3）                | 影响第三方组件许可证（已避开 AGPL / 商业许可）；签名根密钥分发方案                              | 产品负责人 |
| 数据跨境                  | 未知                     | 若有境外租户：模型路由与存储区域策略                                                 | 产品负责人 |

***

## 13.13 关键表 DDL 片段（示意）

PostgreSQL 16；只列安全相关字段，省略索引与注释；`row_version` 用于乐观锁。每张业务表遵循「`tenant_id` + RLS」约定，平台表（如 `tenancy.tenant`）不启用 RLS 但只对平台服务角色可见。

```sql
-- 租户（控制面，平台表，不启用 RLS）
CREATE TABLE tenancy.tenant (
  id              uuid PRIMARY KEY,
  code            text NOT NULL UNIQUE,                    -- 租户号：小写字母数字，创建后不可改
  name            text NOT NULL,
  isolation       text NOT NULL CHECK (isolation IN ('shared','schema','database')),  -- 创建后不可改
  status          text NOT NULL CHECK (status IN ('pending','trial','active','grace','readonly','suspended','purged')),
  valid_from      timestamptz,
  valid_to        timestamptz,
  quota           jsonb NOT NULL DEFAULT '{"users":0}',    -- 0 = 不限；不计平台超管，计租户管理员
  branding        jsonb,
  retention_days  int NOT NULL DEFAULT 30,                 -- 注销后数据保留期
  created_at      timestamptz NOT NULL DEFAULT now(),
  updated_at      timestamptz,
  row_version     int NOT NULL DEFAULT 0
);

-- 自然人与租户成员
CREATE TABLE identity.person (
  id                 uuid PRIMARY KEY,
  identifiers        jsonb NOT NULL,                       -- [{type: phone|email|username, value, verified_at}]
  password_hash      text,                                 -- Argon2id；NULL = 仅上游登录
  password_history   jsonb NOT NULL DEFAULT '[]',
  password_changed_at timestamptz,
  mfa_factors        jsonb NOT NULL DEFAULT '[]',          -- [{type: totp|sms|passkey, enrolled_at}]；秘密只存 secretRef
  status             text NOT NULL CHECK (status IN ('active','disabled','locked','purged')),
  created_at         timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE identity.membership (
  id                 uuid PRIMARY KEY,
  tenant_id          uuid NOT NULL REFERENCES tenancy.tenant(id),
  person_id          uuid NOT NULL REFERENCES identity.person(id),
  account            text NOT NULL,                        -- 租户内唯一、不可改
  display_name       text NOT NULL,
  status             text NOT NULL CHECK (status IN ('active','disabled','locked','handover','purged')),
  is_tenant_admin    boolean NOT NULL DEFAULT false,
  identity_revision  bigint NOT NULL DEFAULT 0,            -- 权限/身份变更即递增；进令牌
  current_profile    text,                                 -- 身份（权限分区），S3
  created_at         timestamptz NOT NULL DEFAULT now(),
  row_version        int NOT NULL DEFAULT 0,
  UNIQUE (tenant_id, account),
  UNIQUE (tenant_id, person_id)
);

-- 会话（SSO 会话；user → sessions 索引）
CREATE TABLE identity.session (
  sid_hash           bytea PRIMARY KEY,                    -- 只存哈希，不存明文 sid
  person_id          uuid NOT NULL,
  membership_id      uuid,                                 -- 选择令牌阶段可为空
  tenant_id          uuid,
  client_id          text NOT NULL,
  device             jsonb,                                -- {type: pc|mobile|agent, ua, ip, geo}
  amr                text[] NOT NULL,                      -- 认证方式，step-up 依据
  acr                text,
  identity_revision  bigint NOT NULL,
  refresh_family     uuid,                                 -- refresh 轮换族；重用检测即整族吊销
  created_at         timestamptz NOT NULL DEFAULT now(),
  last_seen_at       timestamptz NOT NULL DEFAULT now(),
  absolute_expires_at timestamptz NOT NULL,
  revoked_at         timestamptz,
  revoke_reason      text                                  -- password_changed|disabled|tenant_suspended|forced|concurrency|backchannel|...
);
CREATE INDEX ON identity.session (person_id) WHERE revoked_at IS NULL;
CREATE INDEX ON identity.session (tenant_id) WHERE revoked_at IS NULL;

-- 权限点（由插件清单派生，平台表）
CREATE TABLE authz.permission_definition (
  code               text PRIMARY KEY,                     -- 如 qian.crm:customer.export
  plugin_id          text NOT NULL,
  resource_type      text NOT NULL,                        -- 由插件注册的资源类型
  action             text NOT NULL,
  display_name       jsonb NOT NULL,                       -- 多语言
  description        jsonb,
  risk_level         text NOT NULL CHECK (risk_level IN ('low','medium','high','critical')),
  requires_step_up   boolean NOT NULL DEFAULT false,
  side_effect        text NOT NULL CHECK (side_effect IN ('read','write','external','destructive')),
  terminals          text[] NOT NULL DEFAULT '{pc,mobile,api}',
  contract_version   text NOT NULL,
  schema_sha256      bytea                                 -- 规范化 inputSchema 哈希；变更即旧授权失效
);

-- 授权关系（租户表，RLS）
CREATE TABLE authz.grant (
  id                 uuid PRIMARY KEY,
  tenant_id          text NOT NULL,
  subject_type       text NOT NULL CHECK (subject_type IN ('membership','org','position','group','role','client','agent')),
  subject_id         uuid NOT NULL,
  permission_code    text REFERENCES authz.permission_definition(code),
  resource_ref       text,                                 -- URN qian:{tenant}:{app}:{type}:{id}；L3 实例授权时使用
  conditions         jsonb,                                -- L6 上下文条件（IP、时间窗、端）
  source             text NOT NULL CHECK (source IN ('direct','policy_pack','default_role','entitlement')),
  granted_by         uuid NOT NULL,
  granted_at         timestamptz NOT NULL DEFAULT now(),
  valid_from         timestamptz,
  valid_to           timestamptz,
  CHECK (permission_code IS NOT NULL OR resource_ref IS NOT NULL)
);
ALTER TABLE authz.grant ENABLE ROW LEVEL SECURITY;
ALTER TABLE authz.grant FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON authz.grant
  USING (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), ''))
  WITH CHECK (tenant_id = NULLIF(current_setting('app.current_tenant_id', true), ''));

-- 数据权限方案（租户表，RLS）
CREATE TABLE authz.data_scope_rule (
  id                 uuid PRIMARY KEY,
  tenant_id          text NOT NULL,
  resource_ref       text NOT NULL,                        -- 列表 / 模型 / 菜单的 URN
  role_id            uuid NOT NULL,                        -- 同角色内 AND、跨角色 OR
  name               text NOT NULL,
  all_data           boolean NOT NULL DEFAULT false,       -- 任一为 true 则整体放开
  filter_ast         jsonb,                                -- Filter AST：{logic, groups[{logic, conditions[{field, op, value|sysVar}]}]}
  tables             jsonb,                                -- 涉及的表与连接（多表按表拆条件组）
  description        text,                                 -- 可读描述，供 Explain 与 Agent
  created_by         uuid NOT NULL,
  created_at         timestamptz NOT NULL DEFAULT now(),
  row_version        int NOT NULL DEFAULT 0,
  CHECK (all_data OR filter_ast IS NOT NULL)
);
-- 同样的 ENABLE / FORCE ROW LEVEL SECURITY 与 tenant_isolation 策略

-- 命令级审计（哈希链；租户视角受 RLS 限制，平台超管经平台服务角色读取）
CREATE TABLE audit.audit_entry (
  seq                bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
  id                 uuid NOT NULL UNIQUE,
  occurred_at        timestamptz NOT NULL DEFAULT now(),
  tenant_id          uuid,                                 -- 平台级操作可为空
  actor_type         text NOT NULL CHECK (actor_type IN ('user','service','agent','system')),
  actor_id           text NOT NULL,
  on_behalf_of       uuid,                                 -- 代表的 Membership
  act_chain          jsonb,                                -- RFC 8693 act 链（S4）
  channel            text NOT NULL CHECK (channel IN ('ui','api','mcp','a2a','job','event')),
  capability_id      text NOT NULL,
  contract_version   text,
  plugin_id          text,
  resource_refs      text[],
  input_hash         bytea,                                -- 规范化输入 SHA-256；不存明文敏感参数
  idempotency_key    text,
  dry_run            boolean NOT NULL DEFAULT false,
  decision           jsonb,                                -- {allowed, matchedRules[], missingPermissions[], identityRevision}
  outcome            text NOT NULL CHECK (outcome IN ('success','denied','failed','partial')),
  error_code         text,
  trace_id           text,
  client             jsonb,                                -- {ip, ua, device}
  prev_hash          bytea NOT NULL,
  hash               bytea NOT NULL                        -- SHA-256(prev_hash || 规范化记录)
);
CREATE TABLE audit.chain_state (                           -- 链头持久化，重启不丢
  partition_key      text PRIMARY KEY,                     -- 按租户或按服务分链
  last_seq           bigint NOT NULL,
  last_hash          bytea NOT NULL,
  updated_at         timestamptz NOT NULL
);
```

补充说明：

- `authz.role`、`authz.role_assignment`、`authz.resource_type`、`authz.field_policy`、`authz.resource_acl`、`identity.external_identity`、`identity.client` 结构见 ADR-005 / ADR-006 附录，此处省略。
- 商密要求若确认，`hash / prev_hash / input_hash` 的算法由 SHA-256 切换为 SM3，列类型不变。
- 哈希链按 `partition_key` 分链（默认按租户），避免单链成为写入热点；跨链完整性由 `chain_state` 的周期快照签名保证。

***

## 13.14 过渡态（2026-10）

以下为 Kun 当前代码确认的缺口；S1 退出前必须消除，否则 §13 的目标态不成立。修复优先走上游 PR，一个迭代未合入则以 `Kun.*.Patches` 补丁包承载（Q7）。

| #   | 过渡态                                                                                           | 目标态                                       | 落点      |
| --- | --------------------------------------------------------------------------------------------- | ----------------------------------------- | ------- |
| G1  | 无任何鉴权抽象（无 `IPermissionChecker`、策略、`[Authorize]`）                                              | `Kun.Authorization` 端口 + `qian.authz` 实现  | ADR-006 |
| G2  | 网关外部 issuer 只能配一个                                                                             | `qian.identity` 唯一 issuer；多 issuer 仅作过渡开关 | ADR-005 |
| G3  | OIDC 客户端不发 `code_verifier`、不校验 nonce、无 refresh / client_credentials、discovery 不刷新             | 全部补齐；OIDC 一致性测试                           | ADR-005 |
| G4  | 会话无用户 → 会话索引；无后通道登出；日志打印明文 sid                                                                | 会话中心重做；只记 sid 哈希                          | §13.3   |
| G5  | CSRF 推迟                                                                                       | Cookie 方案上线即带 CSRF                        | §13.3   |
| G6  | 三套 S2S 签发器并存（RS256/60s、ES256/15min、Identity）                                                  | 收敛为一套，claim 对齐 iClaw 形状                   | §13.3.1 |
| G7  | 租户 claim 名不统一（`tid` vs `tenant_id`）；`X-Tenant-Id` 头回退与 `FallbackTenantId="default"` fail-open | 统一 `tenant_id`；生产禁用头回退，fail-closed        | §13.4   |
| G8  | `SessionData` 单 TenantId，无一人多租户模型                                                             | 引入 `membership_id`；切换租户重签                 | ADR-005 |
| G9  | 消费端 `MessageContext` 无 `TenantId`、Saga 执行器不建作用域、Outbox 发布器自建 scope → 后台链路丢租户                  | 订阅器建作用域后回灌；Saga 按实例 `TenantId` 开作用域       | ADR-008 |
| G10 | 网关默认不强制认证；限流未挂路由；私钥不能热轮换；gRPC 流式绕过鉴权                                                          | 全部改为强制装配                                  | ADR-010 |
| G11 | RLS 拦截器与模型缓存键 opt-in；忘记 `ReplaceService<IModelCacheKeyFactory>` 即跨租户泄漏                        | `AddKunTenantRls` 自动挂载并强制缓存键；CI 检查多租户基类   | ADR-004 |
| G12 | 多条审计路径并存（事后拦截器、哈希链无持久化）                                                                       | 一套：同事务 `AuditLog` + 持久化哈希链                | ADR-009 |
| G13 | Infisical 为纸面权威；现网无 default-deny / ResourceQuota / Alertmanager；K3s 未开 Secret 加密              | §13.7、ADR-017 的部署基线                       | ADR-017 |
| G14 | 缓存 / 存储键无租户维度                                                                                 | 租户感知装饰器                                   | ADR-004 |

***

## 13.15 待确认

1. 商密（SM2 / SM3）与等保定级：决定签名与哈希算法、日志保留期；本章按「不做假设」写，需产品负责人确认（Q18）。
2. 已裁决（R24）：S2S 令牌与 iClaw 同形，`roles[]` 作为例外保留但限定为粗粒度角色代码，Qian 服务不得仅凭它放行、必须调用 `Kun.Authorization` 决策端口；`project_id` 保留为可选 claim，Qian 只透传不解释（§13.3、ADR-010 D10.4）。
3. 平台超管对租户「不可见、审计可见」（Q12）与「旁路须每次审计」在 UI 上如何呈现（审计条目是否对租户管理员展示超管身份），需产品决定。
4. Agent 在 S4 前对转审 / 协办 / 撤销一律禁止（Q26）；S2 流程动作枚举需预留 `humanOnly` 与 `agentPolicy` 字段，与 ADR-012 核对。
5. 哈希链分链粒度（按租户 vs 按服务）影响跨租户操作（平台运营）的链归属，本章默认按租户并对平台级操作使用 `platform` 分区，需与 ADR-009 核对。
6. 已裁决（R10）：出网 SSRF 基线分两步——平台自有出网点（OIDC 发现、企业微信、SMTP、短信、Apollo、对象存储）S1 必做，低代码连接器与第三方出网点 S2（§13.8）。
7. 已裁决（R11）：数据接口 SQL 文本模式 = 高级权限 + 只读数据库账号 + 超时与行数上限 + 参数化 + 审计 + 发布需租户管理员批准；补充裁决 Q32 文本已同步。
