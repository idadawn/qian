# Qian.Data.Dialect.Pg

- 权威设计为 PRD §7.6、组件模型 §6.1（组件 id 映射）与 S1-DATA-003；主文档为 README.md。
- 运行时入口依赖 Qian.Plugin.Sdk 与 Qian.Data.Abstractions，驱动使用 Npgsql，不引用 Qian.Data 等 Provider 包（依赖门禁）。
- 类型映射是数据：只改 pg-dialect.json，加载时必须经 DataTypeContracts.ReadDialect 核对。
- 探测只用连接串构建器、不池化；错误只给结构化原因，不得把口令或驱动原文写进错误、日志或返回值。
- 组件入口的运行时贡献与宿主目录登记必须使用同一实例并可逆；只支持 global scope，不能以全局 DI 登记伪装租户 scope。独立宿主的 AddQianPgDialect 与组件挂载不能静默去重。
