# Qian.Data.Dialect.Pg

- 权威设计为 PRD §7.6、组件模型 §6.1（组件 id 映射）与 S1-DATA-003；主文档为 README.md。
- 只依赖 Qian.Data.Abstractions 与 Npgsql，不引用 Qian.Data 等 Provider 包（依赖门禁）。
- 类型映射是数据：只改 pg-dialect.json，加载时必须经 DataTypeContracts.ReadDialect 核对。
- 探测只用连接串构建器、不池化；错误只给结构化原因，不得把口令或驱动原文写进错误、日志或返回值。
