# 生命周期 Provider

- 权威设计为 PRD 7.2.1 / 7.17、ADR-020 §2 与 S1-RT-005；主文档为 README.md。只进 platform profile。
- 只依赖 Runtime 与各 seam 的 Definition 包（Lifecycle、Registry、Data、Config、Events、Authz 的 Abstractions）；Registry、Capabilities 的实现由宿主组装，不在本包里另起一份装配或契约检查。
- 验签不能被配置关闭：未签名、签名不符或公钥不在当前有效信任根中一律拒绝，失败的安装不登记制品、不写修订。信任根每次实时读取，不缓存成进程常量。
- 状态写入与集成事件同一事务：事件经领域事件 + AddQianLifecycleEvents 进 Outbox，写入端提交前核对；不得在提交后另行补发。
- 钩子不在数据库事务内执行，以 lifecycle_runs 的认领与租约保证幂等；不要把钩子挪进事务，也不要用进程内锁代替 advisory lock。
- 挂载与否只在 Registry 的 scope_patches（R34）：不新增 Enabled / Disabled 状态或开关列；卸载不改安装状态。
- 租户表必须是 IMultiTenant 并经 Data 的迁移生成器带 FORCE RLS；平台操作不跨租户查询安装行。
- 诊断端点的无权限、跨租户与不存在同为 404（R32）；诊断与日志不输出配置、patch 内容、钩子结果、令牌或公钥。
- 执行器凭据只接受 secretRef://platform/… 引用并在每次调用时解析；仓库地址不得内嵌凭据。
