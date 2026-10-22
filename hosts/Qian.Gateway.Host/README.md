# gateway 业务宿主

入口是本目录 `Program.cs`，可执行项目为 `Qian.Gateway.Host.csproj`，是 gateway profile 的业务启动项目。按 DEC-030 组合真实 Apollo、Identity.Client、Redis、NativeData、NativeAudit 与 Gateway；EF Data 留在平台与 Scheduler。网关契约与安全行为以 [Provider 主文档](../../packages/Qian.Gateway/README.md) 为准。

## 启动输入

`appsettings.json` 提供不含凭据的配置结构。部署前把示例地址替换为当前环境的规范 issuer、下游地址与实际 Traefik CIDR；空 CIDR 数组不会信任任何转发头。环境变量前缀为 `QIAN_`，也可用命令行覆盖；Apollo 的公共/组件/application namespace 仍按 Config Provider 的固定优先级合成。

- 身份服务登记 `Identity:ClientId`、精确的 `Identity:RedirectUri` 与 `Gateway:IdentityAudience`。回调在服务端 `/auth/callback`，与外壳未来页面的最终路径须按身份产品决定配置。
- RSA 私钥投射到 `Gateway:SigningKeyPath`，以原子文件替换完成轮换；不会在生产缺钥时自动生成密钥。
- `ConnectionFiles:Audit`、`ConnectionFiles:PlatformAudit`、`ConnectionFiles:Redis` 指向宿主 Secret 文件。数据库使用独立的 NOINHERIT 受限登录，分别对应 `Audit:TenantRole` / `Audit:PlatformRole`；`Audit:FunctionOwner` 是离线迁移配置的专用 NOLOGIN 函数 owner。宿主登记 `audit_tenant` / `audit_platform` 两个 NativeData 绑定，表与精确保护策略来自 NativeAudit 的固定源码声明。
- `Identity:SecretMountRoot` 下的 `qian/identity/client-secret` 通过固定 `secretRef://platform/identity/client-secret` 解析。生产接收端把这次解析记入真实平台审计链，写失败不会返回密钥，不使用空审计接收端。
- `Identity:DataProtectionKeysDirectory` 是各副本共享的持久密钥目录，Redis 会话使用稳定前缀。
- Apollo 启用 AccessKey 时，配置值为 `secretRef://platform/apollo/access-key`，`ConnectionFiles:ApolloAccessKey` 指向引导 Secret 文件。宿主先按 JSON → Secret 文件 → `QIAN_` 环境变量 → CLI 构造独立引导快照，冻结路径；签名回调每次读取该文件以支持原子轮换，不反向读取尚在装配的 Apollo 配置根。`QIAN_Secrets__Directory` 与 CLI 的 `--Secrets:Directory` 均可指定挂载配置目录。

数据库 schema、函数和 RLS 策略由离线迁移连接调用 `NativeAuditStorage.MigrateAsync` 预先完成；在线宿主没有管理员连接，只校验，不建表、不迁移。全部真实组件挂载为 Ready 后才开始监听；任一依赖失败会阻止启动。启动日志记录动态代码能力；这个开关不是原生产物的独立证明，仍必须核验严格发布及真实二进制运行。停机先停止 HTTP、再逆序卸载 Runtime，最后释放 DI 容器。

```bash
dotnet run --project hosts/Qian.Gateway.Host -c Release -- --urls http://0.0.0.0:8080
```

此命令需要上述真实依赖和 Secret 文件，不会以测试替身自动补齐。开发诊断可使用 `ASPNETCORE_ENVIRONMENT=Development` 连接回环 HTTP 身份发行方；生产要求 HTTPS metadata。

## 当前验证与接续

宿主的 Release 编译与共享生产 Gateway / Identity.Client 的安全专项分别验收。完整原生门是 [生产宿主 driver](../../tests/Qian.Gateway.Host.AotTests/README.md)：直接运行本项目发布产物，连接真实 Apollo、受限 PostgreSQL、Redis 与身份服务，并读回实际生产进程写入的审计链。`Dockerfile.acceptance` 保留测试编排所需 SDK，只用于固定 Linux 验收环境。

当前已启用严格 NativeAOT 发布目标，不抑制 IL 诊断；完整发布与运行成功前，默认 profile 仍按 DEC-029 保留 `build.aot: false`。Kun 的普通包 JWKS 原生兼容修复由 S1-KUN-016 交付并正式发布后消费，不能在 Qian 复制或替换其 JWKS 端点。

`plugins/qian.bundle.gateway/config/qian.patch.yml` 声明与本宿主一致的 NativeData / NativeAudit 绑定及网关默认值。CLI 当前负责 profile 合成/校验，不提供通用进程启动命令；启动本宿主的命令如上，发布入口为 `dotnet publish hosts/Qian.Gateway.Host`，实际运行文件为 `Qian.Gateway.Host`。业务镜像、签名、非 root / 只读根文件系统、distroless 与部署证据属于 DEP-008。
