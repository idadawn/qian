# 授权变更 → 身份修订递增的接线口径

状态：待产品负责人拍板（[S1-DEC-027](../../pm/tasks/S1-DEC-027.md)）。实现任务 [S1-AZ-009](../../pm/tasks/S1-AZ-009.md) 在拍板前标 blocked，不按任何一个选项先行实现。

## 1 要闭环的是什么

R7（[§12.2](../prd/12-boundaries.md) 第 17 条）：权限不进令牌，令牌携带 `identity_revision`，权限变更经推送失效后 ≤ 3 秒内对新请求全部生效。[PRD 7.5](../prd/07-s1-foundation.md) 写的链路是：授权变更 → `authz.grant.changed.v1` → 7.4 递增受影响主体的 `identity_revision` → 7.13 推送刷新。

链路两端都已交付，中间没有调用方：

| 环节 | 现状 | 落点 |
|---|---|---|
| 授权写入 | 角色绑定的授予、撤销、改有效期与租户授权修订同事务提交，`authz.grant.changed.v1` / `authz.role.changed.v1` 同事务进 Outbox | `PostgresTenantRoleStore.Write`（S1-AZ-004） |
| 判定 | 判定快照键含租户授权修订，每次判定读一次；撤销授予后下一次判定即拒绝（`no_matching_grant`） | `PostgresRbacEvaluator`（S1-AZ-003 / 004） |
| PEP 修订下限 | 表 `subject_identity_revisions` 按（租户, 主体种类, 主体）记见过的最高修订，只在出现更高修订的令牌时抬高；更低的令牌拒绝，原因 `identity_revision_stale`，HTTP 403 `AUTHZ_DENIED`，gRPC PermissionDenied | `IdentityRevisionGuard`（S1-AZ-004） |
| 身份端口 | `IIdentityAccounts.BumpIdentityRevisionAsync(personId, reason)` 在当前租户作用域内递增成员修订，返回新的令牌修订（Person 修订 + 成员修订）；不发事件、不写审计 | `Qian.Identity.Server`（S1-IDP-004） |
| 身份侧拒绝 | userinfo 与会话校验拒绝修订落后的令牌（401 `invalid_token`）；会话不吊销，refresh 换到带新修订的令牌 | `Qian.Identity.Server`（S1-IDP-004） |
| 中间环节 | 无。`authz.grant.changed.v1` 只进 Outbox；消费端（S1-EVT-002）未交付，`AddQianIdentitySessionHandlers` 这类订阅处理器只登记、不投递 | — |

## 2 决定选项的几条事实

1. **拓扑**：S1 默认 `platform`（含 `qian.authz`）与 `identity`（`Qian.Identity.Server`）是两个独立 Pod（PRD 7.0.4）；把两者装进同一进程的只有 standalone 宿主、参考宿主与测试宿主。生产拓扑里，授权侧要调到身份端口只有两条路：集成事件（等 [S1-EVT-002](../../pm/tasks/S1-EVT-002.md)），或新开 S2S 调用。
2. **撤销入口**：授予的撤销目前没有对外入口。`RevokeAsync` 是内部 API，角色与授权 CRUD 归 S1-AZ-005；生产里的授权写入只有两处，一是开通编排授予 `tenant.owner`，二是开通时实例化默认策略。
3. **主体解析**：求值器只匹配直接主体（user / service / agent），组织、岗位、用户组的绑定还不参与判定，目录解析归 S1-AZ-005。
4. **主体标识**：user 主体的 `SubjectId` 与令牌 `sub` 同为 Person id（GUID `D` 格式），可直接作端口参数。`service` 没有 Person 与成员身份；`agent` 在 S1 的 PEP 一律拒绝。
5. **时效**：访问令牌有效期 15 分钟。7.13 实时推送（S1-SCH-003，事件类型含 `identityRevisionChanged`）未开工。
6. **客户端**：`@yue/sdk` 的 `PlatformClient` 只在 401 时 refresh（`packages/yue/sdk/src/client.ts`）。PEP 对陈旧修订回 403，不会触发 refresh。

## 3 待拍板

### Q1 接线放在哪一侧

| 选项 | 做法 | 拆分部署（platform / identity 两个 Pod） | 同进程宿主 | 代价 |
|---|---|---|---|---|
| A 身份侧订阅 | `Qian.Identity.Server` 加 `IIntegrationEventHandler<AuthzGrantChangedEvent>`，在信封租户的作用域内调端口，登记方式同 `AddQianIdentitySessionHandlers` | PRD 7.5 的字面链路，也是唯一的生产路径；投递等 S1-EVT-002，交付前不生效，且没有对账兜底（身份侧读不到授权表） | 同样等 S1-EVT-002 | 改动最小；本任务只能交付处理器与直接调用它的测试 |
| B 授权侧进程内调用 | `Write` 提交后，对本次变更涉及的 user 主体经 `ctx.IdentityAccounts()` 调端口；用 `TryInject`，身份组件不在本进程即跳过；`Qian.Authz` 新增对 `Qian.Identity.Abstractions` 的依赖（依赖门禁允许） | 不生效 | 立即闭环；端口返回值可直接用来抬高 PEP 下限（Q5） | 新的组件间依赖，只解决一半拓扑 |
| C（推荐）B + A | 本任务做 B；S1-EVT-002 交付时补 A，验收挂在 EVT-002 或另立任务；同进程宿主不登记 A 的订阅，免得一次变更递增两次 | 随 S1-EVT-002 闭环 | 立即闭环 | 两条路径共用「哪些主体、什么原因」的判据 |
| D 同步 S2S 调用 | 身份暴露内部端点，platform 经 S2S 调用 | 立即生效 | 不需要 | 新增受保护的内部 API 与 S2S 凭据（S1-GW-001）；授权写入的可用性绑在身份服务上 |

推荐 C：拆分部署无论如何要等 S1-EVT-002；B 能让 standalone 与参考宿主现在就闭环，Q5 也能直接用端口返回值。R7 在拆分部署下的闭环随 S1-EVT-002 验收，不在 S1-AZ-009 里宣称完成。

### Q2 事务边界与失败处理

授权变更与身份递增不能同事务：两边是不同的 DbContext 与 schema，拆分部署时还在两个进程。只能先提交授权变更，再递增。递增失败时，授权判定已经生效，损失的是令牌层面的及时失效：旧令牌在身份侧最多多活到过期（≤ 15 分钟），PEP 下限也不抬高。

本题只针对 Q1 的 B 路径。A 路径的可靠性由 Outbox 与 S1-EVT-002 的至少一次、退避与死信保证。

| 选项 | 做法 | 进程崩溃 | 代价 |
|---|---|---|---|
| A 尽力一次 | 提交后调用一次，失败记警告日志 | 丢 | 最小 |
| B（推荐）有限重试 + 告警 | 提交后在同一调用内完成（调用方返回时修订已递增）；失败按退避重试，总时长不超过 3 秒；仍失败记警告与计数，不回滚授权变更 | 丢 | 小，覆盖瞬时故障 |
| C 持久待办 + 后台收尾 | 授权变更同事务写一行「待递增」，提交后立即尝试；失败由后台退避重试，直到成功或判为不可重试；卸载前排空 | 不丢 | 新表、迁移、后台工作与卸载排空，约多 1 天；做法同身份侧会话吊销的两段式 |

不可重试的结果一律跳过并记日志，不进入重试：主体不是 GUID，或该租户没有这个成员（`NOT_FOUND`）。

### Q3 哪些授权变更触发递增

PRD 7.5 只写了 `authz.grant.changed.v1` 触发递增。

`authz.grant.changed.v1` 的三种变更（`assigned` / `updated` / `revoked`）：

- **A（推荐）三种都递增**：口径简单，与 PRD 一致。新增授予并不需要让旧令牌失效，但客户端换一次令牌就能刷新界面权限，代价是一次 refresh。
- **B 只在撤销与收窄有效期时递增**：少一次 refresh，但判据要区分有效期的放宽与收窄。

`authz.role.changed.v1`（建角色、组件默认授权的安装与升级）：

- **A（推荐）不扇出**：判定经租户授权修订即时生效，令牌不失效，界面刷新等 7.13 推送。当前生产路径上，`grants_installed` 只在开通时发生，此时角色还没有绑定。组件升级改默认授权（S1-AZ-007）时再议。
- **B 扇出到该角色的全部直接 user 绑定**：授权侧在同一次写入里解析绑定、逐个递增。一次组件升级可能让大租户的全部成员被迫 refresh。走 Q1 的 A 路径时，身份侧解析不了角色成员，只能由授权侧按主体逐条发事件，或在载荷里带主体列表。
- **C 只在权限减少时扇出**。

### Q4 非 user 主体

| 主体种类 | 推荐处理 | 理由 |
|---|---|---|
| user | 按 Person id 调端口；非 GUID 或该租户无成员（`NOT_FOUND`）时跳过并记日志 | 没有该租户的成员身份，就没有该租户的令牌可失效 |
| organization / position / group | S1 跳过；在 S1-AZ-005 登记：接入目录时按成员扇出，口径同 Q3 | 求值器尚不解析这类绑定，它们还不影响判定 |
| service | 跳过 | 机器主体没有 Person 与成员身份；判定按租户授权修订即时生效 |
| agent | 跳过 | S1 的 PEP 对 agent 一律拒绝（`delegation_not_supported`） |

### Q5 身份侧递增后，PEP 下限是否同步抬高

| 选项 | 做法 | 「旧令牌在 PEP 侧 3 秒内被拒」 | 代价 |
|---|---|---|---|
| A 维持 S1-AZ-004 口径 | 下限只在见到更高修订的令牌时抬高 | 不成立。撤销的权限照样即时拒绝（判定），仍持有的权限对旧令牌继续可用，直到客户端换令牌或令牌过期 | 无 |
| B（推荐）递增后同步抬高 | 用端口返回的新修订把下限抬到该值，只增不减，与懒抬高共用一张表。拆分部署下授权侧拿不到返回值：要么身份侧递增时发事件、授权订阅后抬高（新事件，等 S1-EVT-002），要么那时仍维持懒抬高，届时再定 | 同进程宿主成立 | 小；必须同时拍板 Q6，否则在途客户端会卡在 403 |
| C 按签发时间抬高 | 授权变更同事务记下主体的 not_before，PEP 拒绝签发时间更早的令牌 | 成立，且不依赖身份侧与 S1-EVT-002 | 要把 `iat` 加进主体视图；引入签发方与 PEP 之间的时钟偏差；偏离 [ADR-006](../adr/ADR-006-authorization-model.md) D6.7「以 identity_revision 递增失效」的口径 |

### Q6 陈旧令牌的响应码

PEP 现在对 `identity_revision_stale` 回 403 `AUTHZ_DENIED`（gRPC PermissionDenied），身份侧 userinfo 对同一情形回 401 `invalid_token`，而 `@yue/sdk` 只在 401 时 refresh。这个问题现在就存在：用户在任一客户端换了新修订的令牌（比如改密之后），PEP 下限随之抬高，其余客户端就会持续收到 403。Q5 选 B 后，每次授权变更都会触发它。

| 选项 | 做法 | 后果 |
|---|---|---|
| A 维持 403 | 不改 | 下限抬高后，该用户在该租户其余客户端的请求一律 403，不会自动换令牌，要等令牌过期（≤ 15 分钟）后因 401 才 refresh |
| B（推荐）改 401 | HTTP 回 401 `UNAUTHENTICATED`，gRPC 回 Unauthenticated；决策日志原因仍是 `identity_revision_stale` | 客户端按 401 自动 refresh 后重发请求；要改 S1-AZ-004 已交付的 PEP 映射与相关用例的断言 |

## 4 推荐组合下 S1-AZ-009 交付什么

全部按推荐拍板时：

- **授权侧**：`Write` 提交后，对本次写入里 `authz.grant.changed.v1` 涉及的 user 主体经 `ctx.IdentityAccounts()` 递增（有限重试），再把 PEP 下限抬到返回值；身份组件不在本进程时跳过。
- **PEP**：`identity_revision_stale` 在 HTTP 与 gRPC 都改为 401，调整 S1-AZ-004 的相关用例。
- **验收用例**：用 CRM 参考宿主 `Start(identity: true)`，即 Testcontainers 的 PostgreSQL 加同进程的身份与授权。真实登录拿到带 `identity_revision` 的令牌，撤销该用户的一个角色后，3 秒内应当看到：
  - 修订递增；
  - 旧令牌在 userinfo 与 PEP 都被拒，PEP 的拒绝原因是 `identity_revision_stale`，该用户仍持有的权限也一样被拒；
  - refresh 拿到新修订，并被 PEP 放行；
  - 非 user 主体与无成员身份的主体不报错、不重试。
- **登记**：S1-EVT-002 加一条，交付时补身份侧订阅处理器（Q1 的 A）与拆分部署下 PEP 下限的同步（Q5）；S1-AZ-005 加一条，目录主体接入时按成员扇出（Q4）。

## 5 不在本决策

- 7.13 推送刷新（S1-SCH-003）。
- 身份侧其它修订变化（改密、停用、成员状态）如何同步 PEP 下限：仍按现状懒抬高。
- 修订递增要不要写审计：端口目前不写。
