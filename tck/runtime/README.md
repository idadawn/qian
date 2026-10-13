# 运行时场景格式 v1

权威语义见 [规格 §10.2](../../docs/prd/specs/component-model.md#102-运行时语义-tck)。场景数据不包含 .NET 类型名或代码。`.NET harness` 位于 [RuntimeScenarioTests.cs](../../tests/Qian.Runtime.Tests/RuntimeScenarioTests.cs)，TS 适配器为 `harness-ts.mjs`（由 S1-RT-003 的测试入口传入真实运行时和解析器）。两侧从相同的 YAML 读取断言，不复制另一份预期数据；每个核心场景先解析交付的 minimal profile 与其空组合包。

每个 `*.scenario.yml` 是独立场景；YAML 标量保留 string / number / boolean / null 类型，UTF-8。未知版本或操作必须报错。

| 顶层字段 | 约定 |
|---|---|
| `version` | 数字 `1` |
| `id`、`description` | 稳定编号与中文说明 |
| `events` | 可选数组，每项 `name`、`mode`；mode 为小写五种模式之一，场景内同名事件复用同一 Definition |
| `entries` | 条目数组：`id`，可选 `scope`、`inject[]`、`provides[]`，以及 `apply[]` 动作；缺省 scope 为 `global` |
| `steps` | 顺序执行的操作与预期结果 |

scope 写作 `global` 或 `tenant:id`、`app:id`、`agent-session:id`。服务键按场景原文作不透明字符串处理，包含 `Key:name` 时表示具名注册的就绪键。harness 的条目默认配置为空对象，版本为 `1.0.0`、层级为 0；此子集不模拟 profile/patch。

| Apply 动作 `op` | 字段与语义 |
|---|---|
| `provide` | `key`、`value`（字符串），可选 `name`、`scope`；无 name 时提供键本身 |
| `effect` | `name`，可选 `fail: true`；撤销时记录 `dispose:name`，再按需抛异常 |
| `fail` | 抛出 Apply 异常 |
| `optional` | `key`；断言 TryInject 为空，不声明必需依赖 |
| `on` | `event`、`name`，可选 `scope`、`priority`（默认 0）、`mode`（默认等于 Definition）、`behavior`（return / next / fail）、`value`（字符串或 null） |
| `restrict` | `scope`、`keep[]`；仅保留 Name 属于集合的全局注册 |

每次 Apply 首先记录 `apply:id`。监听器记录 `enter:name`，成功后记录 `exit:name`；waterfall 的 next 行为把下游结果包装为 `name(result)`，链尾结果固定为 `terminal`。其它监听器返回 `value`。失败动作抛普通错误，parallel 的 harness 断言聚合异常和成功结果。

| Step 操作 `op` | 字段与语义 |
|---|---|
| `mount` | `entries[]` 给出进入树的顺序；可选 `error: {code, args}` 断言 Resolve 失败 |
| `unmount` | `entry`、可选 `scope` 标识要卸载的条目 |
| `teardown` | 卸载整棵树 |
| `dump` | `entries[]`；比较运行时真实挂载树中仍存在的条目 id，卸载后不能残留 |
| `cleanupErrors` | `value`；比较已报告的清理异常数量，避免仅断言释放顺序而漏掉被日志吞掉的错误 |
| `failure` | `entry`、`code`、`args`；比较挂载失败的结构化错误与服务冲突原拥有者 |
| `state` | `entry`、可选 `scope`、`value`（Pending / Ready / Failed） |
| `inject` | `entry`、`key`、`value`，可选 `scope`、`view`、`name`；断言解析结果 |
| `missing` | `entry`、`key`，可选 `scope`、`view`；断言可选注入为空 |
| `list` | `entry`、`key`、`value[]`，可选 `scope`、`view`；比较可见 Name 的有序列表 |
| `notFoundEquivalent` | `entry`、`key`、被过滤的 `name`，可选 `scope`、`view`；比较过滤项与不存在项的完整错误体（固定同一 trace-id） |
| `dispatch` | `entry`、`event`，可选 `scope`、`view`；结果 `value`，或 `error: listener`，或 `error: parallel` 与 `partial[]` |
| `trace` | `value[]`；比较整个操作轨迹 |
| `traceContains` | `value[]`；断言并发完成轨迹，不约束线程调度顺序 |

`scope` 用于定位条目；`view` 只改变本次调用上下文，两者不可混淆。所有异步卸载必须等待完成后再执行下一 step。卸载后的注册、监听器与 restriction 不可残留。

核心 YAML 覆盖 T01–T14、T18、T20–T22、T24，附加缺失键、Apply 回滚、集合模式不一致和卸载无残留场景。`missing` 同时检查 .NET keyed service；`notFoundEquivalent` 比较被过滤项与不存在项的完整错误体。T20 同时断言清理异常为零，发现并修复了整树 teardown 时已释放依赖者被再次失效的缺陷。

## 分层装配到真实运行时

`assembly.json` 保存 T15–T17，复用 `tck/profiles/base` 的真实文件，经 `.NET ProfileLoader` / TS `loadProfile` 装配后交给真实运行时；适配器分别为 `RuntimeCompositionTests.cs` 与 `assembly-ts.mjs`。断言整个 config、来源层、最终 Provider 与未改变的 Consumer 结果。T17 在装配期拒绝非法树，检查来源指针与 Apply 零次；不会伪造一个已进入 Runtime 的 Failed 节点。

## 尚未完成的产品验收

S1-RT-003 已合并并完成 `@idadawn/qian-runtime@0.1.0` 私有发布与逻辑别名消费。[主线双侧 CI](https://github.com/idadawn/qian/actions/runs/38146040203) 已持续执行同一组数据：现有 23 个 YAML 场景加 T15–T17 的 3 个装配场景，TS 共 34 项测试、.NET 运行时共 46 项测试通过。

S1-QA-002 仍须完成真实提示词构建/工具执行链路对过滤结果的联调。当前目录与注入断言不能替代产品提示词断言。T19 已增加下节的双侧实际文件监视证据；RT-009 的完整 hooks/生产触发仍需收尾，T23 的完整 platform 宿主等价依赖真实业务 Provider；当前 platform 清单带有骨架组件，不能当作可运行的平台完成证明。

### T19 的实际文件监视场景

[hotreload.json](hotreload.json) 由 Qian.Surfaces 的 .NET 与 TypeScript 测试直接读取。测试启动真实监视器并写入技能文件，在五秒界限内观察 Tools 与 Skills 的内容变化，同时验证代码对象未替换、代码效果未释放；非法更新保留旧内容，src 变化只标记需要重启，卸载后效果恰好释放一次。两侧还校验实际诊断注册的 scope 隔离、共享响应 Schema、重启状态与卸载后的旧引用失效。设置 QIAN_HOT_RELOAD_REPORT 可保存实际断言后的结果，surfaces CI 会精确比较两侧报告。真实 CLI 读取运行态另由 CRM 的签名认证 Kestrel 集成测试及 TS 宿主测试覆盖，包含错误更新恢复。RT-009 的完整 hook 生产方与生产注册表触发、QA-002 的其余验收仍分别跟踪。
