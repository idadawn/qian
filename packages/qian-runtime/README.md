# Qian TypeScript 运行时

依据 [组件模型 §5](../../docs/prd/specs/component-model.md#5-运行时模型) 与 [S1-RT-003](../../pm/tasks/S1-RT-003.md)。公共逻辑导入名为 `@qian/runtime`；私有发布名称待 S1-DEC-014 确认，当前不宣称已经发布。

## 装配与使用

本目录包含上下文、作用域服务、事件、效果栈与装载器。可用于所有 TS profile，主要承载 yue 外壳插件树；这是运行时底座，不新增业务 seam，也不依赖平台三件套。`qian add` 的安装编排由后续 CLI 任务消费本包，当前不执行包安装或远程代码装载。

```ts
import { PluginLoader } from '@qian/runtime';
const runtime = new PluginLoader();
await runtime.mount([{
  entry: { id: 'example', plugin: 'com.example.demo', version: '1.0.0', scope: 'global', layer: 0, config: {} },
  provides: ['message'],
  plugin: { name: 'com.example.demo', apply(ctx) { ctx.provide('message', 'hello'); } }
}]);
console.log(runtime.getContext('example').inject('message'));
await runtime.dispose();
```

`provide`、`on`、`restrict` 返回幂等 disposer，并自动进入本次 Apply 的效果栈；第三方资源用 `effect`。`scoped` 视图共享当前 Apply 的生命周期，scope 之间没有隐含继承。服务名与 Context 方法重名时使用显式 `inject`。必需依赖由 plugin.inject 与 definition.provides 共同建立，缺失/环路在任何 Apply 前拒绝；optionalInject 不阻止 Apply。配置输入克隆并冻结，宿主不得依赖插件修改原配置。

`mount`、`unmount`、`dispose` 串行执行；动态撤销使依赖者立即不可见，`whenIdle` 等待级联释放与恢复。`reload` 用新配置重新挂载，未提供运行时代码替换。`dumpConfig` 返回当前条目快照，卸载条目不残留。

## 固定依赖与语义差异

内部固定 vendor `@cordisjs/core` 3.18.1（MIT），来源 commit、npm integrity 与文件 SHA-256 见 `src/vendor/provenance.json`，构建先核对副本完整性。原许可证随包保留。仅使用其核心，不引入上游 CLI、包加载器、遥测或运行时下载。

上游承担插件 fork/生命周期、作用域资源与服务值存储。Qian 适配层明确处理以下差异：

| 约定 | 适配策略 |
|---|---|
| 异步逆序释放 | 每次 Apply 在上游 fork 挂一个组合 disposer，内部按 LIFO 逐项 await；失败仍继续释放 |
| 原子发布 | Apply 中的注册只对本次上下文可见，成功并满足 provides 后才对其它条目发布 |
| 两层扁平 scope | 服务按显式 scope 选本地项或 global；restriction 只过滤 global，多条取交集 |
| 五种分发 | Qian 按自己的快照、priority 和 scope 调度；emit 隔离异常，parallel 等全部完成后聚合，serial 首错中断 |
| bail 的空值 | 只有 null/undefined 继续；false、0、空字符串都是有效首值 |
| 诊断 | 从冻结错误码目录派生 ErrorDetail；未提供与被过滤统一 NOT_FOUND |

## 模型体验

Definition 定义服务键与事件。`list`、代理属性与显式执行使用同一可见性规则；具名过滤项与不存在项错误体相同。`getPendingDependencies`、`getState`、`getFailure` 面向宿主诊断，不能作为业务接口泄露不可见原因。可配置 `onError` 与 `onPending` 接收隔离的诊断；不默认打印业务异常。

## 验证

```bash
pnpm --filter @qian/runtime build
pnpm --filter @qian/runtime test
pnpm --filter @qian/runtime check:api
pnpm --filter @qian/runtime test:types
pnpm --filter @qian/runtime test:browser
```

TS harness 直接读取 `tck/runtime/*.scenario.yml`，不复制或改写语言独立输入；额外验证异步 Apply、动态撤销、bail false、取消与整树释放。完整 T15–T20/T23 等场景补齐及双侧报告由 S1-QA-002 接入。

## 已知限制与延期项

- npm 私有发布名称待 S1-DEC-014；未完成真实发布前 S1-RT-003 不关闭。
- 只负责宿主内运行时，不实现市场下载、配置 Schema 校验、持久化或远程 Provider 健康探测；由装配器在 mount 前完成。
- 生命周期内不要等待同一装载器的 mount/unmount/whenIdle，避免自等待。
- 调用方须配合取消信号；无法强制终止忽略取消的插件代码。
