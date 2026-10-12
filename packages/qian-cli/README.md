# qian CLI（装配切片）

## S1-RT-011 开发进度

正在补齐安装器，已支持批量 `add`、升级、本地 `list` 和 `remove`。整组组件先在独立目录预检，再一次提交；代码依赖失败会恢复整组宿主、组件目录与还原产物。升级验证所有归属 profile，分别保留已有配置；`--reset-config` 显式恢复新默认值，`--force` 处理本地文件改动，新增反向权限仍须 `--yes`。安装会精确还原 NuGet/npm 包并检查实际文件，中央 NuGet 版本通过 MSBuild 读取。

移除只处理所选 profile 的条目，默认保留 disabled 配置，`--purge` 删除条目；其它 profile 或组件仍引用的包不会删除，宿主原有依赖只借用。卸载记录只保留定位信息，允许随后按原安装名清理保留配置；被复用或改写的条目不会被删除。完成日志保留最近 20 次。

测试使用正式 `Kun.Core` 包编译消费者、实际 npm 包验证导入/移除；另把仓库真实 Config Provider 制成临时 NuGet 包，经签名市场批量安装、消费者运行、重装和移除，并验证实际依赖安装失败时没有半批残留。临时包测试不等于私有包族发布，也不替代 identity/authz/files 命名 E2E 或真实平台运营权限验收。组合包完整交付、代码组件的表面切片、引擎校验和受控中断恢复仍待完成，任务保持 doing。

内联组合包已按真实 profile 组合层安装：允许无 `runtime.form` 的依赖聚合，逐个校验 `components/` 子组件，合并包引用和权限需求，再登记到 `profile.bundles`。卸载保留失去来源的配置，重装可继续使用，多个组合层可一起移除；活动的 required 子组件不能通过卸载父组件绕过保护。来自 NuGet/npm 包内 `qian/` 目录的组件发现仍待补齐，不能以内联测试代替这一路径。

包管理器不经 shell，不输出原始工具日志，npm 生命周期脚本关闭。POSIX 下按本次创建的进程组确认后代已停止后才回滚；查询失败或无法确认停止时保留事务与锁。Windows 尝试终止进程树，但未能确认的失败保留给后续恢复流程，不声称已安全恢复。目录备份不跟随内部符号链接；工作区必须是当前操作者管理的受信任宿主，这不是原生代码沙箱。

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

此工作区包尚未发布。完整安装与远程平台 scope patch 继续由 S1-RT-011 完成；当前支持范围和剩余项见本页开头。五个默认 profile 均可静态解析；minimal 仅 Runtime 与表面装载器，默认输出空条目和空诊断。

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

url 的 source.digest 校验原始归档字节；local/git 的目录 digest 按相对 POSIX 路径排序，对 `路径 TAB 字节数 TAB SHA-256 LF` 的 UTF-8 清单求 SHA-256，不包含 `.git`。路径禁止控制符，因此清单无歧义。分支引用必须带 digest；标签/完整提交可以省略，结果仍锁定实际 commit/digest。显式 @version 选择 `refs/tags/v<version>`，并要求所选清单版本一致；NuGet/npm 版本来自该标签内的精确 runtime 声明。已有目录 digest 仍必须匹配请求的版本内容。代码包的可选 digest 与签名/provenance 验证还需按规格 §11.5 补齐，当前草稿不能当作正式代码供应链验收。

add 会显示文件、反向权限与 secretRef 需求；confirm 需 --yes，restricted 被拒绝，ON_USE 仅提示登记，不写安装锁或激活。整组静态预检后复制表面、还原依赖、写 global profile 条目并再次复核，记录 `.qian/lock.json` 的来源、文件、条目和包归属。相同版本/来源/文件与实际依赖重跑保持不变；升级保留配置，不兼容则拒绝并保持原状。已移除组件的 disabled 配置可在重装时复用。包安装成功不等于 Provider 已在业务宿主运行。

`qian list --json` 输出本地已安装来源，不读取配置值；`qian remove <ref> --json` 执行本地移除，`--purge` 删除所选 profile 条目。手工改写的文件或条目、required 组件以及包版本冲突会被拒绝。进程中断后的恢复入口与租户操作仍待完整验收。
