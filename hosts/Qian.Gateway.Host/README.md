# gateway 业务宿主

入口是本目录 `Program.cs`，可执行项目为 `Qian.Gateway.Host.csproj`。当前按 DEC-029 使用 JIT，真实组合 Apollo、Identity.Client、Redis、Data、Audit 与 Gateway；不是只提供 DI 扩展方法的样例。网关契约与安全行为以 [Provider 主文档](../../packages/Qian.Gateway/README.md) 为准。

## 启动输入

`appsettings.json` 提供不含凭据的配置结构。部署前把示例地址替换为当前环境的规范 issuer、下游地址与实际 Traefik CIDR；空 CIDR 数组不会信任任何转发头。环境变量前缀为 `QIAN_`，也可用命令行覆盖；Apollo 的公共/组件/application namespace 仍按 Config Provider 的固定优先级合成。

- 身份服务登记 `Identity:ClientId`、精确的 `Identity:RedirectUri` 与 `Gateway:IdentityAudience`。回调在服务端 `/auth/callback`，与外壳未来页面的最终路径须按身份产品决定配置。
- RSA 私钥投射到 `Gateway:SigningKeyPath`，以原子文件替换完成轮换；不会在生产缺钥时自动生成密钥。
- `ConnectionFiles:Audit`、`ConnectionFiles:PlatformAudit`、`ConnectionFiles:Redis` 指向宿主 Secret 文件。数据库使用不同的租户/平台受限登录，禁用超级用户与 BYPASSRLS；`Audit:PlatformRole` 与独立平台审计登录一致。
- `Identity:SecretMountRoot` 下的 `qian/identity/client-secret` 通过固定 `secretRef://platform/identity/client-secret` 解析。生产接收端把这次解析记入真实平台审计链，写失败不会返回密钥，不使用空审计接收端。
- `Identity:DataProtectionKeysDirectory` 是各副本共享的持久密钥目录，Redis 会话使用稳定前缀。
- Apollo 启用 AccessKey 时，配置值为 `secretRef://platform/apollo/access-key`，`ConnectionFiles:ApolloAccessKey` 指向引导 Secret 文件。

数据库 schema 和 RLS 策略由迁移 Job 预先完成；在线宿主只校验，不建表、不迁移。全部真实组件挂载为 Ready 后才开始监听；任一依赖失败会阻止启动。

```bash
dotnet run --project hosts/Qian.Gateway.Host -c Release -- --urls http://0.0.0.0:8080
```

此命令需要上述真实依赖和 Secret 文件，不会以测试替身自动补齐。开发诊断可使用 `ASPNETCORE_ENVIRONMENT=Development` 连接回环 HTTP 身份发行方；生产要求 HTTPS metadata。

## 当前验证与接续

宿主项目已完成 Release 编译；安全链路由共享生产 Gateway / Identity.Client 入口在真实 Kestrel、身份服务、Redis 和 CRM 平台运营宿主中验收。完整业务宿主的 NativeAOT 发布与运行尚未通过，不能把编译或这些安全测试当作完整依赖运行证明。

原生 Provider 接入、构建时默认 profile 与宿主输入一致性以及最终 Linux 原生运行由 DEC-030 的后续工作串行整合；Data/Audit 组合替换之前本宿主保持现有 EF 实现。镜像、签名、非 root / 只读根文件系统和 distroless 证据属于 DEP-008。
