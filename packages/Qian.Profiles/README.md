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

gateway/identity 不允许 command hook，S1 拒绝代码热更新；AOT profile 拒绝 function 或未声明 AOT 的代码清单。这只是装配预检，不能代替组件 NativeAOT 编译验收。

## 配置 Schema 验证

两端的 Schema 验证器（.NET 为 JsonSchema.Net 9.4.0，CLI 为 Ajv 8.20）各有方言与缺陷。装配器先按同一组规则预检 Schema，再让两个验证器按同一语义判断，诊断归一后逐字一致，由共享 TCK 约束。

### 写法要求

交给验证器之前，两端按同一顺序预检 Schema（`ProfileLoader.Check` 与 `profiles.ts` 的 `check`）：从根开始按固定关键字表走遍各子 Schema 位置，映射按键名序数、数组按下标。`$ref` 只能指向这些子 Schema 位置，随后的元 Schema 校验因此覆盖每个引用目标。首个违规在 Schema 文件的对应指针处报告，如 `plugins/x/schemas/config.schema.json#/properties/a/pattern`：

- `pattern` 与 `patternProperties` 的键只接受 ECMA-262 可移植子集，见下节。
- `$schema` 只能是 `https://json-schema.org/draft/2020-12/schema`（可带空片段 `#`）：Ajv2020 不能编译 draft-07，JsonSchema.Net 则按声明的方言验证。
- 不支持 `$id`、`$anchor`、`$dynamicAnchor`、`$dynamicRef`、`$recursiveAnchor`、`$recursiveRef`、`$vocabulary`。引用只用文档内的 JSON Pointer；标识与动态引用会改变 `#/…` 的含义，默认值补充与敏感检查也不认它们。
- 不支持 draft-07 的 `dependencies`（改用 `dependentRequired` 或 `dependentSchemas`），也不支持 Ajv 自有的 `$async` 与 `nullable`（可空写成 `"type": ["string", "null"]`）。
- 不支持 `unevaluatedItems` 与 `unevaluatedProperties`。Ajv 统计已求值的项与属性时与规范不同：失败的 `allOf` 分支与 `$ref`、不成立的 `if` 仍计入，`contains` 把全部项算作已求值，`anyOf` 各分支都失败时项数丢失，含 `items` 的分支成立后按数值比较项数；8.20.0 的这套统计在 `anyOf` 含 `if`/`then`、同级有 `patternProperties` 时还会在验证时抛错。封闭的元组写成 `prefixItems` 加 `items: false`，封闭的对象在同一层列全 `properties` 并加 `additionalProperties: false`。
- `enum` 须为非空数组：Ajv 编译时拒绝空数组，JsonSchema.Net 让任何值都不符合。
- `properties` 与 `patternProperties` 不能以 `__proto__` 为键名：Ajv 为防原型污染略过这样的键。
- `$ref` 须为 `#/` 开头、字符合乎 URI 片段的 JSON Pointer，指向本文档内的子 Schema 位置：从根起逐段经上述子 Schema 关键字到达（映射再经键名、数组再经下标），如 `#/$defs/a`、`#/properties/a/items`，目标是对象或布尔值。其他位置（如根下的 `x-shared`）不在元 Schema 的校验范围内，Ajv 对其中不合规的写法（`minLength: -1`、空的 `allOf`）照常编译，JsonSchema.Net 构造失败；`#/` 按 JSON Pointer 指向根下的空键名，Ajv 却当作根。空格与非 ASCII 字符写成百分号编码，须为合法 UTF-8；键名里的 `/` 与 `~` 写成 `~1` 与 `~0`，不能写成 `%2F`（JsonSchema.Net 先解码整段再分段，Ajv 先分段再解码）；数组下标写成不带前导零的十进制数。
- 子 Schema 不能经 `$ref`、`allOf`、`anyOf`、`oneOf`、`not`、`if`、`then`、`else`、`dependentSchemas` 不经属性或数组项又回到自身。这样的引用在同一实例上无限递归：JsonSchema.Net 在构造或求值时报循环，Ajv 在编译或验证时栈溢出，未被引用时两端的结论也不同。

预检之后按 JSON Schema 2020-12 的元 Schema 校验，不符时在 Schema 根部报「组件 config.schema 不符合 JSON Schema 2020-12 的元 Schema。」（Ajv 编译时本就校验，JsonSchema.Net 不校验）。Schema 文件按 UTF-8 与严格 JSON 读取：开头可有一个 UTF-8 BOM，不按 BOM 改用 UTF-16 或 UTF-32；不合法的 UTF-8、重复键、超出双精度范围的数值、含残缺代理项的字符串或键名、超过 64 层的嵌套，都按「profile 或组件内容无法解析。」处理。

### 验证语义

- 正则：两端都把 `pattern` 与 `patternProperties` 的键交给同一翻译规则执行（.NET 在构造后替换编译结果，CLI 通过 Ajv 的 `code.regExp`），字符串值与键名按 ECMA-262 不带标志的语义逐码元判断，additionalProperties、propertyNames 随之一致，规则见 [共享内部源文件](../Common/README.md)。
- 数值按双精度判断，同 JS：.NET 先把 Schema 与待验证的配置副本中的数值换成双精度（超过 2^53 的整数与超长小数随之舍入，`-0` 记为 `0`）。比较、`type: integer` 与 `multipleOf` 都作用在双精度的最短十进制表示上，`multipleOf` 按十进制精确判断，`0.3` 是 `0.1` 的倍数。
- `const`、`enum`、`uniqueItems` 按 JS 的值相等比较：数值按双精度，对象不计键序。`uniqueItems` 比较全部项（Ajv 原本在 `items` 只声明标量类型时跳过类型不符的项）。
- `minLength`、`maxLength` 按码点计数：代理对计 1，残缺代理项与组合字符各计 1（`e` 加组合重音计 2）。
- 只认自有属性：`required`、`properties`、`dependentRequired`、`dependentSchemas` 不把 `constructor`、`toString` 等继承名称当成已提供，带 `toString` 键的对象也照常比较。
- JsonSchema.Net 9.4.0 的三处缺陷由装配器绕过：`dependentSchemas` 与 `patternProperties` 按 JSON Pointer 转义后的段查找，键含 `/` 或 `~` 时前者静默跳过、后者求值时抛错；`prefixItems` 之后的 `items` 从 0 起算位置。
- 属性的 `default` 可以是 `null`，补充后同样参与验证。

### 逐项错误位置

验证失败时，两个验证器的错误归一为出错实例位置，去重后按序数排序：

- 容器关键字（properties、patternProperties、additionalProperties、propertyNames、items、prefixItems、allOf、dependentSchemas、`$ref`）只经由失败的子位置报告；
- 其余失败的关键字报自身位置，含 anyOf、oneOf、not 以及 if 之后失败的 then 或 else；if 的条件本身不计，通过的组合下失败的分支也不计；
- additionalProperties、propertyNames 的错误报到键名；`prefixItems` 之后的 `items: false` 报到多出的各项；contains 只报数组位置；
- oneOf 因两个以上分支同时成立而失败时只报自身位置：Ajv 第二个分支成立后不再求值其余分支。

### 已知分歧

数值只在验证时归一。装配结果中的配置值，.NET 返回原值（JSON 补丁里超过 2^53 的整数保留原文），CLI 返回双精度，`-0` 输出为 `0`。

补丁里含未成对代理项的字符串或键名由表面解析器在补丁文件处拒绝，两端诊断相同，见 [Qian.Surfaces](../Qian.Surfaces/README.md)（S1-RT-022）。

键名不区分大小写的比较：.NET 按宿主 ICU 判断 OrdinalIgnoreCase，CLI 按 ICU 74 的等价类表。Unicode 16.0 起新增的大小写字母（如 U+A7CB 与 U+0264），宿主 ICU 较新（如 macOS）时 .NET 视为相等、CLI 视为不等，见[共享内部源文件](../Common/README.md#cli-的-ts-移植)。

## 结果和边界

`ProfileLoadResult` 返回快照与 `SurfaceDiagnostic`；错误时 Snapshot 为空。诊断外层有文件、行号和级别，内层遵循冻结 ErrorDetail，字段码 `PROFILE_INVALID` 由 Definition 包声明。错误指针定位原 patch 的 `/条目序号/config/字段`；来源层指向最后改写条目的文件。

默认 [profiles](../../profiles/README.md) 的业务叶节点目前均为 `x-skeleton`，会输出 warning；后续组件任务补真实 Provider、完整配置契约与依赖，不能据此宣称平台/网关/身份服务 Ready。按 [S1-DEC-012](../../pm/tasks/S1-DEC-012.md) 的确认结论，minimal 仅包含 Runtime 与表面装载器、初始 patch 为空；测试已从实际交付的 minimal 文件启动真实 Runtime，并通过 overlay 验证静态命令装载。

CLI 使用方式见 [qian](../qian-cli/README.md)，双侧场景与全量检查见 [profile TCK](../../tck/profiles/README.md)。
