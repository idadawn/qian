# Qian.Surfaces

依据 [组件模型规格 §4](../../docs/prd/specs/component-model.md#4-声明式表面的文件格式) 与 [S1-RT-008](../../pm/tasks/S1-RT-008.md)。`Qian.Surfaces.Abstractions` 定义结果、诊断、错误码与端口；本包提供 JSON/YAML 解析、Schema 和静态语义校验。

## 装配位置

本文件夹包含解析器、校验器、工具描述 JCS 哈希和运行时适配器；Schema 唯一源在 [schemas/v2](../../schemas/v2/README.md)。所有 profile 都可使用，不依赖 Registry、Lifecycle 或数据库。它承担声明式目录的 Provider，运行时拥有 Skills、Agents、Commands、Hooks、McpServers、Tools 服务键。

`qian add` 的完整安装编排属于 CLI 任务，本包供它、profile 装载器、CI 和市场准入复用；无代码组件不需要加载程序集或实例化业务插件即可解析。

```csharp
var validator = new SurfaceValidator();
var result = validator.ValidateComponent(componentDirectory, new SurfaceValidationOptions
{
    Profile = "minimal",
    ExecutionPlane = "human-cli",
    Capabilities = ["qian.files/purgeTenantFiles"],
    CatalogComplete = true
});
if (result.IsValid)
{
    var plugin = new DeclarativeSurfacePlugin(result);
    // PluginEntry 由 profile 装配器提供；提供键与依赖用于现有 PluginLoader 的拓扑检查。
    var definition = new PluginDefinition(entry, plugin, plugin.Provides);
    await runtime.MountAsync([definition], cancellationToken);
}
```

`DeclarativeSurfacePlugin` 只登记文档，不执行正文、hook、MCP 或命令。注册使用挂载条目的 scope，借现有 Effect 机制在卸载时撤销；MCP 的 profile 限定会在登记前过滤。快照变化可通过重新校验和 `PluginLoader.ReloadAsync` 替换。

## 校验与诊断

支持 skill、agent、command 的 front matter 与原样正文，以及 hooks、MCP、tool、patch、metadata；清单接受缺省或覆盖的表面指针。JSON 重复键、YAML 重复键或 alias 被拒绝；单文件最多 1 MiB，解析深度最多 64。路径须存在于组件内，不能逃逸或经过符号链接，检查大小写以保持跨平台一致。

`metadata/*.json` 使用同一个 JSON 解析器和路径边界，支持显式目录（包含组件根目录），只读取该目录第一层 JSON 文件；返回 `kind: metadata` 的原始文档快照，并应用通用敏感值检查。目录指向文件、重复键、非法 JSON 或链接均被拒绝。权限点、默认角色、事件等业务 Schema 由各 seam 在登记/发布时验证；解析通过不代表角色授权或安装准入通过。

工具／技能引用从本组件与调用方目录解析；离线时未知的跨组件引用为 warning，`CatalogComplete=true` 时为 error。Qian 事件来自共同事件矩阵或宿主目录；未知大驼峰 Agent 事件告警并透传。hook handler、命令能力和服务方法需由宿主提供已解析目录，不会靠试运行代码推测。

结构校验使用 JSON Schema 2020-12，工具词汇引用冻结 capability-entry 的 `$defs`。工具输入不能包含嵌套租户字段；有副作用时要求幂等键；描述哈希按 RFC 8785 的 IEEE-754 数值域与 UTF-16 字段顺序计算。`ToolContractHash.Compute` 只返回哈希，不擅自改写组件文件。

出网 URL 必须命中 `policy.egress`；敏感字段只接受 secretRef。内联 shell 仅允许受信任组件的人类 CLI 且需匹配显式 argv 前缀；服务端拒绝内联 shell，受限 argv hook 还需宿主明确开启。解析器不提供执行器。

## 模型体验

`SurfaceDiagnostic` 在冻结 ErrorDetail 外附加文件、行号和级别；ErrorDetail 内保留标准文件 URI 片段指针，例如 `skills/guide/SKILL.md#/description`。组件字段错误码由 Definition 的 `SurfaceErrors.All` 定义，使用自有错误目录登记；嵌入宿主统一目录时也可显式登记这些定义。

模型收到诊断后可以定位并修复原文件；错误不回显密钥值或执行输出。CLI 命令正文仍属于人类命令平面，不能因解析成功而进入服务端执行或模型工具平面。

## 验证

[共同 fixture](../../tck/surfaces/README.md) 覆盖所有支持表面、37 个反例、无 src 组件、自定义指针、两侧精确诊断对比以及独立 JCS 浮点／Unicode 向量。.NET 还验证租户 A/B 隔离与卸载撤销。

## 已知限制与延期项

本任务不实现 Agent loop、MCP 进程、hook 执行器、文件监视服务或完整 CLI 安装器。二进制 runtime.entry 的真实定位、版本解算、签名和完整依赖闭包由 SDK/CLI/Registry 任务处理。UI 与低代码表面只检查显式路径，内容 Schema 属于对应模块。宿主须在登记前提供可信目录，并在真正调用时继续执行权限、scope 与执行策略检查。


## 市场文档

`ISurfaceValidator.ValidateMarketplace(file, content)` 与 TS `validateMarketplace` 共用 marketplace/v2，检查字段、安装名唯一性、分类、路径、公钥与远程 URL 约束，并输出同样的文件指针和行号。失败不返回含敏感输入的市场对象。它是静态文档校验，不读取网络或信任根；来源取得、验签、digest 及每个实际组件的 ValidateComponent 由 [CLI 市场链路](../qian-cli/README.md) 继续完成。跨语言向量在 tck/marketplaces。

能力目录可经 Definition 的 IToolContractHasher 端口使用 ToolContractHasher，复用同一 ToolContractHash/JCS 实现；不复制规范化算法，也不在组件库中反向引用本 Provider。

## 声明式表面独立刷新

`SurfaceRegistrationSession` 将共享校验后的文档注册为当前条目的可逆效果，按服务键与贡献名比较新旧内容。只撤销删除或改变的表面，不重放代码 Apply；不变项保持对象引用。替换失败会撤销本次新增项并恢复旧注册。技能同时出现在专用 Skills 目录与模型 Tools 集合，两个入口使用同一份文档；同一集合内的名称冲突不能静默覆盖。

`HotReloadSurfacePlugin` 可包装一个真实代码插件，也可作为无代码组件。它使用文件监视与定期核对，稳定读取并校验后调用上述刷新端口。非法新内容保留上一份有效注册；源码、清单身份或代码派生工具变化只标记 `RestartRequired`。状态不包含文件内容、配置或凭据。卸载先停止并等待监视工作，再撤销表面与代码效果，不在 effect 内递归等待装载器生命周期。

.NET 与 TypeScript 使用同一份 [T19 场景](../../tck/runtime/hotreload.json)，实际修改文件，验证五秒内更新、租户隔离、代码对象引用及效果保持、非法内容保留、源码重启提示和卸载清理。CI 对照两侧实际测试产生的结果；不靠调用手工刷新方法代替文件监视。

当前增量尚未完成实际 hook 执行、生产注册表变更触发以及 `qian doctor` 读取运行态重启提示，RT-009 保持 doing。本次还修正运行时手动撤销注册后的效果栈清理；TypeScript 运行时需要在后续发布流程发布新的兼容补丁版本，不能覆盖已发布的 0.1.0。
