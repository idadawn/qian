# 租户自助开通页面

`qian.tenancy.signup` 的 ui 表面源码（S1-TEN-020），作为私有 pnpm 工作区包交付，分两部分：

- **申请人公开页**：`/signup` 注册并申请开通，`/signup/status` 申请进度。`pnpm --filter @qian/tenancy-signup-ui build` 以 `/signup/` 为基路径输出到 `dist/public`，由申请人接口所在的宿主同源托管（`UseQianTenancySignupPages`，见 [Qian.Tenancy.Signup](../Qian.Tenancy.Signup/README.md)；本机联调由 Dev.Shell 按 `QIAN_DEV_SIGNUP_UI_DIST` 或缺省目录挂上）。
- **运营的开通申请页**：MF2 远程 `ApplicationsPage`（`pnpm --filter @qian/tenancy-signup-ui build:remote` 输出 `dist/remote`，并核对生成的远程类型），在平台控制台的全局 scope 以 `/tenant-applications` 挂载。

页面只用 yue 组件。表单的数据 Schema 都从 [开通 HTTP 契约](../../plugins/qian.tenancy.signup/contracts/http-v1.openapi.json) 的请求体派生（`src/contracts.ts`），页面只补布局与少量本地约束：

- 行业限定为一组代码，文案按语言包显示；
- 确认密码只在页面核对，提交前去掉；
- 空的选填项不提交。

修改契约后运行 `pnpm --filter @qian/tenancy-signup-ui contracts:generate`；`contracts:check` 同时校验类型与运行时 JSON 副本的新鲜度。

## 行为

- 公开页不持有任何令牌。申请人会话只在宿主写的 HttpOnly Cookie 里（`Path=/api/signup`），请求经同源 `PlatformClient` 发出，不写 localStorage / sessionStorage。
- 注册页先核验手机号（发验证码 → 输入验证码），通过后才显示账号与企业信息。
  - 新号码填姓名、邮箱、密码与确认密码。
  - 已有账号的号码把账号区收起为「以某某的身份申请」，不要求姓名与密码（契约要求不得带）。
  - 租户号输入停顿后实时查询可用性（可用 / 已占用 / 保留字 / 不合法），提示经 `aria-describedby` 关联输入框；提交时宿主仍会再核对。
  - 字段错误（租户号占用、密码策略）按 pointer 落在对应输入框。
  - 已有进行中的申请时不再给表单，引导去进度页。
- 进度页按申请状态给出结果、步骤条、驳回原因与申请摘要。
  - 待审核可撤回（二次确认）；驳回或撤回后可「修改后重新提交」，以原申请为底稿。
  - 开通失败对申请人仍显示为开通中，不暴露内部原因。
  - 页面带同源登录页地址（`?next=/login…`，只接受 `/login` 开头的相对地址）时，获批后给「去登录」。
- 运营页：
  - 状态页签带计数，可按关键字与企业规模筛选，表格带自动检查徽标。
  - 审核抽屉分企业信息、申请人、自动检查与开通设置（租户号可改、试用 / 正式、到期日、用户配额、审核备注）；驳回切换到原因表单。
  - 通过与驳回按状态机元数据二次确认；开通失败的申请可重试。
  - 请求全部经外壳交付的 `PlatformClient`。

## 验证

`pnpm --filter @qian/tenancy-signup-ui test` 跑组件测试（Vitest browser mode）：`PlatformClient` 的 transport 换成按契约应答的替身（`src/test/fake-host.ts`），覆盖以下场景，并逐页跑 axe（critical / serious 为 0）：

- 新号码与已有号码的注册；
- 确认密码与服务端字段错误；
- 修改后重新提交的底稿；
- 进度页各状态与撤回；
- 运营审核的通过、驳回与英文切换。

真实宿主上的完整流程（开发期发件箱取验证码、运营在平台控制台审核、获批后登录进入新企业）在 [apps/qian-shell 的真实流程 E2E](../../apps/qian-shell/README.md)。
