# Qian.Profiles

依据 [组件规格 §7](../../docs/prd/specs/component-model.md#7-profile组合包与-patch) 与 [S1-RT-002](../../pm/tasks/S1-RT-002.md)。`Qian.Profiles.Abstractions` 定义装配端口与快照；本包提供本地 profile、组合包及 patch 的静态装配，不依赖平台注册表、生命周期或数据库。

## 装配位置与使用

本目录包含分层装配器、配置 Schema 校验、版本选择与依赖排序。输入表面为 `profiles/<name>/qian.profile.yml`、组件 `.qian-plugin/plugin.json`、组合包及各层 `qian.patch.yml`。所有 profile 均可使用；它是启动装配端口 `IProfileLoader` 的 Provider，不新增业务 seam 或特权服务。`qian add` 的下载、目录市场与包安装由后续 CLI 任务实现，本轮仅消费已在本地的组件内容。宿主还可通过 `ScopePatches` 传入已从数据库读取的末层 patch；本包只做装配，不读写数据库或判定业务权限。

```csharp
var composer = new ProfileLoader(surfaceReader);
var result = composer.Load(new ProfileLoadOptions
{
    WorkingDirectory = projectDirectory,
    Profile = "platform",
    Patches = ["ops/local.patch.yml"],
    IncludeHomePatch = false
});
// 只有 result.IsValid 才能把 Snapshot 交给真实 Provider 解析器与 PluginLoader。
// x-skeleton 条目仅供静态审阅，不能当成可启动的业务 Provider。
```

宿主通过 `ISurfaceReader` 传入统一解析器，不引用表面 Provider 包。快照保留组件目录与清单供宿主校验其表面；这些辅助字段不进入 dump-config。表面实际装载示例与生命周期见 [Qian.Surfaces](../Qian.Surfaces/README.md)。

## 分层规则

按顺序应用组合包、profile patch、`$QIAN_HOME/qian.patch.yml`（默认 `~/.qian`）、重复出现的 `--patch`。缺省 home 文件仍保留 `present: false` 来源层；显式指定但不存在的文件报错。可关闭 home 层以重现构建。组件来自项目 `plugins/`、`.qian/plugins/` 和显式额外目录，组合包内的 `components/` 也可发现。仅选择本地满足范围的最高 SemVer 版本，明确输出 `id@version`；本阶段不下载包，也不读写后续安装任务的 lock 文件。

条目身份是 `(scope,id)`。同层重复身份报错；后层省略 plugin/config 时沿用，出现 config 时替换整个对象。插入要求 plugin 与 config。disabled 条目不在结果树中，后层可重新启用。required 条目不可禁用或换为另一组件；同组件可有独立租户配置。scope 必须在清单允许集合中；同 scope 服务键唯一，依赖先取本 scope 再取 global，缺失与环路均阻止装配。

宿主传入的 `ScopePatchLayer` 在全部文件层之后合入，必须声明一个非全局 scope，数组条目不能改写到 global 或其它 scope。它复用相同 patch/v2 解析、配置校验、required 保护与依赖排序；新局部条目有同 id 的全局条目时可继承其 plugin/config，再应用局部替换。文件中新建条目的显式 plugin/config 要求保持不变。调用方负责租户归属、鉴权、权益与受信制品来源。

`ProfileSnapshot.DisabledEntries` 保留禁用项的身份、层号及来源指针，作为宿主装载辅助信息，不混入默认 dump-config。宿主必须据此对指定 scope 隐藏该全局条目的贡献，不能仅跳过局部挂载，否则仍会看见全局 fallback。装配器输出该信息不代表自动热重载已经完成。

config 使用组件 `runtime.configSchema` 或 `schemas/config.schema.json` 校验；非空配置不能缺少 Schema。默认值补充发生在整体替换之后，支持对象属性、数组 items、文档内 JSON Pointer 引用及 allOf，不继承旧配置，也不为互斥分支猜默认值。配置 Schema 不联网取引用，文档内引用有深度限制。`x-qian-sensitive` 在对象、数组、组合分支及本地引用中均要求 secretRef，取值必须是布尔值，否则装配失败；不会解析密钥。文件错误不回显字段值。

敏感判定按两种读法取并集。`IQianPlugin<TConfig>` 按 Web 默认反序列化，不区分大小写，所以属性名与模式属性的键都不区分大小写匹配：`Password` 同样受 `password` 的标记约束。验证器与直接读 `JsonElement` 的插件区分大小写，所以 `additionalProperties` 只在按原样没有匹配时适用。模式属性的键按 ECMA-262 可移植子集解释，规则与配置扫描相同，见 [共享内部源文件](../Common/README.md)：超出子集或规模上限的写法让装配失败；键名含各方言判断不同的字符时，值必须是 secretRef。CLI 的同名检查（`profiles.ts`）逐条移植同一规则，键名相等也按 .NET 的 OrdinalIgnoreCase 判断，不用 JS 的大小写转换；共享 TCK 要求两端给出逐字相同的诊断。

配置键、属性名与模式属性按序数顺序遍历，敏感检查遇到首个违规即失败：JS 对象把整数形式的键排在前面，按文档顺序时两端报告的首个违规会不同。

两端的 Schema 验证器（.NET 为 JsonSchema.Net，CLI 为 Ajv）对正则的写法与语义各有方言，所以都把 `pattern` 与 `patternProperties` 的键交给同一翻译规则执行：.NET 在构造后替换编译结果，CLI 通过 Ajv 的 `code.regExp`。字符串值与键名按 ECMA-262 不带标志的语义逐码元判断，additionalProperties、unevaluatedProperties、propertyNames 随之一致。交给验证器之前，两端按同一顺序检查各子 Schema 位置上的正则，超出可移植子集的写法在 Schema 文件的对应指针处失败，如 `plugins/x/schemas/config.schema.json#/properties/a/pattern`。这一检查不跟随 `$ref`：引用到其他位置的不支持写法在验证器编译时拒绝，按无法解析处理。

验证失败时，两个验证器的错误归一为出错实例位置，去重后按序数排序：

- 容器关键字（properties、patternProperties、additionalProperties、propertyNames、unevaluatedProperties、items、prefixItems、allOf、dependentSchemas、`$ref`）只经由失败的子位置报告；
- 其余失败的关键字报自身位置，含 anyOf、oneOf、not 以及 if 之后失败的 then 或 else；if 的条件本身不计，通过的组合下失败的分支也不计；
- additionalProperties、unevaluatedProperties、propertyNames 的错误报到键名；contains 只报数组位置。

验证器在少数语义上仍有分歧，暂未对齐：prefixItems 之后的 items（JsonSchema.Net 9.4.0 的逐项位置从 0 起算，Ajv 对 `items: false` 只报数组位置）；unevaluatedItems；minLength 与 maxLength（JsonSchema.Net 按文本元素计数，Ajv 按码点）；multipleOf 的小数（Ajv 用浮点）；contains 或 propertyNames 的子 Schema 经 `$ref` 引用时，Ajv 可能多报位置。配置 Schema 宜避开这些写法。

gateway/identity 不允许 command hook，S1 拒绝代码热更新；AOT profile 拒绝 function 或未声明 AOT 的代码清单。这只是装配预检，不能代替组件 NativeAOT 编译验收。

## 结果和边界

`ProfileLoadResult` 返回快照与 `SurfaceDiagnostic`；错误时 Snapshot 为空。诊断外层有文件、行号和级别，内层遵循冻结 ErrorDetail，字段码 `PROFILE_INVALID` 由 Definition 包声明。错误指针定位原 patch 的 `/条目序号/config/字段`；来源层指向最后改写条目的文件。

默认 [profiles](../../profiles/README.md) 的业务叶节点目前均为 `x-skeleton`，会输出 warning；后续组件任务补真实 Provider、完整配置契约与依赖，不能据此宣称平台/网关/身份服务 Ready。按 [S1-DEC-012](../../pm/tasks/S1-DEC-012.md) 的确认结论，minimal 仅包含 Runtime 与表面装载器、初始 patch 为空；测试已从实际交付的 minimal 文件启动真实 Runtime，并通过 overlay 验证静态命令装载。

CLI 使用方式见 [qian](../qian-cli/README.md)，双侧场景与全量检查见 [profile TCK](../../tck/profiles/README.md)。
