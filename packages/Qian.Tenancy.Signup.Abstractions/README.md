# Qian.Tenancy.Signup.Abstractions

租户自助开通 seam 的 Definition（规格 [租户自助开通](../../docs/prd/specs/tenant-signup.md)）。实现、宿主装配与接口说明在 [Qian.Tenancy.Signup](../Qian.Tenancy.Signup/README.md)。

| 内容 | 说明 |
|---|---|
| `TenancySignupDefinition` | 组件 ID `qian.tenancy.signup`；服务键 `TenancySignup`（`ITenantSignup`）与 `TenancySignupMessenger`（`ISignupMessenger`）；`ctx.TenancySignup()` |
| `ITenantSignup` | 申请人一侧（发码、核验并建立会话、租户号可用性、本人的申请、提交、撤回）与运营一侧（列表与计数、详情、通过、驳回、重试） |
| DTO | 申请人：`StartSignupVerification`、`OpenSignupSession`、`SubmitSignupApplication`、`SignupApplicant`、`SignupApplicantApplication` 等；运营：`SignupApplicationQuery`、`SignupApplicationList`、`SignupApplicationDetail`、`ApproveSignupApplication`、`RejectSignupApplication`；状态机 `SignupStateMachine.Current` |
| `TenancySignupErrors` | `SIGNUP_UNAVAILABLE`（503）、`SIGNUP_CODE_INVALID` / `SIGNUP_CODE_EXPIRED`（400）、`SIGNUP_ACCOUNT_UNAVAILABLE`（403）、`SIGNUP_APPLICATION_OPEN` / `SIGNUP_CODE_TAKEN` / `SIGNUP_STATE_CONFLICT`（409）；限流复用 `RATE_LIMITED`，等待秒数经 `RetryAfterDataKey` 交给宿主写 `Retry-After` |
| `ISignupMessenger` | 发件端口：验证码与审核结果。通知组件（S1-NTF-003）就绪前生产宿主不提供实现，发验证码返回 503；实现须按消息 Id 去重 |

状态机：`submitted` →（通过）`provisioning` →（编排完成）`approved`；`provisioning` →（步骤失败）`failed` →（重试）`provisioning`；`submitted` →（驳回）`rejected` /（撤回）`withdrawn`。`approved`、`rejected`、`withdrawn` 是终态。申请人一侧把 `failed` 显示为 `provisioning`，不暴露内部原因。
