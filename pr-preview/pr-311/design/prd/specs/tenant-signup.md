> For AI agents: the complete documentation index is available at /pr-preview/pr-311/llms.txt, the full documentation bundle is available at /pr-preview/pr-311/llms-full.txt.

# 租户自助开通：注册申请、平台审核与首次进入

> 本页由构建脚本镜像自仓库 [`docs/prd/specs/tenant-signup.md`](https://github.com/idadawn/qian/blob/main/docs/prd/specs/tenant-signup.md)，请修改源文件，不要改本页。

> 状态：S1（S1-DEC-032）。产品负责人 2026-11-03 指示按开通流程原型完成租户相关开发，PRD 7.3 原列为 S3 的「自助申请 / 审核」随之提前到 S1；加入、退出与邀请仍在 S3。本文是实现与验收的依据，裁决编号见 S1-DEC-032。

## 1 范围

✅ S1 做：

- 申请人在公开页面验证手机号、设置密码并提交企业开通申请；手机号已属既有账号时，以该账号申请。
- 申请人查看进度、撤回待审核的申请；被驳回后看到原因，并可另交一条新申请。
- 平台运营在平台控制台的「开通申请」页列表、筛选、查看自动检查，通过（可修正租户号、选试用或正式、有效期、用户配额）或驳回（原因对申请人可见）；开通失败时重试。
- 通过后复用 `qian.tenancy` 的开通编排建租户与管理员成员，再激活为试用或正式；结果通知申请人。
- 平台控制台：持平台超管角色的平台身份进入外壳后看到全局 scope 组件的页面（租户管理、开通申请）。
- 租户管理页按原型改版；企业首次进入时外壳首页显示开通引导与试用提示。
- yue 组件按原型统一刷新样式（§9）。

❌ 不做：邀请链接、加入与退出企业、多管理员（S3）；营业执照等材料上传与实名核验；套餐与计费；申请人凭密码查看进度（见 §6.2）；导航角标与运营站内提醒（依赖通知组件）。

## 2 角色与流程

| 角色         | 做什么                                                                        | 依赖                                            |
| ---------- | -------------------------------------------------------------------------- | --------------------------------------------- |
| 申请人        | 验证手机号 → 填管理员账号与企业信息 → 提交 → 查看进度 / 撤回 / 看驳回原因 → 收到开通通知 → 登录进入企业             | 获批前不登录平台，进度页凭手机验证码访问（§6.2）                    |
| 平台运营（平台超管） | 在平台控制台审核：通过并开通，或驳回                                                         | 平台身份 + `platform.superadmin`，每次操作经超管旁路并写平台审计链 |
| 系统         | `qian.identity` 建账号；`qian.tenancy.signup` 保存申请；`qian.tenancy` 开通编排与激活；发送通知 | 通知组件就绪前用开发期发件箱（§7）                            |

## 3 申请状态机

```text
提交 ──► 待审核 submitted ──运营通过──► 开通中 provisioning ──编排完成──► 已通过 approved
            │   │                              │  ▲
            │   └─申请人撤回─► 已撤回 withdrawn   步骤失败  │重试
            └─运营驳回─► 已驳回 rejected              ▼  │
                                              开通失败 failed
```

| 申请状态         | 租户主档               | 申请人看到           | 平台运营         |
| ------------ | ------------------ | --------------- | ------------ |
| submitted    | 未创建，租户号预留          | 等待审核，可撤回        | 待审核，可通过或驳回   |
| provisioning | `pending`，开通步骤执行中  | 正在开通            | 开通步骤进度       |
| approved     | `trial` 或 `active` | 已开通，到期日；去登录     | 只读，之后在租户管理维护 |
| failed       | `pending`，停在失败步骤   | 仍显示正在开通，不暴露内部原因 | 原因码，可重试      |
| rejected     | 未创建，租户号释放          | 驳回原因，可另交新申请     | 只读           |
| withdrawn    | 未创建，租户号释放          | 可另交新申请          | 只读           |

约束：

- 一个账号（手机号）同时只能有一条 submitted / provisioning / failed 的申请。
- 租户号在这三种状态的申请之间唯一，并且不能与已有租户相同；通过前运营可以修正，开通后不可改。
- 通过、驳回、重试都写平台审计链；驳回原因对申请人可见，审核备注只给运营看。
- 状态只进不退；approved、rejected、withdrawn 是终态。

## 4 组件 `qian.tenancy.signup`

| 问题            | 回答                                                                                                                                                                                                                                                                  |
| ------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 文件夹里的表面       | `.qian-plugin/plugin.json`；`schemas/config.schema.json`（由代码派生）；`contracts/http-v1.openapi.json` 与 `metadata/contracts.json`；`metadata/permissions.json`（资源 `application`：read / approve / reject）；`ui/manifest.json`（运营页 MF2 远程）。申请人页面是同一 UI 包构建的公开静态页，由申请人接口所在宿主托管 |
| 装进哪个 profile  | `identity`（`qian.bundle.identity-server`）：申请人接口必须在身份服务那个公开入口上（网关对代理路由一律要求认证并剥离 Cookie），建账号要进程内调 `IIdentityAccounts`，开通要进程内调 `ITenantProvisioning`，这两者都与身份服务同宿主                                                                                                      |
| seam 角色       | Definition `Qian.Tenancy.Signup.Abstractions`（服务键 `TenancySignup`、错误码、发件端口 `ISignupMessenger`）+ Provider `Qian.Tenancy.Signup`（含两组 HTTP 端点与公开页托管扩展）。Inject：`Data`、`TenancyProvisioning`、`TenancyLifecycle`、`Tenancy`（目录）、身份账号端口、`SuperadminBypass`、`Audit`          |
| 能否 `qian add` | 能；装入即开放公开申请接口，不装即不提供自助开通（取代「数据库开关」）                                                                                                                                                                                                                                 |

数据（schema `plugin_qian_tenancy_signup`，全局表、无租户列）：

- `applications`：申请编号（`AP-yyyyMMdd-NNNN`，同日序号）、自然人 ID、状态、企业名称、租户号、行业、规模、申请说明、联系人姓名、手机号（E.164，只为通知保存，接口一律返回掩码）、联系邮箱、审核结果（方式 / 有效期 / 用户配额 / 驳回原因 / 审核备注 / 审核人 / 时间）、开通结果（租户 ID、成员 ID、失败原因码、尝试次数）、提交与完成时间、并发版本。部分唯一索引：进行中的申请按租户号唯一、按自然人唯一。
- `verifications`：验证码挑战（手机号、验证码的加盐哈希、尝试次数、到期、消费时间、来源地址）。
- `sessions`：申请人会话（令牌 SHA-256、手机号、自然人 ID、到期、吊销时间）。

配置（`schemas/config.schema.json`，平台级）：试用天数（缺省 30）、缺省用户配额（缺省 20，含管理员、不含平台超管）、租户号长度（3–32）与保留字、验证码（6 位、5 分钟、5 次错误作废、60 秒重发间隔、每手机号每小时 5 次、每来源地址每小时 20 次）、申请人会话时长（30 分钟）、缺省国家码（与身份服务同为 86）。

## 5 接口

字段用 camelCase；错误一律是 ErrorDetail。申请人接口与公开页在身份服务的公开入口上，运营接口经网关。

### 5.1 申请人（公开入口）

| 接口                                                  | 说明                                                                                                                                                                                                                                                    |
| --------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `POST /api/signup/verifications` `{phone}`          | 发验证码 → 202 `{id, expiresAt, resendAfter}`。号码已注册与否响应完全相同；超限 429 `RATE_LIMITED`（带 `Retry-After`）；没有发件端口 503 `SIGNUP_UNAVAILABLE`                                                                                                                        |
| `POST /api/signup/session` `{verificationId, code}` | 核验 → 200 申请人视图 `{phone(掩码), existingAccount, displayName?}` 并写会话 Cookie；错误码 `SIGNUP_CODE_INVALID`（含剩余次数）、`SIGNUP_CODE_EXPIRED`；该号码的账号已停用、锁定或是平台身份时 403 `SIGNUP_ACCOUNT_UNAVAILABLE`                                                                   |
| `GET` / `DELETE /api/signup/session`                | 读当前申请人 / 退出                                                                                                                                                                                                                                           |
| `GET /api/signup/code-availability?code=`           | `{code, available, reason?}`，reason 为 `invalid` / `reserved` / `taken`；按来源地址限流                                                                                                                                                                        |
| `GET /api/signup/me/applications`                   | 本人的申请，新的在前                                                                                                                                                                                                                                            |
| `POST /api/signup/me/applications`                  | 提交。新账号必填 `displayName`、`password`，已有账号不得带；共有 `email?`、`companyName`、`code`、`industry?`、`size`、`description?`、`acceptTerms: true` → 201。错误：`SIGNUP_APPLICATION_OPEN`（已有进行中的申请）、`SIGNUP_CODE_TAKEN`（`#/code`）、`PASSWORD_POLICY_VIOLATION`（`#/password`） |
| `POST /api/signup/me/applications/{id}:withdraw`    | 撤回 submitted 的申请；其它状态 409 `SIGNUP_STATE_CONFLICT`                                                                                                                                                                                                     |

### 5.2 平台运营（经网关，平台主体 + 超管旁路 + 平台审计链，R113 同序）

| 接口                                                                                                      | 说明                                                         |
| ------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------- |
| `GET /api/signup/applications?status=&keyword=&size=&skip=&take=`                                       | 列表 `{items, total, counts}`，counts 含各状态与全部；每行带一条最需要关注的自动检查 |
| `GET /api/signup/applications/{id}`                                                                     | 详情：企业信息、申请人（掩码）、自动检查、审核结果、开通步骤、缺省值（试用天数、用户配额）              |
| `POST /api/signup/applications/{id}:approve` `{code?, plan: trial\|formal, validUntil?, users?, note?}` | 通过并开通；租户号可修正。编排失败时申请转 failed 并返回 200 与失败原因码                |
| `POST /api/signup/applications/{id}:reject` `{reason, note?}`                                           | 驳回，原因必填                                                    |
| `POST /api/signup/applications/{id}:retry`                                                              | 对 failed 的申请续跑开通与激活                                        |

权限点 `qian.tenancy.signup:application:read|approve|reject`；approve 风险 critical、reject high。approve / reject 在状态机元数据里标 `humanOnly` + `confirm`。

自动检查：租户号可用（未被租户或其它进行中的申请占用）；同名企业（已有租户或其它进行中的申请名称相同）；申请人账号是否已加入其它企业。

## 6 安全

### 6.1 公开接口

- 写请求只收 `application/json`，并核对 `Origin`（或 `Sec-Fetch-Site`）为同源；会话 Cookie `HttpOnly`、`SameSite=Strict`、`Path=/api/signup`，回环地址之外必须 `Secure`。
- 验证码只存加盐哈希，单次有效；发送与核验按手机号和来源地址限流；响应不区分号码是否已注册，核验通过后才告诉申请人「将以既有账号申请」。
- 密码只经身份组件的建账号端口落为 Argon2id 哈希，本组件不保存、不记录；密码策略错误原样转出。
- 申请人只能读写本人的申请；运营接口对申请人会话一律 401。

### 6.2 申请人访问（不新增登录会话）

获批前申请人没有可登录的身份：统一登录流水线对没有成员身份的账号返回 `NO_AVAILABLE_TENANT`，而「`tenant_id` 为空」在令牌里专指平台身份。为不改令牌语义，进度页凭「手机号 + 验证码」建立只属于本组件的申请人会话；提交成功时直接建立。凭密码查看进度需要身份组件提供「只核验第一因子」的端口，留作后续。

### 6.3 账号

- 新号码：提交时经 `IIdentityAccounts.CreatePersonAsync` 建自然人，手机号标记已验证、申请人自设密码、不强制改密、无任何成员身份。
- 已有号码：以该自然人申请，不建新账号、不改其密码；账号停用、锁定或具备平台访问资格时拒绝（Q12：平台身份不进租户）。
- 通过时经开通编排第 05–07 步建立管理员成员、写指针、授 `tenant.owner`（R96 / R141 同口径），成员 ID 在申请记录上生成一次，重试幂等。

## 7 通知

发件端口 `ISignupMessenger`（验证码、开通结果、驳回结果）。通知组件（S1-NTF-003 短信渠道）就绪前生产宿主不提供实现：发验证码返回 503，自助开通不可用；审核结果发不出时不阻断审核，进度页照常可查（与开通编排第 08 步 `notify_unavailable` 同口径）。开发宿主登记开发期发件箱，只在 Development 环境、只监听回环地址，供手测与 E2E 读取。

## 8 页面

| 页面          | 位置                                                   | 原型      |
| ----------- | ---------------------------------------------------- | ------- |
| 注册并申请开通     | 公开页 `/signup`（身份服务公开入口）                              | 2       |
| 申请进度        | 公开页 `/signup/status`                                 | 3       |
| 开通申请列表与审核抽屉 | 外壳远程 `qian.tenancy.signup`，路由 `/tenant-applications` | 5、6     |
| 租户管理（改版）    | 外壳远程 `qian.tenancy`，路由 `/tenants`                    | 7       |
| 平台控制台导航     | 外壳：平台身份按平台超管角色加载全局 scope 的页面与菜单                      | 5、7 的侧栏 |
| 企业控制台首次进入   | 外壳首页（租户会话）                                           | 8       |

登录页遇 `NO_AVAILABLE_TENANT` 时给出「查看开通申请进度」入口；选择工作空间页给出「申请开通新企业」入口。

## 9 视觉

色值只用 `@idadawn/yue-tokens` 语义色，不改主色。按原型统一调整：应用画布底色、徽标（带状态圆点）、页签（下划线样式，可带计数）、数据表（卡片容器、表头、分页）、侧滑抽屉（页头、可滚动主体、固定页脚）、页头（面包屑与操作区）、外壳（品牌、带图标的导航、用户卡片）、补齐常用图标、新增步骤条。视觉基线随组件改动重新生成并逐张审阅。

## 10 验收

见 S1-TEN-019（后端）、S1-TEN-020（页面）、S1-TEN-018（租户管理改版）、S1-YUE-017（组件样式）、S1-YUE-018（平台控制台）、S1-TEN-021（首次进入）。端到端主线：新号码申请 → 运营通过（试用 30 天、配额 20）→ 申请人凭手机号与密码登录直接进入新企业，外壳首页显示试用提示与开通引导；驳回路径：申请人看到原因、租户号释放、另交新申请。
