# Qian.Files

- 存储键只由服务端生成（`FileKeys`）：`tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}`，任何一段都不能来自请求；改格式要同步验收用例与 README。
- 下载恒为附件：不要给配置、宿主选项或端点加处置开关；local 网关的签名必须覆盖处置参数，验收 2 的用例会检查 Schema、配置读取器与选项类型里没有这类字段。
- 完成上传只信服务端读到的字节：类型以嗅探为准、大小与 SHA-256 自算；不符一律隔离，暂存对象留作证据。放宽嗅探或哈希校验要配篡改用例。
- 所有业务读写都经能力（`[Capability]` + SDK 命令管道），集成事件只在写能力 handler 里 `Outbox.Enqueue`（QIAN0006）；不要绕过管道直接写 `FilesDbContext`，也不要用 `IgnoreQueryFilters`（QIAN0004）。
- 改 `FilesDbContext` 模型要生成迁移（迁移类改为 `internal`）并确认 `dotnet ef migrations has-pending-model-changes` 干净；RLS 由 Qian 迁移生成器按 `IMultiTenant` 生成，测试以受限角色核对。
- Apply 里形如 `Register("名称", …)` 的注册表调用会被生成器当作具名贡献，须有匹配的 `[Contributes]`（`RegistryType` 指向真实注册表）；上传策略是数据，用单参数 `Register(policy)` 登记，不进 `provides`。local 后端除了登记进注册表，还要 `ctx.Provide` 同名具名服务键：装载器在 Apply 后核对清单 provides 都已提供，测试宿主按真实清单的 provides 挂载。
- 组件声明的上传策略经 `DeclaredUploadPolicies` 从 Registry 预装快照汇总；Registry 不进 `Inject`（独立模式与 `qian add` 没有它），只用 `TryInject`。改声明结构要同步 `schemas/v2/files-upload-policies.schema.json` 与 `FilesDeclaredPolicyTests`，用途归属本组件的检查不能放宽。
- Provider 要能同时挂在 global 与租户 scope：经 S1-TEN-004 写入租户配置后，Registry 在该租户 scope 另挂一个实例并接管该租户的请求。Apply 里不要做进程级的独占登记；测试宿主的平台模式（`FilesPlatform`：真实 Registry、运营端点与平台超管）覆盖这条路径。
- `[Capability]` 的描述、副作用与权限点改动后跑 `Qian.Plugin.Tool sync` 刷新 `plugins/qian.files/tools`，再跑 `python3 scripts/check_plugin_manifests.py` 确认无漂移；清单已去骨架（S1-FILE-004）：`runtime.nuget` 的版本与摘要只在包源上确有该版本后同步，不要钉未发布的版本。
