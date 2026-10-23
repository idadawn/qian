# Qian.Lifecycle

平台生命周期控制器是 `Lifecycle` seam 的 Provider（服务键 `ctx.Lifecycle`），只进入 `platform` profile 的 base 组合包。Definition 在 `Qian.Lifecycle.Abstractions`，管理面 HTTP 适配器在 `Qian.Lifecycle.AspNetCore`。分发表面 `plugins/qian.lifecycle` 已带完整的配置 Schema 与诊断、批准两个权限声明，代码字段与配置 Schema 已由稳定入口派生；尚未通过完整安装门，清单继续保留 `x-skeleton`。设计依据：PRD 7.2.1 / 7.17、ADR-020 §2、组件规格 §7.6，任务 S1-RT-005。

控制器管三件事：

- 平台级修订状态机：`Install / Activate / Rollback / Deprecate / Retire`。
- 租户级安装状态机：`Entitle / Provision / Upgrade / Uninstall / Purge`，钩子按幂等键记入 `lifecycle_runs`。
- 经 Registry 写租户 scope patch 的 `Mount / Unmount`，取代 v1 的 `Enable / Disable`（R34）。

它不提供面向业务用户的写端点。写操作都是平台管理动作，由运营工具或宿主管理面在自己完成鉴权后调用；`actor` 只写进记录的操作者字段。唯一的例外是租户管理员批准组件反向权限的管理面 API，判定在控制器内完成，见「反向权限批准」。

## 宿主装配

`LifecyclePlugin` 只接受 global scope，必需服务键是 `Data`、`Registry`、`Config`；可选服务键（清单 `optionalInject`，与 `LifecyclePlugin.OptionalInject` 同一份）是 `Audit` 与 `AuthzSuperadminBypass`：缺席时仍装载，按调用解析，缺席的后果见下文「缺审计时的口径」。宿主还需要准备：

- 数据上下文 `LifecycleDbContext`，迁移随包交付，在宿主启动前执行。
- Registry 与 Lifecycle 的集成事件类型和领域事件处理器。
- 信任根所在的配置节与秘密解析策略。
- 可选的进程内钩子解析器 `LifecycleRuntimeOptions.Hooks`：按组件 ID 与精确版本返回 `IPluginLifecycleHooks`，组件没有钩子时返回 null。本包不从数据库或制品内容加载程序集。
- 管理面端点（诊断与反向权限批准）需要宿主装配的权限判定端口（Kun `IPermissionChecker`），平台主体另需 Authz 的超管旁路；批准与租户级生命周期操作（开通、升级、卸载、清除）另需该租户 scope 可见的审计服务（`ctx.Audit()`），且 Lifecycle 的数据库登录须获审计同事务函数的执行权（`AuditTransactionAccess.CreateMigrationSql` 的生产方角色）；平台级操作（安装、激活、回切、弃用、退役）另需 global 审计条目配置平台链：宿主单独创建受限的平台审计登录、迁移时在租户守卫之后应用 `AuditPlatformAccess.CreateMigrationSql(role)`、以 `AddQianData<PlatformAuditDbContext>` 绑定该登录，并在 `qian.audit` 的 global 条目写 `chain.platformRole`（Audit README「受控平台链」；参考宿主见 `tests/Qian.Lifecycle.Tests/LifecycleHost.cs`、CRM 验收宿主与 `tools/Qian.Dev.TenantOps`）。
- 缺审计时的口径（S1-RT-037，建议裁决 ② 2026-10-23 拍板）：管理面批准缺判定端口、缺旁路或缺审计都与不存在同为 404（R32），对外不区分；租户级生命周期操作缺审计时以 503 `LIFECYCLE_AUDIT_UNAVAILABLE`（`operation` 为操作名）拒绝，不认领执行记录、不改状态；平台级操作在 global 上下文看不到审计服务、或审计端口拒绝本组件为平台生产方（没配 `chain.platformRole`、平台审计登录不可用）时同样 503 拒绝、状态不变（global 看不到审计服务时安装在提交候选制品之前就拒绝；没配 `chain.platformRole` 只在追加时发现，此时候选制品已提交、不登记修订，重提幂等）。三处都先经装载器的日志器记一条 Warning（`Qian.Lifecycle` 事件 7「审计服务不可用」记租户链缺失、事件 8「平台审计链不可用」记平台链缺失，只有操作名、租户或原因，不含载荷），宿主装配错误由日志与无权限、状态冲突区分。

```csharp
builder.Services.AddQianData<LifecycleDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(LifecycleDbContext.Schema));
// Kun 的事件类型表只认第一次 AddKunEventBus，中介者只认最后一次 AddKunMediator：同一宿主的全部登记须并进各自的同一次调用。
builder.Services.AddKunEventBus(events => events.RegisterRegistryEvents().RegisterLifecycleEvents());
builder.Services.AddKunMediator(mediator => mediator.AddQianRegistryEvents().AddQianLifecycleEvents());
builder.Services.AddQianEvents();
builder.Services.AddQianSecrets(new SecretResolverOptions
{
    MountRoot = "/var/run/secrets/qian",
    Bindings = [new SecretBinding("secretRef://platform/trust/k1", new KubernetesSecretLocation("qian", "trust-k1", "public-key"))],
    // 策略 ID 即组件 ID：只允许 qian.lifecycle 解析信任根与执行器凭据。
    Policies = [new SecretAccessPolicy("qian.lifecycle", [new("trust/*", "platform")], [new("trust/*", "platform")])]
});

var lifecycle = new LifecyclePlugin(new LifecycleRuntimeOptions { Hooks = (pluginId, version) => hooks.Find(pluginId, version) });
await runtime.MountAsync([/* data、config、registry、capabilities … */ lifecycleDefinition], ct);
app.MapQianLifecycle(http => runtime.GetContext("lifecycle", ScopeKey.Global));
```

参考宿主 `samples/Qian.Sample.Crm` 的 `CrmCommandApplication.Build` 为此提供了 `events` / `mediator` 两个合成参数，嵌入平台组件的宿主经它们把登记并进同一次调用。漏装领域事件处理器时，激活与升级在提交前失败，不会出现状态已改、事件丢失的情况。

## 平台修订

`InstallAsync(artifact)` 依次执行：

1. 没有签名直接返回 `LIFECYCLE_ARTIFACT_UNSIGNED`。
2. 按 D4 格式重算目录摘要，与制品声明的摘要不一致返回 `INPUT_INVALID`。
3. 对重算的摘要验签，见下一节。
4. 按 `runtime.form` 分流：
   - module：宿主必须已预装同一组件、同一版本、同一摘要的绑定（`RegistryComponentBinding.FromArtifact`），否则返回 `LIFECYCLE_REVISION_NOT_DEPLOYED`。module 的「安装」即随宿主镜像发布。
   - service：清单的 `runtime.image` 必须是 `oci://…@sha256:` 引用，宿主须配置部署执行器。
   - 无代码组件：记为 `declarative`。
   - function / package：返回 `LIFECYCLE_DEPLOYMENT_UNAVAILABLE`，执行器在 S2。
5. 经 Registry `SubmitArtifactAsync` 做清单准入并登记候选制品。
6. 写入修订：同一组件的修订号递增，同一版本不能对应两个制品。

安装结论回写 Registry 的制品目录：验签结果写 `{algorithm: ed25519, keyId}`，状态写 `Installed`；service 修订另写 `Progressing` / `Healthy`。同一摘要重复安装返回已有修订；service 修订上次没有提交部署时，接着提交。

`ActivateAsync` 把原 Active 修订降回 Installed，再提升目标修订。`RollbackAsync` 走同一路径，只是目标修订号必须小于当前 Active。两者都要求目标修订未退役、service 修订已健康，并在同一事务把 `lifecycle.plugin.activated.v1` 放进 Outbox。数据库的部分唯一索引保证同一组件至多一个 Active 修订。

`Deprecate` 让修订不再接受新的开通，`Retire` 让修订不能再被激活、回切、开通或升级到；两者都只接受非 Active 修订。所有平台写入在 PostgreSQL 事务内持组件级 advisory lock，多副本同样串行。

## 验签与信任根

声明式元数据包与 module 制品使用 Ed25519 分离签名：

- 载荷是 D4 目录摘要的 32 字节原始值，与市场文件签名「对内容的 SHA-256 签名」是同一做法。
- `ArtifactSignature.KeyId` 是签名公钥 SubjectPublicKeyInfo（DER）的 SHA-256 指纹。

信任根放在 Apollo 公共 namespace `qian.common.trust`（base 组合包已把它列进 Apollo namespaces）。读法如下：

- `Trust:PlatformKeys` 节逐项给出 `PublicKey`、`NotBefore`、可选的 `NotAfter`。
- `PublicKey` 是 `secretRef://platform/…` 引用，解析出 PEM 编码的 Ed25519 公钥，实际内容在 K8s Secret 挂载里。
- 每次安装实时读取配置节：增删公钥或调整有效期不需要重启。
- 轮换时让新旧公钥的有效期重叠，重叠期内两把公钥的签名都有效；新公钥生效前、旧公钥过期后或撤下后，各自的签名失效。

以下情况一律返回 `LIFECYCLE_SIGNATURE_INVALID`：签名与摘要不符、公钥不在当前有效的信任根中、指纹与公钥不符、签名字节被改动。格式错误或无法读取的信任根条目被跳过并记日志，不会让其它公钥失效。秘密解析的审计只记引用与结论，不含公钥内容。

## 租户安装与钩子

租户安装状态为 `Entitled → Provisioning → Installed ⇄ Upgrading → Uninstalled → Purged`，没有 Enabled / Disabled：挂载与否只在 Registry 的 `scope_patches`（R34）。租户级方法由控制器自己进入目标租户的数据上下文，`tenant_plugin_installs`、`lifecycle_runs`、`mount_failures` 都是强制 RLS 的租户表。

- `EntitleAsync` 记录授权，由 `qian.entitlement`（S1-ENT-001）的授予调用。`policy.required` 的平台组件恒视为已授权。
- `ProvisionAsync` 开通当前 Active 修订，执行 `tenant-provision` 钩子。权益之后还要核对反向权限批准，见下一节。
  - 已 Installed 时直接返回，不认领执行记录、不调钩子、不写记录。
  - 钩子失败时停在 Provisioning，重试沿用原目标版本；重试同样先核对反向权限批准（两次尝试之间批准可能已被收窄），未批准时仍停在 Provisioning，不认领执行记录。
  - Purged 之后重新开通会作废旧执行记录。
  - 开通不挂载。
- `UpgradeAsync(from, to)` 用于升级，也用于租户级回切：
  1. 执行目标版本实现的 `tenant-upgrade` 钩子。
  2. 把本租户 scope 中引用 `组件@from` 的条目逐个改写为 `组件@to`，并立即合入。
  3. 成功后状态为 `Installed(to)`，并发出 `lifecycle.tenant-plugin.upgraded.v1`。
  4. 钩子失败或新版本挂载失败时，条目按相反顺序改回 from，状态回到 `Installed(from)`，挂载失败的原因记入诊断。
  - 数据迁移须前向兼容（扩展 → 迁移 → 收缩），控制器不承诺自动回滚数据。
- `UninstallAsync` 执行 `tenant-uninstall` 钩子，撤下本租户 scope 中引用该组件的条目，业务数据保留。
- `PurgeAsync` 只接受 Uninstalled，执行 `tenant-purge` 钩子删除组件的租户数据。

钩子的幂等键是 `(tenant, plugin, hook, toVersion)`，由此派生一个确定的 UUID（`LifecycleHookContext.IdempotencyKey`），重试时不变。`lifecycle_runs` 的执行规则：

- 执行记录的主键就是幂等键。成功后同一键不再执行；失败记录原因码，可重试。
- 钩子不在数据库事务内执行：先在锁内认领执行记录并取得租约（`runLeaseSeconds`，默认 600 秒），钩子返回后再在锁内写结论。
- 未过期的执行中记录让并发重试返回 `LIFECYCLE_IN_PROGRESS`；租约过期后，重试可以接管。
- 钩子返回值记入执行结果，上限 64 KiB，不得包含秘密或个人信息。
- 钩子上下文已进入目标租户的 scope，可解析该 scope 可见的服务（如数据组件），不要求组件已挂载。

## 反向权限批准（S1-AGT-002）

组件版本在清单 `policy.permissions`（`platform`、`data`、`secrets`、`ui.connectSrc`）与 `policy.egress` 里声明要向平台申请的五类反向权限。声明随制品快照保存在 Registry 的 `plugin_artifacts.manifest`；Lifecycle 经注册表的受信端口按修订摘要读取，不在 `plugin_revisions` 另存一份。

批准与权益是两道门，不合并：

- 权益（`Entitled`）回答「买没买」，由 `EntitleAsync` 记录。
- 批准回答「同不同意」，由租户管理员经 `ApprovePermissionsAsync` 写入本组件的租户表 `permission_approvals`（键为租户、组件、版本）。

`ProvisionAsync` 与 `UpgradeAsync` 在权益之后、置 Provisioning / Upgrading 之前核对批准，与安装行用同一把锁：

- `policy.required` 的平台组件、没有声明反向权限（或五类都为空）的组件不经批准。
- 否则目标版本声明的每一项都须已批准。缺任一项为 422 `LIFECYCLE_PERMISSIONS_UNAPPROVED`，`missing` 按类别列出缺失项；不写任何记录，也不认领执行记录。
- 目标版本没有自己的批准记录时，沿用该租户对该组件最近一次的批准。所以升级只有新增项才须重批，连续升级不会因中间版本没有记录而受阻。
- `authentication: ON_USE` 在 S1 同样在开通与升级时核对（fail-closed）。

批准 API（S1 只有 API，S2 才有 UI），由 `Qian.Lifecycle.AspNetCore` 的 `MapQianLifecycle` 映射：

- `PUT /api/lifecycle/tenants/{tenantId}/plugins/{pluginId}/versions/{version}/permissions`：请求体是五类批准内容 `{ platform, data, egress, secrets: [{ name, scope }], ui: { connectSrc } }`，缺省的类别视为空。整份替换该版本的批准记录；内容不变时不写入，批准时间、批准人与行版本都不变。
- `GET` 同一路径读取批准记录，没有记录为 404。
- 判定：路径里的租户须等于宿主已建立的租户上下文，路径只用来核对、不用来选择租户。调用者须是该租户已验证的用户或服务主体（无委托），并经宿主的权限判定端口持 `qian.lifecycle:install:approve`（默认授予租户 Owner 与 Admin）。平台主体只经 Authz 的超管显式旁路（运营代批，`approvedVia = superadmin`），由运营宿主先选定已验证的目标租户，旁路把 `bypass = superadmin` 记入该租户审计链。无权限、跨租户、委托、Agent、缺判定端口、未安装的版本都与不存在同为 404（R32）。
- 内容只在判定之后校验。请求体读不成（含未知成员，例如租户字段）为 `INPUT_INVALID`，pointer 为 `#`；形状不对或不是该版本声明过的项也是 `INPUT_INVALID`，pointer 指向请求体中的那一项。
- 批准人取已验证主体，不由调用方指定。响应带 `Cache-Control: private, no-store`。
- 批准入审计链（ADR-009 D9.8）：每次写入（首次批准或整份替换）在同一个可串行化事务里向该租户审计链追加一条记录，审计写不进去或事务提交失败时两者都不留；内容不变的重复批准不写记录，也不记审计。
  - 记录：能力 ID `qian.lifecycle/permissions.approve`、契约版本 `v1`、通道 `api`，主体即批准人（`user` 或 `service`），资源同判定资源 `lifecycle/install:{tenantId}/{pluginId}@{version}`，结果 `success`，发生时间即批准时间。
  - 输入哈希是规范化本次内容（每类去重并按序数排序）的 SHA-256；载荷为 `{ pluginId, version, via, approved, previous }`，`via` 为渠道（`tenant` / `superadmin`），`previous` 是被替换的上一份内容，首次批准为 null。两份内容合起来超过审计载荷上限（64 KiB）时，`approved` / `previous` 只记 `{ sha256 }`。
  - 运营代批同样写这条记录；超管旁路自己的判定审计由 Authz 另写。
  - 同一租户并发写审计链时，可串行化冲突的一方换新会话重做整个批准（最多 5 次），仍冲突返回 409 `CONFLICT`。
- 可信平台插件（例如秘密解析的审批来源）经 `GetApprovedPermissionsAsync` 读取批准记录，调用方负责权限。
- 批准与诊断两个权限点都在 `plugins/qian.lifecycle/metadata/permissions.json` 声明，`metadata/roles.json` 默认授予租户 Owner 与 Admin；Authz 从注册表的预装快照编译。

## 生命周期操作入审计链（ADR-009 D9.8，S1-RT-037）

租户级操作完成的状态迁移与该租户审计链的条目在同一个可串行化事务里提交（`AppendEnlistedAsync`，与批准同一做法）：审计写不进去或事务提交失败时，状态迁移与条目都不留；同租户审计链的可串行化冲突或死锁由控制器换新会话重做整个最终迁移（最多 5 次），仍冲突返回 409 `CONFLICT`。

- 记录点只在最终迁移处，各一条：开通 `Provisioning → Installed`（能力 `qian.lifecycle/tenant-plugin.provision`）、升级成功 `Upgrading → Installed(to)`（`tenant-plugin.upgrade`，与 upgraded 事件同一事务）、卸载 `Installed → Uninstalled`（`tenant-plugin.uninstall`）、清除 `Uninstalled → Purged`（`tenant-plugin.purge`）。所以钩子失败后的重试、续跑与幂等返回（已开通再开通、已卸载再卸载）都不重复记；钩子失败、升级失败回到 `Installed(from)` 不入链（ADR-009「业务失败即无审计」），原因在 `lifecycle_runs` 与诊断。
- 记录：契约版本 `v1`、通道 `api`、结果 `success`，资源同批准的判定资源 `lifecycle/install:{tenantId}/{pluginId}@{version}`（升级指向目标版本），发生时间即安装行的更新时间。主体取调用方打开的事件作用域（与集成事件同一口径：`EventEnvelopeScope` 的 actor，含 `on_behalf_of`），没有作用域时记本组件服务主体 `qian.lifecycle`；`actor` 参数作为 `operator` 写进载荷。输入哈希是 `[租户, 组件, 能力, 版本…]` 的 SHA-256。载荷：`{ pluginId, operator, version }`，升级为 `{ pluginId, operator, fromVersion, toVersion }`。
- 授权（`EntitleAsync`）、挂载与卸载条目（`Mount / Unmount`，经 Registry 同事务发 mounted / unmounted 事件）不在本节范围。
- 平台级操作（安装、激活、回切、弃用、退役）没有租户，记平台链（S1-RT-037 建议裁决 ①，2026-10-23 拍板为 A）。审计端口的平台链入口 `AppendPlatformAsync` 是独立事务（专用平台登录，RLS 只放行该登录），挂不进本组件的事务，所以在状态变更提交之前追加：追加失败（链冲突、数据库错误、平台链未配置）则本事务不提交、状态未改；追加成功之后、本事务提交完成之前的任何失败（提交失败、取消、审计端口已提交而客户端收到异常）都可能在平台链多留一条没有对应状态变更的条目（ADR-009 §3 的崩溃窗口取「宁多不漏」），重试成功后再记一条，复核时以修订行为准。
- 平台记录点各一条：安装登记修订（`qian.lifecycle/plugin-revision.install`；同一摘要重复安装返回已有修订不记，service 修订的部署观测与健康回写不入链）、激活 `Installed → Active`（`plugin-revision.activate`，与 activated 事件同一事务提交）、回切（`plugin-revision.rollback`，载荷同激活，`previousRevision` 为被降回的修订）、弃用与退役（`plugin-revision.deprecate / retire`，载荷带 `fromState / toState`）；已激活再激活、已弃用再弃用直接返回，不记。资源 `lifecycle/revision:{pluginId}/{revision}`，输入哈希是 `[组件, 能力, 修订号, 版本]` 的 SHA-256，发生时间取修订行的 `InstalledAt / ActivatedAt`（弃用、退役取操作时刻），主体、通道、契约版本与载荷里的 `pluginId / operator` 同租户条目，修订号在载荷里是数字；安装载荷另带 `artifactDigest / form / signedBy`。

## 挂载、契约准入与诊断

`MountAsync(scope, entry)` 经 Registry 的公开挂载写入 patch/v2 条目：同一事务写 `scope_patches` 与 `registry.scope.mounted.v1`，随后立即合入该 scope。带 `plugin` 的条目要求组件已开通、且版本与安装记录一致；预装的 `policy.required` 平台组件除外。

声明契约索引（`metadata/contracts.json`）的组件版本，在 Registry 的 scope Reconciler 合入时经平台契约准入（R76）：

- OpenAPI 契约以 `OpenApiContractPlugin` 与预装的较低版本基线比较。
- proto 契约以 `GrpcContractPlugin` 经宿主授权的反射对端比较；没有对端时 fail-closed。

挂载路径与 Reconciler 用的是同一个包装。装配失败时：

- 结果为 `Failed`，原因码为错误码，契约不兼容为 `PRECONDITION_FAILED`。
- 条目恢复为挂载前的 patch，该 scope 其余条目照常可用。
- 原因持久记入 `mount_failures`，同一条目之后挂载成功或卸载时清除。

`UnmountAsync` 写禁用 patch 或撤销本 scope 引入的 patch，同一事务发 `registry.scope.unmounted.v1`。卸载不改安装状态，不新增任何数据库开关。

`GET /api/lifecycle/diagnostics?scope=` 返回该 scope 的诊断，缺省是当前租户：

- 判定：调用者须是该租户已验证的用户或服务主体（无委托），并经宿主装配的权限判定端口持有 `qian.lifecycle:diagnostics:read`（默认授予租户 Owner 与 Admin）。平台主体与批准 API 一样只经 Authz 的超管显式旁路（资源记为 `lifecycle/diagnostics:tenant:<id>`）。无权限、跨租户、缺判定端口都与不存在同为 404（R32）。
- 内容：Registry 合入时的当前失败，以及经 Lifecycle 挂载或升级失败、已撤回的条目（组件修订与原因码）。
- 响应带 `Cache-Control: private, no-store`。
- 端点不挂插件门禁：scope 合入失败会收紧本 scope 的全部注册，诊断仍须可达。

## 部署执行器

`executor` 配置选择 `gitops` 或 `local`，完整契约见 `plugins/qian.lifecycle/schemas/config.schema.json`。省略时安装 service 组件返回 `LIFECYCLE_DEPLOYMENT_UNAVAILABLE`，module 与声明式组件不受影响。两种执行器产出相同的期望状态：

- 每个工作负载一个目录，包含 Deployment、Service 与 kustomization。工作负载名含版本，同一组件的多个版本可以同时运行。
- 容器使用 digest 引用的镜像，readinessProbe 指向组件的健康路径（`runtime.service.health`，缺省 `/health`；端口缺省 8080）。
- 容器 drop ALL capabilities，使用 RuntimeDefault seccomp，不挂载 ServiceAccount 令牌。
- 注解记录组件、版本、修订号与制品摘要。
- 同一请求逐字节生成相同的文件，重复提交不产生新提交。

健康是两个条件同时满足：工作负载就绪，且组件健康端点返回成功。观测结果回写修订的 `Health`，同时回写 Registry 制品目录的 `Progressing` / `Healthy`；任何读取失败都按未就绪处理。

- gitops：
  - 用 git 命令行克隆或同步部署仓库，把片段写到 `path/<workload>/`，维护 `path/kustomization.yaml` 的资源列表，以 `qian.lifecycle` 身份提交并推送；推送被拒时重新同步，最多三次。提交 SHA 是部署凭据。
  - 仓库地址不得带凭据，推送凭据由宿主环境的 git 凭据助手提供。
  - 观测读 Deployment 的 generation、updated / available 副本与版本注解，并经 K8s 服务代理请求组件健康路径。只读令牌与集群 CA 都是 `secretRef://platform/…` 引用，每次调用实时解析；配置 CA 后只信任它签发的证书链。
- local：开发态把同样的文件写到 `stateDirectory/<workload>/`，内容摘要是部署凭据。观测先连通端点、再请求健康路径；端点按 `组件@版本` 或 `组件` 配置，缺省 `http://127.0.0.1:<port>`。

## 数据与迁移

上下文使用 schema `plugin_qian_lifecycle`，包含五张表：

- `plugin_revisions`：平台级目录，不带租户。
- `tenant_plugin_installs`、`lifecycle_runs`、`mount_failures`、`permission_approvals`：租户表，迁移复用 Data 的 SQL 生成器自动加 FORCE RLS 与两条策略。`permission_approvals` 由迁移 `PermissionApprovals` 新建，批准内容为规范化（每类去重、按序数排序）的 jsonb。

迁移随包交付（`Migrations/`）。宿主必须在启动前以受限的迁移角色执行，因为 Data 的启动检查拒绝带着待执行迁移启动；应用连接使用非超级用户、无 BYPASSRLS 的角色。

## 集成事件

| 事件 | 产生方 | 载荷 |
|---|---|---|
| `lifecycle.plugin.activated.v1` | 激活与平台级回切（平台事件，不带租户） | 组件、修订号、版本、此前的 Active 修订 |
| `lifecycle.tenant-plugin.upgraded.v1` | 租户升级与租户级回切 | 组件、原版本、目标版本 |
| `registry.scope.mounted.v1` / `registry.scope.unmounted.v1` | Registry 的公开挂载与卸载（见 Registry README） | scope、条目、修订；mounted 另带组件与版本 |

状态行登记领域事件，`AddQianLifecycleEvents` 的处理器在同一 DI scope 把信封写进数据会话的 Outbox 缓冲，随本次保存落库；写入端在提交前核对已发布标记。

## 模型体验

不提供 Agent 工具。管理面诊断只返回条目、组件修订与原因码，不含配置、patch 内容或钩子结果；错误使用登记过的生命周期错误码，`LIFECYCLE_INPUT_INVALID` 的 pointer 指向 `plugins/qian.lifecycle/config#/…` 中出错的字段。

## 测试证据

- `tests/Qian.Lifecycle.Tests`（需要 Docker；GitOps 用例另启动 K3s 单节点集群并拉取 podinfo 镜像）：
  - `SignatureTests`：未签名、签名不符、内容被改、陌生公钥、签名字节被改都安装失败且不留记录；信任根重叠轮换。
  - `GitOpsExecutorTests`：本机裸仓库 + 真实 K3s，片段经 `kubectl apply -k` 收敛后双条件回写，工作负载缩到 0 后回到 Progressing。
  - `LocalExecutorTests`：等价的开发态执行器。
  - `LifecycleUnitTests`：迁移与 RLS、片段生成、配置解析、幂等键。
  - `PermissionApprovalTests`：五类声明随制品快照落库并经注册表端口读出；未批准的开通被拒且不写记录，逐项批准后开通，重复批准不改记录；无权限、跨租户、委托、Agent、未安装版本同为 404，内容校验只在判定之后；超管代批经旁路并记下目标租户与资源；`policy.required` 与无声明的组件免批；升级只在新增项时须重批；批准与整份替换入租户审计链、与批准同一事务、超限退为哈希、链冲突换会话重做；开通续跑按原目标版本（而不是两次尝试之间激活的新 Active 修订）重新核对批准。
  - `LifecycleAuditTests`：开通、升级、卸载、清除各写一条租户审计链条目（主体取事件作用域，否则为本组件服务主体），重复操作、续跑与钩子重试不重复记；审计写入失败或提交失败时状态迁移与条目都不留且可续跑；宿主撤下审计后操作以 503 `LIFECYCLE_AUDIT_UNAVAILABLE` 拒绝、状态不变并记宿主级告警，批准 API 仍 404 且同样记告警；同租户两个组件并发完成开通时链冲突的一方换新会话重做、三条记录都在。平台链：安装、激活、回切、弃用、退役各一条平台条目（序号连续，资源、输入哈希与载荷逐字段核对，租户链为空），重复安装与幂等返回不记；平台条目写不进去时激活不提交，状态变更提交失败时平台链多留一条，没配 `chain.platformRole` 或撤下审计时以 503 拒绝、状态不变并记事件 8 的告警。
  - `LifecycleUnitTests` 另核对清单的 `optionalInject` 与 `LifecyclePlugin.OptionalInject` 一致。
  - `TenantFieldInstallTests`：`tck/surfaces/cases.json` 中工具输入点名租户字段的 42 个反例（含 properties 之外的写法与越出 Schema 位置的引用，S1-AGT-004 / S1-AGT-007）经 `InstallAsync` 全部失败（`INPUT_INVALID` / `REGISTRY_INPUT_INVALID`，pointer 与 fixture 相同），不登记修订、不留候选制品；只是含有 tenant 的名称照常安装。
- `tests/Qian.Sample.Crm.Tests` 的 `LifecycleAcceptanceTests`：参考应用 1.0.0 / 1.0.1 / 1.1.0 三个预装修订，在真实 PostgreSQL、Kestrel 与 JWT 下覆盖：
  - A、B 开通与挂载，重复开通无副作用，卸载后 5 秒内 SDK 入口恢复 404。
  - 激活 n+1 后 A 升级、B 保持 n，平台级与租户级回切后 A 由 n 处理且升级期间写入的数据可读。
  - 破坏性 OpenAPI 契约在挂载、Reconciler 与升级三条路径上为 Failed，原因进入诊断端点。
  - `PermissionApprovalHttpTests`：批准 API 经真实 JWT 与 RBAC，A 的 Owner 批准后才能开通，B 的使用者、跨租户与未安装版本同为 404，带租户字段的请求体为 `INPUT_INVALID`。
- `tests/Qian.Capabilities.Tests` 的 `GrpcReflectionTests`：契约准入对 proto 契约套用 `GrpcContractPlugin`，没有反射对端时 `PRECONDITION_FAILED`，有对端时兼容版本 Ready。
- `tests/Qian.Registry.Tests` 的 `ScopeMountTests`：公开挂载 / 卸载的 patch、Outbox 信封与 5 秒内 SDK 拦截恢复。

## 已知限制与延期项

- 网关路由与前端外壳两处「未挂载」拦截的接线归 S1-ENT-002；这里只验证 SDK 请求入口。
- cosign、OCI 制品与 Harbor 的签名链归 S1-RT-013（`qian pack / sign / publish`）。本包只验声明式包与 module 制品的 Ed25519 分离签名。
- Argo 收敛与发布链归 S1-DEP-005：GitOps 测试以 `kubectl apply -k` 代替 Argo 应用部署仓库。资源配额、NetworkPolicy、迁移 Job 等完整片段归 S1-DEP-004。
- 授权表与权益 API 归 S1-ENT-001，经 `EntitleAsync` 进入状态机。租户开户编排（主档、Kun `ITenantProvisioner`、`tenant_provision_steps`）归 S1-TEN-003，并由它调用这里的 `ProvisionAsync`。
- service 形态的 gRPC 钩子（`qian.lifecycle.v1.Lifecycle`）尚未实现，service 组件的钩子步骤按无钩子处理。平台级 install 钩子与 `migrations.platform / tenant` 的编排也未实现；module 的迁移随宿主镜像在启动前执行。
- `Retire` 不检查是否仍有租户停在该版本：租户安装行受 RLS 约束，平台操作不跨租户查询，平台超管旁路归 S1-AZ-003。
- `Mount` 只接受当前租户的 tenant scope；app / agent-session 的归属证明尚未实现，保持拒绝。
- 挂载失败时恢复挂载前的 patch，不让失败条目阻断整个 scope；失败原因经诊断端点给出。
- 反向权限批准只有 API：管理 UI 归 S2。批准入租户审计链但不发集成事件；超管代批除批准条目外另由 Authz 旁路记自己的判定审计。
- 平台级操作入平台链不是同事务（审计端口没有平台链的同事务入口）：状态变更提交失败时平台链可能多一条孤儿条目（宁多不漏）；补同事务端口要改 Audit 的 RLS 策略与 SECURITY DEFINER 函数，归 Audit 所有者另立任务（S1-AUD-009）。
- 批准记录还没有消费方：秘密解析、出站 egress 与外壳 CSP `connect-src` 仍按宿主配置生效，按批准记录收紧归后续接线。撤销只能整份替换为更小的内容，不影响已开通的租户，只影响之后的开通与升级。

S1-RT-029 的 Registry 管理写入通过 `MountAtRevisionAsync` / `UnmountAtRevisionAsync` 复用 Mount / Unmount 核心，显式携带调用方修订，不用服务端读取的新版本覆盖预期值。重复卸载的禁用 patch 与墓碑在当前修订下幂等，旧修订仍拒绝；授权统一在 Registry 管理入口，内部受信任生命周期接口不直接暴露为匿名 HTTP。

## 清单入口与代码派生

`plugins/qian.lifecycle/.qian-plugin/plugin.json` 的稳定入口是 `LifecycleComponent`，允许 global scope，依赖 `Data`、`Registry` 与 `Config`，可选依赖 `Audit` 与 `AuthzSuperadminBypass`（`optionalInject`），提供 `Lifecycle`。使用宿主登记的 `LifecycleRuntimeOptions`，未登记时采用空钩子解析器，再委托 `LifecyclePlugin`。

入口的 `Inject`、独立服务声明与 `ComponentConfiguration.Schema` 是清单代码字段及配置 Schema 的来源；通过 `qian-plugin sync` 更新组件目录，再用 `check` 验证漂移。`optionalInject` 不由生成器派生，手写在清单里，与 `LifecyclePlugin.OptionalInject` 的一致性由 `LifecycleUnitTests` 守住；seam 图把它画成虚线并标「（可选）」，profile 装配在 Provider 在树里时把它排在本组件之前。元数据补齐不替代真实安装验收，本组件继续保留 `x-skeleton`。
