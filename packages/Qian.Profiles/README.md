# Qian.Profiles

依据 [组件规格 §7](../../docs/prd/specs/component-model.md#7-profile组合包与-patch) 与 [S1-RT-002](../../pm/tasks/S1-RT-002.md)。`Qian.Profiles.Abstractions` 定义装配端口与快照；本包提供本地 profile、组合包及 patch 的静态装配，不依赖平台注册表、生命周期或数据库。

## 装配位置与使用

本目录包含分层装配器、配置 Schema 校验、版本选择与依赖排序。输入表面为 `profiles/<name>/qian.profile.yml`、组件 `.qian-plugin/plugin.json`、组合包及各层 `qian.patch.yml`。所有 profile 均可使用；它是启动装配端口 `IProfileLoader` 的 Provider，不新增业务 seam 或特权服务。`qian add` 的下载、目录市场与包安装由后续 CLI 任务实现，本轮仅消费已在本地的组件内容。

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

config 使用组件 `runtime.configSchema` 或 `schemas/config.schema.json` 校验；非空配置不能缺少 Schema。默认值补充发生在整体替换之后，支持对象属性、数组 items、文档内 JSON Pointer 引用及 allOf，不继承旧配置，也不为互斥分支猜默认值。配置 Schema 不联网取引用，文档内引用有深度限制。`x-qian-sensitive` 在对象、数组、组合分支及本地引用中均要求 secretRef；不会解析密钥。文件错误不回显字段值。

gateway/identity 不允许 command hook，S1 拒绝代码热更新；AOT profile 拒绝 function 或未声明 AOT 的代码清单。这只是装配预检，不能代替组件 NativeAOT 编译验收。

## 结果和边界

`ProfileLoadResult` 返回快照与 `SurfaceDiagnostic`；错误时 Snapshot 为空。诊断外层有文件、行号和级别，内层遵循冻结 ErrorDetail，字段码 `PROFILE_INVALID` 由 Definition 包声明。错误指针定位原 patch 的 `/条目序号/config/字段`；来源层指向最后改写条目的文件。

默认 [profiles](../../profiles/README.md) 的业务叶节点目前均为 `x-skeleton`，会输出 warning；后续组件任务补真实 Provider、完整配置契约与依赖，不能据此宣称平台/网关/身份服务 Ready。minimal 默认清单待 [S1-DEC-012](../../pm/tasks/S1-DEC-012.md) 裁决；共用 TCK 的自建空树已在真实 Runtime 启动，并验证了静态命令装载。

CLI 使用方式见 [qian](../qian-cli/README.md)，双侧场景与全量检查见 [profile TCK](../../tck/profiles/README.md)。
