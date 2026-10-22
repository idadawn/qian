# Qian.Files

- 存储键只由服务端生成（`FileKeys`）：`tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}`，任何一段都不能来自请求；改格式要同步验收用例与 README。
- 下载恒为附件：不要给配置、宿主选项或端点加处置开关；local 网关的签名必须覆盖处置参数，验收 2 的用例会检查 Schema、配置读取器与选项类型里没有这类字段。
- 完成上传只信服务端读到的字节：类型以嗅探为准、大小与 SHA-256 自算；不符一律隔离，暂存对象留作证据。放宽嗅探或哈希校验要配篡改用例。
- 所有业务读写都经能力（`[Capability]` + SDK 命令管道），集成事件只在写能力 handler 里 `Outbox.Enqueue`（QIAN0006）；不要绕过管道直接写 `FilesDbContext`，也不要用 `IgnoreQueryFilters`（QIAN0004）。
- 改 `FilesDbContext` 模型要生成迁移（迁移类改为 `internal`）并确认 `dotnet ef migrations has-pending-model-changes` 干净；RLS 由 Qian 迁移生成器按 `IMultiTenant` 生成，测试以受限角色核对。
- `[Capability]` 的描述、副作用与权限点改动后跑 `Qian.Plugin.Tool sync` 刷新 `plugins/qian.files/tools`，再跑 `check` 确认无漂移；`x-skeleton` 按 S1-DEC-018 口径保留，不在本包任务里移除。
