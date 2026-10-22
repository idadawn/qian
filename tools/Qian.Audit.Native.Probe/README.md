# NativeAudit Linux 原生验收

探针启动即要求 Linux 且 `RuntimeFeature.IsDynamicCodeSupported == false`。它挂载真实 NativeData 与 NativeAudit，以专用受限登录验证租户追加、同 ID 重放/冲突、全部字段共享协议回放、读取/验链、真实业务同事务提交与外层回滚、跨租户隔离、独立平台链和卸载失效。迁移由共享 PostgreSQL fixture 独立完成，在线组件只读核验。

本机 Docker 路径：

```bash
docker build --progress=plain -f tools/Qian.Audit.Native.Probe/Dockerfile -t qian-native-audit-probe:local .
QIAN_REQUIRE_DOCKER=1 dotnet test --project tests/Qian.Audit.Native.AotTests -c Release
```

Dockerfile 固定 SDK 镜像摘要，按 amd64 / arm64 选择 Linux RID；启用 `IlcTreatWarningsAsErrors`、逐条裁剪诊断，核验可执行文件存在且对应托管 DLL 不存在。它是验收镜像，业务 gateway 的 distroless 组合另由 GW-001 验收。

Linux CI 使用同一 driver 运行原生进程：

```bash
dotnet publish tools/Qian.Audit.Native.Probe -c Release -r linux-x64 -p:IlcTreatWarningsAsErrors=true -p:TrimmerSingleWarn=false -o /tmp/qian-native-audit
QIAN_REQUIRE_DOCKER=1 QIAN_NATIVE_AUDIT_EXECUTABLE=/tmp/qian-native-audit/Qian.Audit.Native.Probe dotnet test --project tests/Qian.Audit.Native.AotTests -c Release
```

临时配置文件只对当前用户开放读写，运行后删除；Docker 路径将配置复制到短期验收容器。随机凭据不进入命令行或输出。CI 入口为 `.github/workflows/dotnet.yml` 的 `native-audit-aot`，严格发布和真库运行都通过才算验收，不以普通构建或跳过代替。

2026-10-23 本机 Linux arm64 严格发布通过，IL 警告为 0，产物为原生可执行文件；镜像真库 driver 1/1 与 Linux 容器内直接启动原生进程的 CI 同款 driver 1/1 均通过，TRX 保留 13 条实际断言输出。清单据此登记 `x-aot`；linux-x64 的持续验证由上述远端 CI 执行，本机结果不代替该 CI 状态。正式 feed 安装门仍保留 `x-skeleton`。
