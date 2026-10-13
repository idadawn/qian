# qian CLI（装配切片）

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

此工作区包尚未发布。代码包安装、升级/移除、启动进程与远程平台 scope patch 属于 S1-RT-011；市场切片只执行下文明确列出的无代码安装。五个默认 profile 均可静态解析；minimal 仅 Runtime 与表面装载器，默认输出空条目和空诊断。

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

url 的 source.digest 校验原始归档字节；local/git 的目录 digest 按相对 POSIX 路径排序，对 `路径 TAB 字节数 TAB SHA-256 LF` 的 UTF-8 清单求 SHA-256，不包含 `.git`。路径禁止控制符，因此清单无歧义。分支引用必须带 digest；标签/完整提交可以省略，结果仍锁定实际 commit/digest。显式 @version 选择 `refs/tags/v<version>`，并要求所选清单版本一致；NuGet/npm 版本来自该标签内的精确 runtime 声明，不改写或安装它们。已有目录 digest 仍必须匹配请求的版本内容。

无代码 add 会显示文件、反向权限与 secretRef 需求；confirm 需 --yes，restricted 被拒绝，ON_USE 仅提示登记，不写安装锁或激活。该切片复制已校验表面、追加 global profile 条目，再次验证 profile 后记录 `.qian/lock.json` 的 plugins 数组，包含来源市场、source、digest、可用 commit、公钥指纹、文件和 profile 条目。已有修改或不同版本拒绝覆盖；同一版本/来源/内容重跑无变化。项目安装锁防并发写入，journal 保存变更与回滚数据，异常逆序恢复。进程中断后的 journal 恢复、代码依赖与租户操作仍由完整 CLI 任务提供，不能据本切片宣称它们已实现。
