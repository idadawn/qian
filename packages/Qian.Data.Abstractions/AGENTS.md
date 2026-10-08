# Qian.Data.Abstractions

- 本包拥有数据上下文基类与错误契约；保持业务组件只依赖 Definition 的方向。
- 修改隔离检查必须同步 `tests/Qian.Data.Tests`，禁止为了通过测试关闭校验。
- `QianDbContext` 的启动安全范围与完整数据功能边界见本目录 README；未交付项不得写成已实现。
- 公共 API 变动登记 PublicAPI 文件；新增错误码在本包定义，遵守统一 ErrorDetail。
