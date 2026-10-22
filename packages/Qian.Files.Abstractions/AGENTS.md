# Qian.Files.Abstractions

- 本包是 `qian.files` 的 Definition：只放服务键、门面与注册表接口、契约记录、错误码与事件；不引用 Provider、Kun 存储包或任何 SDK 实现，依赖图门禁把关。
- 能力 ID 按 R23 写成 `qian.files/<名称>`，权限点为 `qian.files:file:<动作>`；改名要同步 Provider 的 `[Capability]`、`plugins/qian.files/tools`（`Qian.Plugin.Tool sync` 生成）、`metadata/permissions.json` 与 README。
- 契约记录一旦发布只增不改：新增字段用可选 `init` 属性，`FilesContractJson` 保持拒绝未知字段与遵守必填 / 可空标注。
- 业务错误只用 409 / 422，消息参数与 `contracts/v0/error-codes.json` 的文本一致（`QUOTA_EXCEEDED` 的 `{remaining}`）；新增错误码在 `FilesErrors.All` 登记。
- 公共 API 变动登记 PublicAPI 文件；README 的「已知限制与延期项」要与实际交付一致，未实现的不写成已实现。
