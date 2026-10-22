# Profile 镜像的交付输入与恢复条件

本目录记录 profile 镜像的真实输入和验收准备。2026-10-14 产品负责人确认 DEP-003 保持 blocked；2026-10-16 按 R106 拆为两段：

- S1-DEP-003 交付镜像构建与供应链流水线，用 CI 内构建、不发布的自检镜像做端到端验证，现在即可推进；
- S1-DEP-008 把三个业务镜像接入流水线并复验。原 DEP-003 的三项验收原文移到这里，等下表的前置交付。

## 已核对的输入

| 镜像 | 既定形态 | 当前缺口 | 负责的前置任务 |
|---|---|---|---|
| qian-platform | JIT，包含 shell 与 model-gateway 组合包 | 现有 profile 是静态装配基线；RP、外壳静态制品和完整业务装配尚未交付 | S1-IDP-005、S1-YUE-001 及 profile 的业务组件任务 |
| qian-identity | JIT | Qian.Identity.Server 与可启动的唯一 issuer 宿主尚未交付 | S1-IDP-002 |
| qian-gateway | NativeAOT、distroless | `hosts/Qian.Gateway.Host` 已完成严格 Linux 原生发布与真实依赖运行；仍需接入产品镜像、供应链及只读非 root 验收 | S1-DEP-008；宿主证据见 S1-GW-001 |

证据来源是仓库现状：[profile 装配基线](../../profiles/README.md) 明确其它 profile 的业务叶节点仍为 `x-skeleton`，只有 minimal 的真实 Runtime 启动已验收；`apps/` 当前仅有前端验证应用，相关身份、网关与外壳任务仍未完成。上述表格表示交付缺口。基线探针、minimal、Storybook 页面和 DEP-003 的自检镜像，都不能替代三个业务镜像。

## 可以固定的构建与验收约定

1. 构建入口、运行镜像模板与门禁由 S1-DEP-003 先行交付，用自检镜像验证；三个宿主与组件闭包就绪后，在 S1-DEP-008 接入各自的发布入口。依赖与构建版本锁定，源码 SHA、KunVersion 和 profile 修订均写入构建证据。
2. 多阶段构建；platform/identity 使用 JIT，gateway 使用 Linux NativeAOT 与 distroless 运行镜像。S1-GW-001 已在实际生产宿主上完成完整依赖原生运行并恢复默认 AOT；S1-DEP-008 仍须在最终产品镜像上复验。以非 root UID 启动，以只读根文件系统运行，只为明确需要写入的目录提供临时卷。
3. profile、组合包、组件清单与对应 patch 随镜像发布；环境 overlay 以 ConfigMap 挂载。分别在源码树与镜像内执行同一 dump-config 合成，比较解析后的结构与来源层，阻止配置漂移。
4. 镜像构建成功后按不可变 digest 生成 Syft SBOM，执行漏洞扫描，生成 Cosign 签名和 SBOM 关联证明。验签必须约束 CI 发布者身份/issuer，不能只确认某处存在一个签名文件。
5. 每个镜像保留 digest、构建 SHA、SBOM、扫描报告、签名验证结果、非 root/只读运行证据和配置比对结果。shell 的实际静态制品必须存在于 platform 镜像，并在真实 gateway 路由下可读取。
6. S1-DEP-005 继续负责发布、迁移先行、Argo 收敛和真实平台 PG/Redis/RabbitMQ/Apollo 联调。DEP-003 与 DEP-008 的供应链验收都不替代该发布门。

## 构建与供应链流水线（S1-DEP-003）

| 文件 | 作用 |
|---|---|
| `jit.Dockerfile` / `aot.Dockerfile` | 两种运行镜像模板。都是多阶段构建；JIT 运行在 `aspnet:10.0-noble-chiseled-extra`，NativeAOT 运行在 distroless 的 `runtime-deps:10.0-noble-chiseled`；基础镜像按多架构索引摘要钉定 |
| `*.Dockerfile.dockerignore` | 构建上下文白名单，只送 .NET 源码、Schema、profile 与组件目录；新增宿主项目时在这里放行 |
| `vulnerability-allowlist.json` | 漏洞豁免清单，默认为空 |
| `../../scripts/images.py` | 统一构建入口 `build` 与 CI 自检 `selfcheck` |
| `../../scripts/image-tools.lock.json` | syft、grype、cosign 的锁定版本与各平台归档 sha256，由 `prepare_image_tools.py` 下载校验 |
| `../../tools/Qian.Image.SelfCheck*` | 自检程序：`dump-config` 合成 profile，`self-report` 自报运行期身份与文件系统状态 |
| `../../.github/workflows/images.yml` | CI 自检 |

### 构建入口

```bash
python3 scripts/images.py build --kind jit --project <csproj> --assembly <程序集名> --tag <registry/名称:标签>
```

`build` 构建并推送镜像，输出按 digest 的引用和构建证据（源码 SHA、KunVersion、profile 与组件目录的 git 树修订）。推到哪个 registry 由 `--tag` 决定；向外部 registry 发布归 S1-DEP-008 与 S1-DEP-005。两种模板的运行约定相同：

- 入口固定为 `/app/entrypoint`；用户为 chiseled 镜像内置的非 root 用户 `$APP_UID`；
- profile 与组件目录随镜像发布到 `/app/qian`（`QIAN_ROOT`），环境级 overlay 由 ConfigMap 挂载后经 `--patch` 传入；
- 部署时 `readOnlyRootFilesystem: true`，只为标签 `io.qian.writable-paths` 列出的目录（目前只有 `/tmp`）挂临时卷；
- 标签记录源码 SHA（`org.opencontainers.image.revision`）、KunVersion、profile 与组件目录修订和镜像角色（`io.qian.image-role`：`product` 或 `selfcheck`）。

### 门禁

`selfcheck` 在本机起临时 registry，用 `build` 同一路径构建 JIT 与 NativeAOT 两个自检镜像（角色 `selfcheck`，只存在于这个临时 registry），然后依次检查：

1. **SBOM 与漏洞扫描**：Syft 按 digest 生成 SPDX SBOM，Grype 扫描 SBOM，达到 `--fail-on`（默认 `high`）的漏洞即失败。豁免项写成 `{"id": "CVE-…", "reason": "…", "until": "YYYY-MM-DD"}`，理由与期限缺一不可，期限不是完整日期或已过期都视同无效。反例：把已知含严重漏洞的 System.Text.Encodings.Web 4.5.0（CVE-2021-26701）写成组件清单，经同一 Grype 与阈值判定必须报出违规。JIT 镜像的 SBOM 含 .NET 共享框架和应用的 NuGet 依赖。NativeAOT 镜像的 SBOM 只有 chiseled 系统包：编进原生可执行文件的 .NET 运行时和 NuGet 依赖，Syft 读不到。qian-gateway 接入时（S1-DEP-008）要另补发布期依赖清单，并纳入扫描。
2. **签名与 SBOM 证明**：Cosign 签名并附 SBOM 证明（`spdxjson`）。验签用 `--certificate-identity` 与 `--certificate-oidc-issuer` 精确约束发布者身份与 issuer。四个反例：缺签名、签名身份不符、issuer 不符、缺 SBOM 证明，都必须验不过，而且报错要对得上预期原因，网络或配置错误不算拦住。
3. **配置漂移**：五个 profile 分别在源码树与镜像内执行 `dump-config` 合成，逐项比较解析后的结构、来源层与诊断，只忽略诊断里每次随机生成的 `trace_id`。反例：在镜像内改动 standalone 的 `qian.patch.yml`，必须报出漂移。漂移比对只在 JIT 自检镜像上做：NativeAOT 镜像里没有 `dump-config`，qian-gateway 接入时（S1-DEP-008）要让网关宿主支持合成输出，或把镜像内的 `/app/qian` 交给自检程序合成。
4. **运行期证据**：镜像配置的用户不是 root。以 `--read-only` 加 `/tmp` 临时卷启动后，自报的有效 UID / GID 非 0、根挂载只读、写探针被拒、`/tmp` 可写；JIT 镜像可以动态生成代码，NativeAOT 镜像不可以。另做一次不加只读的对照运行，写探针必须可写，证明只读运行里的写失败来自只读根，而不是权限不足。

结果写到 `TestResults/images/evidence.json`（构建证据、两个镜像的 digest、SBOM 与扫描摘要、各门禁与反例结果），逐条命令与输出写到同目录的 `commands.log`；CI 把整个目录上传为 `image-selfcheck` 产物。PR 运行里构建的是 GitHub 合成的合并提交，证据另记 PR 头与 base 的 SHA 和运行号。工作流按路径触发，也可手动运行；新披露的漏洞不会自动触发重扫，产品镜像上线后（S1-DEP-008 / S1-DEP-005）要加定时扫描。

自检签名用本次运行临时签发的 CA：证书的 SAN 是发布工作流身份 `https://github.com/idadawn/qian/.github/workflows/images.yml@refs/heads/main`，OIDC issuer 扩展是 `https://token.actions.githubusercontent.com`。cosign 的外网访问被不可达代理阻断，不申请 OIDC 令牌，也不写公共透明日志。正式发布改用 GitHub OIDC 无密钥签名，签名记录会写进公共 Rekor 日志，属于对外操作，要在 S1-DEP-008 由产品负责人确认后启用；验签约束的身份与 issuer 形式不变。

### 本机运行

需要 Docker（BuildKit）、`global.json` 指定的 .NET SDK、python3 与 openssl：

```bash
python3 scripts/prepare_image_tools.py --destination "${TMPDIR:-/tmp}/qian-image-tools"
```

```bash
python3 scripts/images.py selfcheck --tools "${TMPDIR:-/tmp}/qian-image-tools"
```

macOS 的 Docker Desktop 守护进程运行在虚拟机里，推不到宿主机上发布的临时 registry 端口（实测连接被拒），完整的 selfcheck 只能在 Linux 主机或 CI 上跑。在 Docker Desktop 上可以分段验证：构建、运行期证据、漂移比对和 SBOM 扫描用本地镜像跑，签名与验签对推到临时 registry 的任意镜像跑。

## 接入真实镜像（S1-DEP-008）

前置任务完成后，重新核对三件事：有效 profile 中每个必需叶节点的交付状态、三个宿主的发布入口、shell 制品路径；核对无误后再认领 S1-DEP-008。三项验收保持 DEP-003 的原文，只有真实镜像及其验证证据全部齐全，才可勾选和关闭。截至 2026-10-16，没有构建、发布或签署任何业务镜像，也没有修改集群。
