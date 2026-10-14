# 声明式表面共同验收

`cases.json` 由 `scripts/generate_surface_fixtures.py` 生成，包含 58 个独立组件场景（46 个反例）。fixture 保存完整文件内容与宿主目录选项，两端都写入临时组件目录后运行真实文件解析器。`jcs-vectors.json` 另提供独立文字规范值，覆盖浮点、负零、最小非零浮点、Unicode 和 UTF-16 属性顺序。

```bash
python3 scripts/generate_surface_fixtures.py
pnpm --filter @qian/surfaces build
QIAN_SURFACE_REPORT=/tmp/surfaces-ts.json pnpm --filter @qian/surfaces test
QIAN_SURFACE_REPORT=/tmp/surfaces-dotnet.json dotnet run --project tests/Qian.Surfaces.Tests -c Release
node packages/qian-surfaces/scripts/compare-fixtures.mjs /tmp/surfaces-dotnet.json /tmp/surfaces-ts.json
```

对比有效性、错误码、文件、JSON Pointer、行号与 error/warning 级别；文案和每次生成的 trace-id 不参与对比。另以冻结 error-detail Schema 验证 .NET 错误体，并验证技能目录的租户隔离和 Effect 卸载。

元数据场景覆盖默认、自定义及根目录、目录类型、JSON 语法、重复键、敏感值和符号链接；原始快照与诊断均参加两侧比较，业务 Schema 由相应 seam 的验收负责。

未成对代理项场景（S1-RT-022）覆盖 JSON 元数据的字符串值与键名、YAML 补丁与 Markdown front matter 的双引号标量，以及与重复键、alias 同时出现时的先后，YamlDotNet 会抛非语法异常的连续高代理项与未闭合流式集合，照常接受的转义代理对。`parse-vectors.json` 另有转义代理对的三种 YAML 写法；直接传入的字符串含字面量的未成对代理项时，两端单测按同一断言覆盖。
