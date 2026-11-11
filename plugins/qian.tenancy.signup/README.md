# qian.tenancy.signup

租户自助开通组件（规格 [租户自助开通](../../docs/prd/specs/tenant-signup.md)，S1-TEN-019）。实现与主文档在 [Qian.Tenancy.Signup](../../packages/Qian.Tenancy.Signup/README.md)，Definition 为 Qian.Tenancy.Signup.Abstractions。

本目录是组件的四问表面：

- `.qian-plugin/plugin.json`：唯一清单。`inject`、`provides`、`runtime` 与 `schemas/config.schema.json` 由 Provider 入口派生（`python3 scripts/check_plugin_manifests.py --sync`），不手改；`optionalInject` 手写，与入口代码的 `TenancySignupComponent.OptionalInject` 一致（单元测试核对）。
- `contracts/http-v1.openapi.json` 与 `metadata/contracts.json`：两组 HTTP 端点（申请人、平台运营）的契约，由真实端点导出，`tests/Qian.Tenancy.Signup.Tests` 比对漂移。
- `metadata/permissions.json`：资源 `application` 的 read / approve / reject；approve 风险 critical、reject high，重试按 approve 授权。
- `ui/manifest.json`：运营页的 MF2 远程（`/remotes/qian.tenancy.signup/mf-manifest.json`，暴露 `ApplicationsPage`，路由 `/tenant-applications`，导航「开通申请」）。申请人公开页（`/signup`、`/signup/status`）是同一 UI 包构建的静态页，由申请人接口所在宿主经 `UseQianTenancySignupPages` 托管，不在这份清单里。

装进 identity profile（`qian.bundle.identity-server`）：申请人接口必须在身份服务的公开入口上，建账号与开通都要进程内调用身份与租户组件的端口。staging 首次部署（S1-DEP-011）由平台宿主的同进程身份分支装配（内置扩展 overlay 加 `tenancy.signup` 条目，运营接口挂运营端点组）。清单已去掉 `x-skeleton`（R150 ①）：启动证据是平台宿主的装配与同进程集成测试，`qian add` 独立安装验收尚未完成。
