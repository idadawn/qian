# Qian.Config.Apollo

Apollo 原始 namespace、合并快照和离线缓存强制经过 `SecretSafety` 扫描；`ApolloConfigurationSchema` 可传入敏感属性规则，`ValidateApolloSnapshot` 只能追加校验。秘密通过 Config seam 的 [密钥 Provider](../Qian.Config.Secrets/README.md) 使用时解析，不能以明文写入 Apollo。

Config seam 的 Provider，复用当前 KunVersion 的 Kun.Configuration.Apollo 协议客户端。文件夹包含标准配置源装配、Apollo 引导解析、只读适配器、两个只读插件入口与一个可选草稿管理入口。标准模式只用 IConfiguration；Apollo 模式用于平台/服务配置，租户设置仍由 Tenancy 管理。

## 独立使用与源顺序

```csharp
var root = new ConfigurationBuilder().AddQianConfigurationSources(new QianConfigurationOptions
{
    BasePath = contentRoot,
    EnvironmentName = environmentName,
    SecretDirectory = mountedSecretDirectory,
    Arguments = args,
    ResolveAccessKey = reference => secretStore.Resolve(reference)
}).Build();
services.AddQianConfiguration(root);
services.Configure<SampleOptions>(root.GetSection("Sample"));
```

构建器必须为空，避免未知源悄悄改变优先级。顺序固定为 appsettings.json / appsettings.{EnvironmentName}.json → Apollo 公共 namespace → 插件 namespace → application → K8s Secret 文件 → 环境变量 → 命令行。公共/插件 namespace 各自保持声明顺序，application 固定最后。Secret 目录配置后必须存在，文件名中的双下划线遵循 KeyPerFile 的层次键规则。独立模式设 UseApollo=false 后仍保留其它标准配置源。

配置根由创建方释放；根释放时关闭文件监视与 Apollo 长轮询。宿主应使用同一根绑定 IOptionsMonitor 与只读 IConfigService。HTTP 宿主继续安装统一 Qian 错误处理。

## 引导项

引导只读取宿主文件、Secret 文件、环境变量和命令行，不读取 Apollo 自己返回的值。可以仅用以下环境变量启动：

| 键 | 约束 |
|---|---|
| Apollo__AppId | 必填应用标识 |
| Apollo__Env | 必填 UAT 或 PRO，用于校验和缓存隔离 |
| Apollo__ConfigServer__0 等 | 直连 ConfigService 的 HTTP(S) 地址，无内嵌凭据、查询参数和 fragment |
| Apollo__Cluster | 默认 default |
| Apollo__Namespaces__0 等 | 可选，默认 application；允许 qian.common.*、qian.plugin.*、application |
| Apollo__Label | 可选实例灰度标签 |
| Apollo__CacheDir | 可选缓存目录，下面按 UAT/PRO 隔离 |
| Apollo__AccessKey | 可选 platform/dev secretRef；有引用时必须提供解析回调 |

AccessKey 每次签名前解析；解析为空或服务返回 401/403 时，不用缓存伪装为首次加载成功。引用和引导值不进入 Apollo 快照；签名和协议缓存复用 Kun。默认客户端超时 70 秒，满足服务端 60 秒长轮询；自带客户端需由调用方管理生命周期及可信重定向。

## 插件装配与模型体验

ConfigurationPlugin 对应 qian.config，借用宿主标准配置根。ApolloConfigurationPlugin 对应 qian.config.apollo，要求根中确有本包安装的 Apollo 源，并核对 appId、env、meta 和 namespaces 声明；不匹配直接失败。两者只挂载于 global，提供 Config 服务键，经 ctx.Config() 读取。插件卸载撤销自己登记的服务和错误码，宿主配置根继续由宿主管理，不破坏独立消费者。

`ConfigurationPlugin` 与 `ApolloConfigurationPlugin` 分别带 `[QianPlugin]`，对应 `qian.config` 与 `qian.config.apollo`。同一程序集生成两份按 ID 隔离的元数据，分别同步各自唯一清单的 inject、provides、runtime 和 configSchema；标准入口直接登记 Config 服务；Apollo 公开入口通过 DelegatePlugin 显式委托内部实现，包含同一 Config 登记、重载通知与发布审计。CI 检查两个入口的清单漂移。程序集引用 Qian.Plugin.Sdk，生成器只参与编译。S1-RT-011 已从私有源 `qian add config` 安装并运行标准入口；Apollo 的清单已派生，完整安装门仍待验收。

只读配置端口面向受信任代码，不自动生成模型工具；秘密解析仍经 Config 的权限策略，配置值 HTTP 接口按下文执行授权与脱敏。

## 强类型配置：先校验、失败保留旧值

```csharp
services.AddQianOptions<SampleOptions>("Sample", (section, options) =>
{
    if (section["PageSize"] is { } value) options.PageSize = int.Parse(value, CultureInfo.InvariantCulture);
}, plugin: "qian.sample", schema: SampleJson.Default.SampleOptions);
services.AddSingleton<IValidateOptions<SampleOptions>, SampleOptionsValidator>(); // [OptionsValidator] 源生成的 DataAnnotations 校验器
```

`AddQianOptions<T>` 把 `ValidatedOptionsMonitor<T>` 登记为 `IOptionsMonitor<T>`：配置根变更后先构造并校验候选值（`IValidateOptions<T>`，以及绑定委托抛出的格式、溢出与操作异常），通过才替换并通知监听者。失败时保留上一份有效值、不通知监听者，计数 `qian.config.reload.failed`（Meter `Qian.Config`，Prometheus 导出名 `qian_config_reload_failed_total`，标签 `options.type`、`options.name`、`plugin`），记 Warning 日志（EventId 2101，不含值），并在配置组件挂载期间发进程内事件 `config/reload-failed`；修正后的下一次变更照常生效。`critical` 默认 true，经 `ValidateOnStart` 在启动时校验；设为 false 时首次读取才校验，首次读取失败直接抛出。同一类型只能登记一次。绑定由调用方委托完成，不走反射绑定，配合源生成校验器接受 AOT 分析；本包打开 `IsAotCompatible` 启用兼容性分析；`qian.config.apollo` 的实际兼容声明以 GW-001 的完整生产宿主严格 Linux NativeAOT 发布与真实 Apollo 运行证据为依据，默认 gateway 已恢复 AOT。业务镜像交付仍由 DEP-008 验收。传入 `schema`（`JsonTypeInfo<T>`）时同时登记 `QianOptionsSchema`，`Export()` 输出不含配置值的 JSON Schema 2020-12。

Kun 的 `AddKunOptions` 三个重载都要求动态代码，`LastGoodOptionsMonitor` 是 internal 且没有失败钩子，所以本包自带等价监视器（S1-CFG-003 路线 b）；Kun 补上 AOT 入口与失败钩子后可以改回复用。

告警事件 `ConfigRuntimeEvents.ReloadFailed`（Emit）的载荷是 `ConfigReloadFailed`：options 类型与名称、配置节、失败条数、错误码 `CONFIG_RELOAD_REJECTED` 与可选组件 ID，不含配置值，监听者用 `ctx.On(ConfigRuntimeEvents.ReloadFailed, ...)`。两个 Provider 都会转发，所以 standalone 同样可用；事件 Definition 由 qian.config 唯一拥有，qian.config.apollo 以 SharedEvents 显式引用，两个 metadata/events.json 的 name、mode、payload、source 与 definitionComponent 同源。此共享只覆盖事件契约，不共享权限声明；配置组件未挂载时只有指标与日志。跨进程集成事件名 `config.reload.failed.v1`（`ConfigEvents.ReloadFailedType`）先保留：Kun 的集成事件总线只能在 DbContext 工作单元里暂存，配置重载不在业务事务中，跨进程投递等 S1-EVT-003 发布 Outbox 写入契约后接上。

## Apollo 发布进平台审计链

ApolloConfigurationPlugin 把每个 namespace 快照的接受与拒绝写进平台审计链（PRD 7.8 / 7.11）：能力 ID `qian.config/release.applied` 与 `qian.config/release.rejected`，主体为 `service` + AppId，通道 `event`，资源 `apollo://<AppId>/<Env>/<Cluster>/<namespace>`，result 为 applied / rejected；`inputHash` 是脱敏后规范化快照的 SHA-256（敏感键以 `[REDACTED]` 参与），补充数据只有 namespace、变更键数与拒绝原因码。拒绝在 Kun 客户端的 namespace 与合并快照校验处记录，接受在重载回调或从缓存启动时确认；同一 namespace 的同一快照、同一结果只记一次，客户端每 5 秒重拉被拒候选不会重复入链。

审计组件可以缺席：记录暂存在有界队列（256 条，满时丢弃最旧的），`qian.audit` 就绪后按序补写；审计组件卸载后等新的服务就绪再继续。写入失败（例如平台链未显式启用 `chain.platformRole`）首次记 Warning（EventId 2112），之后记 Debug，记录丢弃，不阻断配置生效。已知不覆盖：namespace 内容变了但合并结果不变的发布会立即确认；Apollo 的 releaseKey 在 Kun 客户端内部，记录里没有。

## 配置 Schema 与配置值接口

`Qian.Config.AspNetCore` 只依赖 Definition 与 Runtime：

```csharp
app.MapQianConfig(http => configContext); // global 配置组件上下文，须能注入 Registry、Config 与 AuthzEnforcement
```

- `GET /api/config/schemas/{plugin}`：对已认证调用方返回组件 config.schema 原文（`application/schema+json`），不含配置值。Schema 只取 Registry 预装集合中由 `RegistryComponentBinding.FromArtifact` 捕获的制品文件；未预装、只有静态绑定（没有文件快照）或没有 Schema 的组件为 404。
- `GET /api/config/values/{plugin}`：只对平台主体开放，租户主体在判定前 403。处理器内经 `IAuthzEnforcement` 判定 `qian.config:values:read`（拒绝 403、审计不可用 404，都写决策审计），放行后返回配置节 `qian.plugin.<组件 id>` 的 `{ key, sensitive, value }`；Schema 标 `x-qian-sensitive`、疑似密钥键名或值的键 `sensitive: true` 且不给值，也不给引用。先判定再查存在，未授权调用方看不出哪些组件已预装。
- 两个端点归属 `PlatformHttpContract("qian.config")`，响应带 `Cache-Control: private, no-store`，错误为 ErrorDetail；值端点带 `RequiresPermission` 元数据，OpenAPI 写出 `x-qian-permission`。宿主不要再给它加 `RequireQianPermission`，否则会重复判定。权限点声明在 `plugins/qian.config/metadata/permissions.json`；qian.config 已通过既有真实安装门并移除骨架标记，其权限声明随制品预装后由 Authz 的正常路径读取；声明不等于授予。qian.config.apollo 保留骨架标记，等待自己的真实安装验收。

## Apollo namespace 骨架

组件私有 namespace 为 `qian.plugin.<组件 id>`，键名约定 `qian.plugin.<组件 id>:<属性路径>`（带 namespace 名，合并进 `IConfiguration` 后不与其它组件或 application 撞键，组件以 `ReadSection("qian.plugin.<组件 id>")` 读取）。[Qian.Config.Tool](../../tools/Qian.Config.Tool/README.md) 由组件 config.schema 推导每个宿主的 namespace 骨架并比对线上键，缺键、多键即非零退出。

## 运维边界

L0–L4 降级矩阵、排查顺序与证据见 [配置运维手册](../../docs/ops/configuration-runbook.md)。

## 验证与边界

快速测试复用 ApolloFixture 协议替身，覆盖五层顺序、namespace 顺序、环境引导、访问密钥引用和插件生命周期；S1-CFG-003 的热更新校验（OptionsReloadTests）、发布审计（ReleaseAuditTests）、namespace 骨架比对（NamespaceSkeletonTests）同样以它为线上。最终验收的四个真实 Apollo 类同属一个集合，共用一套 RealApolloPortalFixture：真实 Apollo 2.5.2 Portal、ConfigService/AdminService 与 MySQL 8.0.43，整个集合只启动一次（S1-QA-017）。通过 AdminService 编辑并发布，验证同一个 IOptionsMonitor 在 60 秒内更新、停服务后缓存可启动、无缓存失败，以及一次发布在真实 PostgreSQL 平台审计链上多一条记录、链校验通过、持久化行不含配置键值（RealApolloReleaseAuditTests）。配置 Schema 与配置值接口由 ConfigEndpointsTests 在真实 Kestrel + JWT 上验证。

这证明本机/CI 的真实服务链路。生产 Apollo 的高可用部署、Portal OIDC 登录与 Portal 侧操作审计及托管 MySQL 仍按部署任务（S1-DEP-002）验收。

## 同程序集的两个组件

`ConfigurationPlugin` 对应 `qian.config`，`ApolloConfigurationPlugin` 对应 `qian.config.apollo`；每个 ID 各有一个入口和一份清单。二者只允许 global，均提供 `Config`，不会把两个入口的服务声明合并为虚构组件。前者仅接受空对象并借用宿主配置；后者逐项校验 appId / env / meta / namespaces 与宿主已有 Apollo 配置源一致，配置 Schema 来自 `ComponentConfiguration`。

`qian.config` 已通过既有真实安装门，移除骨架标记；Apollo 组件仍保留标记。新增代码元数据不改变配置源所有权或秘密解析边界。

## 特性开关与草稿管理

`IFeatureFlags` 是 Config seam 的只读子端口：组件通过 `ctx.Config().Flags.IsEnabled(name, tenantId)` 求值，独立宿主的 `AddQianConfiguration` 同时登记 `IFeatureFlags`。它不查询权益、不增删租户 scope，也不把灰度规则当作租户设置。规则源为公共 namespace `qian.common.flags` 的 `flag.<name>`；标准 IConfiguration 模式可提供同样的键。配置优先级沿既有源顺序，Apollo 原始 namespace 和合并快照都先校验，合法高优先级覆盖仍被允许。

首次启用时先在 Apollo 建立并发布公共 namespace `qian.common.flags`；跨应用消费先关联该公共 namespace。随后在消费宿主的可信 `Apollo:Namespaces` 引导和该宿主 `qian.config.apollo` 条目的 `config.namespaces` 中同时加入它，两处必须与真实配置源匹配。用该宿主自己的配置或 patch 声明订阅；仓库默认 base / gateway / identity-server 仍保留原订阅列表，不因引入这个库自动请求新 namespace。没有订阅或没有该规则时默认 false；已声明但服务端不存在或尚未发布的 namespace 会按现有启动门拒绝，不能把 404 当作有效空配置。

例如已有 `application`、`qian.common.trust` 的宿主，显式启用后组件条目为：

```yaml
- id: config
  plugin: qian.config.apollo
  config:
    namespaces: [application, qian.common.trust, qian.common.flags]
```

宿主可信引导也应包含这三项；草稿管理组件不能替消费宿主修改订阅。

规则值是 JSON 字符串，例如：

```json
{"default":false,"tenants":["00000000-0000-0000-0000-000000000001"],"percentage":10}
```

[规则 Schema](../../plugins/qian.config.apollo/schemas/feature-flag.rule.schema.json) 固定三个字段：必填布尔 `default`、可选且不重复的非空规范 UUID 白名单 `tenants`（最多 256 项）、可选整数 `percentage`（0–100）。拒绝未知或重复属性、空租户、非整数百分比、超界与超过 16 KiB 的规则，最多 1024 个开关。名称为 1–100 个小写 ASCII 字符，首字符为字母，其余可为字母、数字、点、下划线或连字符。名称区分大小写，不能借 IConfiguration 的不区分大小写行为建立别名。

求值顺序为白名单命中 true → 百分比命中 true → default；没有该名称返回 false。稳定桶协议为 `SHA-256(UTF8(name + "\n" + tenantId.ToString("D")))` 前 8 字节的大端无符号整数模 10000，桶小于 `percentage × 100` 即命中。同一个规则名与租户跨进程相同；百分比增加时原来命中的集合保留。它与进程随机化的 GetHashCode 无关。

首次规则无效时拒绝取得服务；热更新先建立完整候选快照，任何规则无效即保留整份旧快照。标准源触发已有 `qian.config.reload.failed` 指标与 `config/reload-failed` 事件，日志不含值；Apollo 候选在进入缓存之前被拒绝，并复用发布拒绝审计。卸载只释放本实例监听器，已取得的灰度端口失效，宿主配置根仍由宿主拥有。

可选的 [`qian.config.flags`](../../plugins/qian.config.flags/README.md) 独立负责草稿写入。S1-DEC-031 选择只保存草稿、Portal 人工发布：固定 Portal OpenAPI `PUT` 编辑，禁止重定向，不调用 release；编辑 Token 只授予目标 namespace 的 ModifyNamespace，独立操作员持有 ReleaseNamespace。每次请求经 Config 的秘密解析器取得平台 `secretRef://platform/apollo/flags/...`，不缓存 Token、不把它放入工具输入。Apollo 响应正文不进入错误或日志。

可信宿主通过 `AddQianFeatureFlagManagement` 登记 `bindingId → ApolloFlagManagementBinding`，包含 Portal 根地址、AppId、UAT/PRO、Cluster、Portal 已存在操作员、Token 引用及 NativeData 平台回执绑定名；组件 JSON 只写 `{"bindingId":"primary"}`。该入口在本组件挂载时才登记实际 `FeatureFlagDrafts` 与执行绑定，不是绕过组件模型的任意配置写接口。默认 profile 不安装它。需要 HTTP 的宿主显式调用 `MapQianFeatureFlags`，通过 `POST /api/config/flags` 调用同一执行端口；入口固定要求 Bearer scheme，不接受 Cookie 认证，响应 `private, no-store`。

`flags.set` 元数据为 `write`、`confirm`、`requiresConfirmation:true`、`idempotent:true`，要求 `qian.config.flags:flag:set`。输入包括 `name`、`rule`、必填非空 UUID `idempotencyKey` 和 `confirmed:true`；确认字段只表达显式动作确认，不能代替身份、权限或未来 S3 审批票据。执行每次重读宿主已经认证的 ICurrentUser，拒绝租户、Agent 与委托主体，经 AuthzEnforcement 的平台旁路判定，再先写平台审计；任一前置失败都不请求 Portal。S1 不向 Agent 开放此平台工具。

回执存储使用独立 `plugin_qian_config_flags.draft_receipts`，由 `FeatureFlagDraftStorage.MigrateAsync` 在离线迁移连接上建立。在线连接是 NativeData 的专用 LOGIN/NOINHERIT 平台绑定，不能建库、建角色、BYPASSRLS、拥有表或 schema。表 FORCE RLS，tenant_id 只能为空；固定 restrictive guard 同时检查平台登录角色、空租户列和空租户 GUC。角色只有 SELECT、INSERT、UPDATE(state,completed_at)，没有删除、TRUNCATE、TRIGGER 或修改幂等输入字段权限。NativeData 的预期策略由 `FeatureFlagDraftStorage.ExpectedGuard(role)` 给出，不能从线上自动学习。管理组件挂载和每次回执事务还核对本表的真实主键、固定 guard 与列级权限；事务持有表锁，避免校验到执行之间的迁移漂移。

回执按目标 Portal/应用/环境/集群/namespace 与幂等键唯一，保存主体、规范输入摘要、规则摘要和状态，不保存规则内容。先提交 `started` 再发送编辑请求；成功后持久化 `pending-publish` 再返回。相同键、相同主体与输入重放原回执，不再次写 Portal，也不会覆盖之后的新草稿；回执状态描述原操作，不查询或断言当前是否已经人工发布；同键不同主体或输入返回 CONFLICT。并发尚在执行、崩溃或外部结果不明的旧键失败关闭；保留 started/uncertain，不盲目重发，也不把未知报成成功。需操作员核对 Portal 后显式使用新键；新键表示新编辑请求，正常覆盖该名称当前草稿。操作审计仅带尝试ID、幂等键、目标摘要和结果，可与持久回执对应，不带 Token 或规则明文。补充审计失败不能逆转已持久成功回执，只记不含原值的告警。

真实 Apollo 公共 namespace 与 Runtime scope 挂载/卸载验收在 `RealApolloFeatureFlagsTests`；权益结果在本任务测试中明确为消费侧 fixture。真实权益、挂载与灰度的三处联合拦截由既有 S1-ENT-002 验收，不以本任务代替。Portal 权限分离、草稿不生效、独立发布后真实 ConfigService 生效，以及真实 PostgreSQL 回执与故障窗口由专属测试覆盖。
