# Qian.Events.Abstractions

- 权威设计为 PRD §7.10、ADR-008 与 S1-EVT-001；主文档为 README.md。
- 信封形状以 contracts/v0/event-envelope.schema.json 为准；改字段必须同时改 Schema、fixture 与 Qian.Events.Tests 的往返用例。
- 只依赖 Qian.Runtime，不引用 Kun 传输实现、RabbitMQ 或任何 Provider。
- 事件来源只能由可信执行端（SDK 命令管道、宿主后台）开启，不从请求输入读取 producer 或 actor。
- 公共 API 变动登记 PublicAPI 文件；新增错误码在本包定义，遵守统一 ErrorDetail。
