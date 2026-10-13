# Qian.Data.AspNetCore

- 权威设计为 PRD §7.6 与 S1-DATA-003；主文档为 README.md。
- 只依赖 Qian.Data.Abstractions、Qian.Plugin.Sdk.AspNetCore 与 Kun.AspNetCore，经 Definition 接口调用服务，不引用 Qian.Data。
- 每个端点都要 RequireKunPermission，权限点必须与 plugins/qian.data/metadata/permissions.json 一致（DataPermissionMetadataTests 守门）。
- 响应只用 DataContractJson 的源生成元数据序列化；请求体按成员白名单读取，不接受口令。
