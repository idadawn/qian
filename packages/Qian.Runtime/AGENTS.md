# Qian.Runtime 开发约定

- 行为以根仓库组件模型规格 §5 为准；改公共行为同时更新 `tck/runtime/` 与 .NET harness。
- 只依赖 Kun.Core、Kun.Mediator、.NET DI 与 Qian.Runtime.Errors；Kun.Validation 的桥接在 Errors.AspNetCore。
- 服务、监听器和 restriction 都归属某次 Apply 的效果栈；Scoped 视图共享该栈，不能创建独立资源生命周期。
- 生命周期操作由装载器串行化；不要在 Apply 或 effect 内等待该装载器的 Mount / Unmount / WhenIdle。
- 不加入业务状态、数据库、租户层级或运行时代码热装卸；清单与 patch 解析由对应组件负责。
- 公共 API 写中文 XML 注释并登记 PublicAPI 文件。
