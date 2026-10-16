# 本机租户运营手测宿主

- 只供开发机手测（S1-QA-012），不打包、不进 profile，不替代 qian dev（S1-RT-013）或 K3s 部署（S1-DEP-002）。
- 装配跟随集成测试：`tests/Qian.Sample.Crm.Tests` 的 LifecyclePlatform、AttachLifecycle、ManagementHost 改了，这里同步改，不另起一套语义。
- 运行期连接只用受限角色；不得为图方便改用超级用户、BYPASSRLS，或关闭验签、issuer、audience、有效期校验。
- `/__dev/` 路由不是产品契约，授权不得弱于运营端点；对应产品端点交付后删除或替换。
- 开发密钥、口令只在 `.qian-dev/`（git 忽略），不提交、不打印。
- 改了走查脚本，本机实际跑一遍 `scripts/dev-tenant-ops.sh walkthrough --yes`。
