# Qian.Registry

注册表属于 `Registry` seam 的 Provider，只进入 `platform` 的 base 组合包；Definition 为 `Qian.Registry.Abstractions`。分发表面位于 `plugins/qian.registry`，当前仍是配置骨架，消费者的 standalone profile 不独立安装这项平台插件（R34）。已提供 `RegistryPlugin` 与宿主 scope 同步服务，并接通真实 Data / Runtime；管理面、完整发布准入与安装链仍未完成，不能以本地装载宣称组件已可从正式源安装。

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

当前登记只产生 `Submitted`，签名结果为空；没有接收“已验签”布尔值并直接标记可信的入口。校验环境 profile 只是本次静态检查的上下文，不能把一个 profile 的结果当作其它 profile 的准入结果。验签、扫描、发布、修订激活和实际安装仍未完成，候选目录记录不得作为权限登记、权益或运行时挂载依据。

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

## 未完成门禁

S1-RT-004 仍为 doing：制品验签/发布与修订目录、完整安装准入、真实权限/权益检查、App/会话归属、管理 scope 视图与 404/403 隐藏语义、`/api/kernel/*` 兼容别名均需继续实现与验收。`plugins/qian.registry` 的骨架标记保留。完整任务验收不因这层持久化可用而提前勾选。
