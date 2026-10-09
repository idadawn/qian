# 声明式表面开发约定

- 结构真相源是 `schemas/v2`；语义按组件模型规格 §4、§10、§11，不执行任何输入内容。
- .NET 与 TS 改动必须同时更新共同 fixture，并验证错误码、指针、行号和级别一致。
- 不把行号加进冻结 ErrorDetail；不改写 v0 契约以迁就实现。
- 路径必须留在组件内；不跟随符号链接，不回显敏感值。
- 公共 API 通过 PublicAPI / API Extractor 登记；完整说明只维护 Provider README。
