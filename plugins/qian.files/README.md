# qian.files

文件组件的 Provider 位于 [packages/Qian.Files](../../packages/Qian.Files/README.md)，Definition 在 [Qian.Files.Abstractions](../../packages/Qian.Files.Abstractions/README.md)，HTTP 表面在 [Qian.Files.AspNetCore](../../packages/Qian.Files.AspNetCore/README.md)（S1-FILE-001）。已提供租户前缀存储键、用途上传策略、租户配额、local 后端的签名直传 / 直下、完成时服务端校验与隔离、附件下载、审计与 `files.object.created/deleted.v1` 事件。

清单的 inject、provides 与 runtime 字段，以及 `tools/*.json`（六个能力 `qian.files/presign-upload`、`complete-upload`、`get`、`download-url`、`delete`、`quota`）由 Qian.Plugin.Tool 从实际程序集同步，CI 的 `scripts/check_plugin_manifests.py` 发现入口后核对不漂移；`provides` 含 local 后端的具名贡献 `Files.Backends:local`（入口上的 `[Contributes]` 声明）。`runtime.nuget` 列出 `Qian.Files.Abstractions` 与 `Qian.Files`，锁步发布后人工同步版本与摘要（发布链给出待同步清单归 S1-RT-034）。

- `schemas/config.schema.json`：`storage`（`kind`；local 还须 `root` 与 `endpoint`）、`quota.perTenantBytes`（平台默认，租户覆盖经租户配置优先）、`policies[]`（平台默认上传策略）。各层都不接受未知字段，也没有任何下载处置开关。
- `metadata/permissions.json` 与 `metadata/roles.json`：资源 `file` 的 read / write / delete（write 中风险、delete 高风险）；tenant.owner 与 tenant.admin 全权，tenant.developer 与 tenant.user 只读写、不能删除。
- 其他组件在自己文件夹的 `metadata/upload-policies.json` 声明本组件的上传用途（结构见 `schemas/v2/files-upload-policies.schema.json`）；平台宿主由 qian.files 经 Registry 汇总，同用途以 `policies[]` 为准。

计划进入 platform 与 standalone profile，可挂在 global 或 tenant scope。本文件夹仍标记 x-skeleton：按 S1-DEC-018 口径，锁步发布并经 `qian add data files` 从私有源真实安装后去标（S1-FILE-004）。基础组合包的 files 补丁已带 `storage.endpoint`（示例域名）；local 后端缺它会在挂载时报 `INPUT_INVALID`。
