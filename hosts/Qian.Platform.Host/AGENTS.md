# platform 生产宿主

- 这里只做 platform profile 的组合与宿主装配，不复制 Registry、Capabilities、Authz、Tenancy、Audit 等组件实现。
- 挂哪些条目由 profile 决定：启停条目改 overlay（宿主 overlay `platform.overlay.yml` 只关掉没有 Provider 的条目），不在代码里写挂载列表；目录项必须与组件清单的 `runtime.entry` 一致。
- 只认网关 S2S：受众 `qian-platform` 与 `qian-operations` 按路径分（R144），校验 issuer、受众、有效期与签名，只收 RS256；不加 Cookie、外部访问令牌或开发期签名钥。
- 运行期连接只用受限登录，凭据只从 Secret 文件读；迁移走 `--migrate-only`，在线副本不挂迁移属主的凭据。
- 装配入口要能复用：身份分支（S1-DEP-011）、module 应用等经 `PlatformHostComposition` 的扩展登记接入，不把逻辑堆进 `Program.cs`。
- 改动后跑 CRM 集成测试里的 `PlatformHostTests`（`dotnet test --project tests/Qian.Sample.Crm.Tests --filter-class Qian.Sample.Crm.Tests.PlatformHostTests`）。
