# Qian.Plugin.Sdk

本模块是 S1-RT-014 的 SDK 与源生成器入口。开发者规格为组件模型 §3、§5 及 PRD §7.2.3；组件库只依赖 Runtime、SDK 与 Definition，SDK 自身不引用 Provider。当前交付代码声明、源生成能力契约、唯一清单校验，以及已在真实 CRM 命令中使用的 Mediator/事务/waterfall 管道；完整默认装配、平台/App/会话入口和发布安装仍在实现。

## 代码契约

QianPluginAttribute 标注程序集内唯一、顶层非泛型的 IQianPlugin 实现。ComponentMetadataGenerator 用 Roslyn 语义模型读取 Inject 集合和 ApplyAsync 中直接作用于当前上下文的 Provide 调用，生成运行时入口与服务键。支持公开静态只读 ServiceKey 字段及常量构造；条件注册、延迟注册、上下文逃逸到辅助方法或局部别名、动态 Inject 及具名 seam 暂时明确报 QIANSDK002，不能静默生成空提供项。迁移到这些尚未支持的表达式前需扩展分析与对应回归。

CapabilityAttribute 标注顶层非泛型 partial 请求，并指定响应类型和 System.Text.Json 源生成上下文。上下文必须显式登记请求与响应。生成器校验短名、描述、冻结词汇、scope、权限点和幂等声明，生成该请求的 QianCapability 属性。它使用正式 Kun CapabilityDescriptor 从 JsonTypeInfo 导出输入/输出 Schema；不会用反射序列化请求，也不执行业务 handler。序列化设置属于真实契约：例如 UnmappedMemberHandling.Disallow 和必填构造参数必须与公开输入 Schema 一致。

描述、SideEffect、PolicyHint、RequiredScopes、RequiredPermission、RequiredSecrets、数据/成本分级、版本、Agent 设置与审计声明保存在不可变契约中。写操作默认审计，查询默认关闭，可显式覆盖；destructive 的策略提示默认 confirm。生成契约只提供事实，不会凭这些字段授予权限、建立可信主体或绕过安装状态。

## 实际命令执行

生成请求实现 ICapabilityRequest，同时生成不可变 RequiredPermissions。AddQianCapability 经已有 AddAuthorizedHandler 原子登记权限行为；宿主提供实际插件上下文解析器和固定通道，请求体不能选择这些值。当前入口处理带真实租户和非空权限点的数据能力，平台 null 租户、匿名能力和完整 app/session lineage 尚未开放。

Kun 每次 Send 建立新的 DI scope。SDK 在这个 scope 中通过 IDataService 打开业务数据会话，把它绑定到 CommandDataSession，handler 只消费这份上下文与 Outbox。外层开启 SERIALIZABLE，实际 command/dispatching 事件包裹 handler 和保存步骤，审计追加完成后才提交。代理延迟构造真正的 handler，且转交基础组件卸载与请求共同控制的取消令牌。

CommandPolicies 只允许对应的 qian.tenancy、qian.authz、qian.audit 生产方登记监听器；服务键、监听器和取消资源归属同一 Effect 生命周期。执行前要求三种存活登记，进入链尾前要求三种监听器实际参与；重复调用 next 不能再次执行业务。卸载撤销后新请求拒绝，进行中的数据库操作可取消并回滚。租户策略读取真实主档，RBAC 策略读取真实授权并核对 OAuth scope，AuditPlugin 根据真实生成 Schema 写同事务审计。当前 RbacCommandPolicyPlugin 仅提供命令 PEP，完整 Authz 数据/字段策略与管理服务仍待完成。

宿主须先装身份适配器，再装数据默认服务，防止匿名兜底先占用 Kun 当前用户端口。SDK 同时核验 Kun 与 Qian 身份一致，并复制主体快照；不通过替换成宽松策略修复装配错误。参考宿主的 S2S 验签与主体映射已经过真实 HTTP 回归，但不替代真实 Qian Identity 交互登录。

SensitiveAttribute 把实际 JSON 属性标记为 sensitive。顶层字段及整个顶层容器可遮盖；发现尚未支持的嵌套敏感属性会编译失败，必须把其顶层容器整体标敏感，不能静默忽略。字段名沿实际 JsonPropertyName 或序列化命名策略派生，Audit 仍只存脱敏输入哈希。

## 单一清单与构建期工具

生成代码提供 IComponentCodeMetadataFactory；构建工具读取此工厂，不实例化业务插件或调用 Apply。源生成器只向编译器提交 C#，不在编译过程中写项目文件。真实 DataPlugin 已采用该路径，CI 用其编译程序集复验 plugins/qian.data/.qian-plugin/plugin.json。

工具只用于显式选择的本地可信项目构建。加载生成工厂及 JsonTypeInfo 会加载程序集，因此不能用于未知市场制品的准入或沙箱校验。工具与组件共用 SDK/Runtime 的程序集身份，避免独立加载上下文复制服务键类型。

```bash
dotnet run --project tools/Qian.Plugin.Tool -c Release -- sync packages/Qian.Data/bin/Release/net10.0/Qian.Data.dll plugins/qian.data
dotnet run --project tools/Qian.Plugin.Tool -c Release -- check packages/Qian.Data/bin/Release/net10.0/Qian.Data.dll plugins/qian.data
```

sync 只合并代码派生的 inject、provides、runtime.form、runtime.entry 及 tools 表面；手写 id、version、interface、policy、scope、包版本和路径保持原值。数组和入口的原有格式不包裹：在允许的 x-qian-generated 扩展中，以 $generated: true 和 JSON Pointer 列出派生字段，工具文件也带所属标记。工具落到原有 tools 表面，不产生第二份清单。

check 只读，并以文件路径及 JSON Pointer 报告漂移，发现差异返回非零。删除代码能力后，旧工具仍存在也会失败；sync 只有同时具备清单索引与文件所属标记才删除过期工具，不覆盖同名手写工具。孤立派生文件、重复键、越界路径、符号链接及第二份派生清单被拒绝。先在临时快照验证完整组件，再改目标文件；单次写入用临时文件替换，同步中抛错会恢复已改文件。预检最多 4096 个文件、64 MiB，不复制构建输出、依赖目录或 Git 数据。

工具的校验使用已有 SurfaceValidator 和 ToolContractHash，不另写 Schema 或哈希规范。能力绑定的已知目录来自这次实际编译出的能力列表；验证文件有效不代表它已安装、已授权或已真实执行。

## HTTP 插件可见性

Qian.Plugin.Sdk.AspNetCore 提供 PluginEnabledMiddleware。宿主用 AddQianPluginEndpoints 登记真实 Registry 所在上下文，在路由、认证、可信租户边界之后安装 UseQianPluginEndpoints；Minimal API 用 RequireQianPlugin 绑定具体 entryId、componentId、服务键和贡献名，可选核对精确版本。绑定来自服务器装配，不从 URL、请求头或正文推导。

中间件要求主身份的唯一 tenant_id 与 ITenantContext 相同，读取 GetScopeEntriesAsync 合入后的实际绑定视图。未挂载、被停用、遮蔽/restrict 隐藏和不存在统一为 NOT_FOUND；响应不携带管理原因、配置或服务对象。RequireQianPlugin 还登记 endpoint filter，漏装中间件或顺序错误时不会让 handler 绕过检查。租户中间件不接受伪造 X-Tenant-Id；此可见性检查不代替后续 Authz 权限检查。

Registry 条目整体卸载时，宿主上下文工厂的失效也映射为 NOT_FOUND；只在解析该上下文的边界处理缺失/失效异常，不吞掉后续 Registry 读取的其它故障。T23 以业务 Provider 仍 Ready 的状态验证写入口被拒且数据库/审计不变。

端点执行中解析实际服务时仍使用同一租户 scope。视图是只读快照，不是永久授权租约；调用方不能把它缓存成永久可调用资格。当前为有可信租户的 Minimal API；MVC 适配、平台 null 租户及 app/session lineage 仍需对应入口实现。

## 验证与剩余边界

专项覆盖实际编译生成与 JsonTypeInfo、审计默认值、元数据副本、错误声明诊断、代码删除与重复能力、服务注册不可静态证明时拒绝、唯一清单同步/漂移以及手写文件和路径保护。真实 Data 程序集的 sync/check 已接入 CI；其 x-skeleton 继续保留，因为默认安全装配、私有 NuGet 锁步发布和实际 qian add 代码安装尚未通过。

真实 CRM 已验证创建、同事务 Outbox/审计、错误回滚、幂等、并发租户与锁等待时卸载取消；列表与详情也通过同一真实管道，分别覆盖默认不审计和显式审计。查询会拒绝已跟踪的业务变更，且不开放 Outbox 写入端口。仍须完成：其余 SDK 注解与默认装配、平台/应用/会话执行边界、包族正式私有发布和安装。这些局部真实验收不代替 QA-002 的完整平台验收。

## 可执行能力绑定

CapabilityBindingRegistration 在所属组件的 Apply 中，以生成契约的 component/name 登记 IExecutableCapability；实际调用反序列化为源生成请求类型，再进入真实 ISender 和已装配的命令管道。注册与取消随 Effect 撤销。调用者必须提供与当前租户一致的实际 scope 视图；每次调用重新解析同一具名绑定，缓存引用不能绕过 restrict、替换或卸载。Global 视图不能用于已有租户的调用。

这只是目录与执行共享的基础绑定，尚未实现完整 Capabilities 权限过滤、模型提示词、OpenAPI 聚合或完整 platform。构建期组件生成器只识别真实 SDK 的 ProvideCapability 方法，不给应用伪造的同名方法豁免。
