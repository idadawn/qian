# NativeData Linux 原生验收

探针必须以 Linux NativeAOT 原生二进制运行，启动即验证 `RuntimeFeature.IsDynamicCodeSupported == false`。它挂载真实 NativeData，使用受限 PostgreSQL 登录执行读写、SERIALIZABLE 事务、释放回滚、租户隔离、平台读写、advisory lock 清理和卸载。数据库与随机凭据由共享 Testcontainers fixture 准备。

本机 Docker 路径：

```bash
docker build --progress=plain -f tools/Qian.Data.Native.Probe/Dockerfile -t qian-native-data-probe:local .
QIAN_REQUIRE_DOCKER=1 dotnet test --project tests/Qian.Data.Native.Postgres.AotTests -c Release
```

Dockerfile 固定 SDK 镜像摘要，按实际 amd64 / arm64 平台选择 Linux RID，开启 `IlcTreatWarningsAsErrors` 与逐条裁剪诊断，并核验可执行文件存在、对应托管 DLL 不存在。镜像只是验收工具，不是业务 distroless 镜像。

Linux CI 路径：

```bash
dotnet publish tools/Qian.Data.Native.Probe -c Release -r linux-x64 -p:IlcTreatWarningsAsErrors=true -p:TrimmerSingleWarn=false -o /tmp/qian-native-data
QIAN_REQUIRE_DOCKER=1 QIAN_NATIVE_DATA_EXECUTABLE=/tmp/qian-native-data/Qian.Data.Native.Probe dotnet test --project tests/Qian.Data.Native.Postgres.AotTests -c Release
```

同一 driver 通过 `QIAN_NATIVE_DATA_EXECUTABLE` 运行本机 Linux 原生进程，或通过 `QIAN_NATIVE_DATA_IMAGE`（默认 `qian-native-data-probe:local`）运行镜像。直接进程路径只传临时配置文件名，文件权限为当前用户读写且在结束时删除；镜像路径把同一配置复制进验收容器，结束时删除容器。口令不进入命令行、Git 或输出。

CI 持续门在 `.github/workflows/dotnet.yml` 的 `native-data-aot`：严格发布与真实 PostgreSQL 运行均通过才算成功。它独立于常规解决方案测试，避免把缺少原生制品时的跳过当作验收。
