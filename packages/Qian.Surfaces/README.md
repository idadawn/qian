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

支持 skill、agent、command 的 front matter 与原样正文，以及 hooks、MCP、tool、patch、metadata；清单接受缺省或覆盖的表面指针。JSON 重复键、YAML 重复键或 alias 被拒绝；单文件最多 1 MiB，容器最多嵌套 64 层。字符串与键名不能含未成对的 UTF-16 代理项（`\u` 转义出的 D800–DFFF 码元没有配对）：JSON 在所在行报「JSON 字符串或键名含未成对的代理项。」，语法错误、过深与非有限数值优先；YAML 双引号标量里的这类转义按语法错误报在标量起始行（YamlDotNet 拒绝，`yaml` 包原本接受），转义的代理对照常解码；直接传入的字符串含字面量的未成对代理项时按值无效处理。致命错误（语法、大小、嵌套、值、代理项）只报告它自己，先前发现的重复键不再列出。两端的检查顺序与行号约定见下文。路径须存在于组件内，不能逃逸或经过符号链接，检查大小写以保持跨平台一致。

`metadata/*.json` 使用同一个 JSON 解析器和路径边界，支持显式目录（包含组件根目录），只读取该目录第一层 JSON 文件；返回 `kind: metadata` 的原始文档快照，并应用通用敏感值检查。目录指向文件、重复键、非法 JSON 或链接均被拒绝。权限点、默认角色、事件等业务 Schema 由各 seam 在登记/发布时验证；解析通过不代表角色授权或安装准入通过。

工具／技能引用从本组件与调用方目录解析；离线时未知的跨组件引用为 warning，`CatalogComplete=true` 时为 error。`inject[]` 只在宿主给出 `ServiceKeys`（目标装配中已有 Provider 的服务键，含具名注册）时检查：给出即视为完整目录，目录外的键为 error，`optionalInject` 不检查；注册表的候选登记总会带上预装组件的 provides。Qian 事件来自共同事件矩阵或宿主目录；未知大驼峰 Agent 事件告警并透传。hook handler、命令能力和服务方法需由宿主提供已解析目录，不会靠试运行代码推测。

结构校验使用 JSON Schema 2020-12，工具词汇引用冻结 capability-entry 的 `$defs`。工具输入不能点名租户字段：嵌套属性，以及任何层级的 `required`、`dependentRequired`、`dependentSchemas`、能匹配租户名的 `patternProperties` 与 `propertyNames` 用 `const` / `enum` 列出的名称，以及片段途经实例数据或停在名称表本身上的 `$ref` / `$dynamicRef`（S1-AGT-007）；开放的映射不点名字段，不检查（规则与能力输入契约共用，见 [packages/Common](../Common/README.md#tenantfieldrule-的判定规则)）；有副作用时要求幂等键；描述哈希按 RFC 8785 的 IEEE-754 数值域与 UTF-16 字段顺序计算。`ToolContractHash.Compute` 只返回哈希，不擅自改写组件文件。

出网 URL 必须命中 `policy.egress`；敏感字段只接受 secretRef。内联 shell 仅允许受信任组件的人类 CLI 且需匹配显式 argv 前缀；服务端拒绝内联 shell，受限 argv hook 还需宿主明确开启。解析器不提供执行器。

### 检查顺序与行号约定

两端的解析器（.NET 的 `SurfaceReader`：YamlDotNet 18.1.0 与 `Utf8JsonReader`；CLI 的 `parseSurface`：`yaml` 2.9.1 与 `jsonc-parser` 3.3.1）对同一内容给出同一条诊断，有效性、指针（文件根）、行号与文案都相同，由[表面 TCK](../../tck/surfaces/README.md) 的 `syntax-vectors.json` 逐条核对（S1-RT-025）。行号从 1 起按换行符计，开头的 BOM 不占位置，CRLF 视同 LF；Markdown front matter 的行号从文件第 1 行（开头的 `---`）算起。

YAML（含 front matter）报最先遇到的一处：

1. 逐行预检，先于解析。含 NUL、单独的回车符、U+0085、U+2028 或 U+2029 的行，报「YAML 不能含 NUL、单独的回车符、U+0085、U+2028 或 U+2029。」；以 `%` 开头的行（指令），报「不支持 YAML 指令（以 % 开头的行）。」；第二个文档开始的行，报「YAML 只能包含一个文档。」。第二个文档指内容或 `---` 之后又出现 `---`，或 `...` 之后还有空行与注释以外的内容，其中的内容不再检查。
2. 按文中位置先到先报。语法错误报「YAML 语法无效或存在重复键。」，行号如下：
   - 引号标量的错误（未闭合、转义无效、续行缩进不足、含未成对的代理项）报在标量开始的行；
   - 行首缩进里的制表符报在所在行，块标量正文里空格之后的制表符属于内容；
   - 流式集合的续行缩进不足报在该续行：续行开头的空格须多于所在块集合的缩进，最外层流式集合的结束括号可以与块集合同列；
   - 多行隐式键报在冒号所在行；普通标量键遇到缩进不超过块缩进的续行即告结束时，以及块映射里第二个起的引号键，报在键开始的行；
   - 到输入末尾才发现的错误（如未闭合的流式集合）报在最后一行的下一行（内容末尾没有换行时先补一个），front matter 里即结束分隔符 `---` 所在的行；
   - 下一节所列写法报在所述的行，其余报在 YamlDotNet 报告的位置。

   第 65 层集合也按位置参与先后，报通用文案「文件语法、大小、嵌套深度或值无效。」，在第 1 行。
3. 整个流没有上述错误时，有 alias 报通用文案（第 1 行），否则报首个重复键所在的行。键按名称比较：普通标量取原文，引号与块标量取解码后的文本，所以 `1` 与 `"1"` 重复，`null` 与 `~` 不重复。
4. 取值时的错误报通用文案（第 1 行）：显式 tag 与内容不符、自定义 tag、`.inf` 与 `.nan`、超出双精度范围的数值、不是字符串的键。

JSON 的语法错误（含根值之后多余的内容）、第 65 层容器与非有限数值（如 `1e999`）按文中位置先到先报。语法错误报「JSON 语法无效。」，在出错的行，到输入末尾才发现的报在输入末尾所在的行；过深与非有限数值报通用文案，在第 1 行。数值要读到其后的分隔符（空白、逗号、右括号或 `/`，根值还可以是输入末尾）才算读完，所以非有限数值之后紧跟非法字符、或在嵌套值里直接遇到输入末尾时，报语法错误。这些都没有时报首个未成对的代理项，再逐个报重复键（指针为该键）。

### 两端都拒绝的写法

两库对下列写法的结论不同，两端都按语法错误拒绝：

- 块映射里的空键，即冒号前没有键（`: 1`）或显式键 `?` 之后为空，报在条目开始的行。YamlDotNet 18.1.0 在嵌套块映射里会吞掉空键之后的值；流式映射里的空键照常接受。
- 隐式键与冒号不在同一行，`yaml` 包在第二个起的键上接受。普通标量键与冒号之间只隔空白时报在冒号所在行；隔着注释、键带引号或带 anchor、tag 时，报在键开始的行。
- 块序列项 `-` 或显式键 `?` 之后的空白里有制表符、其后是 anchor、tag 或紧凑的块集合，`yaml` 包拒绝，YamlDotNet 接受。报在所在行。
- 块映射第二个起的键是 alias 且紧跟冒号（如 `*x: 1`）。YamlDotNet 把冒号当作值指示符，`yaml` 包按规范读进 alias 名。报在所在行。
- 双引号标量里转义的换行之后紧跟空行。`yaml` 包少算一个换行，与规范和 YamlDotNet 不同。报在标量开始的行。
- YamlDotNet 18.1.0 比规范严的几处，CLI 同样拒绝：
  - 字面块标量（`|`）首行只有空格，且空格数不少于显式缩进加 2（没写缩进指示时为 2）；
  - 折叠块标量（`>`）首行是空行，且开头各空行的空格多于首个内容行的缩进；
  - 流式集合值之后同一行紧跟冒号（`a: []: b`）；
  - 流式映射里带 anchor 的标量值之后还有条目（`{x: &a 1, y: 2}`）。

### 已知分歧

- **数值的表示：** JSON 数值 .NET 保留原文（如超过 2^53 的整数、`1e-400`、`-1e00`），CLI 为双精度。YAML 两端都取双精度，输出文本可能不同。按双精度比较时相同。
- **多处错误时的行号：** YamlDotNet 要读完一行（或一个多行普通标量）的记号才交给解析器，其间的扫描错误先报；CLI 报文中最先的那处。两端都拒绝，行号可能不同。差分里，常见写法的轻度变异 10,000 份中有 8 份如此，大量随机变异的 6,000 份中约 3%。
- **第 65 层集合之后同一行还有扫描错误：** .NET 报语法错误，CLI 报过深，都在第 1 行。
- **显式键 `?` 的内容是有错的块映射：** .NET 报语法错误，CLI 报值无效（键不是字符串）。
- **本地 tag 含方括号（如 `!c]`）：** YamlDotNet 读进 tag，按自定义 tag 报值无效（第 1 行）；`yaml` 包在方括号处截断，在所在行报语法错误。

## 模型体验

`SurfaceDiagnostic` 在冻结 ErrorDetail 外附加文件、行号和级别；ErrorDetail 内保留标准文件 URI 片段指针，例如 `skills/guide/SKILL.md#/description`。组件字段错误码由 Definition 的 `SurfaceErrors.All` 定义，使用自有错误目录登记；嵌入宿主统一目录时也可显式登记这些定义。

模型收到诊断后可以定位并修复原文件；错误不回显密钥值或执行输出。CLI 命令正文仍属于人类命令平面，不能因解析成功而进入服务端执行或模型工具平面。

## 验证

[共同 fixture](../../tck/surfaces/README.md) 覆盖所有支持表面、129 个组件场景（110 个反例）、无 src 组件、自定义指针、两侧精确诊断对比、74 条解析诊断向量与 16 条 YAML 取值向量，以及独立 JCS 浮点／Unicode 向量。.NET 还验证租户 A/B 隔离与卸载撤销。

## 已知限制与延期项

静态校验器不执行 Agent loop、MCP 进程或 hook；文件监视与 hook 执行增量见下文。二进制 runtime.entry 的真实定位、版本解算、签名和完整依赖闭包由 SDK/CLI/Registry 任务处理。UI 与低代码表面只检查显式路径，内容 Schema 属于对应模块。宿主须在登记前提供可信目录，并在真正调用时继续执行权限、scope 与执行策略检查。


## 市场文档

`ISurfaceValidator.ValidateMarketplace(file, content)` 与 TS `validateMarketplace` 共用 marketplace/v2，检查字段、安装名唯一性、分类、路径、公钥与远程 URL 约束，并输出同样的文件指针和行号。失败不返回含敏感输入的市场对象。它是静态文档校验，不读取网络或信任根；来源取得、验签、digest 及每个实际组件的 ValidateComponent 由 [CLI 市场链路](../qian-cli/README.md) 继续完成。跨语言向量在 tck/marketplaces。

能力目录可经 Definition 的 IToolContractHasher 端口使用 ToolContractHasher，复用同一 ToolContractHash/JCS 实现；不复制规范化算法，也不在组件库中反向引用本 Provider。

## 声明式表面独立刷新

`SurfaceRegistrationSession` 将共享校验后的文档注册为当前条目的可逆效果，按服务键与贡献名比较新旧内容，不重放代码 Apply；不变项保持对象引用。刷新先登记新增项，再经运行时注册句柄原位替换内容改变的项，最后撤销删除的项。注册按 key/name 独占（R38），不能先登记后撤销；先撤销后登记会让读取方（例如事件到达时读取 hook 文档的桥接）在两步之间看到缺失，原位替换则只让读取方看到旧文档或新文档。替换失败会撤销本次新增项并把已替换的项恢复为旧文档，删除项此时尚未撤销。技能同时出现在专用 Skills 目录与模型 Tools 集合，两个入口使用同一份文档；同一集合内的名称冲突不能静默覆盖。

`HotReloadSurfacePlugin` 可包装一个真实代码插件，也可作为无代码组件。它使用文件监视与定期核对，稳定读取并校验后调用上述刷新端口。非法新内容保留上一份有效注册；源码、清单身份或代码派生工具变化只标记 `RestartRequired`。状态不包含文件内容、配置或凭据。卸载先停止并等待监视工作，再撤销表面与代码效果，不在 effect 内递归等待装载器生命周期。

.NET 与 TypeScript 使用同一份 [T19 场景](../../tck/runtime/hotreload.json)，实际修改文件，验证五秒内更新、租户隔离、代码对象引用及效果保持、非法内容保留、源码重启提示和卸载清理。CI 对照两侧实际测试产生的结果；不靠调用手工刷新方法代替文件监视。轮询期间 Tools 必须恰好一项，过渡期缺失即失败。两侧会话单测另覆盖并发读取方在反复替换期间从不缺失、每次登记/替换/撤销之后新旧都有的文档仍可解析、新增冲突与替换中途失败的回滚。

[无代码技能场景](../../tck/runtime/skill-only.json) 另覆盖真实交付的 minimal profile 装配：组件只有清单与 SKILL.md，没有 runtime 声明或 src，两端均能装入租户 A、进入配置输出与 Skills/Tools，租户 B 不可见，卸载无残留。共享反例还验证任意 shell hook、服务端内联 shell 和未授权内联 shell 在创建运行时表面插件时即被拒绝；明确允许的人类 CLI 声明可以通过加载准入。这不代表本包实现了人类 CLI 的 shell 执行器。

当前已接入下节的 hook 执行增量与运行态诊断。按 R83，RT-009 的 hooks 桥接验收以测试宿主经真实 qian.tenancy 生产方上下文发布的租户事件为准，四项验收均有证据（见下节末段）；完整事件矩阵、真实开户事件生产方（S1-TEN-003）与 registry/entry-mounted 等生产方接线仍未完成。本次还修正运行时手动撤销注册后的效果栈清理；TypeScript 运行时需要在后续发布流程发布新的兼容补丁版本，不能覆盖已发布的 0.1.0。

### 运行态诊断

监视器把 `SurfaceReloadDiagnostics:<component>` 作为可逆的具名注册挂到同一 scope，属于表面监视的诊断集合，不是业务能力或安装状态来源。.NET 的 `SurfaceRuntimeDiagnostics.Read(ctx)` 与 TS 的 `readSurfaceRuntimeDiagnostics(ctx)` 都通过实际 `ctx.List` 读取，继承相同的遮蔽、restrict 和卸载语义；卸载后旧诊断对象也拒绝读取。输出只含条目、scope、修订号、变化的相对文件名以及重启/更新拒绝标志，不含配置和文件正文。

`Qian.Surfaces.Hosting` 的 `MapQianSurfaceDiagnostics(source, authorizationPolicy)` 映射 `GET /api/lifecycle/diagnostics/surfaces`。宿主必须显式提供已登记的授权策略，source 从可信身份获得 scope，不能从请求参数接受租户。它适用于需要 HTTP 诊断的 minimal 参考宿主及后续 platform 组装；不实现完整 lifecycle 管理面。TS 宿主可在同一路径先完成自己的认证授权，再返回上述读取函数的结果。无代码表面组件仍通过现有 `qian add --no-code` 安装；监视器及 HTTP 代码的安装归完整代码包安装流程。

`qian doctor --profile minimal --url https://host --json` 使用环境中的 `QIAN_ACCESS_TOKEN` 读取实时状态；本机回环允许 HTTP，远端只允许 HTTPS，拒绝重定向和超限/错误协议响应。`ROLLING_RESTART_REQUIRED` 提示滚动重启并返回非零，`SURFACE_UPDATE_REJECTED` 提示修正表面且保留上次有效注册。没有 `--url` 时仅验证静态 profile，`runtimeVerified` 为 false；不能将其作为运行态健康证明。诊断协议由 `schemas/v2/surface-diagnostics.schema.json` 共同校验。真实 CLI 进程已分别对 .NET Kestrel/签名 S2S 和 TS 文件监视宿主验收，T19 同时检查诊断 scope、重启状态与卸载失效。

## 真实 hook 桥接增量

运行时执行位于 `Qian.Surfaces.Hosting`，其 Definition 复用 `Qian.Surfaces.Abstractions`；通用表面校验器仍只解析与校验。`HookBridgePlugin` 包装实际代码插件，在 Apply 中核对声明是否能解析到真实 `IHookHandler`，并通过普通 `ctx.On` 连接运行时事件。当前支持 R39 的四个 serial 事件：tenant/provisioned、tenant/purged、registry/entry-mounted、registry/entry-disabled。事件生产方与 scope 来自真实上下文，不能从 JSON 伪造。

代码组件用 SDK 的 `ExposeCapabilityHook` 把已经实际登记的能力暴露为 handler，输入投影由可信代码完成。调用继续进入实际 Mediator，执行租户检查、RBAC、业务/Outbox 保存及同事务审计。SDK 的私有异步上下文只在本次目标能力调用期间标记 hook 通道，结束后恢复；请求体不能选择通道。hook 查询也会进入审计。Audit DTO、SDK 通道词汇与 PostgreSQL 事务函数现均接受任务规定的 hook 值。

桥接执行时重新读取当前 scope 的 hook 文档与处理器；隐藏、卸载及目标撤销均不能沿旧声明继续调用。`ISurfaceRuntimeValidator` 在热替换之前复核实际依赖。修改 matcher 或目标后，实际文件监视会更新下一次事件的执行行为，不重放代码 Apply。

真库验收使用经过 RS256 认证的 HTTP 入口、实际 Tenancy 生产方上下文、真实事件分发、客户能力、RBAC、Outbox 和 Audit。它证明桥接、通道隔离、权限拒绝、不可见声明不执行、缺失 handler 装载失败以及文件热更新。测试入口不执行租户完整开户或开通编排：R83 把「租户创建触发」的验收改为测试宿主发布事件，真实开户流程的触发归 S1-TEN-003。

hooks.json 的 Qian 事件名按 R39 的固定矩阵是 `<domain>/<event>`，租户创建对应 `tenant/provisioned`。验收原文写的 `tenant.created.v1` 是跨进程集成事件 ID（ADR-008 D8.7），hooks 校验器不接受它作触发名；要让集成事件触发 hooks，需要事件订阅端（S1-EVT-002）把它转成进程内事件。这条衔接已登记到 S1-TEN-003 的日志，随真实开户触发一并处理，归属待复核裁决。HTTP / 受限 argv 执行器与其它分发模式仍未完成，目前这些未支持的声明会拒绝装载。
