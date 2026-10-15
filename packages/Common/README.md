# 共享内部源文件

这里放多个 Qian 包共用、又不该成为公开 API 的实现。目录本身不是项目，也不单独打包：需要的包在 csproj 里把源文件链接进来编译，类型一律 `internal`，命名空间为 `Qian.Common`。

| 文件 | 编译进 | 用途 |
|---|---|---|
| `SchemaPattern.cs` | Qian.Audit、Qian.Config.Abstractions、Qian.Profiles | JSON Schema 动态键模式（`patternProperties`）的统一解释：按 ECMA-262 可移植子集翻译、可选按 OrdinalIgnoreCase 折叠、判断键名在各方言下是否稳定；NonBacktracking 引擎、1 秒超时与有界缓存也只在这里定义 |

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

- 模式按 ECMA-262 解释（JSON Schema 2020-12 核心规范 §6.4）。`\d`、`\w`、`\s` 只认 ASCII，`.` 不匹配换行。
- 只接受各方言含义相同的写法：字面量、字符类与范围、`\d \w \s` 及其否定、`.`、`^`、`$`、分组、非捕获组、`|` 与量词（含惰性）。其余写法翻译时抛 `NotSupportedException`，由调用方按 Schema 无效失败关闭：词边界、内联选项、Unicode 属性、命名组、环视、反向引用、代理项等。
- `IsStable` 为假的键名含有方言判断不同的码元（换行、只有 ECMA-262 算作空白的字符、U+0130、代理项），调用方不对它做匹配，而是整体按敏感处理。
- `foldCase` 用于配置：配置键与 Web 默认反序列化都不区分大小写。折叠后，键名能匹配，当且仅当存在一个与它相等（`StringComparer.OrdinalIgnoreCase`）的键名按原模式能匹配。审计脱敏面对的是区分大小写的 JSON，不折叠。
