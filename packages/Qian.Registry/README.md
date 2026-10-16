# Qian.Registry

注册表属于 `Registry` seam 的 Provider，只进入 `platform` 的 base 组合包；Definition 为 `Qian.Registry.Abstractions`。分发表面位于 `plugins/qian.registry`，当前仍是配置骨架，消费者的 standalone profile 不独立安装这项平台插件（R34）。已提供 `RegistryPlugin` 与宿主 scope 同步服务，并接通真实 Data / Runtime；管理面 scope 视图（只读，HTTP 适配器为 `Qian.Registry.AspNetCore`）、候选制品的清单准入、供可信平台插件调用的公开挂载 / 卸载与契约准入已接入，安装链由 [Qian.Lifecycle](../Qian.Lifecycle/README.md) 编排（S1-RT-005）。scope patch 的 HTTP 写入仍未开放，不能以本地装载宣称组件已可从正式源安装。

## 已实现的持久化基础

`RegistryDbContext` 继承 `QianDbContext`，由宿主以 `AddQianData<RegistryDbContext>()` 装配，使用 `plugin_qian_registry`。迁移复用 Data 的 SQL 生成器，为 `scope_patches` 自动生成强制 RLS；应用连接必须使用非超级用户且无 BYPASSRLS 的角色，管理员仅执行迁移。上下文不会在启动时建表或迁移。

内部 `PostgresPatchRepository` 只提供数据库原语，尚不向消费者暴露写入服务。每次调用使用独立 Data 会话，要求有效租户上下文；tenant scope 必须与当前租户一致。app / agent-session 行同样以当前租户作 RLS 键，所属关系和运营授权仍必须由后续注册表服务检查。`patch/v2` 的语法、重复键与 Schema 校验直接复用 `ISurfaceReader`，路由身份不能被请求体覆盖；完整写入使用内部 `RegistryPatchWriter`：在同一事务内锁定租户/scope 并用共用装配器校验组件配置、依赖和 required；真实权益、鉴权与制品信任仍需在公开服务层接入。

写入使用条件修订：0 表示创建，已有条目必须带当前 revision；相同修订下提交相同 patch 不增加版本。数据库唯一键与并发列负责拒绝同时创建和同时更新的竞争，不把后一个写入静默覆盖前一个。撤销写内部墓碑、清除配置和 note 并增加修订；重建从保留的修订继续增长，旧客户端无法利用版本重置覆盖新条目。墓碑不进入有效 patch 列表，不产生另一个 Enabled / Disabled 状态机。

`disabled` 的显式 false 与字段缺省分开保存，保证文件 patch 的继承语义；`x-` 扩展保持原样。业务层收到的 `ScopePatchSnapshot.Patch` 仍是原有 patch 对象，revision、主体和时间在快照外层。快照的默认 `ToString` 不展开配置与 note。

每次成功变更在同一 PostgreSQL 事务中提交 `pg_notify`，通知只含租户、scope、条目、修订和撤销标记；配置和 note 不广播。通知语句失败则数据写入也回滚。内部监听器已接入通知消费、断线恢复与周期重新读取；租户 scope 的真实运行时重合入已接入并通过五秒内生效检查；应用/会话 lineage 与完整管理接口验收仍未完成。

## 数据库层合入与写前校验

`RegistryProfileComposer` 只依赖 `IProfileLoader`，不引用 Profiles Provider。它读取当前租户 scope 的有效行，作为文件层之后的 `ScopePatchLayer` 传入共用装配器；不生成临时配置文件，不把租户数据写回项目目录。省略 plugin/config 的局部 patch 可沿用同 id 的全局条目，显式 config 整段替换并重新应用 Schema 默认值。禁用项在 `DisabledEntries` 中保留 scope/id/来源，供后续运行时安装 restriction，避免禁用后回退到同名全局服务。

`RegistryPatchWriter` 的准入与写入处于同一数据库事务，使用租户/scope 维度的 PostgreSQL 事务锁，跨副本串行读取并校验最终配置。两个不同条目不能各自通过预检后共同引入重复 Provider 或破坏依赖；失败不增加修订、不留下记录、不发送通知。该内部入口也不等于已实现完整安装权限。

真实测试覆盖受限角色隔离、独立会话竞争、撤销重建的旧修订拒绝、通知失败整笔回滚、配置准入失败保留原修订，以及两个独立宿主并发提交冲突 Provider。文件和内存 scope 层的 .NET / TS 装配规则由同一套 profile TCK 精确比较。

## 不可变制品候选目录

内部 `RegistryArtifactContent` 接收已获取的文件字节，复制为私有快照后核对目录摘要，再用共用 `ISurfaceValidator` 读取清单与表面。源目录不会在摘要计算和校验之间被再次读取；路径、大小和文件数量受限，不执行组件内容。目录摘要与 CLI 使用同一 D4 格式，见[共用样例](../../tck/registry-artifacts/README.md)。`metadata/*.json` 直接取共用表面校验器的文档快照，CLI 与注册表共用目录、重复键与敏感值规则；其权限、角色等业务 Schema 仍由各 seam 在发布准入时验证。

`PostgresArtifactCatalog` 将清单、声明式文档、逐文件摘要与元数据快照写入 `plugin_artifacts`。该表是全局候选目录，不含租户挂载状态。摘要是不可变主键；两个宿主并发登记相同摘要只保存一份，后续重复提交不会覆盖首次提交主体与时间。内部读取返回独立的只读集合，不依赖仍可变的调用方文件内容。

登记只产生 `Submitted`，签名结果为空；没有接收“已验签”布尔值并直接标记可信的入口。验签由 Lifecycle 完成，结论经 `RecordArtifactStateAsync` 回写：状态为 `Installed`（无需部署）、`Progressing` 或 `Healthy`（service 修订的双条件健康，见 `RegistryArtifactStates`），验签结论写 `{algorithm: ed25519, keyId}`；只接受已登记的摘要。校验环境 profile 只是本次静态检查的上下文，不能把一个 profile 的结果当作其它 profile 的准入结果。扫描与市场发布仍未完成，候选目录记录不得作为权限登记、权益或运行时挂载依据。

## 通知消费与断线恢复

内部 `PostgresPatchListener` 通过同一 Data 端口和受限应用角色持有一个宿主级监听连接，不继承启动请求的租户上下文。监听生效后立即重新读取，并每秒核对一次，覆盖连接建立前或断线期间丢失的通知；恢复后重新 LISTEN 并再次读取。通知载荷只用于唤醒，不作为配置、权限或可信租户输入，实际数据仍由后续服务按已验证的 scope 读库。

读取回调失败会重试，日志只记异常类型；释放时取消后台任务并等待连接与会话关闭。监听器就绪仅表示数据库订阅成功，不能代替业务服务的就绪判断。真实双宿主测试覆盖通知后重新读取、在测试数据库中终止监听连接后恢复、启动请求上下文隔离、释放后无回调，以及回调失败的恢复与日志脱敏。

## 预装组件元数据入口

宿主在初始 Mount 前用 `RegistryComponentBinding` 把实际预装的 `PluginDefinition` 与同一次 platform 静态校验结果绑定。入口检查组件身份、版本、inject/provides，并拒绝 `x-skeleton`；宿主把绑定集合交给 `RegistryRuntimeOptions.InstalledComponents`。预装组件可以尚未挂载，元数据仍可供权限模块提前登记，避免权限模块与业务插件互等。

`IRegistryService.GetInstalledComponents()` 在 Registry 服务就绪后即可调用，不必等待数据库监听启动，也不要求所有业务插件先进入 Ready。结果只包含宿主绑定集合，清单和 metadata 均为只读快照；不查询 `Submitted` 候选目录，不重新读取可变源文件，也不授予租户角色或运行资格。各 seam 必须继续校验业务 Schema。宿主同步时只解析预装集合中的相同组件版本和清单；仅往工作目录添加文件不会自动获得租户挂载资格。

这个入口供可信平台插件消费，不能直接作为面向业务用户的能力目录公开。安装元数据目前随宿主捕获；在线发布、修订激活和声明变更仍需后续受控更新流程。它不替代包源、验签或正式安装验收。

## Registry 插件与真实运行时重合入

`RegistryPlugin` 只允许 global scope 的 platform 宿主，必需服务键为 `Data`，提供 `RegistryDefinition.Key` / `IRegistryService`。`RegistryRuntimeOptions` 注入当前真实 `PluginLoader`、共用 profile/表面端口、静态 platform 配置与预装 Provider 解析器；组件 Provider 由宿主解析，本包不装载代码程序集。解析结果的条目身份、scope、版本、config、inject 和 provides 必须与已校验清单相符。

宿主在全局插件树 Ready 后调用 `StartAsync`，在已验证的租户上下文中调用 `SynchronizeAsync`。后者读取该租户的完整数据库层并装入真实运行时，此后 PostgreSQL 通知及周期核对自动重合入。当前公开宿主入口只接受与当前租户一致的 tenant scope；app / agent-session 尚缺真实归属证明，保持拒绝。此接口不是 HTTP 管理写入端点，不能直接接受客户端主体或自行信任请求头建立上下文。

```csharp
await runtime.MountAsync(globalDefinitions, ct);
var registry = runtime.GetContext("registry", ScopeKey.Global).Registry();
await registry.StartAsync(ct);
// 当前 ITenantContext 必须已由认证/租户流程建立。
await registry.SynchronizeAsync(verifiedTenantScope, ct);
// 停止接收请求后，在装载器之外先停注册表服务，再释放运行时。
await registry.DisposeAsync();
await runtime.DisposeAsync();
```

局部条目遮蔽同 id 全局条目的全部贡献；禁用项使用 restriction 阻止回退到全局版本。切换时先收紧全局可见性，再撤销旧局部条目并挂载新配置；失败会撤销该 scope 已拥有的条目并阻断该 scope 的全局可见项，其他 scope 继续使用。恢复后重新挂载，配置没有变化时保持同一 Apply 上下文，不做每秒无意义重载。平台局部节点的运行时适配器依赖 Registry 服务键，组件库在 minimal / standalone 下的依赖不变。

停止顺序是宿主生命周期的一部分：先停止监听并撤销本服务拥有的局部节点，再停止 Runtime。插件 effect 取消并等待后台监听退出，不在 Apply/effect 中调用同一装载器的 Mount/Unmount；整树释放也会排空后台任务，事后重复释放服务保持幂等。错误码登记、服务登记与 restriction 均随所属上下文撤销。

测试从文件构建平台测试 profile，实际挂载 `DataPlugin`、`RegistryPlugin` 与 `DeclarativeSurfacePlugin`。独立写入宿主修改真实受限 PG 后，另一宿主的真实 `PluginLoader` 在五秒内完成新增、重载、禁用与撤销，旧上下文失效，B 和全局条目保持不变；另验证坏配置隔离与恢复、解析器跨 scope 改写拒绝、重复核对不重载及停止后的清理。默认 platform 的全部 Provider、真实身份/权限接口和 App/会话 scope 验收仍在后续清单中。

## 管理面 scope 视图（R32 / R48）

`GetScopeViewAsync` / `GetScopeEntryAsync` 是管理面的读取入口。HTTP 适配器 `Qian.Registry.AspNetCore` 的 `MapQianRegistry` 把它们映射为 `GET /api/registry/scopes/{scopeKey}/entries` 与 `GET /api/registry/scopes/{scopeKey}/entries/{entryId}`，并保留 v1 别名 `/api/kernel/scopes/...`：别名返回同一结果（含错误响应），带 `Deprecation` 与指向后继路径的 `Link: <…>; rel="successor-version"`，与能力目录共用 SDK 的 `DeprecatedAlias`（R33）。

判定先于一切读取：调用者须是当前已验证租户的用户或服务主体（无委托），scope 只能是该租户自己的 tenant scope；权限经宿主装配的 Kun `IPermissionChecker` 判定 `qian.registry:scope:read`（`RegistryDefinition.ScopeReadPermission`，声明在 `plugins/qian.registry/metadata`，默认授予租户 Owner 与 Admin）。缺判定端口、无权限、跨租户与不存在的条目一律 404 `NOT_FOUND`，彼此无法区分；令牌中的角色不授予权限。

平台主体（令牌 `tenant_id` 为 null 的用户，无委托）读取目标租户的 scope 视图时，调用方先把租户上下文切到该租户，判定只走 Authz 的平台超管显式旁路 `ISuperadminBypass`（ADR-006 D6.3，S1-AZ-003），资源记为 `registry/scope:tenant:<id>`、通道 `api`；旁路放行前把 `bypass = superadmin` 记入目标租户的审计链。旁路端口未装配、未配置专用平台登录、非超管、审计失败或 scope 不是当前租户上下文时，与不存在同为 404；不回退到租户判定器，也不认令牌里的角色。HTTP 入口仍经租户边界（缺 `tenant_id` 拒绝），平台运营者的 HTTP 入口随 S1-TEN-006 的平台运营接口提供。

持有权限时，已挂载且 Ready 的条目返回视图：条目、组件、版本、所属 scope 文本（`global` 或 `tenant:<id>`）、层号与本 scope 的 patch 修订，不含配置。未挂载以 403 `PLUGIN_NOT_MOUNTED` 返回，message 说明哪一层导致——被租户 scope patch 禁用、被平台 profile 某层禁用、平台 profile 与租户 scope patch 均未声明、或 scope 合入失败已全部撤下；pointer 指向 `registry/scope-patches.json` 中的 patch 条目，本 scope 没有该条目时指向该层文档根。已挂载但仍在等待依赖为 403 `PLUGIN_DEPENDENCY_PENDING`。「已知条目」只取宿主预装绑定、全局已装配条目与本 scope 自己的 patch 行，其它租户的局部条目不会经 403 暴露。

S1 不产生另外两个原因码：profile 没有 restriction 声明来源，`PLUGIN_RESTRICTED` 待 restriction 声明落地后接入；权益检查归 S1-ENT-001，`PLUGIN_NOT_ENTITLED` 由其接入。平台运营者跨租户查询走上述超管旁路。列表与单条读取都先在与装载相同的锁内合入该 scope 的数据库层；管理面不挂插件端点门禁，因为 scope 合入失败时本 scope 的注册（包括注册表自己的）全部收紧，诊断仍须可达。

真实测试（`ScopeManagementTests`）使用受限 PG、Qian.Authz 的 `RbacPermissionChecker` 读真实角色授予、Kestrel + JWT 与租户边界：A 写 patch、B 不写时，B 的 Owner 得到 403 `PLUGIN_NOT_MOUNTED` 与 pointer，B 的使用者、未知条目与跨租户查询得到相同的 404；别名结果一致并带两个响应头；B 的禁用 patch、坏配置收紧与 A 的撤销各自给出原因；未装配判定端口时即使数据库已授予也只得到 404。`ScopeSuperadminTests` 用真实 Authz / Audit 插件与专用平台登录验证超管旁路：未装 Authz 时 404，装上后超管读到租户 A 的视图且 A 的审计链有 bypass 记录，非超管与跨租户 scope 仍为 404 且不写审计。

## 候选制品登记入口（清单准入）

`SubmitArtifactAsync` 供 Lifecycle 等可信平台插件登记候选制品，调用方负责权限：先核验 D4 摘要，再以 platform / server 做共用静态校验，`SurfaceValidationOptions.ServiceKeys` 取预装组件的 provides，inject 无 Provider 即被拒；通过后幂等写入 `Submitted`。不合格时抛 `INPUT_INVALID`，`errors[]` 为 `REGISTRY_INPUT_INVALID`，pointer 是指向制品文件的 JSON Pointer，候选目录不留记录。

`tck/surfaces/cases.json` 中清单准入的五类反例——缺任一 S1 必填字段（按 Schema 的 required 逐个覆盖）、OCI 引用用 tag、`scopes[]` 含非法种类、工具顶层输入含租户字段、`inject[]` 无 Provider——在 .NET 与 TS 校验器上逐项一致，并经本入口全部拒绝、pointer 与 fixture 相同；digest 引用与 inject 有 Provider 的对照正例登记为 `Submitted`（`ManifestIntakeTests`）。登记不等于验签、发布、安装或任何租户已挂载。

## 公开挂载 / 卸载与集成事件（S1-RT-005）

`MountAsync` / `UnmountAsync` 是 PRD 7.2.1 的 Mount / Unmount，供 Lifecycle 等可信平台插件调用，调用方负责权限与权益；它们不是 HTTP 端点。

- 只接受与租户上下文一致的 tenant scope。
- 写入复用内部 `RegistryPatchWriter` 的装配校验：patch/v2 格式、组件须在宿主预装集合、配置 Schema、依赖。写入按条件修订，0 表示创建。
- `MountAsync` 的条目不能带 `disabled: true`。同一内容重复挂载不增加修订，也不再发事件。
- `UnmountAsync` 对平台 profile 引入的条目写 `{id, disabled: true}`，对只由本 scope 引入的条目撤销 patch（墓碑）。本 scope 本来就看不到该条目时返回 NOT_FOUND。卸载不新增任何开关列或开关行。
- 每次成功写入在同一事务把 `registry.scope.mounted.v1` 或 `registry.scope.unmounted.v1` 信封放进 Outbox，载荷只含 scope、条目、修订与组件引用，不含配置。

宿主须以 `RegisterRegistryEvents` 登记事件类型、以 `AddQianRegistryEvents` 登记领域事件处理器，并装配 `AddQianEvents`。漏装处理器时写入在提交前失败，不留 patch 也不留 Outbox。

Lifecycle 还用到另外三个入口：

- `GetScopePatchAsync` / `GetScopePatchesAsync`：读当前租户 scope 的有效 patch 与修订，用于条件写入与升级时找出引用某组件的条目。
- `RestoreScopePatchAsync`：新条目装配失败时恢复挂载前的 patch（可以是禁用 patch），没有前值时撤销；事件按恢复后是否可见选择 mounted 或 unmounted。

真实测试 `ScopeMountTests` 覆盖：patch 与同事务信封、重复挂载幂等、卸载墓碑与禁用 patch、跨租户 / 禁用挂载 / 未预装 / 旧修订的拒绝不写 patch 也不发事件、漏装处理器整笔失败，以及卸载后 5 秒内 SDK 入口恢复 404、租户 B 不受影响。

## 契约准入与装配诊断（R76）

scope Reconciler 解析条目时，对在 `metadata/contracts.json` 声明契约族的组件版本，经 Capabilities 以 `CapabilitiesDefinition.AdmissionKey` 提供的 `IContractAdmission` 包装插件（规则见 [Capabilities](../Qian.Capabilities/README.md) 的「平台挂载准入」）。准入服务不可见时该条目 Apply 失败（`UNAVAILABLE`），不跳过检查。

装配失败沿用原有的收紧规则：已提交的禁用不回退，该 scope 被阻断，其他 scope 不受影响。本副本最近一次失败记为诊断：条目 Apply 失败带条目、组件修订与原因码，其余为 scope 级失败；下一次合入成功即清空。`GetScopeDiagnosticsAsync` 在与装载相同的锁内先合入数据库 scope，再返回这些诊断，供 Lifecycle 的管理面使用，调用方负责权限。Lifecycle 的挂载失败会恢复挂载前的 patch，并把原因另行持久化，见 [Qian.Lifecycle](../Qian.Lifecycle/README.md)。

## 剩余工作

scope patch 的 HTTP 写入（`PUT /api/registry/tenants/{tenant}/patches/{scope}/{entry}`）尚未开放，归 S1-RT-029（R86）。App / 会话归属与权益（S1-ENT-001）在各自任务中接入；平台超管旁路已接入（S1-AZ-003，见上文管理面一节）。`plugins/qian.registry` 的骨架标记保留，组件目录清单的合并生成与骨架移除归 S1-RT-033（R94）。

## 面向业务入口的 scope 视图

GetScopeEntriesAsync 接受已验证租户的 scope 和服务键，在与重合入相同的串行锁中读取数据库层、完成真实装载，再从 Runtime 的遮蔽/restrict 后注册快照返回已安装且 Ready 的绑定。返回仅含条目、组件、版本、scope、服务键及贡献名，不包含配置或服务对象；坏配置阻断的 scope 返回空视图，管理诊断保留在管理路径。查询另一租户或未经归属验证的 app/session 被拒绝。

GetInstalledComponents 仍是可信安装声明集合：预装未挂载、或已被租户停用的组件可以保留其中，不能据此放行业务入口。SDK HTTP 门禁已用真实 Kestrel、S2S 验签、受限 PG 和运行时验证禁用/未知同为 404、租户 B 不受影响、restrict 交集及中间件漏装/错序不能执行 handler。此证据不关闭完整 platform、权限管理或提示词/能力三处过滤验收。

## 原始制品文件的预装快照

`RegistryComponentBinding.FromArtifact` 接收原始文件字节与 D4 目录摘要，先复制内容、核验摘要及 platform/server 静态表面，再核对实际 Provider 的身份与服务声明。它把不可变文件快照交给 `InstalledComponentMetadata`；`FilePaths` 只列组件内路径，`ReadFile` 每次返回独立副本，不重新读取宿主目录或网络。已有只传静态校验结果的构造方式保留，但不持有文件快照或制品摘要，不能用于声明契约文件的组件。

该快照让 Capabilities 从完整版本制品读取契约基线，避免拿当前用户过滤后的接口文档、可变工作目录或 Submitted 候选记录替代基线。摘要与静态校验仍不代表签名验证、发布、权益或安装授权已经完成。

S1-TEN-004（R97）增加 GetConfigurationViewAsync：可信平台组件在当前租户上下文中读取同一合成器的 Ready 视图，外部调用仍走 GetScopeViewAsync 的主体授权。RegistryEntryView.Config 为实际合成配置，与 ITenantSettings 共用；默认 ToString 不输出配置。MountAsync 成功提交后以真实 qian.registry 上下文派发 registry/entry-mounted，载荷仅 entryId/revision，不广播 config/note；Tenancy 用它失效缓存。该补充不代表 RT-032 其它停用、卸载或宿主接线已经交付。
