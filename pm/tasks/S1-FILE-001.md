---
id: S1-FILE-001
title: Qian.Files 核心：租户前缀键、配额、上传策略与预签名 API
status: review
stage: S1
area: FILE
wave: 2
priority: P0
type: feature
owner: "claude"
estimate: 5d
depends: [S1-RT-001, S1-TEN-004, S1-AZ-004, S1-AUD-001, S1-EVT-001, S1-KUN-006, S1-KUN-009]
refs:
  - docs/prd/07-s1-foundation.md#7.7
  - docs/prd/specs/component-model.md#6.2
  - docs/adr/ADR-019-library-first-packages.md#2
updated: 2026-10-22
---

## 背景

组件 `qian.files`（`Qian.Files.Abstractions` / `ctx.Files`）基于 Kun 新线 `IFileStorage`：`IQianFileService`、`file_objects` 表、存储键 `tenants/{tenant_id}/{plugin_id}/{yyyy}/{MM}/{uuid}{ext}` 服务端生成、配额读 7.3（`QUOTA_EXCEEDED`）、`UploadPolicy` 由清单 `contributes: qian.files.upload-policy/v1` 声明、MIME 服务端嗅探、`ContentDispositionPolicy = attachment` 不可关闭；`POST /api/files/uploads:presign`（TTL ≤ 15 分钟）、`POST /files/{id}:complete`、`GET /files/{id}/download-url`（TTL ≤ 5 分钟、经 7.5）、`DELETE`（软删）；事件 `files.object.created/deleted.v1`；下载审计；旧线以 Banned API 禁用。退出标准点名能力。

## 验收

- [x] IT：租户 A 的下载 URL 对 B 的文件 id 返回 404；任何文件的 `storage_key` 都以 `tenants/{tenant_id}/` 开头
- [x] IT：上传 `text/html` 文件后下载响应头为 `Content-Disposition: attachment`，且无配置项可改变
- [x] IT：配额 10 MB 时第 11 MB 上传返回 `QUOTA_EXCEEDED`；软删后配额释放
- [x] IT：预签名 URL 过期后 PUT 失败；`complete` 时 sha256 不符返回错误并标记 `quarantined`
- [ ] IT：`files.presign-upload` 调用产生审计与 `files.object.created.v1` 事件；`files.delete` 标 `destructive` + `confirm`

## 进度日志

- 2026-10-06 planner：创建任务
- 2026-10-22 claude：开始
- 2026-10-22 claude：先核实结论：① Kun.Storage.Local v2026.10.14 的预签名上传地址为 `{PublicBaseUrl}/{key}?expires&contentType`、大小上限只在 `X-Kun-Max-Bytes` 请求头，下载地址带 `disposition`，两者都不带签名；`UploadAsync` 流式限长，超限删除半截文件并抛 InvalidOperationException。② `RegistryComponentBinding` 拒绝 x-skeleton 清单，清单去骨架前 S1-TEN-004 的租户配置写不进 qian.files 条目（ITenantSettings 无条目 NOT_FOUND、无 Registry ServiceNotReady）。③ SDK 命令管道里 Kun 的 PermissionBehavior 读宿主 DI 的 `IPermissionChecker`，缺失即 AUTHZ_DENIED 且不写决策审计；参考宿主登记 RbacPermissionChecker。④ QIAN0006 只允许写能力 handler 入 Outbox，QIAN0004 禁用 IgnoreQueryFilters。⑤ 基础组合包（qian.bundle.base / identity-server）的 files 补丁只有 kind、root 与配额，没有 endpoint。⑥ 旧线 `Kun.Storage.Abstractions.IFileStorage` 已由仓库根 BannedSymbols.txt 对全部代码树禁用（Directory.Build.targets），新包自动受约束
- 2026-10-22 claude：路线：按备忘录 §7.2 缩减版只做 local。`IFileService` 是能力门面：六个能力（R23：`qian.files/presign-upload`、`complete-upload`、`get`、`download-url`、`delete`、`quota`）全部经 SDK 命令管道，租户校验、授权、业务写入、Outbox 与审计在同一个可串行化事务；配额与幂等判定的并发冲突按同一幂等键整笔重发（最多 5 次，用尽 CONFLICT）。直传写上传暂存键（与正式键同目录、`.upload` 结尾），local 网关的 HMAC-SHA256 签名覆盖方法、键、全部查询字段与大小头，同一地址只写一次；完成时读头部 4 KiB 嗅探真实类型，边算 SHA-256 边复制到按真实类型取扩展名的正式键，不符即删正式对象并隔离、暂存留证。下载地址恒为附件且签名覆盖处置参数，直下另加 nosniff 与 CSP sandbox。PEP 由宿主经 `MapQianFiles` 的 requirePermission 回调接入（本包不依赖 Qian.Authz.AspNetCore），构建期核对权限元数据与授权要求都在。上传策略来自组件配置 `policies[]` 与 `IFileService.Policies.Register`；后端注册表登记 local，s3 / oss 留给 S1-FILE-002
- 2026-10-22 claude：验收 1 证据：tests/Qian.Files.Tests/FilesAcceptanceTests.cs `验收1_租户A对B的文件ID一律404_…` 在真实宿主（RS256 S2S 令牌、Kun 多租户生产模式、Tenancy / Authz（AddQianAuthzEnforcement 生产模式 + RequireQianPermission 回调）/ Audit / Events 组件，files 元数据经 IDataMigrator 由受限属主角色迁移，业务连接为 NOSUPERUSER / NOBYPASSRLS 登录）上：A 对 B 文件的 download-url、GET、complete、DELETE 均 404 ErrorDetail，B 自己 200；数据库行的 storage_key 与 upload_key 都以 `tenants/{tenant_id}/` 开头，A 的键为 `tenants/{A}/acme.crm/{yyyy}/{MM}/{id}.txt`，存储根下的全部磁盘文件都在两个租户目录内；受限登录直连按会话租户只见本租户行、未设租户 0 行；B 里只有 tenant.user 的同一主体 DELETE 403、未绑定角色的主体 presign 403，两条拒绝都进决策审计（result denied、permission 分别为 delete / write）；六个 /api/files 端点各带 PluginEndpointBinding、RequiresPermission 与授权要求，两个直传端点为 AllowAnonymous + PermissionEnforcedInHandler
- 2026-10-22 claude：验收 2 证据：`验收2_上传text_html后下载恒为附件…`：上传含 script 的 HTML（声明 text/html，策略允许）→ available、键扩展名 .html；下载地址不超过 5 分钟，直下响应 `Content-Disposition: attachment; filename=page.html`、`X-Content-Type-Options: nosniff`、`Content-Security-Policy: sandbox`、内容原样；把 disposition 改为 inline 或删掉均 404。无配置可改：config.schema.json 根、storage、quota、policies 项都 `additionalProperties: false` 且全部属性名不含 disposition / inline / attachment / forceDownload；配置读取器对顶层 `disposition`、`quota.inline` 报 INPUT_INVALID（指针到字段），local 后端对 `storage.forceDownload` 报 INPUT_INVALID `#/storage/forceDownload`；FilesSettings、QianFilesOptions、FilesEndpointOptions 无对应属性
- 2026-10-22 claude：验收 3 证据：`验收3_10MiB配额下第11个MiB返回QUOTA_EXCEEDED_…`：组件配置 `quota.perTenantBytes` = 10 MiB，A 完成 10 个 1 MiB 文件后 quota 为（10 MiB，10 MiB，10）；第 11 个 MiB 与 1 字节的申请都 422 `QUOTA_EXCEEDED`、pointer `#/size`、message「文件大小超出租户配额（剩余 0 字节）。」；B 同时可申请 1 MiB；软删一个文件 200（status deleted、deletedAt 有值）后 quota 为（10 MiB，9 MiB，9），再删 404、download-url 404，可再申请 1 MiB，之后再满。上限来源顺序（租户覆盖优先、Registry 缺席或无条目回退组件配置、其他错误照常抛出）由 FilesUnitTests `配额上限_租户配置覆盖优先_…` 用替身 ITenantSettings 锁定，见建议裁决 2
- 2026-10-22 claude：验收 4 证据：`验收4_直传地址过期后PUT失败_…`：presign 回执过期时间在 14–15 分钟之间，预占 15 字节计入配额；手动时钟前进 15 分 1 秒后 PUT 404、暂存键无文件；完成时限内 complete 409 `FILE_UPLOAD_INCOMPLETE`，再前进 10 分钟 409 `FILE_UPLOAD_EXPIRED`，预占不再计入配额。哈希不符：申请 sha256(declared) 后 PUT 等长的另一段内容 201，complete 两次都 422 `FILE_CHECKSUM_MISMATCH`；GET 为 quarantined / checksum_mismatch，正式对象不存在、暂存对象内容即上传字节；download-url 409 `FILE_NOT_AVAILABLE`（message 含 quarantined）；隔离文件按声明大小计入配额、删除后释放；Outbox 无事件
- 2026-10-22 claude：验收 5 证据（未勾选，见建议裁决 1）：`验收5_申请直传写审计_…`：presign 后能力审计恰一条（qian.files/presign-upload、service、主体、success、api、租户 A）；complete 后 Outbox 一条 `files.object.created.v1` v1 信封（type、tenant_id A、producer qian.files、actor service / 主体，payload 为 fileId、pluginId acme.crm、purpose、size 15、contentType text/plain、sha256）；download-url 与 delete 各记审计，能力审计顺序为 presign-upload、complete-upload、download-url、delete，事件为 created、deleted。契约：六个能力 ID 按 R23；delete 的生成元数据与 plugins/qian.files/tools/delete.json 均为 sideEffect destructive、policyHint confirm、agent.requiresConfirmation true、audit true、requiredPermission qian.files:file:delete；download-url 为 read 且 audit true。宿主装 AddQianCapabilities，六个契约在挂载时通过登记期检查
- 2026-10-22 claude：其余用例：`直传网关_…`（伪造签名、改类型头、缺大小头 404，超出声明大小 413 后可重传，同地址二次写 409，下载地址改指暂存键 404，下载地址过期 404）；`完成时按服务端嗅探的类型判定_…`（声明 image/png 的 HTML 在只收 PNG 的用途下 422 `FILE_TYPE_NOT_ALLOWED` 并隔离为 type_not_allowed / text/html；声明 text/plain 的 PNG 入库为 image/png、键扩展名 .png）；`申请按策略判定_…`（未知用途 INPUT_INVALID，errors[0] 为 VALUE_NOT_ALLOWED `#/purpose` 并列出已登记用途；超 maxSize `FILE_TOO_LARGE`；类型不允许带 allowed；非法 sha256 INPUT_INVALID；非 JSON 415；同键同参返回同一文件、同键改参 409、完成后同键 409；maxCount 满 `FILE_COUNT_EXCEEDED`）；FilesUnitTests 的嗅探与配置读取、注册表冲突（REGISTRATION_CONFLICT 的 key / owner）、local 网关的越界键与过期；FilesComponentManifestTests 核对清单代码字段、六个 tools 文件与生成契约逐项一致、x-skeleton 仍在、runtime.nuget 为不带摘要的对象，以及权限 / 角色声明能编译为目录
- 2026-10-22 claude：偏离：① 门面名 `IFileService`（与 IAuditService、IAuthzService 一致），不是背景里的 `IQianFileService`。② 在 PRD 点名的 presign-upload / download-url / delete 之外另有 complete-upload、get、quota 三个能力，HTTP 也多 `GET /api/files/{id}` 与 `GET /api/files/quota`，使每个 HTTP 操作都是能力、Agent 与 PEP 同一口径；路由均在 `/api/files` 下。③ 错误码除 contracts/v0/README.md 示例里的 QUOTA_EXCEEDED（owner qian.files、422、标题与消息逐字一致）外，另增 7 个文件域业务码（全部 409 / 422，见 Qian.Files.Abstractions README）。④ complete 与 delete 按状态幂等，接受可选 `Idempotency-Key` 头；软删后重复删除返回 404（QIAN0004 不允许绕过过滤器取回原行）。⑤ 上传策略未从清单 `contributes: qian.files.upload-policy/v1` 读取（由 Registry 汇总已安装组件策略的方式尚未接，见建议裁决 7）；同步 main 合入 #207（S1-RT-033）后，local 后端改由入口上的 `[Contributes]` 声明，`provides` 派生出 `Files.Backends:local`，上传策略作为数据改用单参数 `IUploadPolicyRegistry.Register(policy)`，不进 `provides`（用途名含 `/` 与 `.`，也不合具名贡献名的格式）。⑥ 清单漂移由 #207 引入的 `scripts/check_plugin_manifests.py`（CI 动态发现全部代码入口）覆盖，本机输出「qian.files：代码与唯一清单一致」；FilesComponentManifestTests 另在进程内核对 tools 与生成契约。⑦ local 签名密钥由宿主选项 `QianFilesOptions.LocalSigningKey` 提供，未给时每次挂载随机生成（单实例开发可用，多实例须配置同一密钥）
- 2026-10-22 claude：建议裁决（交复核人）：1）验收 5 的事件时机：`files.object.created.v1` 在完成上传、文件真正存在且通过大小 / 哈希 / 类型校验时发出，presign 只建 uploading 行并记审计；若在 presign 发 created，消费方会收到可能永远不会上传的对象，还需要另一个事件表示可用。建议按完成时发出确认后勾选验收 5。2）验收 3 的上限来源：任务书把「上限来自 S1-TEN-004 写入的 ITenantSettings」列为条件，但 Registry 拒绝 x-skeleton 清单，去骨架前写不进 qian.files 条目；集成测试按组件配置 10 MiB 验收，覆盖优先级由单元测试锁定，经 TEN-004 写入的 IT 建议随 qian.files 去骨架后补（见 6）。3）隔离文件计入配额（暂存证据仍占存储），删除后释放，README 已写明。4）PRD「配额（字节数、文件数）」本任务只做租户字节配额与用途级 maxCount，租户级文件数上限未做，建议并入 S1-FILE-003 或另立任务。5）门面命名按偏离 ①，请确认。6）qian.files 去骨架无任务承接：#207 只给已通过 `qian add` 的组件去标；按 S1-DEC-018 口径需先发布 Qian.Files 包并经 `qian add files` 真实安装（PRD 7.7 的独立模式 E2E），建议另立任务，届时补基础组合包的 `storage.endpoint` 与 TEN-004 写入路径的 IT。7）上传策略的清单声明（`contributes: qian.files.upload-policy/v1`）需要 Registry 汇总已安装组件的策略，建议并入 6 的任务或 S1-FILE-003
- 2026-10-22 claude：回链：去骨架（建议裁决 6：qian.bundle.base 与 qian.bundle.identity-server 的 files 补丁须补 `storage.endpoint`，否则 local 挂载报 INPUT_INVALID `#/storage/endpoint`；TEN-004 写入路径的 IT）；S1-FILE-002（s3 / oss 与 patch 切换）；S1-FILE-003（分片、秒传、暂存残留 / 软删对象 / 隔离证据的保留期物理清理）；S1-QA-005（独立模式没有命令策略时能力一律拒绝，最小独立组合）；S2（预览、病毒扫描，scanStatus 恒为 unscanned）
- 2026-10-22 claude：本机门禁（二次同步 main 833f754226、含 #207 S1-RT-033 后的最终树）：dotnet build Qian.slnx -c Release 0 警告 0 错误；check_deps --no-restore 47 包通过（含 Qian.Files 三包）；tests/gates 94 项通过；`scripts/check_plugin_manifests.py` 16 个组件代码与清单一致（含 qian.files）；`dotnet ef migrations has-pending-model-changes`（FilesDbContext）无待生成变更；按合并后的 main 重跑 `qian docs generate`，module-graph / seam-graph 的合并冲突以重生成结果为准并已提交，docs/generated 无差异；check_docs 308 个文件、pm validate 198 个任务、pm build 通过；qian-cli profiles.test.mjs 3/3（骨架计数未变）。全量 dotnet test（QIAN_REQUIRE_DOCKER=1，带 QIAN_OASDIFF_PATH / QIAN_BUF_PATH，--max-parallel-test-modules 4）1245/1245 通过，其中 Qian.Files.Tests 28 例；首次同步 945ace6668 后的全量 1192/1192 亦通过
- 2026-10-22 claude：提交评审：验收 1–4 已勾选，验收 5 待建议裁决 1
