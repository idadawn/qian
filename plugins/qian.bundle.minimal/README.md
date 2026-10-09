# qian.bundle.minimal

按 [S1-DEC-012](../../pm/tasks/S1-DEC-012.md) 确认的口径装入 `minimal` profile：宿主只需 `Qian.Runtime` 与声明式表面装载器，组合包的 patch 为空，不默认挂载业务组件，也不引入平台注册表三件套。

本目录包含清单与 patch 表面，是无代码的组合包；不新增业务 seam 或 Provider。它可被现有本地装配器加载，后续 `qian add` 安装流程可复用同一目录。包尚未发布。测试会从随仓库交付的 profile 和此组合包启动真实 Runtime，再通过 overlay 挂载无代码命令表面。
