# qian CLI（装配切片）

## 当前状态

S1-RT-011 已于 2026-10-16 验收（收口项见 [S1-RT-035](../../pm/tasks/S1-RT-035.md)）。安装器已支持批量 `add`、升级、本地 `list` 和 `remove`。整组组件先在独立目录预检，再一次提交；代码依赖失败会恢复整组宿主、组件目录与还原产物。升级验证所有归属 profile，分别保留已有配置；`--reset-config` 显式恢复新默认值，`--force` 处理本地文件改动，新增反向权限仍须 `--yes`。安装会精确还原 NuGet/npm 包并检查实际文件，中央 NuGet 版本通过 MSBuild 读取。

移除只处理所选 profile 的条目，默认保留 disabled 配置，`--purge` 删除条目；其它 profile 或组件仍引用的包不会删除，宿主原有依赖只借用。卸载记录只保留定位信息，允许随后按原安装名清理保留配置；被复用或改写的条目不会被删除。完成日志保留最近 20 次。

测试使用正式 `Kun.Core` 包编译消费者、实际 npm 包验证导入/移除；另把仓库真实 Config Provider 制成临时 NuGet 包，经签名市场批量安装、消费者运行、重装和移除，并验证实际依赖安装失败时没有半批残留。临时包测试不等于私有包族发布，私有源上的真实安装见下一段。identity / authz / files 的完整组合归 S1-QA-005（R84）；租户 scope patch 已通过单条平台注册表端点接入（见下文）；组合层复杂边界、一键简写参数仍待完成；引擎版本比对、安装末检与崩溃恢复见下文。

私有源 E2E（`test/private-feed.e2e.mjs`，`pnpm --filter qian test:feed`，需要读包令牌 `QIAN_FEED_TOKEN`）：空白 ASP.NET Core 项目经 CLI 支持的签名本地市场执行 `qian add data events config --yes`，市场条目指向仓库里的真实组件文件夹。`Qian.Data`、`Qian.Events`、`Qian.Config.Apollo` 只从正式私有源（GitHub Packages）还原，宿主 csproj 写入精确版本区间（如 `[0.1.0]`），归档摘要等于清单 `runtime.nuget[].digest`，`.qian/lock.json` 记录来源市场、组件摘要与包归档摘要。宿主按 `dump-config` 解析出的条目和清单 `runtime.entry` 挂载三个 Provider，全部就绪；`qian list` 列出来源市场；`qian remove config events data --purge` 逆序撤销后，项目文件、profile patch 与文件集合回到安装前。保留的只有 `.qian/` 工作区本身、`.gitignore` 里两条 `.qian` 忽略规则。CLI 不再强制生成 `packages.lock.json`；以实际 assets 与包归档核验安装。宿主原有锁文件或 `RestorePackagesWithLockFile` 设置继续保留，自定义 `NuGetLockFilePath` 也不被擅自开启或删除。令牌只经环境变量传给 NuGet.Config 的 `%QIAN_FEED_TOKEN%`，不写文件、不回显。CI 的 surfaces 工作流以带 `packages: read` 的 `GITHUB_TOKEN` 运行此用例；发布新版本后要同步各组件清单的 `runtime.nuget` 版本与摘要。

未通过校验器或验签的组件 `add` 失败时退出 1、标准输出为空，标准错误是一行 `{diagnostics:[…]}`：每条带 ErrorDetail（含 trace_id），逐项错误给出 字段级错误码与 JSON Pointer；不留下锁、patch 或组件目录（`marketplaces.test.mjs`）。

`add --surfaces skills,commands --no-code` 从已经整体通过来源与语义校验的组件中选择表面，保留技能目录附件和被引用文件，再重新校验投影。原来源摘要与安装文件摘要分别记录；代码组件的运行时字段与代码服务声明不会留在投影清单里，未选中的工具仍被技能引用时拒绝安装。相同版本改选表面属于内容替换，需要 `--force`；已有 required 代码及子组件不能借此被移除。选择普通组件的现有技能不会获取或安装其代码依赖；包内组合组件需要在隔离目录读取包元数据，NuGet 元数据发现仍需 .NET SDK，但不改变宿主依赖。组合包自身的声明表面也有独立挂载条目，使用完整组件 id 加 `-surfaces` 避免覆盖其子组件配置。

只选技能或命令时不安装代码配置；显式选择 `schemas` 时保留配置 Schema，普通组件的已有配置仍受升级校验约束。`ui` 不能与 `--no-code` 同选。组合包保留所需的装配 patch，代码被排除后只挂载具有所选表面的子组件；它们原本声明的非代码依赖不会被伪造补齐。实际 .NET 表面运行时验证了生成清单可注册技能、看不到原代码服务并可卸载；真实 Config 包验证了无 csproj 宿主也能获取包内技能而不添加依赖。

组合包按真实 profile 组合层安装：允许无 `runtime.form` 的依赖聚合，逐个校验 `components/` 子组件，合并包引用和权限需求，再登记到 `profile.bundles`。卸载保留失去来源的配置，重装可继续使用，多个组合层可一起移除；活动的 required 子组件不能通过卸载父组件绕过保护。

预检还会在隔离目录获取声明引用的包，从实际 NuGet 归档读取 `qian/` 组件内容，并读取 npm 的 `qian.component` 指针。它继承宿主包源配置但不复制凭据，不修改宿主项目或 node_modules；发现的组件重新经过同一验证器。市场与包内同名内容冲突时拒绝覆盖，lock 分别保留原来源摘要、完整交付摘要和实际包归档摘要。

所有显式代码包都记录归档 SHA-256，包括没有 Qian 表面的包；`runtime.nuget[] / npm[].digest` 会与实际归档比对，共享或递归发现的包声明也不能冲突。安装后和幂等重跑时，再核对宿主实际 NuGet 归档，以及 npm 顶层/隐藏安装锁与预检归档的 SHA-512 关联；预检和安装之间发生换包会整批回滚。真实 NuGet、npm 客户端测试覆盖错误摘要、安装记录漂移和同版本换包回滚。摘要一致性不等于发布者真实性。§11.5 的 NuGet 原生包签名与 npm provenance 由 [S1-RT-036](../../pm/tasks/S1-RT-036.md) 承接：验证实际包发布者、证书或 provenance 身份及其信任策略，把验证证据与已还原归档绑定，并补错误证书、伪造身份、篡改证明和离线策略的负例；本任务不把市场 Ed25519 与归档 digest 当作这两项验收。

包管理器不经 shell，不输出原始工具日志，npm 生命周期脚本关闭。POSIX 下按本次创建的进程组确认后代已停止后才回滚；查询失败或无法确认停止时保留事务与锁。Windows 尝试终止进程树，但未能确认的失败保留给后续恢复流程，不声称已安全恢复。目录备份不跟随内部符号链接；工作区必须是当前操作者管理的受信任宿主，这不是原生代码沙箱。

每次包管理器操作（宿主项目求值、NuGet / npm 的还原、安装与移除、预检取包）都有超时，缺省 600 秒，可用环境变量 `QIAN_PACKAGE_TIMEOUT` 按整数秒（1–86400）调整。非法值会让 `add` / `remove` 在任何改动之前以结构化错误失败。超时后照常停止整个进程组并回滚，诊断写明超时秒数与配置方式；未确认停止时仍保留安装锁、日志与备份。预检时在隔离目录的 NuGet 还原与安装阶段一样允许并行下载：包与版本已精确钉定、结果另经归档摘要核验，逐个下载只会让冷缓存下的预检慢上几倍。

## 安装收尾与版本核对

安装在提交事务前对受影响组件执行 doctor 子集：比对锁定文件清单、重新校验组件表面与 engines。若实际 Runtime 版本变化，还按安装锁中的清单和挂载记录纳入声明该引擎的既有组件及相关 profile，不扫描工作区其它目录。任一错误都回滚整批，幂等重跑也不能把末检失败报成成功。返回的 `verification` 明确列出检查、组件及 profile，`runtimeVerified: false` 表示没有执行或探测 Provider。profile 级诊断只出现在批次的 `diagnostics`，不复制到每个组件；单组件 CLI 输出把两层诊断合并一次。

每个结果提供 `defaultPrompt` 和 `nextSteps`：列出仍需在宿主密钥提供者配置的 `secretRef://` 引用，以及清单/市场推荐的 profile。CLI 不解析密钥引用、不读取私钥、不执行组件入口，下一步提示不代表密钥已配置或服务已就绪。

`engines.qian` 来自实际 `Qian.Runtime` / `@idadawn/qian-runtime`（兼容 `@qian/runtime`）包，`engines.kun` 来自 `Kun.Core`，`engines.yue` 来自 `@idadawn/yue-core`（兼容 `@yue/core`）。版本证据读取宿主 `project.assets.json` 与实际 npm 包；预检按整批包计划判断：拟新增或升级的 Runtime 直接引用采用计划精确版本，未改变且精确钉定的宿主直接引用采用实际版本。候选包的隔离还原图不能覆盖宿主引用；可能随本次还原变化的传递 Runtime 在 `enginePlan.pending` 中列出，提交前必须以宿主最终还原结果核验。已确定版本仍在写入前检查；不兼容或末检缺少证据都会失败回滚。CLI 自身版本不能作为宿主版本。范围遵循标准 SemVer，未显式许可的预发布版本不会被放行。`--no-code` 投影不再要求已移除的 Kun/UI 代码运行时，但保留的 qian 范围仍约束承载表面的 Runtime。

```bash
qian list --outdated
qian list --surfaces
qian list --surfaces skills,metadata
qian doctor --fix --profile minimal --no-home --json
```

`--outdated` 用原安装市场与名称解析当前组件，重新经过验签、来源 digest 和组件校验，只列出版本更高的项，并给出 `version` 与 `latestVersion`。不可用或不可信市场返回错误，不能据此宣称已是最新版。`--surfaces` 显示锁定清单指针对应的文件；带逗号分隔值时按表面类型过滤，支持自定义 metadata 等路径。列表不输出配置、任意 lock 扩展字段或来源凭据。

## 安装中断恢复

`doctor --fix` 根据 `.qian/.install.lock` 与 journal 进行恢复。包命令先等待启动屏障，写入者的进程组与启动身份持久化后才允许实际包命令开始；安装锁覆盖预检和最终写入。CLI 被终止后，恢复只在原拥有者及整个写入进程组确认退出时逆序还原 journal 备份，再清除安装锁。它不会终止未知进程，也不能只删锁冒充回滚。完成后的重复恢复无副作用，已完成日志保留最近 20 次。

活动安装、仍能写文件的后代、PID 身份冲突、损坏或旧版缺少归属证据的日志、越界路径和不安全符号链接都会拒绝自动恢复并保留锁、日志和备份。POSIX 的 Linux/macOS 提供跨进程身份与组核验；Windows 保留普通安装和原有终止流程，但不能证明安全的崩溃恢复会明确拒绝。工作区仍须由当前操作者管理，恢复不是隔离原生代码的沙箱。

错误分类由 `Qian.Surfaces.Abstractions/SurfaceErrors.cs` 与 `@qian/surfaces` 同步拥有：市场验签 `SURFACE_SIGNATURE_INVALID`、来源/归档摘要 `SURFACE_DIGEST_MISMATCH`、安装锁 `SURFACE_INSTALLATION_BUSY`、本地改写 `SURFACE_LOCAL_MODIFIED`、profile 不允许 `SURFACE_PROFILE_DENIED`、恢复证据不足 `SURFACE_RECOVERY_REQUIRED`、包操作失败 `SURFACE_PACKAGE_UNAVAILABLE`、版本不兼容 `SURFACE_ENGINE_INCOMPATIBLE`。它们是 ErrorDetail 的字段码；协议码只出现在顶层。旧 `SURFACE_HASH_MISMATCH` 继续仅表示工具契约哈希不一致；组件码不放进只属于 `qian.runtime` 的 v0 内置错误目录。`--json` 的错误统一一行，文件位置只输出相对路径。

URL / Git 完整 CLI 安装、lock 和移除用例分别用替代 fetch 传输与 Git 本地来源重写提供确定性内容，真实 Git 提交与归档校验均照常执行；这些用例不声称验证了生产网络。真实私有 NuGet 源安装、Provider 运行与卸载由独立 E2E 提供。

## 已交付命令

当前实现 [S1-RT-002](../../pm/tasks/S1-RT-002.md) 的 `dump-config`，以及 [S1-RT-010](../../pm/tasks/S1-RT-010.md) 的文档生成与 skill-only 文档模板初始化。共享表面解析与 Schema 包为 `@qian/surfaces`，profile 合成规则以 [Qian.Profiles](../Qian.Profiles/README.md) 为唯一说明。

在仓库根目录执行：

```bash
pnpm install
pnpm --filter qian... build
pnpm exec qian --profile platform --dump-config --no-home
pnpm exec qian --profile platform dump-config --patch ./ops/local.patch.yml --format json
```

overlay 示例（不会修改 base 组合包）：

```yaml
- id: files
  config:
    storage: {kind: local, root: /tmp/qian-local-files}
  note: 本地配置验证
```

输出的 `layers` 列出 bundle、profile-patch、home-patch、overlay；每条 `entries[].layer` 指向最后改写来源。默认输出 YAML，可用 `--json` 或 `--format json`。`--entry <id>` 仅过滤条目；`--cwd <目录>` 切换项目，`--home <目录>` 指定 home，`--no-home` 关闭 home 叠加。profile 缺省先读项目 `.qian/config.yml` 的 `defaultProfile`，再回落 platform。

`--resolve`、`--redact` 表示本切片始终采用的行为：始终解析并验证依赖，始终保留 secretRef，不读取密钥。合法配置退出 0；非法参数或配置退出 1、标准输出为空、标准错误输出带 ErrorDetail 的 diagnostics。业务骨架 warning 会出现在成功输出中，不能用作 Provider 就绪证明。

此工作区包尚未发布。当前支持范围和剩余项见本页开头。五个默认 profile 均可静态解析；minimal 仅 Runtime 与表面装载器，默认输出空条目和空诊断。

## 运行态表面诊断

```bash
pnpm exec qian doctor --profile minimal --url https://host --no-home --json
```

授权令牌从 `QIAN_ACCESS_TOKEN` 环境变量读取，不写入命令参数或输出。诊断读取实际宿主的表面监视状态，重启或更新拒绝会给出操作提示并退出 1；连接、授权或协议失败同样退出 1，不降级成静态检查成功。没有 `--url` 时只检查本地 profile 装配，输出 `runtimeVerified: false` 与已执行的 `checks`。当前不覆盖过期版本、密钥可解析性等完整 doctor 项，不能用 `healthy` 判断未执行的检查。接口、scope 和卸载语义见 [表面主文档](../Qian.Surfaces/README.md#运行态诊断)。

## 派生文档与文档模板

`qian docs generate --profile platform` 输出配置目录、模块图、事件图与 seam 图；事件模式错误返回非零并保留旧文档。参数与边界见 [文档生成器](../Qian.Docs/README.md)。生成器默认不叠加 home，显式 `--home` 可开启。

`qian init --template skill-only --id com.example.guide --name 使用指引` 消费同一套 README/AGENTS 模板，生成可校验的无代码技能与 minimal 配置；拒绝覆盖已有文件。其它 init 模板留给完整 CLI 任务。


## 市场与来源锁定（S1-RT-016）

项目 `.qian/config.yml` 的 marketplaces/trust.keys 是项目配置；`$QIAN_HOME/marketplaces.json`（默认 `~/.qian/marketplaces.json`）保存本机登记，形状为 `{ "marketplaces": [], "trust": { "keys": [] } }`。登记 API `registerMarketplace` 修改本机文件，或按相同结构维护该文件；项目同名登记覆盖本机，随后按 priority 升序、同优先级保持声明顺序。`--home` 可使用独立本机目录，`--no-home` 关闭该层。市场短名须与其 marketplace.json 的 name 一致。

```yaml
defaultProfile: minimal
marketplaces:
  - name: tenant-private
    source: ./catalogs/private
    priority: 10
  - name: official
    source: git+https://github.com/idadawn/qian-plugins.git#stable
    priority: 20
trust:
  keys: [./.qian/keys/publisher.pub]
```

```bash
pnpm exec qian list --markets --json
pnpm exec qian list --remote --json
pnpm exec qian add tenant-private/guide@1.0.0 --no-code --dry-run --json
pnpm exec qian add guide@1.0.0 --from tenant-private --no-code --yes --json
```

list --remote 合并条目、标注市场、优先级、信任与首选项；它只完成目录验签，不声称条目内容已下载校验。解析/安装会取得具体内容、校验 digest 并调用 @qian/surfaces。local 路径相对市场仓库根；url 接受 HTTPS ZIP/tgz；git-subdir 用裸仓库获取和 archive，不运行 checkout、hook、过滤器或组件代码。归档不能越界、包含链接/特殊文件或超过 64 MiB/10,000 文件限制。

市场签名为 `.qian/marketplace.json.sig`，支持 64 字节原始签名或 Base64，签名载荷是 marketplace.json 原始字节的 SHA-256。owner.publicKeys 声明的 Ed25519 公钥还须命中客户端信任根；内联私钥、凭据 URL、越界和符号链接被拒绝。每次操作重新读取市场，不沿用被撤销公钥的旧快照。未验证市场只有显式 --allow-unverified 才参与解析，且不能使用代码或高于 community 的组件。

url 的 source.digest 校验原始归档字节；local/git 的目录 digest 按相对 POSIX 路径排序，对 `路径 TAB 字节数 TAB SHA-256 LF` 的 UTF-8 清单求 SHA-256，不包含 `.git`。路径禁止控制符，因此清单无歧义。分支引用必须带 digest；标签/完整提交可以省略，结果仍锁定实际 commit/digest。显式 @version 选择 `refs/tags/v<version>`，并要求所选清单版本一致；NuGet/npm 版本来自该标签内的精确 runtime 声明。已有目录 digest 仍必须匹配请求的版本内容。代码包声明 digest 已接入预检与实际安装复核；原生签名/provenance 验证仍需按规格 §11.5 补齐，当前草稿不能当作正式代码供应链验收。

add 会显示文件、反向权限与 secretRef 需求；confirm 需 --yes，restricted 被拒绝，ON_USE 仅提示登记，不写安装锁或激活。整组静态预检后复制表面、还原依赖、写 global profile 条目并再次复核，记录 `.qian/lock.json` 的来源、文件、条目和包归属。相同版本/来源/文件与实际依赖重跑保持不变；升级保留配置，不兼容则拒绝并保持原状。已移除组件的 disabled 配置可在重装时复用。包安装成功不等于 Provider 已在业务宿主运行。

`qian list --json` 输出本地已安装来源，不读取配置值；`qian remove <ref> --json` 执行本地移除，`--purge` 删除所选 profile 条目。手工改写的文件或条目、required 组件以及包版本冲突会被拒绝。进程中断后的恢复入口仍待完整验收；租户操作已交付单条运营写入。

## 租户 scope 运营入口

S1 只提供运营 CLI：平台已经安装、且租户已经开通的精确版本，经 Registry 写 scope patch。连接通过 `--url` 或 `QIAN_PLATFORM_URL` 提供，令牌只从 `QIAN_ACCESS_TOKEN` 环境变量读取；需平台超管，服务端在目标租户审计。普通租户即使持 scope:write，也因 Entitlement 未交付而拒绝。

```bash
qian add acme.notes@1.0.0 --entry notes --scope tenant:01900000-0000-7000-8000-000000000001 --url https://platform.example --json
qian add --patch ./notes.patch.yml --scope tenant:01900000-0000-7000-8000-000000000001 --url https://platform.example --json
qian remove notes --scope tenant:01900000-0000-7000-8000-000000000001 --url https://platform.example --json
```

`--patch` 接受一条 patch/v2（或只含一条的数组），可只改已有平台条目的 config。每次只允许一个条目，多条在任何请求之前拒绝；单条 remove 委托 Lifecycle，由 Runtime 按注册逆序释放贡献。不提供远端批量事务，不执行本地包安装或 purge。`--dry-run` 只读取授权视图并输出条目标识、修订与操作，不显示配置或令牌。

CLI 先读取 scope 视图的当前修订，再条件写入；创建用 0，冲突不重试、不自动覆盖。`--revision <n>` 可显式传入已核对的修订（例如重新启用已禁用条目）；连续 remove 不可见条目且未传 revision 时返回 `Unchanged`，不发 PUT，显式旧修订仍由服务端按 409 拒绝。失败输出 `diagnostics[].error` 的 ErrorDetail 并退出 1，保留服务器错误类型、代码和 trace；HTTP 只允许本机回环，不跟随重定向，不把凭据写进命令参数、文件或输出。网络错误可能发生在提交之后，应先核对远端状态再决定重试。
