# 共享内部源文件

这里放多个 Qian 包共用、又不该成为公开 API 的实现。目录本身不是项目，也不单独打包：需要的包在 csproj 里把源文件链接进来编译，类型一律 `internal`，命名空间为 `Qian.Common`。

| 文件 | 编译进 | 用途 |
|---|---|---|
| `SchemaPattern.cs` | Qian.Audit、Qian.Config.Abstractions、Qian.Profiles、Qian.Surfaces、Qian.Capabilities.Abstractions | JSON Schema 正则（`pattern` 与 `patternProperties` 的键）的统一解释：按 ECMA-262 可移植子集翻译、可选按 OrdinalIgnoreCase 折叠、判断键名在各方言下是否稳定；NonBacktracking 引擎、1 秒超时与有界缓存也只在这里定义。@qian/surfaces 有逐条对应的 TS 移植，见下文 |
| `TenantFieldRule.cs` | Qian.Surfaces、Qian.Capabilities.Abstractions（都须同时链接 `SchemaPattern.cs`） | 输入 Schema 不得点名租户字段：工具的语义校验与能力输入契约（`TenantInputContract`）共用的写法清单与遍历，见下文；@qian/surfaces 的 `tenantFields` 逐条对应 |

## 约定

- 只依赖 BCL，不引用任何 Qian 包或第三方包。链接进哪个包都不改变该包的依赖图，依赖图门禁照常只看 csproj 的引用。
- 每个包各编译一份，静态状态（如编译缓存）按程序集各自独立。
- 行为以 `tests/Qian.Common.Tests` 为准，它直接编译这里的源文件；各包另测自己的接入方式，如错误码与失败关闭。
- 需要对外公开时，在合适的包里新增公开 API 并登记 PublicAPI 文件，不要让外部代码依赖这里的类型。

链接写法：

```xml
<ItemGroup><Compile Include="../Common/SchemaPattern.cs" Link="Common/SchemaPattern.cs" /></ItemGroup>
```

## SchemaPattern 的判定规则

- 模式按 ECMA-262 不带标志的语义解释（JSON Schema 2020-12 核心规范 §6.4），逐 UTF-16 码元匹配：`\d`、`\w` 只认 ASCII，`\s` 为 ECMA-262 的空白与行终止符，`.` 不匹配 `\n`、`\r`、U+2028、U+2029，`^` 与 `$` 只在开头与末尾成立（`$` 翻译成 `\z`）。代理对按两个码元计，与带 u 标志的 JS 正则不同。
- 只接受各方言含义相同的写法：字面量、字符类与范围、`\d \w \s` 及其否定、`.`、`^`、`$`、分组、非捕获组、`|` 与量词（含惰性）。其余写法翻译时抛 `NotSupportedException`，由调用方按 Schema 无效失败关闭：词边界、内联选项、Unicode 属性、命名组、环视、反向引用、代理项等。
- 分组必须配对，`{n,m}` 要求 n ≤ m。展开量词后，字符、类、`.` 与锚点的总数不超过 1000（`MaxSize`），每个量词边界也不超过 1000；`{n,}` 按 n+1 份计。NonBacktracking 按展开后的规模估算自动机，含锚点时约为该数的 5 倍，超过 10000 即拒绝构造。这条规则在翻译时先行拒绝，接受与否不取决于 .NET 的估算；上限处的对抗写法都能构造，有测试守住。
- `IsStable` 为假的键名含有方言判断不同的码元（换行、只有 ECMA-262 算作空白的字符、U+0130、代理项）。翻译结果按 ECMA-262 判断它们，但审计与配置扫描随后还由 Schema 验证器按 .NET 的 ECMAScript 选项判断，两者可能不同，所以调用方不对这类键名做匹配，而是整体按敏感处理。
- profile 装配的两端 Schema 验证器（.NET 的 JsonSchema.Net 与 CLI 的 Ajv）也用翻译结果执行 `pattern` 与 `patternProperties`，字符串值与键名都按上述语义判断，见 [Qian.Profiles](../Qian.Profiles/README.md)。
- `foldCase` 用于配置：配置键与 Web 默认反序列化都不区分大小写。折叠后，键名能匹配，当且仅当存在一个与它相等（`StringComparer.OrdinalIgnoreCase`）的键名按原模式能匹配。审计脱敏面对的是区分大小写的 JSON，不折叠。

## TS 移植

本地 CLI 的 profile 装配（`packages/qian-cli/src/profiles.ts`）与 `ProfileLoader` 判定相同，表面校验器的工具租户字段检查（`packages/qian-surfaces/src/index.ts`）与 `Qian.Surfaces` 判定相同，所以 `SchemaPattern` 有一份逐条对应的移植 `packages/qian-surfaces/src/schema-pattern.ts`，由 `@qian/surfaces` 导出，CLI 从那里引入（S1-AGT-004 之前在 `packages/qian-cli/src`）：

- 接受与拒绝、规模上限、`IsStable`、折叠与翻译输出都与本实现逐字一致。
- 匹配不交给 JS 正则：`KeyPattern` 把翻译结果构造成自动机逐码元模拟，与 NonBacktracking 一样线性时间。折叠会让原本互斥的分支重叠，例如 `^(?:[a-z]|[A-Z])+$` 折叠后两个分支相同，V8 的回溯引擎遇到以 `!` 结尾的长键名会指数级变慢；未折叠的 `^(a|a)*$` 同样如此。`KeyPattern` 也是 profile 装配时 Ajv 的正则引擎（`code.regExp`）。
- 键名相等用 `sameKey`，按 .NET 的 `string.Equals(…, StringComparison.OrdinalIgnoreCase)` 判断，含增补平面的大小写对与残缺代理项的处理；不用 JS 的 `toUpperCase` / `toLowerCase`，它们会把 `ß` 变成 `SS`，也把 `ſ`、`ı`、开尔文符号归到 ASCII 字母。

两份文件由 `tests/Qian.Common.Tests` 按本实现与 .NET 比较器生成，测试比对仓库里的内容：

- `packages/qian-surfaces/src/ordinal-case.ts`：OrdinalIgnoreCase 等价类表（BMP 与增补平面），只收录各宿主一致的部分（见下文）；
- `tck/profiles/key-vectors.json`：模式的翻译结果、在固定键名上的匹配（不折叠与折叠，由 NonBacktracking 执行）、在值层语料上的匹配（不折叠，含行终止符、非 ASCII 空白、代理对与残缺代理项）、全部 BMP 码元的稳定性与键名相等向量。@qian/surfaces 的 `test/schema-pattern.test.mjs` 逐条比对，值层另与 V8 不带标志的 `RegExp` 逐例对照。

OrdinalIgnoreCase 在 Linux 与 macOS 上按宿主 ICU 的大小写映射判断，所以 .NET 的结果随宿主而异。Unicode 16.0 起新增的大小写字母（如 U+A7CB 与 U+0264、U+1C89 与 U+1C8A、Garay 文字），Ubuntu 24.04 的 ICU 74 视为不等，较新的 ICU（如 macOS 的）视为相等；CI 与 .NET 10 的默认镜像都是 ICU 74。等价类表不收录这些字母，TS 移植按 ICU 74 的结果把它们当作互不相等；测试生成表时按名单去掉它们，所以在各宿主上生成的表相同。宿主 ICU 较新时，.NET 对这些键名的比较与 TS 移植不同。

改了 `SchemaPattern.cs` 或升级 .NET 后，重新生成并同步 TS 移植，直到两端测试都通过。宿主 ICU 又新增大小写对时，测试在该宿主上会列出多出的等价类，把新字母补进 `SchemaPatternTests` 的名单：

```bash
QIAN_UPDATE_VECTORS=1 dotnet run --project tests/Qian.Common.Tests -c Release
pnpm --filter @qian/surfaces build && pnpm --filter @qian/surfaces test && pnpm --filter qian build && pnpm --filter qian test
```

## TenantFieldRule 的判定规则

租户只来自令牌（PRD 7.18），所以输入 Schema 不得点名租户字段（S1-AGT-004）。工具文件的语义校验（`Qian.Surfaces` 与 @qian/surfaces，`SURFACE_POLICY_DENIED`）与能力输入契约（`TenantInputContract`，能力编译为 `tenant-field`）按同一规则报告：

- 租户名按 contracts/v0 的 `TenantFieldName` 整体匹配：`tenant`、`tenantId`、`tenant_id`、`tenant-id` 的 ASCII 大小写变体。不用 `ToLowerInvariant` 加 `$`：.NET 的 `$` 会放过末尾换行，把 `tenant` 后接换行也算成租户名。
- 点名的写法：`properties`、`dependentSchemas`、`dependentRequired` 与旧式 `dependencies` 的名称；`required` 与 `dependentRequired` / `dependencies` 的数组项；`patternProperties` 的模式；`propertyNames` 在肯定位置（根与 `allOf` / `anyOf` / `oneOf` / `then` / `else` 之下）用 `const` / `enum` 列出的名称。模式按 `SchemaPattern` 的可移植子集解释，与 JSON Schema 一样不隐式加锚点，能匹配租户名的任一 ASCII 大小写写法即违规；超出子集无法判定，同样违规。试配用全部 832 种写法而不用折叠，结果不随宿主 ICU 变化。
- 遍历：未知成员照常往下查；`example`、`examples`、`default`、`const`、`enum` 是实例数据，不当 Schema 检查。名称表（`properties`、`patternProperties`、`dependentSchemas`、`$defs`、`definitions`）的条目是名称，叫 `default`、`enum` 也照常往下查。按成员出现顺序报告，名称表先查全部名称、再逐条往下，两端校验器的诊断顺序因此一致。
- 能力契约另按文档内 `$ref` 在引用处继续检查（HTTP 契约要从请求体跟到 `components`），表面校验器不解析引用，`$defs` 照常遍历。
- 本规则是点名检查，不是封闭性检查（S1-AGT-004 的裁决，2026-10-22 用户认可）：
  - 嵌套 object 不要求关闭。开放的映射与自由 JSON（`additionalProperties` 为 Schema 或缺省）不点名任何字段，不检查；租户隔离由令牌派生的租户上下文、RLS，以及平台 API 对显式租户参数的核对保证，与 Schema 是否封闭无关。
  - `propertyNames` 只在上述肯定位置列出租户名时算点名：`pattern` 只收紧键名格式，`not` 之下是排除，`if` 之下是条件，都不算；经 `$ref` 间接列出的名称不检查。
  - 超出可移植子集的模式按无法判定拒绝；实例数据不检查；旧式 `dependencies` 照常检查（Ajv 2020 仍执行它）。
