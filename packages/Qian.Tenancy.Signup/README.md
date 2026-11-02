# Qian.Tenancy.Signup

租户自助开通的 Provider（组件 `qian.tenancy.signup`，规格 [租户自助开通](../../docs/prd/specs/tenant-signup.md)，S1-TEN-019）：申请人在公开页凭手机号与验证码提交开通申请，平台运营审核，通过后经 `qian.tenancy` 的开通编排建立租户与管理员成员，申请人用自己的密码经统一登录流水线直接登录新租户。Definition 在 [Qian.Tenancy.Signup.Abstractions](../Qian.Tenancy.Signup.Abstractions/README.md)，组件表面在 [plugins/qian.tenancy.signup](../../plugins/qian.tenancy.signup/README.md)。

## 组件四问

| 问题 | 回答 |
|---|---|
| 文件夹里的表面 | 清单、配置 Schema（由 `ComponentConfiguration` 派生）、HTTP 契约、权限目录（`application`：read / approve / reject）、运营页 UI 清单 |
| 装进哪个 profile | `identity`（`qian.bundle.identity-server`）：申请人接口与公开页必须在身份服务的公开入口上；建账号进程内调 `IIdentityAccounts`，开通进程内调 `ITenantProvisioning` |
| seam 角色 | 提供 `TenancySignup`（`ITenantSignup`）。Inject：`Data`、`Tenancy`、`TenancyProvisioning`、`TenancyLifecycle`、`TenancyOperations`、`IdentityAccounts`；optionalInject：`AuthzSuperadminBypass`（运营端点）、`TenancySignupMessenger`（发件端口） |
| 能否 `qian add` | 能；装入即开放公开申请接口，不装即不提供自助开通 |

## 宿主装配

```csharp
// 数据：全局表，schema plugin_qian_tenancy_signup；迁移随包提供（与其它带迁移的组件一样由迁移任务或建表脚本执行）。
builder.Services.AddQianData<TenancySignupDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(TenancySignupDbContext.Schema));

// 身份服务的公开入口：申请人接口与公开页走自己的分支，不经业务的租户入口管线。
app.MapWhen(http => TenancySignupEndpoints.IsApplicantPath(http.Request.Path) || http.Request.Path.StartsWithSegments(TenancySignupPagesHosting.PagesPath), branch =>
{
    branch.UseQianTenancySignupPages(signupDist);   // packages 的 UI 构建产物，可选
    branch.UseRouting();
    branch.UseEndpoints(endpoints => endpoints.MapQianTenancySignupApplicant(_ => loader.GetContext("tenancy.signup", ScopeKey.Global)));
});

// 平台运营接口：已认证的平台主体（宿主自己的认证），经网关转发；Authz 超管旁路在同一插件树里时才可用，否则一律 404。
app.MapQianTenancySignupOperations(_ => loader.GetContext("tenancy.signup", ScopeKey.Global));
```

挂载前宿主须已登记 `TenancySignupDbContext`（否则装载失败 `PRECONDITION_FAILED`）；组件装载时把 `SIGNUP_*` 错误码登记到宿主的错误码目录。反向代理之后的宿主须先启用转发头：同源核对比较的是请求的协议与主机。

## 接口

申请人（公开，规格 §5.1）：

| 接口 | 结果 |
|---|---|
| `POST /api/signup/verifications` `{phone}` | 202 `{id, expiresAt, resendAfter}`；号码已注册与否响应相同。超限 429 `RATE_LIMITED`（`Retry-After`），没有发件端口 503 `SIGNUP_UNAVAILABLE` |
| `POST /api/signup/session` `{verificationId, code}` | 200 `{phone, existingAccount, displayName?}` 并写会话 Cookie；`SIGNUP_CODE_INVALID`（文案含剩余次数）、`SIGNUP_CODE_EXPIRED`、403 `SIGNUP_ACCOUNT_UNAVAILABLE` |
| `GET` / `DELETE /api/signup/session` | 当前申请人（无效会话 401）/ 退出（204） |
| `GET /api/signup/code-availability?code=` | `{code, available, reason?}`，reason 为 `invalid` / `reserved` / `taken`；按来源地址限流 |
| `GET /api/signup/me/applications` | `{items}`，本人的申请，新的在前；`failed` 显示为 `provisioning` |
| `POST /api/signup/me/applications` | 201 申请；新号码必填 `displayName`、`password`，既有账号不得带。`SIGNUP_APPLICATION_OPEN`、`SIGNUP_CODE_TAKEN`（`#/code`）、`PASSWORD_POLICY_VIOLATION`（`#/password`） |
| `POST /api/signup/me/applications/{id}:withdraw` | 200 申请；只对 `submitted`，否则 409 `SIGNUP_STATE_CONFLICT`；不是本人的 404 |

平台运营（规格 §5.2；R113 同序：认证与平台主体 → 输入校验 → 组件上下文 → 超管旁路与平台审计 → 执行）：

| 接口 | 权限点 |
|---|---|
| `GET /api/signup/applications?status=&keyword=&size=&skip=&take=` → `{items, total, counts}` | `qian.tenancy.signup:application:read` |
| `GET /api/signup/applications/{id}` → 详情（企业、申请人掩码、自动检查、审核、开通步骤、缺省值） | read |
| `POST /api/signup/applications/{id}:approve` `{plan, code?, validUntil?, users?, note?}` | approve |
| `POST /api/signup/applications/{id}:reject` `{reason, note?}` | reject |
| `POST /api/signup/applications/{id}:retry` | approve |
| `GET /api/signup/meta/state-machine` → 状态、迁移与操作的 Agent 可见性 | read |

## 安全

- 写请求只收 `application/json`，`Origin`（缺省时 `Sec-Fetch-Site`）必须同源；会话 Cookie `qian.signup`：`HttpOnly`、`SameSite=Strict`、`Path=/api/signup`，回环之外 `Secure`。令牌只存 SHA-256，提交后轮换并绑定到申请人的自然人。
- 验证码只存 HMAC-SHA256 加盐哈希，单次有效，错误达到上限即作废；发送按手机号（重发间隔、每小时次数）与来源地址（每小时次数）限流，核验次数随之受限。响应不区分号码是否已注册，核验通过后才告诉申请人「将以既有账号申请」。
- 密码只交给身份组件的建账号端口（Argon2id），本组件不保存、不记录；密码策略错误原样转出（位置换成 `#/password`）。
- 申请人只能读写本人的申请，别人的申请与不存在同为 404；运营接口对申请人会话一律 401（不认这个 Cookie）。
- 运营端点只收人类平台用户：租户主体与 Agent 403、无凭据 401；请求体任何未知字段（含 `tenantId` / `tenant_id` / `id`）在绑定时 400。通过、驳回、重试在平台审计链各有一条超管旁路记录。

## 开通

通过时申请转 `provisioning`，管理员成员 ID 在申请上只生成一次；以申请的租户号、企业名称、成员 ID 与申请人自然人、用户配额调 `ITenantProvisioning.ProvisionAsync`（编排第 05–07 步建管理员成员、写指针、授 `tenant.owner`），三步都成功后 `ITenantLifecycle.ActivateAsync` 激活为试用或正式。任何一步失败申请转 `failed` 并记原因码（Qian 错误码或编排步骤的原因码），接口照常 200；重试把同一份输入再交给编排，编排按租户号幂等、只续跑没成功的步骤，不重复建租户或成员。审核结果经发件端口通知，发不出不阻断审核。

## 已知限制

- 发件端口在通知组件（S1-NTF-003 短信渠道）就绪前没有生产实现：发验证码 503，自助开通不可用；开发宿主（tools/Qian.Dev.Shell）登记只在 Development、只监听回环地址的开发期发件箱。
- 租户号可用性查询的限流在进程内，多副本时阈值按副本数放大；发码与核验的限流在数据库里，跨副本一致。
- 「申请人已加入其它企业」取身份组件的登录索引（含成员已停用的租户）；身份 Provider 不提供该查询时检查记为提示、不带数量。
