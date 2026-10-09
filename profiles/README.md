# profile 装配基线

交付 platform、gateway、identity、standalone、minimal 五份静态配置及对应组合包。按 [S1-DEC-012](../pm/tasks/S1-DEC-012.md) 的确认结论，minimal 只包含 Runtime 与声明式表面装载器，初始 patch 为空，不默认加载业务组件。

其它 profile 的业务叶节点均显式标记 `x-skeleton: true`，可解析、可校验、可 dump；这不代表对应业务 Provider、平台或身份服务器已可启动。minimal 无业务骨架警告；测试从实际交付文件启动真实 Runtime，并通过 overlay 挂载无代码表面。
