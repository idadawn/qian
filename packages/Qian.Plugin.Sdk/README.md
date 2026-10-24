# Qian.Plugin.Sdk

本模块是 S1-RT-014 / S1-RT-033 的 SDK 与源生成器入口。开发者规格为组件模型 §3、§5 及 PRD §7.2.3；组件库只依赖 Runtime、SDK 与 Definition，SDK 自身不引用 Provider。当前交付代码声明、源生成能力契约、唯一清单校验、已在真实 CRM 命令中使用的 Mediator/事务/waterfall 管道，以及包族在 GitHub Packages 的 NuGet 锁步发布；组件的清单派生、真实安装与平台验收分别有各自的验证门。

## 代码契约

QianPluginAttribute 标注一个组件 ID 的顶层非泛型 IQianPlugin 入口。同程序集可以有多个不同 ID；每个 ID 对应唯一清单、一个 runtime.entry，check / sync 按目标清单 ID 选择元数据。重复 ID、跨组件能力归属均失败；多组件程序集里的 CapabilityAttribute 必须填写 ComponentId，单组件程序集可省略。

ComponentMetadataGenerator 用 Roslyn 语义符号读取 Inject 集合和 ApplyAsync 的服务登记。直接 Provide 支持公开静态只读 ServiceKey 字段与常量构造，也支持固定名称的 Provide(key, name, value)。CommandPolicies.Register 按真实 SDK 方法与固定策略种类派生 CommandTenancy / CommandAuthorization / CommandAudit，并核对组件归属。注册表 Register 由独立 Contributes 声明绑定具体注册表类型、服务键字段和固定贡献名；同名的无关 Register 不能冒充扩展点：

```csharp
[Contributes(typeof(DataDialectContract), nameof(DataDialectContract.Key), "pg",
    RegistryType = typeof(IDataDialectRegistry))]
```

条件服务注册、动态 Inject、动态贡献名、未知上下文逃逸仍报 QIANSDK002，不把未知结果当作空集合。不同构造模式改变依赖的可编程实现，使用固定模式的薄入口与 DelegatePlugin 声明：入口显式给出 Inject，通过 Provides / Contributes 声明完整的普通服务输出，只能恰好一次无条件委托给指定 IQianPlugin 的真正 ApplyAsync，并转交同一 ctx、config 和 ct。运行时装载仍校验实际提供项；调用方不得把不同模式的依赖并成一个虚假清单。

这些实现构件及契约包装器、动态表面适配器不另造组件 ID。具体归属在 [实现构件分类](../../scripts/plugin-implementations.json) 登记：Authz、Identity、OIDC 的原可编程实现归所属组件；Capabilities 的 OpenAPI / gRPC / 导航包装器和 Registry 的 scope 适配器保留被包装组件归属；Surfaces / HookBridge 使用输入组件快照。CI 从全部已编译的 packages / samples 程序集发现真实 IQianPlugin 实现，新实现既无入口注解也无有效分类就失败，删除实现后留下的分类也失败。

CapabilityAttribute 标注顶层非泛型 partial 请求，并指定响应类型和 System.Text.Json 源生成上下文。上下文必须显式登记请求与响应。生成器校验短名、描述、冻结词汇、scope、权限点和幂等声明，生成该请求的 QianCapability 属性。它使用正式 Kun CapabilityDescriptor 从 JsonTypeInfo 导出输入/输出 Schema；不会用反射序列化请求，也不执行业务 handler。序列化设置属于真实契约：例如 UnmappedMemberHandling.Disallow 和必填构造参数必须与公开输入 Schema 一致。

描述、SideEffect、PolicyHint、RequiredScopes、RequiredPermission、RequiredSecrets、数据/成本分级、版本、Agent 设置与审计声明保存在不可变契约中。写操作默认审计，查询默认关闭，可显式覆盖；destructive 的策略提示默认 confirm。生成契约只提供事实，不会凭这些字段授予权限、建立可信主体或绕过安装状态。

## 实际命令执行

生成请求实现 ICapabilityRequest，同时生成不可变 RequiredPermissions。AddQianCapability 经已有 AddAuthorizedHandler 原子登记权限行为；宿主提供实际插件上下文解析器和固定通道，请求体不能选择这些值。当前入口处理带真实租户和非空权限点的数据能力，平台 null 租户、匿名能力和完整 app/session lineage 尚未开放。

Kun 每次 Send 建立新的 DI scope。SDK 在这个 scope 中通过 IDataService 打开业务数据会话，把它绑定到 CommandDataSession，handler 只消费这份上下文与 Outbox。外层开启 SERIALIZABLE，实际 command/dispatching 事件包裹 handler 和保存步骤，审计追加完成后才提交。代理延迟构造真正的 handler，且转交基础组件卸载与请求共同控制的取消令牌。

写命令的 handler、领域通知与保存都运行在 SDK 开启的事件来源作用域（Qian.Events.Abstractions 的 EventEnvelopeScope）内：producer 是能力所属组件，actor 是已验证主体（有委托时 actor 为代理、on_behalf_of 为被代表人），causation_id 是本次调用 id，traceparent 与调用的 trace 一致。这样经 Outbox 发出的集成事件都带完整的 v1 信封；作用域外发布在运行时被拒绝，编译期另有 QIAN0006 拦截（见 [Qian.Events](../Qian.Events/README.md)）。

CommandPolicies 只允许对应的 qian.tenancy、qian.authz、qian.audit 生产方登记监听器；服务键、监听器和取消资源归属同一 Effect 生命周期。执行前要求三种存活登记，进入链尾前要求三种监听器实际参与；重复调用 next 不能再次执行业务。卸载撤销后新请求拒绝，进行中的数据库操作可取消并回滚。租户策略读取真实主档，RBAC 策略读取真实授权并核对 OAuth scope，AuditPlugin 根据真实生成 Schema 写同事务审计。RBAC 策略由 Qian.Authz 的 `AuthzPlugin` 在 Apply 中登记（只含命令 PEP 的子插件为 `RbacCommandPolicyPlugin`）；完整 Authz 数据/字段策略与管理服务仍待完成。

身份适配器与数据组件的登记先后不影响结果：Kun 2026.10.25 起数据约定不再以匿名兜底占用 Kun 当前用户端口（S1-KUN-017）。`AddQianCapability` 另登记一个启动检查：宿主启动时这个端口解析为 `AnonymousCurrentUser` 就启动失败，错误里写明端口与改法，而不是等到执行时才对一切能力判拒（403 `AUTHZ_DENIED`，且到不了权限引擎、不写决策审计）。SDK 同时核验 Kun 与 Qian 身份一致，并复制主体快照；不通过替换成宽松策略修复装配错误。参考宿主的 S2S 验签与主体映射已经过真实 HTTP 回归，但不替代真实 Qian Identity 交互登录。

SensitiveAttribute 把实际 JSON 属性标记为 sensitive。顶层字段及整个顶层容器可遮盖；发现尚未支持的嵌套敏感属性会编译失败，必须把其顶层容器整体标敏感，不能静默忽略。字段名沿实际 JsonPropertyName 或序列化命名策略派生，Audit 仍只存脱敏输入哈希。

## 单一清单与构建期工具

生成代码提供 IComponentCodeMetadataFactory；构建工具读取此工厂，不实例化业务插件或调用 Apply。源生成器只向编译器提交 C#，不在编译过程中写项目文件。全部带代码的组件入口与 CRM 参考组件采用该路径；CI 用 `python3 scripts/check_plugin_manifests.py` 动态发现并逐个复验，不维护三个硬编码组件步骤。`--sync` 是显式本地同步入口，默认模式只读。

工具只用于显式选择的本地可信项目构建。加载生成工厂及 JsonTypeInfo 会加载程序集，因此不能用于未知市场制品的准入或沙箱校验。工具与组件共用 SDK/Runtime 的程序集身份，避免独立加载上下文复制服务键类型。

```bash
dotnet run --project tools/Qian.Plugin.Tool -c Release -- sync packages/Qian.Data/bin/Release/net10.0/Qian.Data.dll plugins/qian.data
dotnet run --project tools/Qian.Plugin.Tool -c Release -- check packages/Qian.Data/bin/Release/net10.0/Qian.Data.dll plugins/qian.data
```

sync 合并唯一清单的派生字段与既有表面；数组与入口不改变协议形状。清单 x-qian-generated 记录字段指针、工具归属以及每份 metadata / 配置文件的来源、指针和内容哈希；权限文件仍是 authz-permissions 规定的数组，不往它里面塞生成标记。路径沿清单的 surfaces.tools、surfaces.metadata 与 runtime.configSchema；未声明时采用 ./tools、./metadata 与 ./schemas/config.schema.json。

| 内容 | 真相源与派生位置 |
|---|---|
| 必需服务、普通提供项与固定具名贡献 | Inject / Apply 的语义分析，或稳定委托入口的独立声明 → inject / provides |
| 运行形态与入口 | QianPlugin 类型 → runtime.form / runtime.entry |
| 配置契约 | QianConfigSchema 的完整代码 Schema 常量 → runtime.configSchema 与其指向文件；它是显式代码声明，不猜测任意 JsonElement 分支 |
| 能力与工具 | Capability + 源生成 JsonTypeInfo，或 CapabilitySource 引用的静态只读 CapabilityContract → tools/*.json |
| 权限声明 | 现有 Permission 属性 → metadata/permissions.json；资源、动作、风险、step-up、终端保留；不写入 plugin.json |
| 事件声明 | 现有 QianEvent 属性 → metadata/events.json；核对静态事件的实际模式，保留名称、载荷/结果类型和声明来源 |
| 组件身份、版本与发布 | id、version、author、license、interface、runtime 的包/镜像信息、policy、scopes、依赖与路径仍手写；身份必须匹配代码 |
| 尚未有代码声明的权限、角色、其他表面 | 保持手写；未声明的文件不会被自动接管或删除 |

Definition 包的事件/权限可通过 QianPlugin.MetadataTypes 显式纳入组件；同一类型只有一个拥有者。同程序集的其它 Provider 可以用 `[SharedEvents(typeof(Events), "拥有者组件ID")]` 显式引用该组件已纳入的事件类型：生成器要求唯一且实际存在的拥有者，类型必须有 QianEvent，不能带 Permission。两个事件文件保存同一 name / mode / payload / source 及 definitionComponent；这只共享事件契约，不改变权限、服务或组件归属。跨程序集拥有者尚不可静态确认时明确诊断。多组件程序集的本地声明用 ComponentOwner 指定归属。CapabilitySource 只引用与真实绑定共用的静态契约，不能构造业务插件或 handler；同样经过跨组件归属、重名与工具 Schema 校验。SDK 内部 ExecutableCapabilities 注册按完整 capability ID 走能力与 tools 协议，不伪装成普通 serviceKeys 中的贡献短名。

check 只读，并以文件路径及 JSON Pointer 报告漂移，发现差异返回非零。删除代码能力后，旧工具仍存在也会失败；sync 只有同时具备清单索引与文件所属标记才删除过期工具，不覆盖同名手写工具。孤立派生文件、重复键、越界路径、符号链接及第二份清单被拒绝。元数据的删除与覆盖还须核对允许的表面路径、所属组件与原内容哈希；被手改后拒绝覆盖且保留文件，恢复原派生内容后再修改代码并同步。删除配置代码声明时同步撤下所属 Schema 文件和 runtime.configSchema 指针。先在临时快照验证完整组件，再改目标文件；单次写入用临时文件替换，同步中抛错会恢复已改文件。预检最多 4096 个文件、64 MiB，不复制构建输出、依赖目录或 Git 数据。

工具的校验使用已有 SurfaceValidator 和 ToolContractHash，不另写 Schema 或哈希规范。能力绑定的已知目录来自这次实际编译出的能力列表；验证文件有效不代表它已安装、已授权或已真实执行。

验收用例（Qian.Plugin.Sdk.Tests 的 CodeDerivedManifestTests）不手写元数据：组件源码经真实编译器、两个 SDK 源生成器与 System.Text.Json 源生成器编译成程序集，再起 CI 用的同一个工具进程。`[Capability]` 的 Description、SideEffect、PolicyHint、RequiredScopes 逐项落进清单 `surfaces.tools` 指向的工具文件（未写 PolicyHint 时 destructive 派生为 confirm、其余为 allow）；删掉代码里的一个 `[Capability]` 而保留清单与工具文件，check 返回 1 并列出 `tools/<name>.json` 与 `.qian-plugin/plugin.json#/x-qian-generated/tools` 两处漂移，且不改动任何文件。

输入失败统一输出 ErrorDetail，包含 `errors[]` 的原因与文件指针：无入口为 SDK_METADATA_MISSING，ID 不匹配为 SDK_COMPONENT_MISMATCH，第二清单为 SDK_MANIFEST_DUPLICATE，手写冲突为 SDK_HANDWRITTEN_CONFLICT，路径逃逸为 SDK_PATH_ESCAPE；无效工厂/跨组件元数据为 SDK_METADATA_INVALID，文件语法或 Schema 问题为 SDK_MANIFEST_INVALID。check 的正常派生漂移返回 1 并列出差异；输入拒绝返回 2，sync 也不会写入。

## HTTP 插件可见性

Qian.Plugin.Sdk.AspNetCore 提供 PluginEnabledMiddleware。宿主用 AddQianPluginEndpoints 登记真实 Registry 所在上下文，在路由、认证、可信租户边界之后安装 UseQianPluginEndpoints；Minimal API 用 RequireQianPlugin 绑定具体 entryId、componentId、服务键和贡献名，可选核对精确版本。绑定来自服务器装配，不从 URL、请求头或正文推导。

中间件要求主身份的唯一 tenant_id 与 ITenantContext 相同，读取 GetScopeEntriesAsync 合入后的实际绑定视图。未挂载、被停用、遮蔽/restrict 隐藏和不存在统一为 NOT_FOUND；响应不携带管理原因、配置或服务对象。RequireQianPlugin 还登记 endpoint filter，漏装中间件或顺序错误时不会让 handler 绕过检查。租户中间件不接受伪造 X-Tenant-Id；此可见性检查不代替后续 Authz 权限检查。

Registry 条目整体卸载时，宿主上下文工厂的失效也映射为 NOT_FOUND；只在解析该上下文的边界处理缺失/失效异常，不吞掉后续 Registry 读取的其它故障。T23 以业务 Provider 仍 Ready 的状态验证写入口被拒且数据库/审计不变。

端点执行中解析实际服务时仍使用同一租户 scope。视图是只读快照，不是永久授权租约；调用方不能把它缓存成永久可调用资格。当前为有可信租户的 Minimal API；MVC 适配、平台 null 租户及 app/session lineage 仍需对应入口实现。

## 包族发布

`Qian.slnx` 里 `IsPackable=true` 且包名以 `Qian.` 开头的全部项目组成包族（与依赖图门禁同一口径），按 ADR-019 D19.7 以同一 SemVer 版本发到 GitHub Packages 的 NuGet 源 `https://nuget.pkg.github.com/idadawn/index.json`。GitHub 的 NuGet 包随关联仓库定可见性：`idadawn/qian` 是公开仓库，包随之公开可见（公开后不能改回私有），从该源安装仍需 GitHub 令牌；首个版本 0.1.0 发布时出现这一情况，产品负责人 2026-10-15 选择保留公开，与 ADR-019「S1 私有源」的出入记在 S1-RT-014 日志。发布只运行 main 上的 `dotnet-release` 工作流（手动触发，输入版本号），由 [scripts/dotnet_release.py](../../scripts/dotnet_release.py) 逐步核对：

1. 本提交的 dotnet 工作流已在 main 通过；版本号是不带 v 前缀与构建元数据的 SemVer 2.0，包族里任何包在包源里都还没有这个版本。
2. 带版本号打包后核对制品：包族齐全、各一个、版本一致，包族内依赖的下限钉在同一版本，源生成器包只在 `analyzers/dotnet/cs` 下带自身分析器且是开发依赖，许可证与仓库地址齐全。
3. 推送后经 GitHub REST API 核对包族每个包都有这个版本、关联到发布仓库，且可见性与仓库一致。
4. 先用本地目录、推送后再用包源，在干净的消费者项目里以独立包缓存还原 `Qian.Plugin.Sdk`、`Qian.Plugin.Sdk.Generators`、`Qian.Data` 的精确版本；构建时包里的源生成器派生组件元数据，运行后核对派生结果与包里 qian.data 的生成元数据，并核对还原出的每个 `Qian.*` 包都是同一版本、都来自给定源。

推送中途失败会在包源里留下部分包；同一版本不能再发，换新版本号重新触发。本机可用同一脚本预检（`--no-build` 只验打包形态，程序集版本仍是本地构建的版本）：

```bash
dotnet pack Qian.slnx -c Release --no-build -p:Version=0.1.0-local.1 -o /tmp/qian-nupkgs
python3 scripts/dotnet_release.py verify-packages --version 0.1.0-local.1 --dir /tmp/qian-nupkgs
python3 scripts/dotnet_release.py verify-consumer --version 0.1.0-local.1 --source /tmp/qian-nupkgs
```

消费方的 `nuget.config` 用 `packageSourceMapping` 把 `Qian.*` 映射到 GitHub Packages、其余映射到 nuget.org（Kun 正式包在 nuget.org，ADR-021）；读取 GitHub Packages 需要带 `read:packages` 权限的令牌。组件项目引用 `Qian.Plugin.Sdk` 与 `Qian.Plugin.Sdk.Generators`（后者 `PrivateAssets="all"`）的同一版本：

```xml
<packageSources>
  <clear />
  <add key="qian" value="https://nuget.pkg.github.com/idadawn/index.json" />
  <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
</packageSources>
<packageSourceMapping>
  <packageSource key="qian"><package pattern="Qian.*" /></packageSource>
  <packageSource key="nuget.org"><package pattern="*" /></packageSource>
</packageSourceMapping>
```

## 验证与剩余边界

专项覆盖实际编译生成与 JsonTypeInfo、审计默认值、元数据副本、错误声明诊断、代码删除与重复能力、服务注册不可静态证明时拒绝、唯一清单同步/漂移以及手写文件和路径保护。全仓代码组件 check 与实现构件分类已接入 CI；Qian.Data.Tests 另按仓库清单的 runtime.entry 装载真实 Provider（不直接构造具体类型），核对清单派生字段与生成元数据一致，挂载后的会话默认带租户填充、租户过滤与 RLS。按 S1-DEC-018，S1-RT-011 从私有源真实执行 `qian add data` 并运行 Provider 后，Data、Events 与 Config 已通过对应真实安装门并移除 x-skeleton；其余组件补齐元数据后继续保留原门状态。

SDK 自身的依赖边界由 CI 的依赖图门禁守住：`Qian.Plugin.Sdk`、`.AspNetCore`、`.Generators` 的完整传递依赖里不能出现任何 Provider 包与 `Qian.Registry` / `Qian.Lifecycle` / `Qian.Capabilities`，门禁自测另断言这三个包在 `Qian.slnx` 的检查范围内（见 [tests/README.md](../../tests/README.md) 4.3）。

真实 CRM 已验证创建、同事务 Outbox/审计、错误回滚、幂等、并发租户与锁等待时卸载取消；列表与详情也通过同一真实管道，分别覆盖默认不审计和显式审计。查询会拒绝已跟踪的业务变更，且不开放 Outbox 写入端口。按 R94 推迟到 S2 的是 IQianPlugin 基类、客户端门面、生命周期钩子基类与 DI 注册代码生成；S1 以接口和 ctx.Inject 为准。QianJob 已随 S1-SCH-001 交付：在已有 Capability 的 partial 请求上标注 QianJob("code")，Apply 用 ProvideJob 同时登记真实能力与 local 作业目录；生成 metadata/jobs.json，目录同步、归属与手改保护沿用同一工具。调度及取消语义见 [Scheduler 主文档](../Qian.Scheduler/README.md)。UI/低代码与 Wasm 表面按各自 S2 规划，代码热装卸尚未承诺；动态注册不会自动变成清单模板。具名 seam 已由 Contributes 的独立声明覆盖，权限与事件已派生到各自 metadata 文件。应用/会话执行边界与完整平台验收继续按各自任务推进；这些局部验收不代替 QA-002。

## 可执行能力绑定

CapabilityBindingRegistration 在所属组件的 Apply 中，以生成契约的 component/name 登记 IExecutableCapability；实际调用反序列化为源生成请求类型，再进入真实 ISender 和已装配的命令管道。注册与取消随 Effect 撤销。调用者必须提供与当前租户一致的实际 scope 视图；每次调用重新解析同一具名绑定，缓存引用不能绕过 restrict、替换或卸载。Global 视图不能用于已有租户的调用。

这只是目录与执行共享的基础绑定，尚未实现完整 Capabilities 权限过滤、模型提示词、OpenAPI 聚合或完整 platform。构建期组件生成器只识别真实 SDK 的 ProvideCapability 方法，不给应用伪造的同名方法豁免。

## 开户初始化 hook（S1-DEC-023）

仅租户开通编排可开启不可序列化的内部授权作用域，SDK hook 适配器核对生产方对象、租户、精确能力清单、请求类型与权限点后消费一次。生命周期结束、嵌套或重复消费均拒绝。正常能力仍要求已认证租户主体；初始化执行只使用编排传入的真实操作者快照，经过 Kun PermissionBehavior 与三项 command 策略，但初始化身份及权限判定来自一次性凭据，不查询普通 RBAC；业务事务与 hook 审计照常执行，不伪造或签发登录令牌。Authz Definition 的内部注册重载只为 Kun 原行为提供受该凭据限制的身份/判定适配器，不更换宿主 DI 或普通 RBAC 引擎。

初始化注册适配器会核对 Kun 权限行为工厂的 ServiceType、泛型请求/响应与程序集；依赖升级改变登记结构时直接拒绝注册，不包装其它 DI 工厂。
