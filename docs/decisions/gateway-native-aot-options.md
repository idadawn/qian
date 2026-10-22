# 网关完整依赖的 NativeAOT 阻塞与可选实施路径

状态：待负责人在 [S1-DEC-030](../../pm/tasks/S1-DEC-030.md) 确认实施路径。本文记录 S1-GW-001 的实际技术探针，不改变 S1-DEC-029，不修改 GW-001 或 DEP-008 的验收。

## 已复现的证据

- 基线：`88c5f3c33d`，仅增加 [完整依赖探针](../../tools/Qian.Gateway.AotProbe/Program.cs)，没有修改 Data、Audit、Kun 的实现或安全检查。
- 普通 Release 构建：`dotnet build tools/Qian.Gateway.AotProbe -c Release`，零警告、零错误。
- Linux 环境：本机 Docker `linux/aarch64`；SDK 镜像 `mcr.microsoft.com/dotnet/sdk:10.0` 固定到摘要 `sha256:747cbade5bfd918a175ac32f8f1d931edec2c7d0d5397a3de72eaef1ac1f66d2`。
- 可复现命令：`docker build --progress=plain -f tools/Qian.Gateway.AotProbe/Dockerfile -t qian-gateway-aot-probe:gw001 .`。Dockerfile 安装 clang / zlib 后执行严格的 `dotnet publish -c Release -r linux-arm64 -p:IlcTreatWarningsAsErrors=true -p:TrimmerSingleWarn=false`。
- 结果：ILC 报错，发布命令退出 1（末尾 `MSB3077`）。**没有成功发布产物，没有原生运行证据。** 不能恢复 `build.aot: true` 或为这些组件填写 `x-aot: true`。
- 原始本地日志：`TestResults/gateway-aot-probe/linux-arm64-publish.log`。该目录是忽略的测试产物，不提交原始大日志；下表保留定位信息。BuildKit 会重复末尾诊断，不用原始行数判断工作量。

探针真实登记 Apollo 配置源、Identity.Client 与 Redis 会话、`AddQianData<AuditDbContext>`、`AuditComponent`、Kun 网关及签名轮换环；通过真实 `PluginLoader` 挂载，并包含审计追加、读取、验链的运行入口。它不包含假 Data、不禁用 RLS、不在在线启动中迁移，也没有关闭裁剪或任何 IL 警告。

| 类型 | 已观测诊断及源码位置 | 性质 |
|---|---|---|
| 泛型裁剪元数据 | `Qian.Data/QianDataExtensions.cs` 的 `AddCore<TContext>`，`ConfigureDbContext` / `AddDbContext` 调用报告 `IL2091`；Kun 的 `AddKunTenantRls<TContext>` / `AddKunDataConventions<TContext>` 同类 | 可逐项补齐准确的保留注解并回归；Kun 改动必须在 Kun 仓库交付 |
| JSON 元数据 | `Qian.Audit/AuditCommandPolicy.cs:28` 的匿名类型 `SerializeToElement`，`PostgresAuditStore.cs:155` 的 `Deserialize<JsonElement>` 报 `IL2026` / `IL3050` | 可改源生成元数据或直接 JSON DOM 操作，不改变审计协议 |
| 上游反射分发 | `Kun.EntityFrameworkCore.InProcessDomainEventDispatcher.DispatchAsync` 的 `MakeGenericMethod` 报 `IL3050` | 需要静态注册/生成分发路径及 Kun 上游验收；不能在 Qian 复制上游实现 |
| EF 运行时模型、查询与访问器 | EF 的 `PropertyBaseExtensions.CreateMemberAccess` / `RelationalPropertyExtensions.CreateFieldValueGetter` / 内部加载器存在 `IL3050`、`IL2060`；实际 Audit 存储使用 LINQ 查询与 `SaveChanges` | 结构性问题，补 JSON 注解不能解决。原生运行需要受支持的模型、查询和访问器生成路径 |
| 当前 Data 安全约束 | `Qian.Data.Abstractions/QianDbContext.cs:109` 在 `core.Model != null` 时明确拒绝预编译模型；同时强制租户模型缓存键、自定义模型校验及双 RLS 拦截器 | 不允许直接添加 `UseModel` 绕过检查；需要经确认的新安全装配设计和跨租户回归 |

EF 10 随包文档还明确列出运行时错误：`Model building is not supported when publishing with NativeAOT. Use a compiled model.` 与 `Query wasn't precompiled and dynamic code isn't supported with NativeAOT.` 这说明即使消除部分编译诊断，仍必须执行真实数据库路径验证，不能以“编译通过”代替运行验收。

通用背景：2026-10-23 核对的 [Microsoft Learn NativeAOT 文档](https://learn.microsoft.com/en-us/ef/core/performance/nativeaot-and-precompiled-queries) 仍将该能力列为实验性，说明需生成模型与查询，动态查询有限制，并不建议用于生产。页面自身最后更新于 2025-04-24，因此只作为机制与成熟度背景；本次 .NET 10 结论以固定镜像下的实际发布诊断和当前源码安全约束为依据。

## 路径 A：增加专供边缘宿主的原生 PostgreSQL 数据与审计 Provider

推荐作为最终 AOT 的优先方案，但涉及 Provider 边界与默认组合调整，须先确认，不由 GW-001 擅自实施。

1. **独立 Data 工作**：保留现有 `IDataService.OpenAsync<TContext>` 与 EF Provider；新增明确命名的连接/事务 Definition 与原生 PostgreSQL Provider。原生端口拥有受限登录角色校验、租户上下文、连接与事务两处 RLS 上下文设置/清理、事务释放与回滚；不返回 DbContext，不把现有 EF 端口实现成抛异常的占位。发布新的服务键及真实组件清单，避免以同一个键提供不兼容类型。
2. **独立 Audit 工作**：新增依赖上述原生端口的审计 Provider，继续实现现有 `IAuditService`。链头与记录必须原子提交，保留幂等 ID、并发重试、平台专用角色、租户隔离、相同规范化哈希、`AppendEnlistedAsync`、读取和验链。复用同一规范化算法与 Schema；不创造第二套审计协议。数据库迁移由独立迁移 Job 执行，在线原生宿主只校验。
3. **Kun 工作**：这个路径不把 EF 标为 AOT；对实际保留的 Kun 网关、身份、配置与 Npgsql 调用闭包逐项验证。只有真实仍存在的上游 AOT 问题才建 Kun 修复任务，不能预先宣称“不需任何上游修复”。
4. **组合工作**：经决策后把 gateway 的 Data / Audit 条目换成实际原生 Provider；Config / Identity 保持真实实现。完整 Linux 发布与真实 Apollo、Redis、受限 PostgreSQL 运行全部通过后，才能恢复 gateway AOT 标记。platform / scheduler 的 EF 路径与安全约束不变。

主要代价：新增受治理的数据端口与存储 Provider，需要确保现有审计 Schema、事务和隔离行为完全一致。优势是无需把运行时 EF 模型构建带入边缘原生进程。

## 路径 B：保留现有 EF Provider，完成受约束的预编译数据路径

这是另一条最终 AOT 路径，当前尚无可行性承诺，需要先做独立技术验证。

1. **独立 Data 工作**：设计仅接受本构建产物生成且可校验的模型入口，重新证明 schema、租户过滤、软删除过滤、双 RLS 拦截器和跨宿主租户缓存不会被预编译模型绕过。不能直接删除 `core.Model` 防护，也不能接受调用方任意提供的模型。
2. **独立 Audit 工作**：为租户与平台两个上下文生成原生兼容模型和所有真实查询路径；处理动态分页/过滤组合、写入访问器与事务。补齐 JSON 元数据，保持实际写入、回滚、并发与验链契约。
3. **独立 Kun 工作**：修复泛型裁剪注解、反射事件分发及原生上下文实际需要的上游路径；在 Kun 仓库发版，Qian 通过唯一 `KunVersion` 升级消费。
4. 每一步都必须执行 NativeAOT 真实数据库测试；如果 EF/Npgsql 当前版本不能表达安全模型或查询，明确报告，不添加 IL 抑制，不换成空操作。

主要代价：改动现有持久化安全装配与上游模块，影响面包含 scheduler、identity、tenancy 等消费者；可行性与工作量均高于普通网关包装。

## 路径 C：先交付网关安全宿主，保持最终 AOT 的阻塞状态

这是交付顺序，不是取消 AOT 的替代方案，可与 A 或 B 组合。

- GW-001 的强制认证、真实 Cookie 换 S2S、契约校验、双钥轮换和伪造转发头验收继续实施。
- 在当前任务定义下，AOT 项仍未通过，GW-001 不能标为 done。若负责人决定拆分任务，需明确新任务、依赖与验收的迁移记录，不能把未通过项直接勾选。
- `profiles/gateway` 继续如实声明 JIT，Data / Audit 不填虚假 AOT 声明；DEP-008 的完整 Linux NativeAOT 和 AOT distroless 镜像门继续有效，未通过前不得宣称最终业务镜像完成。
- 建议先采用 C 推进已授权且不受阻塞的安全实现，同时由负责人选择 A 或 B 承接最终数据路径；默认推荐 A，避免为了边缘宿主放松已有 EF 安全约束。
