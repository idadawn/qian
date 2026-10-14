# profile 装配 TCK

`base/` 为共同组件和分层输入，`cases.json` 指定场景覆盖文件、参数与独立验收断言；覆盖文件的内容为字符串时按 UTF-8 写入，为 `{"base64": …}` 时按原样字节写入（用于 BOM 等编码）。测试只在临时目录展开，不改工作区。包含 120 个场景：bundle/profile/home/overlay 顺序、整段替换和默认值、插入、disabled 恢复、required 保护、scope、版本范围、非法配置和 JSON Pointer、依赖缺失/重复/环路、AOT/HMR、骨架告警、敏感字段及引用/数组；新增内存 scope 末层的继承/整段替换、禁用定位、跨 scope 改写拒绝、required 保护与敏感配置拒绝。

15 个 `sensitive-*` 场景约束敏感键名的判定（S1-CFG-007、S1-CFG-008）：属性名与模式属性的键按 OrdinalIgnoreCase 匹配（含增补平面，`ß`、`ſ`、`ı`、开尔文符号不等于 ASCII 字母），`additionalProperties` 只在按原样没有匹配时适用，模式按 ECMA-262 可移植子集并折叠后判定（否定类折叠后匹配得更多），方言不稳定的键名要求 secretRef，不支持或超出规模上限的模式在 Schema 预检处失败、先于不稳定键名报告，`x-qian-sensitive` 不是布尔值时无法解析；配置键与模式按序数顺序遍历，两端报告同一个首个违规。断言字段 `reason` 要求 `pointer` 处的逐项错误文案与之相同。

S1-CFG-008 另加三组场景，约束配置 Schema 验证的诊断，规则见 [Qian.Profiles](../../packages/Qian.Profiles/README.md)：

- 9 个 `config-diagnostics-*`：两个验证器的错误归一为出错实例位置。缺少 required 与子属性错误同时报告，同一位置只报一次，按序数排序，节点自身的断言不因子节点失败而丢失，propertyNames 报到键名，contains 报数组位置，组合的汇总照常报告，if 的条件与通过的组合下失败的分支不计。
- 6 个 `config-pattern-*`：`pattern` 与 `patternProperties` 按 ECMA-262 不带标志的语义逐码元执行。`$` 只在末尾成立，`.` 不匹配行终止符，`\s` 含非 ASCII 空白，代理对按两个码元计，键名判定影响 additionalProperties，对抗写法线性时间完成。
- 5 个 `schema-pattern-*`：交给验证器之前预检 Schema 各子 Schema 位置上的正则。超出可移植子集的写法（含只有一种方言接受的写法）在 Schema 文件的对应指针处失败，多处违规按序数顺序报告首个；`$ref` 只能指向子 Schema 位置，引用 `x-shared` 等其他位置时在 `$ref` 处报告（S1-CFG-009）。

S1-CFG-009 再加三组，约束两个验证器的其余语义与 Schema 的写法要求：

- 18 个 `config-semantics-*`：两端按同一语义判断。prefixItems 之后的 items 从前缀之后的下标起算（`items: false` 报到多出的各项），文本长度按码点计数，multipleOf 按十进制精确判断，数值按双精度比较（超过 2^53 的整数、1e30、1e-30），const、enum、uniqueItems 按 JS 的值相等比较全部项，带 `toString` 键的对象照常比较，继承名称不算已提供，contains 与 propertyNames 的子 Schema 经 `$ref` 引用时只报数组或键名，oneOf 多个分支同时成立时只报自身位置，dependentSchemas 与 patternProperties 的键可含 `/`，属性的 `default` 可为 null，Ajv 原本会抛错的 anyOf 含 if/then 加 patternProperties 照常验证。
- 14 个 `schema-rule-*`：两端解释不同的写法在 Schema 文件的对应指针处失败：不符合 2020-12 元 Schema、`$schema` 不是 2020-12、`$id` 等标识、`nullable`、`unevaluatedItems` 与 `unevaluatedProperties`、空 enum、无法解析或不指向子 Schema 位置的 `$ref`（含 `#/`、元 Schema 校验不到的位置、末尾的换行与百分号编码的换行）、不经属性或数组项的循环引用、以 `__proto__` 为键名的 properties。
- 5 个 `schema-file-*`：Schema 文件含重复键、超出双精度范围的数值或残缺代理项、以 UTF-16 的 BOM 开头时按无法解析处理，以 UTF-8 的 BOM 开头时照常读取。

`key-vectors.json` 由 `tests/Qian.Common.Tests` 按 .NET 的模式翻译器与 OrdinalIgnoreCase 生成，CLI 的模式移植逐条比对，生成方法见 [共享内部源文件](../../packages/Common/README.md#cli-的-ts-移植)。

```bash
pnpm --filter qian... build
QIAN_PROFILE_REPORT=/tmp/profiles-ts.json pnpm --filter qian test
QIAN_PROFILE_REPORT=/tmp/profiles-dotnet.json dotnet run --project tests/Qian.Profiles.Tests -c Release
node packages/qian-surfaces/scripts/compare-fixtures.mjs /tmp/profiles-dotnet.json /tmp/profiles-ts.json
```

两侧报告比较完整快照与诊断，唯一归一化为随机 trace_id 以及 ErrorDetail 可选空字段；不掩盖错误码、文件、指针、行号、配置或来源差异。另有默认五个 profile 与真实 CLI 子进程测试；.NET 从实际交付的 minimal profile 和组合包文件启动真实 PluginLoader，再通过 overlay 挂载静态命令。minimal 默认无业务组件、无骨架警告，符合 S1-DEC-012 的确认结论。
