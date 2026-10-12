# Qian.Plugin.Sdk

本模块是 S1-RT-014 的 SDK 与源生成器入口。开发者规格为组件模型 §3、§5 及 PRD §7.2.3；组件库只依赖 Runtime、SDK 与 Definition，SDK 自身不引用 Provider。当前交付代码声明、源生成能力契约和唯一清单校验；实际命令执行协议、默认安全装配和发布安装仍在实现。

## 代码契约

QianPluginAttribute 标注程序集内唯一、顶层非泛型的 IQianPlugin 实现。ComponentMetadataGenerator 用 Roslyn 语义模型读取 Inject 集合和 ApplyAsync 中直接作用于当前上下文的 Provide 调用，生成运行时入口与服务键。支持公开静态只读 ServiceKey 字段及常量构造；条件注册、延迟注册、上下文逃逸到辅助方法或局部别名、动态 Inject 及具名 seam 暂时明确报 QIANSDK002，不能静默生成空提供项。迁移到这些尚未支持的表达式前需扩展分析与对应回归。

CapabilityAttribute 标注顶层非泛型 partial 请求，并指定响应类型和 System.Text.Json 源生成上下文。上下文必须显式登记请求与响应。生成器校验短名、描述、冻结词汇、scope、权限点和幂等声明，生成该请求的 QianCapability 属性。它使用正式 Kun CapabilityDescriptor 从 JsonTypeInfo 导出输入/输出 Schema；不会用反射序列化请求，也不执行业务 handler。序列化设置属于真实契约：例如 UnmappedMemberHandling.Disallow 和必填构造参数必须与公开输入 Schema 一致。

描述、SideEffect、PolicyHint、RequiredScopes、RequiredPermission、RequiredSecrets、数据/成本分级、版本、Agent 设置与审计声明保存在不可变契约中。写操作默认审计，查询默认关闭，可显式覆盖；destructive 的策略提示默认 confirm。生成契约只提供事实，不会凭这些字段授予权限、建立可信主体或绕过安装状态。

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

## 验证与剩余边界

专项覆盖实际编译生成与 JsonTypeInfo、审计默认值、元数据副本、错误声明诊断、代码删除与重复能力、服务注册不可静态证明时拒绝、唯一清单同步/漂移以及手写文件和路径保护。真实 Data 程序集的 sync/check 已接入 CI；其 x-skeleton 继续保留，因为默认安全装配、私有 NuGet 锁步发布和实际 qian add 代码安装尚未通过。

仍须完成：command/dispatching 的生产执行端、Mediator 与租户/鉴权/审计监听、敏感字段 Schema 派生、其余 SDK 注解与默认装配、组件未挂载 404、中间件及包族正式私有发布。当前数据与元数据验收不替代真实 CRM 创建命令、Outbox 和 QA-002 的完整平台验收。
