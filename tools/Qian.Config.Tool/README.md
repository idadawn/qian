# Apollo namespace 骨架工具

由组件 `config.schema` 推导每个宿主的 Apollo namespace 骨架，并可与 ConfigService 上的线上键比对（PRD 7.8、ADR-007 D7.9）。工具只读仓库与 ConfigService，不发布配置，输出只有键名、计数与 Schema 指针，不回显配置值。

## 推导规则

- 用 `ProfileLoader` 装配 `profiles/` 下的 profile（不含 home 层），找出 `qian.config.apollo` 条目：其 `appId` 即宿主 AppId，`namespaces` 即宿主订阅的 namespace。没有 Apollo 条目的 profile（standalone、minimal）不推导。
- global 条目中配置 Schema 有属性的组件，各得一个私有 namespace `qian.plugin.<组件 id>`；配置组件自身（`qian.config`、`qian.config.*`）的条目是引导配置，不进 Apollo。
- 键名约定 `qian.plugin.<组件 id>:<属性路径>`，路径用冒号分层，数组用非负整数下标。Kun 的 Apollo 客户端不转换键名，各 namespace 合并进同一个 `IConfiguration`，带上 namespace 名才不会与其它组件或 `application` 撞键；组件以 `ReadSection("qian.plugin.<组件 id>")` 读取。
- Apollo 的 namespace 名上限 32 个字符、键名上限 128 个字符；超出时给 warning，不参与线上比对。

## 用法

构建 `Qian.slnx` 后：

```bash
dotnet tools/Qian.Config.Tool/bin/Release/net10.0/Qian.Config.Tool.dll . --out <目录>
```

为每个 profile 写出 `<目录>/<profile>/<namespace>.properties`，按 Apollo Portal 文本模式书写（每行「键 = 值」，以第一个 = 分隔，不转义）：只写 Schema 的 `default`（或 `const`），没有默认值的键与敏感键写成注释，敏感键只能填 `secretRef://…`，必填键在文件头列出。仓库 CI 的 `dotnet` 工作流对全部 profile 执行这一步，骨架随测试结果上传。

```bash
dotnet tools/Qian.Config.Tool/bin/Release/net10.0/Qian.Config.Tool.dll . --profile platform \
  --compare http://apollo-config.apollo:8080 [--app-id qian-platform] [--cluster default] [--access-key-env QIAN_APOLLO_ACCESS_KEY]
```

逐个拉取 `GET /configs/{appId}/{cluster}/{namespace}`，按 namespace 骨架判定：

- 缺键：已出现的对象里（根对象总是出现），Schema `required` 的属性没有任何键；宿主已订阅的 namespace 在线上不存在也算缺键。未订阅且不存在的 namespace 不参与比对。
- 多键：不带本 namespace 前缀的键；`additionalProperties: false` 的对象里没有的属性；标量下的子键；对象或数组本身被赋值；数组下标不是非负整数。`patternProperties`、`if/then/else` 与 `dependentSchemas` 让该层视为开放。

AppId 默认取 profile 的 Apollo 条目，`--app-id` 只能与单个启用 Apollo 的 profile 一起用。开启 AccessKey 的 AppId 用 `--access-key-env` 指定存放密钥的环境变量名（不接受命令行明文），签名与运行期 `Qian.Config.Apollo` 相同。

返回 0 表示通过；1 表示有缺键 / 多键或 profile 装配失败；2 表示参数错误、仓库不可读、ConfigService 不可达或拒绝访问。

## 验证

`tests/Qian.Config.Tests/NamespaceSkeletonTests.cs` 以 ApolloFixture 协议替身作线上，覆盖 Schema 变更后的缺键 / 多键、线上漂移、未订阅 namespace、AccessKey 与骨架文件内容；`tests/gates/test_config_namespaces.py` 真实启动本工具与本机协议替身，对仓库 profile 推导骨架并验证缺键 / 多键非零退出、不回显线上值。对 staging Apollo 的比对是部署流水线的发布前步骤，见 [配置运维手册](../../docs/ops/configuration-runbook.md)。
