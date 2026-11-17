> For AI agents: the complete documentation index is available at /llms.txt, the full documentation bundle is available at /llms-full.txt.

# system 级预装组件的文件夹变了而版本号没变

> 本页由构建脚本镜像自仓库 [`docs/decisions/system-component-same-version.md`](https://github.com/idadawn/qian/blob/main/docs/decisions/system-component-same-version.md)，请修改源文件，不要改本页。

状态：已裁决。产品负责人 2026-11-17 作答「全部按推荐」（R167，见 `docs/decisions/key-decisions-v0.md` 与 ADR-017 §11）：取做法 A、分三步，D2–D5 取推荐项。第一步归 S1-DEP-012，第二步归 S1-DEP-013，第三步并入 S1-DEP-005。手册第 9 节的 fail-closed 说法已随登记更正，平台宿主 README 的同一说法随 S1-DEP-012 更正。下文保留决策时的分析。

决策准备（S1-DEC-037）只核实与比较，不改实现、不动集群。代码按 main `041e97a04e` 核对；staging 现在运行的 `9399cf9584` 与它相比，system 级组件的文件夹没有变化。文中标「按代码推断」的结论没有在集群或测试里复现过；2.4 的结论另经一次独立的代码阅读复核，结论一致。

## 1 问题

2026-11-17 把 staging 从 `424f6e7d07` 换到 main `9399cf9584` 构建的镜像时，有两个 system 级组件的文件夹变了，版本号仍是 `1.0.0`：

- `qian.identity.server`（S1-IDP-017）：`metadata/permissions.json` 新增 `member:create`，`metadata/roles.json` 把它授给 `tenant.owner` 与 `tenant.admin`，另加工具 `tools/directory.member.add.json`。
- `qian.tenancy`：契约 `contracts/http-v1.openapi.json` 的两处请求体加了可选字段 `initialPassword`。

staging 库里已有这两个组件 `1.0.0` 的权限目录与修订，系统组件修订 Job 失败，钩子失败中止同步，工作负载没有变。产品负责人确认后删除 8 个 `plugin_qian_*` schema、清库重来，部署与 E2E 随后通过。过程与证据在 qian#324（已合入）：`pm/tasks/S1-DEP-011.md` 的进度日志，以及[运维手册](/design/ops/staging-first-deploy.md)第 4、7、9 节。

这件事会反复出现。签名脚本覆盖的 13 个 system 级 module 组件，自建立以来版本号从没改过，全是 `1.0.0`。近一个月（10-17 起，不计合并提交）里，`qian.identity.server` 的文件夹被 8 个提交改过，`qian.tenancy` 7 个，`qian.authz`、`qian.config`、`qian.files`、`qian.lifecycle` 各 4 个。照现在的做法，带功能的 staging 部署几乎每次都要清库。

## 2 已核实的事实

代码位置见第 6 节，链接固定在 `041e97a04e`。

### 2.1 权限目录按「组件 + 版本」不可变，authz 挂不起来

- `PostgresPermissionCatalogStore.PutAsync`：同一组件、同一版本已有目录时，内容不同即抛 `CONFLICT`（pointer `metadata#/version`），内容相同直接返回。`PermissionCatalogPersistenceTests` 把「版本不可变、重复登记幂等」当作验收。
- 平台宿主在第一遍校验时，从每个带 `permissions.json` 的预装组件文件夹编译权限目录，经 `new AuthzPlugin(目录)` 交给 authz。`ApplyAsync` 对每份目录先 `PutAsync`、再登记活动声明，任何一个冲突都让 authz 的 Apply 失败。
- capabilities 注入 authz 提供的 `CommandAuthorization`，于是停在 Pending。
- 宿主按 profile 条目顺序找第一个没就绪的条目报错（`PlatformRuntime.StartAsync`）。base 组合包里 capabilities 排在 authz 前面，所以报的是「平台插件树的条目未就绪：capabilities」。Pending 的条目没有失败异常，所以内层异常为空，真正失败的 authz 不出现在错误里。

### 2.2 Lifecycle 不让同一版本换制品

- `LifecycleController.InstallAsync`：同组件已有同版本、不同摘要的修订时，抛「同一版本已安装为另一个制品」。`plugin_revisions` 对 `(plugin_id, version)` 有唯一索引。Lifecycle README 把它写成规则：「同一版本不能对应两个制品」。
- module 修订还要求宿主预装了同组件、同版本、同摘要的绑定。

### 2.3 修订 Job 的注释只在升了版本时成立

`PlatformSystemRevisions` 的注释写「新摘要（镜像换了组件文件夹）安装后激活，旧修订按 Lifecycle 的规则保留可回切」。同版本换摘要时走不到安装这一步：Job 先启动整棵插件树，在 2.1 就失败了；即使绕过 2.1，安装也会撞上 2.2。

### 2.4 升版本这条路今天的情况

**Lifecycle。** 新版本、新摘要的修订能安装并激活，原来激活的修订降回 Installed，不冲突。

**契约准入既不拦，也不比较。**

- `ContractAdmission` 的基线是同组件已预装版本里低于候选、声明同一契约 id 与同一种类的最高版本。「已预装版本」取自 Registry 的预装快照（`InstalledContractRepository.Versions`）。
- 平台宿主每个 profile 条目只解析出一个组件版本（`ProfileLoader` 取满足范围的最高版本），预装绑定按条目各读一个文件夹；authz 的构造也要求每个组件只有一份权限目录。所以平台宿主里每个组件只有一个预装版本，永远找不到基线。
- 准入只包在 Registry 的 scope Reconciler 合入的条目上，这些 scope 只有 tenant、app、agent-session 三种。初始插件树的 global 条目直接挂载，不经准入。
- 结论：升 global 的 system 级组件版本时，契约准入既不拦也不做兼容比较。手册第 9 节与平台宿主 README 的「已知限制」都说，没有装配 OpenAPI / proto 兼容检查器时跨版本升级会 fail-closed；这说的不是平台宿主上实际发生的事。要走到 fail-closed，得同一镜像预装两个版本、条目又经 scope Reconciler 合入（例如测试里的临时组件文件夹）。那时平台宿主确实没有 OpenAPI 兼容检查器可用（只有本机联调工具 `Qian.Dev.TenantOps` 登记了），比较会抛错，该 scope 被挡下。

**现有租户（按代码推断）。**

- Lifecycle 的租户开通只在开通那一刻取激活修订的版本。把租户从旧版本升到新版本的 `UpgradeAsync` 没有 HTTP 入口，在生产代码里也没有调用方，只有测试调用。
- Authz 在 `tenant/provisioned` 时只给尚未装进该租户的组件实例化默认策略，注释写明不会把旧租户静默升到新模板。把默认策略换到新版本的 `InstallDefaultsAsync(…, expectedVersion)` 存在，`TenantDefaultRoleTests` 也验过它，但同样只有测试调用。
- 判定按租户策略安装的版本查活动登记，也就是 `registry.Find(权限点, 租户的策略版本)`，查不到即拒绝，原因 `permission_not_registered`，经命令 PEP 对外是 403 `AUTHZ_DENIED`。登记表按精确版本索引，平台宿主只登记预装的那个版本。
- 所以在今天的代码上升版本，修订 Job 会成功，同步也会是 Synced，但已有租户对这个组件的全部权限点都会被拒绝，不只是新增的那一个。平台超管对这些租户资源的显式旁路也会拒绝，因为旁路同样按租户的策略版本核对登记。新开的租户拿到新版本，不受影响。
- 也就是说，只升版本、不补租户这一步，会把今天这种报错的失败换成不报错的权限缺失，比现状更糟。

**租户设置。** 租户设置写入的 scope patch 只带条目 id 与配置、不带版本，升版本后照常合入；新版本的配置 Schema 要接受旧配置。

### 2.5 CI 测不到

- `PlatformIdentityHostTests` 每个测试类用一个全新的 PostgreSQL 容器。修订 Job 用同一份 `plugins/` 连跑两遍，断言 11 个预装组件各有一个激活修订、第二遍不再安装。
- 没有任何用例在同一个库上先后用两份不同的组件文件夹跑 Job，也没有用例把「升版本」和「已有租户」放进同一个宿主。authz 与 Lifecycle 的单元用例分别验了不可变与换版本函数，两者之间没有接线。

## 3 三种做法

### A 升版本门禁 + 让升版本在有数据的库上走通

**门禁：不让同版本换内容进 main。**

- 新脚本（暂名 `scripts/check_component_versions.py`）复用 `scripts/sign-system-revisions.py` 的目录摘要算法。Lifecycle 核对制品、平台宿主读预装文件夹用的也是这个算法。
- 「上次发布」的基线记在仓库里的一个文件里（暂名 `deploy/images/released-components.json`）：上次部署到有持久数据环境的平台镜像里，每个组件的 id、版本、摘要与源码提交。
  - 初值取 staging 现在的 `9399cf9584`。它与 main 的 system 级组件文件夹相同，所以现在不需要升任何版本。
  - 每次部署后随部署登记的 PR 更新，手册第 4 节加一步。
- 规则：
  - 摘要相对基线变了而 `plugin.json` 的版本没变，即失败，报出组件名并提示升版本。
  - 版本低于基线也失败；基线里没有的新组件放行。
  - 同一个发布周期里，同一组件只需升一次。
- 门禁跑在 docs-and-pm 工作流里，只需 Python，不依赖 .NET；签名脚本签名前再查一遍。
- 版本建议：只改文档升修订号，加权限点或可选字段升次版本，破坏性变化升主版本。摘要覆盖整个文件夹，组件文件夹里的 README 改动也要升修订号；生成器的输出格式一变，所有组件都要升。

**补上 2.4 的缺口，让升版本走通。**

- 修订 Job 激活新版本之后对齐租户：对每个开通在别的版本上的租户，调 `UpgradeAsync(租户, 组件, 原版本, 新版本, "platform-release")`。
  - 它执行目标版本的 tenant-upgrade 钩子（system 组件目前没有），改写该租户 scope 里引用旧版本的条目，写一条租户审计链记录 `qian.lifecycle/tenant-plugin.upgrade`。
  - Job 要能在平台级列出租户。失败的租户列出来、以退出码 1 结束，可以重跑。
- Authz 在租户级升级成功后，把该租户这个组件的默认策略换到新版本（`InstallDefaultsAsync` 带 `expectedVersion` 为原版本）。
  - 由 Lifecycle 派发新的钩子事件，Authz 照 `tenant/provisioned` 的方式监听。
  - 它会递增租户授权修订和受影响成员的身份修订（S1-DEC-027）：旧令牌 401，换令牌后恢复。
- 护栏（第一步先上，租户对齐落地后撤掉）：Job 发现有租户还开通在旧版本时，拒绝激活新版本并报出组件名。这样保住今天这种报错的失败，不让它变成静默的权限缺失。
- 宿主诊断：先报 Failed 的条目和它的错误码，再报 Pending 的条目和它在等的服务键。
- 文档：更正手册第 9 节与平台宿主 README 里 fail-closed 那句；Lifecycle、Authz 的 README 写明升版本时租户怎么跟进。
- 可选，可并入 S1-DEP-005：
  - 门禁里用已锁版本的 oasdiff 与 buf 把契约和基线比较，破坏性变化要求升主版本，补上平台宿主里缺的契约兼容比较。
  - 生产滚动发布期间新旧副本并存，authz 还要从库里登记租户仍在用的旧版本目录，否则窗口内老副本会拒绝已升级的租户。

**要补的用例。**

1. 同一个库、两份组件文件夹。先用 V1（X\@1.0.0）跑 Job，开一个租户 T 并授管理员；再换 V2（X\@1.1.0，新增权限点并授给 `tenant.admin`）跑 Job。断言：
   - X 有 1.0.0（Installed）与 1.1.0（Active）两个修订，平台链有 1.1.0 的安装与激活；
   - T 开通在 1.1.0，租户链有 `tenant-plugin.upgrade`；
   - T 的策略安装是 1.1.0，管理员原有的权限点仍放行，新权限点也放行；
   - 平台超管对 T 的旁路照常；V2 之后新开的租户直接是 1.1.0。
2. 同一个库换 V2'：X 的文件夹变了，版本仍是 1.0.0。门禁的用例拒绝它；直接跑 Job 时尽快失败，错误里点名 X 与目录冲突，而不是「capabilities 未就绪」。
3. V2 再跑一遍：不新增修订、不新增审计记录，也不递增授权修订。
4. 退回 V1（镜像回切）：按 D5 的结论，要么 Job 重新激活 1.0.0 并把 T 对齐回去，要么失败并列出停在新版本的租户。
5. 门禁自测（`tests/gates/`）：同版本改摘要失败、升版本通过、版本回退失败、只改 README 也算改动、新组件放行、基线文件格式。
6. 宿主诊断：authz 的 Apply 失败时，错误里点名 authz 与 `CONFLICT`。

**影响。**

- 审计：安装、激活、租户升级都照现有的能力入链，不新增审计语义。
- 回切：跨版本回切镜像，要把租户对齐回旧版本。Lifecycle 支持租户级回切，authz 的 `InstallDefaultsAsync` 也能换回旧版本。这一步落地之前构建的镜像做不到，回切到那些镜像仍要清库。迁移只向前的限制不变。
- 现有租户：在 Job 里升级。从策略切换到新副本就绪之间，被升级的组件对这些租户短暂不可用；staging 用 Recreate，本来就有这段停机。

**代价。** 门禁与护栏约 1–2 天；租户对齐、authz 接线与用例约 3–5 天。此后每个发布周期里改到组件文件夹，就要升一次版本。

### B 允许 system 级预装组件同版本受控替换

只对 `policy.tier` 为 system、由镜像预装、新摘要在签名清单里验签通过的组件，由修订 Job 用新内容替换「这个版本」的修订与权限目录。

**要改的代码。**

- Lifecycle：
  - 去掉 `(plugin_id, version)` 唯一索引（随包 EF 迁移），system 预装组件同一版本可以再装一个修订；
  - 按版本取修订的地方（开通续跑、`UpgradeAsync` 的目标）改为取该版本激活的或最新的修订；
  - 安装载荷带上被替换的修订号，Lifecycle README 的规则改写。
- Authz：
  - `PutAsync` 加一个只给修订 Job 用的替换模式。在线副本仍要求内容一致，滚动期间重启的老副本就会失败，而不是把目录换回旧内容。
  - 目录要记下来源摘要。authz 没有随包迁移，离线 DDL 也不升级已有的表，S1-DEP-005 的正式迁移链之前加不了列，只能放进 jsonb 快照。
  - 目录替换要写一条平台审计记录（新旧快照哈希与摘要）；今天目录写入不入链。
- 现有租户：版本没变，`InstallCore` 见到同版本直接返回，这次的 `member:create` 这类新授予到不了已有租户，Job 要逐租户刷新默认授予。被删掉的权限点没有外键约束，引用它的授予会静默失效。
- 文档：Lifecycle 与 Authz 的 README、组件规格；「system 级组件的版本不再唯一对应内容」要写进 ADR。

**要补的用例。** 同一个库、同版本两份文件夹：

- Job 成功，同一版本有两个修订，新的那个激活；
- 目录被替换并入链，刷新后 T 的管理员有新权限点；
- 内容一致的在线副本能启动，带旧内容的副本失败并报清原因；
- 退回旧文件夹时再替换回去（只对带 B 的镜像成立）。

**影响。**

- 审计：链上能看到同一版本两次安装、摘要不同。但版本号不再告诉运营与租户生效的是哪套权限。同版本替换可以给内置角色加授予，包括高风险权限点，没有任何版本信号，只剩目录替换那一条审计记录。
- 回切：Lifecycle 一侧可以把旧修订重新激活；authz 要再替换回去，B 之前构建的镜像做不到。
- 现有租户：旧权限点照常可用；新授予要逐租户刷新。

**代价。** 约 4–6 天，要改已 Accepted 的设计。生产上它与不可变发布事件（ADR-017 D17.1）、以版本追溯内容的供应链做法相冲突，大概只能限定在 staging，形成两套规则。逐租户的那部分工作并没有省掉，而那正是 A 的主要成本。

### C 维持现状：组件文件夹一变，staging 就清库重来

- 做法：把手册第 7 节「同版本换摘要（清库重来）」定为正式流程：备份，删 `plugin_qian_*` schema，完整同步，重新引导平台运营，跑 E2E，删初始密码的 Secret 与引导 Job。可以再加一个部署前检查的小脚本：比较已部署提交与新提交之间 system 级组件的文件夹有没有变，变了就提示要清库。
- 要改的代码：没有（检查脚本可选）。
- 影响：
  - 审计：staging 的审计链每次从 seq 1 重来，连续性的证据随之清空。
  - 回切：组件文件夹变过之后，回切镜像同样要清库。
  - 现有租户：staging 的全部租户、账号与平台运营每次都删掉，ops 要重新引导，E2E 要重跑。
- 代价：
  - 现在不用开发；每次受影响的部署都要产品负责人确认，约半小时运维，并且丢数据。
  - staging 当不了长期的演示或验收环境，也演练不了升级。
  - 生产没有解决：第一次生产升级会撞上同一堵墙，那时不能清库，S1-DEP-005 之前反正要解决。

### 三种做法对照

| 做法 | 开发量         | staging 换镜像 | 生产升级      | 改已定设计                     | 主要风险               |
| -- | ----------- | ----------- | --------- | ------------------------- | ------------------ |
| A  | 约 4–7 天，分两步 | 第二步之后不再清库   | 同一条路，可沿用  | 不改，只补缺口                   | 每个发布周期改到组件文件夹就要升版本 |
| B  | 约 4–6 天     | 不再清库        | 与不可变发布相冲突 | 改 Lifecycle、Authz 规则与 ADR | 版本号不再对应权限内容        |
| C  | 无           | 每次清库        | 没有解决      | 不改                        | 丢数据，生产问题推后         |

## 4 推荐

推荐 A，分三步：

1. 第一步，约 1–2 天，可以马上开工：门禁与基线文件（初值取 `9399cf9584`）、修订 Job 的护栏、宿主诊断、文档更正。这一阶段 staging 遇到组件文件夹变化仍按 C 清库，但 PR 里已经升了版本，部署前就知道要清库。
2. 第二步，约 3–5 天：修订 Job 对齐租户、authz 换版本、同一个库两份文件夹的用例，然后撤掉护栏。此后 staging 换镜像不再清库。
3. 第三步可选，并入 S1-DEP-005：契约兼容比较进门禁；滚动发布时 authz 同时登记租户仍在用的旧版本目录。

理由：

- 保住「版本号唯一对应内容」这条不变式。authz 的目录与授予、Lifecycle 的修订、租户审计的载荷、反向权限批准、契约基线都是按它设计的。
- 第一次生产升级要走的正是同一条路，A 的工作不会白做。
- B 一样要逐租户处理，又多了治理风险，还要改已定的设计。
- C 每次丢数据，生产的问题只是推后。

## 5 待拍板（已裁决：全部取推荐项，R167）

- D1 选哪种做法：A（推荐，分三步）/ B / C。
- D2（选 A 时）「上次发布」的基线放哪：推荐放在仓库里的基线文件，随部署登记的 PR 更新；备选：每次部署打 git 标签，门禁按标签取基线。
- D3（选 A 时）门禁范围：推荐与签名清单同一口径的 system 级 module 组件，按整个文件夹算（含 README，不改摘要算法）；备选：`plugins/` 下全部非骨架组件。
- D4（选 A 时）第二步落地之前，staging 遇到组件文件夹变化怎么办：推荐仍按手册第 7 节清库，每次先经产品负责人确认；备选：staging 清库改为常设授权，只在部署记录里登记。
- D5（选 A 时）跨版本回切镜像：推荐修订 Job 双向对齐，租户跟着镜像里的版本升或降；备选：只支持向上，跨版本回切时清库。

## 6 参考

- qian#324：staging 部署 `9399cf9584` 的进度日志，手册第 4、7、9 节补的同版本换摘要限制与清库步骤。
- [运维手册](/design/ops/staging-first-deploy.md)、[平台宿主 README](https://github.com/idadawn/qian/blob/main/hosts/Qian.Platform.Host/README.md)、[Lifecycle README](https://github.com/idadawn/qian/blob/main/packages/Qian.Lifecycle/README.md)、[Authz README](https://github.com/idadawn/qian/blob/main/packages/Qian.Authz/README.md)、[ADR-017](/design/adr/adr-017-deployment.md)、[PRD 7.2.1](/design/prd/07-s1-foundation.md)。
- 代码位置（main `041e97a04e`）：
  - 2.1：[PostgresPermissionCatalogStore.cs L14–28](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/PostgresPermissionCatalogStore.cs#L14-L28)、[PlatformRuntime.cs L77–82](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/hosts/Qian.Platform.Host/PlatformRuntime.cs#L77-L82)、[PlatformComponents.cs L122](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/hosts/Qian.Platform.Host/PlatformComponents.cs#L122)、[AuthzPlugin.cs L37–42 与 L86–93](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/AuthzPlugin.cs#L37-L93)、[capabilities 清单 L17](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/plugins/qian.capabilities/.qian-plugin/plugin.json#L17)、[PlatformRuntime.cs L98–101](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/hosts/Qian.Platform.Host/PlatformRuntime.cs#L98-L101)、[PermissionCatalogPersistenceTests.cs L38–55](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/tests/Qian.Authz.Tests/PermissionCatalogPersistenceTests.cs#L38-L55)。
  - 2.2：[LifecycleController.cs L34–36 与 L45–47](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Lifecycle/LifecycleController.cs#L34-L47)、[LifecycleDbContext.cs L43](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Lifecycle/LifecycleDbContext.cs#L43)、[Lifecycle README L58](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Lifecycle/README.md?plain=1#L58)。
  - 2.3：[PlatformSystemRevisions.cs L16 与 L39](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/hosts/Qian.Platform.Host/PlatformSystemRevisions.cs#L16-L39)。
  - 2.4 契约准入：[ContractAdmission.cs L29–37](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Capabilities/ContractAdmission.cs#L29-L37)、[InstalledContractRepository.cs L61–65](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Capabilities/InstalledContractRepository.cs#L61-L65)、[ProfileLoader.cs L134–136](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Profiles/ProfileLoader.cs#L134-L136)、[PlatformRuntime.cs L76](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/hosts/Qian.Platform.Host/PlatformRuntime.cs#L76)、[RegistryScopeReconciler.cs L29](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Registry/RegistryScopeReconciler.cs#L29) 与 [L282–285](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Registry/RegistryScopeReconciler.cs#L282-L285)。
  - 2.4 现有租户：[LifecycleController.Tenants.cs L55–71](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Lifecycle/LifecycleController.Tenants.cs#L55-L71) 与 [L99–170](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Lifecycle/LifecycleController.Tenants.cs#L99-L170)、[PostgresTenantRoleStore.cs L26–33](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/PostgresTenantRoleStore.cs#L26-L33) 与 [L127–136](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/PostgresTenantRoleStore.cs#L127-L136)、[PostgresRbacEvaluator.cs L68–72](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/PostgresRbacEvaluator.cs#L68-L72)、[PermissionRegistry.cs L23–27](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/PermissionRegistry.cs#L23-L27)、[SuperadminBypassService.cs L78–91](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/packages/Qian.Authz/SuperadminBypassService.cs#L78-L91)、[TenantDefaultRoleTests.cs L72](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/tests/Qian.Authz.Tests/TenantDefaultRoleTests.cs#L72)。
  - 2.5：[PlatformIdentityHostTests.cs L34](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/tests/Qian.Sample.Crm.Tests/PlatformIdentityHostTests.cs#L34) 与 [L97–117](https://github.com/idadawn/qian/blob/041e97a04eba5b4d3d8be5a5ead1e481906c676e/tests/Qian.Sample.Crm.Tests/PlatformIdentityHostTests.cs#L97-L117)。
