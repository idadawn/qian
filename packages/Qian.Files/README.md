# Qian.Files

文件组件 `qian.files` 的 Provider（S1-FILE-001，PRD 7.7）：服务端生成租户前缀存储键，按用途上传策略与租户配额签发短时直传地址，完成时由服务端自算大小与 SHA-256、嗅探真实类型，下载恒为附件。S1 只接 `local` 后端（Kun.Storage.Local），s3 / oss 由 S1-FILE-002 作为后端登记。

## 宿主装配

`FilesPlugin`（`[QianPlugin("qian.files")]`，可挂 global 或 tenant scope，`Inject` 为 `Data`，提供 `Files` 与具名贡献 `Files.Backends:local`）在 `Apply` 里核对下面三项，缺任何一项都拒绝挂载：

```csharp
builder.Services.AddKunEventBus(registrar => registrar.RegisterFilesEvents() /* 以及其他组件的事件 */);
builder.Services.AddQianData<FilesDbContext>((_, options) => options.UseNpgsql(business).UseKunSchema(FilesDbContext.Schema));
builder.Services.AddKunMediator(registry => registry.AddQianFiles(sp => /* 按请求租户收窄的 qian.files 上下文 */, options =>
{
    options.LocalSigningKey = signingKey; // 多实例共用同一把 ≥32 字节密钥；不给则每次挂载随机生成
}));
```

能力经 SDK 命令管道执行，宿主还须与参考宿主（`samples/Qian.Sample.Crm`）一样装好基础组合：Tenancy / Authz / Audit 三个 Provider 提供命令策略，DI 里登记 `IPermissionChecker`（真实 RBAC，Kun 的 `PermissionBehavior` 读它），`ICurrentUser` 只取已验证令牌。缺任一命令策略时每个能力都按 `AUTHZ_DENIED` 拒绝，不会降级放行。HTTP 端点见 [Qian.Files.AspNetCore](../Qian.Files.AspNetCore/README.md)。

元数据表随包提供 EF 迁移（schema `plugin_qian_files`：`file_objects` 与 `outbox_messages`），由迁移任务（`--migrate-only` / `IDataMigrator`）以属主角色执行；业务连接只需该 schema 的 DML 权限，`file_objects` 按 `tenant_id` 启用 RLS。

## 组件配置

与 [plugins/qian.files/schemas/config.schema.json](../../plugins/qian.files/schemas/config.schema.json) 一致，未知字段一律 `INPUT_INVALID` 并指向该字段：

| 成员 | 说明 |
| --- | --- |
| `storage.kind` | 必填，按名称选用 `Files.Backends` 里的后端；S1 只登记 `local` |
| `storage.root` | local 必填：对象根目录（绝对路径） |
| `storage.endpoint` | local 必填：宿主映射直传 / 直下端点的公开基址（绝对 http(s) 地址，路径与 `MapQianFiles` 的 `TransferPath` 一致，缺省 `/files/blobs`） |
| `quota.perTenantBytes` | 租户字节配额的平台默认值，缺省 10 GiB |
| `policies[]` | 平台默认上传策略：`purpose`、`allowedTypes`、`maxSize`、可选 `maxCount`；与组件 `metadata/upload-policies.json` 的声明同用途时以这里为准 |

宿主选项 `QianFilesOptions` 只有时钟与 local 签名密钥；没有任何配置项或选项能改变下载处置方式。

## 上传与下载

1. **申请**（`presign-upload`）：校验用途、文件名（单段、无控制字符）、大小、声明类型与 SHA-256；同一幂等键、同一参数重发返回同一文件并重新签发地址，参数不同返回 `CONFLICT`。通过后插入 `uploading` 行（按声明大小计入配额），签发不超过 15 分钟的直传地址。
2. **直传**：客户端按回执的方法、地址与必需请求头（`Content-Type` 与 `X-Kun-Max-Bytes`）PUT 到上传暂存键（与正式键同目录、以 `.upload` 结尾）。签名（HMAC-SHA256）覆盖方法、对象键、过期时间、类型与大小上限；签名不符、过期、类型头不符都与不存在同为 404，超过声明大小 413，同一地址写第二次 409。
3. **完成**（`complete-upload`）：读暂存对象头部 4 KiB 嗅探真实类型（魔数与活跃 Web 标记，宁可判严），类型不在策略内即隔离；否则边算 SHA-256 边复制到按真实类型取扩展名的正式键，大小或哈希不符则删除正式对象并隔离（暂存对象留作证据）。相符即 `available`，写入 `files.object.created.v1`，提交后删除暂存对象。重复完成按状态返回同一结果；隔离的文件以 `FILE_CHECKSUM_MISMATCH` / `FILE_TYPE_NOT_ALLOWED` 告知调用方。
4. **下载**（`download-url`）：只对 `available` 文件签发不超过 5 分钟的地址，每次签发记审计。地址恒带 `Content-Disposition: attachment`，签名覆盖处置参数；直下端点另加 `X-Content-Type-Options: nosniff` 与 `Content-Security-Policy: sandbox`。

存储键由服务端生成：`tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}`，`plugin_id` 取用途的组件 ID 段，客户端不能指定任何一段。

## 配额

上限先取租户配置覆盖（`ITenantSettings` 的 `qian.files` / `quota.perTenantBytes`，由 S1-TEN-004 的租户配置写入 Registry 合成配置），宿主没有 Registry 或 Registry 里没有 qian.files 条目时回退组件配置。在用量按声明大小统计：`available`、`quarantined`，以及上传地址到期后 10 分钟完成时限内的 `uploading` 预占；软删立即释放。隔离文件计入配额，因为它的暂存字节作为证据仍占存储，删除后释放。配额与 `maxCount` 的判定和插入在同一个可串行化事务里，并发冲突整笔按同一幂等键重发（最多 5 次，用尽返回 `CONFLICT`）。

## 审计与事件

申请、完成、删除与下载地址签发都由命令管道在成功提交时写审计链（能力 ID、主体、输入哈希、通道），失败的调用不记能力审计；HTTP PEP 的拒绝由授权组件记决策审计。事件只在文件真正可用（完成）与可用文件被软删时发出，隔离或未完成的文件不发事件。

## 依赖边界

依赖 `Qian.Files.Abstractions`、`Qian.Data.Abstractions`、`Qian.Tenancy.Abstractions`、`Qian.Plugin.Sdk` 与 Kun.Storage.Local；不引用其他 Provider（Authz、Audit、Tenancy 的实现由宿主组合）。Kun 旧存储线（`Kun.Storage.Abstractions.IFileStorage`）由仓库根 `BannedSymbols.txt` 禁用。

## 验证

`tests/Qian.Files.Tests`：真实宿主（S2S 令牌、生产模式租户解析与 HTTP PEP、Tenancy / Authz / Audit / Events 组件、包内迁移由受限属主角色执行、业务连接为 NOSUPERUSER / NOBYPASSRLS 登录）上逐条验收租户隔离与存储键前缀、HTML 附件下载、10 MiB 配额与软删释放、直传过期与哈希不符隔离、审计与事件；另有直传网关篡改、嗅探隔离、幂等与策略校验、配置读取、配额来源顺序、清单与代码一致等用例。

## 模型体验

能力描述、输入输出 Schema 与副作用由 `[Capability]` 派生到 `plugins/qian.files/tools/*.json`，模型经通用执行入口调用时与 HTTP 走同一管道和同一错误码。直传与下载地址是短时签名 URL，模型和浏览器都拿不到存储凭据；删除须确认，下载地址签发留审计。

## 已知限制与延期项

- 只有 `local` 后端；local 是单节点磁盘，多实例需共享存储目录并配置同一 `LocalSigningKey`。s3 / oss 与 patch 切换归 S1-FILE-002。
- 不支持分片上传、秒传；暂存残留、软删对象与隔离证据的保留期物理清理归 S1-FILE-003。完成上传在能力事务内同步复制整个文件，单文件大小应由用途策略的 `maxSize` 控制在合理范围，大文件走分片后再改。
- 不做病毒扫描与预览：`scanStatus` 恒为 `unscanned`，预览归 S2。
- 软删后的文件对全局查询过滤器不可见，重复删除返回 404 而不是原回执（QIAN0004 不允许绕过过滤器），不会重复产生副作用。
- 清单仍带 `x-skeleton`：按 S1-DEC-018 口径，锁步发布并经 `qian add data files` 从私有源真实安装后去标（S1-FILE-004）。基础组合包（`qian.bundle.base`、`qian.bundle.identity-server`）的 files 补丁已带 `storage.endpoint`（示例域名，部署时按实际公开基址覆盖）；`runtime.nuget` 的版本与摘要在发布后人工同步，发布链自动给出待同步清单归 S1-RT-034。
- 租户配额覆盖依赖 Registry 里有 qian.files 条目，清单去骨架前无法经 S1-TEN-004 写入；集成测试用组件配置的 10 MiB 上限验收，覆盖优先级由单元测试锁定，经 TEN-004 写入的集成测试随 S1-FILE-004 去标后补。
- 组件声明的上传策略（`metadata/upload-policies.json`）只在平台宿主经 Registry 预装快照汇总，同组件预装多个版本时取最高版本的声明；不按租户实际挂载的组件过滤。声明不合法时，Registry 已就绪则挂载失败，否则在第一次用到策略时报 `INPUT_INVALID`（指针形如 `acme.crm/metadata/upload-policies.json#/0/purpose`）。
- 独立模式（不装 Tenancy / Authz / Audit）没有命令策略，能力一律拒绝；最小独立组合与样例归 S1-QA-005。
