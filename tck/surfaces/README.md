# 声明式表面共同验收

`cases.json` 由 `scripts/generate_surface_fixtures.py` 生成，包含 100 个独立组件场景（81 个反例）。fixture 保存完整文件内容与宿主目录选项，两端都写入临时组件目录后运行真实文件解析器。`jcs-vectors.json` 另提供独立文字规范值，覆盖浮点、负零、最小非零浮点、Unicode 和 UTF-16 属性顺序。

```bash
python3 scripts/generate_surface_fixtures.py
pnpm --filter @qian/surfaces build
QIAN_SURFACE_REPORT=/tmp/surfaces-ts.json pnpm --filter @qian/surfaces test
QIAN_SURFACE_REPORT=/tmp/surfaces-dotnet.json dotnet run --project tests/Qian.Surfaces.Tests -c Release
node packages/qian-surfaces/scripts/compare-fixtures.mjs /tmp/surfaces-dotnet.json /tmp/surfaces-ts.json
```

对比有效性、错误码、文件、JSON Pointer、行号与 error/warning 级别；文案和每次生成的 trace-id 不参与对比。期望里写明行号的反例，两端还各自核对该行号。另以冻结 error-detail Schema 验证 .NET 错误体，并验证技能目录的租户隔离和 Effect 卸载。

元数据场景覆盖默认、自定义及根目录、目录类型、JSON 语法、重复键、敏感值和符号链接；原始快照与诊断均参加两侧比较，业务 Schema 由相应 seam 的验收负责。

清单准入场景（S1-RT-004）覆盖缺任一 S1 必填字段（按 plugin Schema 的 required 逐个生成）、OCI 引用用 tag（digest 为对照正例）、`scopes[]` 含非法种类、工具顶层输入含租户字段，以及 `inject[]` 引用无 Provider 的服务键：宿主经 `serviceKeys` / `ServiceKeys` 给出目标装配的服务键目录时视为完整目录，目录外的 inject 为 error，省略时不检查。`propertyNames` 不合格时两侧都指向出错的属性名本身。注册表的候选制品登记入口用同一批 fixture 验证「登记失败并带 JSON Pointer」（`tests/Qian.Registry.Tests/ManifestIntakeTests.cs`）。

工具输入的租户字段（S1-AGT-002）：顶层由 tool Schema 的 `propertyNames`（v0 `TenantFieldName`）拒绝，为 `SURFACE_SCHEMA_INVALID`；嵌套属性、`$defs`、数组项与组合子句由语义检查拒绝，为 `SURFACE_POLICY_DENIED`。两处各自实现同一条名称规则（`tenant`、`tenantId`、`tenant_id`、`tenant-id`，大小写不敏感），所以每种写法两层各有一例；名称整体匹配，`lieutenant`、`tenants` 这类只是含有 tenant 的名称照常通过。生命周期的安装入口用这批 fixture 验证「安装失败、INPUT_INVALID 带 JSON Pointer、不登记修订」（`tests/Qian.Lifecycle.Tests/TenantFieldInstallTests.cs`）。

未成对代理项场景（S1-RT-022）覆盖 JSON 元数据的字符串值与键名、YAML 补丁与 Markdown front matter 的双引号标量，以及与重复键、alias 同时出现时的先后，YamlDotNet 会抛非语法异常的连续高代理项与未闭合流式集合，照常接受的转义代理对。`parse-vectors.json` 另有转义代理对的三种 YAML 写法；直接传入的字符串含字面量的未成对代理项时，两端单测按同一断言覆盖。

解析诊断（S1-RT-025）：`syntax-vectors.json` 由同一脚本生成，74 条向量各给出文件名、内容与期望的唯一诊断。两端直接调用解析器，逐条核对行号与文案，指针为文件根，错误码为 `SURFACE_SYNTAX_INVALID`。覆盖范围：

- YAML：引号标量与流式集合的未闭合和续行缩进不足、制表符、多行隐式键与换行之后的冒号、多文档、指令、禁用字符、重复键与 alias 的先后、空键、过深、显式 tag、BOM、front matter 的行号偏移，以及两库处理不同、两端都拒绝的写法；
- JSON：过深、非有限数值与语法错误的先后，数值之后的分隔符，根值之后多余的内容。

组件场景另覆盖补丁、skill 与元数据里的同类情形。`parse-vectors.json` 补充照常接受的写法：深度 64、显式 tag、十六进制与八进制的舍入、`null` 与 `~` 键、CRLF 与 BOM、块标量结尾换行、块标量正文里的制表符、流式集合结束括号的位置、流式映射的空键、转义换行与 `-` 之后的制表符。约定见 [Qian.Surfaces](../../packages/Qian.Surfaces/README.md#检查顺序与行号约定)。
