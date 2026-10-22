# 生产网关完整原生运行验收

driver 启动实际 `hosts/Qian.Gateway.Host` 进程，不创建替代网关，不在生产增加测试端点。默认必须提供 `QIAN_GATEWAY_HOST_EXECUTABLE` 指向严格发布的 Linux 原生二进制，缺失、非 ELF 文件头或仍含同名托管 DLL 均失败；生产启动日志另检查动态代码能力关闭。普通托管构建的 runtimeconfig 也可能关闭该能力，因此它不能单独证明 NativeAOT，必须与严格发布、产物形态及实际运行一起判定。

真实依赖由共享 fixture 编排：PostgreSQL 16、Redis 7、Apollo 2.5.2 ConfigService / AdminService 与 MySQL 8。数据库由离线 `NativeAuditStorage` 迁移，在线进程只有独立租户/平台受限登录；角色与凭据随机生成。身份侧复用真实 Qian Identity 内置账号、密码登录、防伪、PKCE、授权码与令牌流程，租户目录沿已有身份测试的可控 fixture。回调为测试证书保护的 HTTPS；生产程序只在明确 Development 环境接受回环 HTTP 身份发行方。

验收顺序：真实 Apollo 发布网关 issuer → 生产宿主挂载全部组件并 ready → 匿名 401 → 真实 Cookie 和 Bearer 分别换为 60 秒 S2S → 独立下游按生产 JWKS 验签、核对租户/受众与冻结 Schema → 错误 Bearer 不降级 Cookie、伪造转发头不改变 IP → 读回密钥解析与 Apollo 发布产生的平台审计并验链 → SIGTERM 正常释放。下游是专门的验签端点；真实平台租户运营路由继续由 `Qian.Sample.Crm.Tests/GatewayAcceptanceTests.cs` 验收。

Linux 本机或 CI：

```bash
dotnet publish hosts/Qian.Gateway.Host -c Release -r linux-x64 -p:IlcTreatWarningsAsErrors=true -p:TrimmerSingleWarn=false -o /tmp/qian-gateway-native
QIAN_REQUIRE_DOCKER=1 QIAN_GATEWAY_HOST_EXECUTABLE=/tmp/qian-gateway-native/Qian.Gateway.Host dotnet test --project tests/Qian.Gateway.Host.AotTests -c Release --report-xunit-trx
```

macOS 用固定 Linux 验收镜像运行同一 driver；Docker socket 只交给测试编排进程，生产进程仍通过临时 Secret 文件读取业务凭据：

```bash
docker build -f hosts/Qian.Gateway.Host/Dockerfile.acceptance -t qian-gateway-host-acceptance:local .
docker run --rm --entrypoint dotnet -v /var/run/docker.sock:/var/run/docker.sock -e TESTCONTAINERS_HOST_OVERRIDE=host.docker.internal -e QIAN_REQUIRE_DOCKER=1 -e QIAN_GATEWAY_HOST_EXECUTABLE=/gateway/Qian.Gateway.Host qian-gateway-host-acceptance:local test --project tests/Qian.Gateway.Host.AotTests -c Release --no-build --report-xunit-trx
```

开发时可显式设 `QIAN_GATEWAY_HOST_DLL` 做同一生产入口的 JIT 接线预检，不能同时设置原生变量。该结果会标为 JIT，不能替代严格原生发布与运行。

引导专项另复用共享 Apollo 协议 fixture 验证 AccessKey 签名：文件路径仅由 `QIAN_ConnectionFiles__ApolloAccessKey` 或 CLI 传入时，首次配置加载已能签名；`QIAN_Secrets__Directory` / CLI 目录覆盖生效，冻结路径后的密钥文件轮换也能再次加载。它与使用真实 Apollo 服务的生产宿主完整联调分开记录。

2026-10-23 验收使用源码 `e9cef6a70d`、正式 Kun `2026.10.23` 与 Dockerfile 固定 SDK 摘要：linux-arm64 严格发布无 IL 诊断；原生生产宿主和两个签名引导用例共 3/3 通过。生产原生进程因密钥解析与 Apollo 发布写入 3 条平台审计，第二个真实 NativeAudit 组合读回并验链；NativeAudit 读取/验链自身的原生执行另由 AUD-008 专项覆盖。验收镜像摘要为 `sha256:ae6872db3e43387d95ce3d1afce1e69ed1eee5e3c7e6f6a1ff18bdc6db123f76`。发布日志与 TRX 保存在本地/CI 的 `TestResults/gateway-host-native-*`；镜像仅用于验收，不代表 DEP-008 业务镜像已发布。
