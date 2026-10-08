# 运行时场景格式 v1

权威语义见 [规格 §10.2](../../docs/prd/specs/component-model.md#102-运行时语义-tck)。这里保存 S1-RT-001 验收所需的语言无关数据，不包含 .NET 类型名或代码。`.NET harness` 位于 [RuntimeScenarioTests.cs](../../tests/Qian.Runtime.Tests/RuntimeScenarioTests.cs)，TS harness 由 S1-RT-003 复用，完整套件由 S1-QA-002 补齐。

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
| `state` | `entry`、可选 `scope`、`value`（Pending / Ready / Failed） |
| `inject` | `entry`、`key`、`value`，可选 `scope`、`view`、`name`；断言解析结果 |
| `missing` | `entry`、`key`，可选 `scope`、`view`；断言可选注入为空 |
| `list` | `entry`、`key`、`value[]`，可选 `scope`、`view`；比较可见 Name 的有序列表 |
| `notFoundEquivalent` | `entry`、`key`、被过滤的 `name`，可选 `scope`、`view`；比较过滤项与不存在项的完整错误体（固定同一 trace-id） |
| `dispatch` | `entry`、`event`，可选 `scope`、`view`；结果 `value`，或 `error: listener`，或 `error: parallel` 与 `partial[]` |
| `trace` | `value[]`；比较整个操作轨迹 |
| `traceContains` | `value[]`；断言并发完成轨迹，不约束线程调度顺序 |

`scope` 用于定位条目；`view` 只改变本次调用上下文，两者不可混淆。所有异步卸载必须等待完成后再执行下一 step。卸载后的注册、监听器与 restriction 不可残留。

本任务覆盖 T01–T14、T21、T22、T24，T18 另有重复注册单元测试，T20 由 T04 的整树 teardown 验证；附加缺失键与 Apply 回滚场景。T15–T17、T19、T23 涉及尚未实现的 profile、校验器与声明式装配，在相应任务和完整套件中补充。
