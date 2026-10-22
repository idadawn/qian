# qian.files

文件组件的 Provider 位于 [packages/Qian.Files](../../packages/Qian.Files/README.md)，Definition 在 [Qian.Files.Abstractions](../../packages/Qian.Files.Abstractions/README.md)，HTTP 表面在 [Qian.Files.AspNetCore](../../packages/Qian.Files.AspNetCore/README.md)（S1-FILE-001）。已提供租户前缀存储键、用途上传策略、租户配额、local 后端的签名直传 / 直下、完成时服务端校验与隔离、附件下载、审计与 `files.object.created/deleted.v1` 事件。

清单的 inject、provides 与 runtime 字段，以及 `tools/*.json`（六个能力 `qian.files/presign-upload`、`complete-upload`、`get`、`download-url`、`delete`、`quota`）由 Qian.Plugin.Tool 从实际程序集同步；CI 的漂移检查目前只逐个列举三个组件，本组件由 `tests/Qian.Files.Tests` 的清单用例在进程内核对，CI 改为对每个带代码组件跑 `check` 归 S1-RT-033。`runtime.nuget` 列出 `Qian.Files.Abstractions` 与 `Qian.Files`，发布后由发布链补摘要。

- `schemas/config.schema.json`：`storage`（`kind`；local 还须 `root` 与 `endpoint`）、`quota.perTenantBytes`（平台默认，租户覆盖经租户配置优先）、`policies[]`（平台默认上传策略）。各层都不接受未知字段，也没有任何下载处置开关。
- `metadata/permissions.json` 与 `metadata/roles.json`：资源 `file` 的 read / write / delete（write 中风险、delete 高风险）；tenant.owner 与 tenant.admin 全权，tenant.developer 与 tenant.user 只读写、不能删除。

计划进入 platform 与 standalone profile，可挂在 global 或 tenant scope。本文件夹仍标记 x-skeleton：按 S1-DEC-018 口径，发布并经 `qian add files` 真实安装后才去标（随 S1-RT-033）；去标时基础组合包的 files 补丁要补 `storage.endpoint`，local 后端缺它会在挂载时报 `INPUT_INVALID`。
