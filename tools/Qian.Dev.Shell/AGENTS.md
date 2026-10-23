# 外壳认证联调环境

- 只供开发机联调与 CI 的真实流程验收（S1-YUE-002），不打包、不进 profile，不替代 qian dev（S1-RT-013）或 K3s 部署（S1-DEP-002）。
- 装配跟随 Qian.Dev.TenantOps 与集成测试：`tests/Qian.Sample.Crm.Tests` 的 ConfigureIdentity / AttachLifecycle、`tests/Shared/GatewayTestHost`、`tests/Qian.Identity.Client.Tests` 改了，这里同步改，不另起一套语义。
- 两个宿主只接受回环地址；运行期连接只用受限角色；不得为图方便改用超级用户、BYPASSRLS，或关闭验签、issuer、audience、有效期校验、PKCE。
- 浏览器零令牌：不得往响应体、URL、前端存储里放访问 / 刷新令牌；`/__dev/` 路由不是产品契约，授权不得弱于 BFF 自身（已认证 + 防伪），对应产品能力交付后删除或替换。
- 种子密码只在 `seed/accounts.json`（E2E 读同一文件），不写进其它代码、日志或对话；开发密钥、口令、运行时配置只在 `.qian-dev/`（git 忽略）。
- 改了本工程、脚本、认证页族或外壳薄入口，本机跑一遍 `scripts/dev-shell.sh e2e`；CI 的 shell-auth 工作流跑的是同一条命令，不要在工作流里另写一套步骤。
