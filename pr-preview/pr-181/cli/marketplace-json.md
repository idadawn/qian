> For AI agents: the complete documentation index is available at /pr-preview/pr-181/llms.txt, the full documentation bundle is available at /pr-preview/pr-181/llms-full.txt.

# 目录市场 marketplace.json

> 本页由构建脚本同步自 [组件模型规格 §8](/pr-preview/pr-181/design/prd/specs/component-model.md)（仓库 `docs/prd/specs/component-model.md`），不另写；请修改规格源文件。

## 8.1 `.qian/marketplace.json` 完整示例

```json
{
  "$schema": "https://qian.dev/schemas/marketplace/v2",
  "name": "qian-official",
  "version": "2026.10.6",
  "interface": { "displayName": "Qian 官方市场", "description": "官方组件：平台基础能力、组合包、运维技能与参考应用",
                 "homepage": "https://plugins.example.internal/qian", "icon": "./assets/market.svg" },
  "owner": { "name": "Qian Platform Team", "url": "https://docs.example.internal/qian", "publicKeys": ["./keys/qian-official-2026.pub"] },
  "categories": ["bundle", "storage", "identity", "authz", "operations", "sample"],
  "plugins": [
    { "name": "files",
      "source": { "type": "local", "path": "./plugins/qian.files" },
      "policy": { "installation": "auto", "authentication": "ON_INSTALL", "profiles": ["platform", "standalone"] },
      "category": "storage",
      "interface": { "displayName": "文件存储", "description": "多租户文件服务", "brandColor": "#0F766E", "defaultPrompt": ["把这份合同扫描件上传到当前租户"] } },
    { "name": "identity",
      "source": { "type": "git-subdir", "repo": "https://github.com/idadawn/qian-plugins.git", "ref": "v1.0.0", "path": "plugins/qian.identity.client", "digest": "sha256:9f2c…" },
      "policy": { "installation": "confirm", "authentication": "ON_INSTALL", "profiles": ["platform", "standalone", "gateway"] },
      "category": "identity",
      "interface": { "displayName": "身份客户端", "description": "RP 接入、Cookie 会话、S2S 校验", "brandColor": "#1D4ED8" } },
    { "name": "wecom-connector",
      "source": { "type": "url", "url": "https://plugins.example.internal/qian/qian.identity.connector.wecom-1.0.0.tgz", "digest": "sha256:0c41…" },
      "policy": { "installation": "confirm", "authentication": "ON_USE", "profiles": ["identity"] },
      "category": "identity",
      "interface": { "displayName": "企业微信连接器", "description": "扫码登录与通讯录同步", "brandColor": "#07C160" } },
    { "name": "tenant-onboarding",
      "source": { "type": "git-subdir", "repo": "https://github.com/example-ops/qian-skills.git", "ref": "main", "path": "tenant-onboarding", "digest": "sha256:77ab…" },
      "policy": { "installation": "auto", "authentication": "ON_INSTALL", "profiles": ["platform"] },
      "category": "operations",
      "interface": { "displayName": "租户上线核对", "description": "只读核对技能", "brandColor": "#7C3AED" } }
  ]
}
```

## 8.2 字段表

| 字段                                                            | 类型                              | 必填                                   | 说明                                                                                                 |
| ------------------------------------------------------------- | ------------------------------- | ------------------------------------ | -------------------------------------------------------------------------------------------------- |
| `name`                                                        | `^[a-z][a-z0-9-]*$`             | 是                                    | 市场短名；`qian add <market>/<name>` 的前缀                                                                |
| `version`                                                     | `string`                        | 否                                    | 市场文件版本（CalVer 建议）                                                                                  |
| `interface.displayName` / `description` / `homepage` / `icon` | 展示                              | `displayName` 必填                     | 目录网站首页                                                                                             |
| `owner.name` / `url` / `publicKeys[]`                         | —                               | `name` 必填                            | Ed25519 公钥（PEM 路径或内联）；无公钥 = 未签名市场（§11.5）                                                           |
| `categories[]`                                                | `string[]`                      | 否                                    | 条目 `category` 必须在其中（省略则不限）                                                                         |
| `plugins[].name`                                              | `^[a-z][a-z0-9-]*$`             | 是                                    | 市场内唯一的安装名（不是组件 id）                                                                                 |
| `plugins[].source.type`                                       | `local \| url \| git-subdir`    | 是                                    | 来源类型                                                                                               |
| `plugins[].source.path` / `url` / `repo` + `ref` + `path`     | —                               | 按类型                                  | `local`：相对市场仓库根；`url`：tgz / zip，解压根含 `.qian-plugin/plugin.json`；`git-subdir`：仓库、提交 / 标签 / 分支、子目录   |
| `plugins[].source.digest`                                     | `sha256:`                       | `url` 必填；`git-subdir` 的 `ref` 为分支时必填 | 归档或子目录内容哈希（目录 digest = 文件 `路径 + 字节数 + SHA-256` 清单的哈希，沿 v1 D4）                                      |
| `plugins[].policy.installation`                               | `auto \| confirm \| restricted` | 是                                    | `auto` 直接安装；`confirm` 先展示反向权限、密钥需求与表面清单并要求确认（`--yes` 跳过）；`restricted` 只允许平台运营经审核台安装（S3）。值域已裁决（R40） |
| `plugins[].policy.authentication`                             | `ON_INSTALL \| ON_USE`          | 是                                    | 与组件清单一致；不一致以市场为准并告警                                                                                |
| `plugins[].policy.profiles[]`                                 | `string[]`                      | 否                                    | 推荐 profile                                                                                         |
| `plugins[].category`                                          | `string`                        | 是                                    | 分类                                                                                                 |
| `plugins[].interface`                                         | 同清单 `interface` 的子集             | 否                                    | 目录网站用；**不复制**清单其它字段，详情页读组件自身的 `plugin.json` 与 README                                               |

## 8.3 多市场与优先级

项目级 `.qian/config.yml`：

```yaml
defaultProfile: platform
marketplaces:
  - { name: acme-private,  source: "git+https://git.example.internal/platform/qian-market.git#main", priority: 10 }
  - { name: qian-official, source: "git+https://github.com/idadawn/qian-plugins.git#stable", priority: 20 }      # 官方市场仓库 qian-plugins，S1 私有（§13 第 4 条）
  - { name: community,     source: "github-topic:qian-plugin", priority: 90, trust: unverified }
trust:
  keys: ["./.qian/keys/acme.pub"]            # 本项目额外信任的市场公钥
```

`qian add <name>` 按 `priority` 升序逐个市场查找 `plugins[].name`，首个命中即用；多市场同名时告警并列出来源，用 `--from <market>` 或 `<market>/<name>` 指定；`trust: unverified` 的市场只有加 `--allow-unverified` 才参与解析，且只允许安装无代码组件、tier 最高 `community`（§11.5；已裁决 R40）。`qian list --markets` 打印解析顺序。

## 8.4 社区发现：GitHub topic `qian-plugin`

官方市场仓库名为 `qian-plugins`（与 Qian 同组织下的独立仓库，根目录 `.qian/marketplace.json`），S1 私有、S3 随许可证评估公开（ADR-020 §7 第 4 条，已拍板）；社区发现走以下 topic 机制。

公开仓库打上 topic `qian-plugin` 且根目录含 `.qian/marketplace.json`（多组件）或 `.qian-plugin/plugin.json`（单组件仓库）即可被发现。官方目录网站的生成器每日抓取 topic 结果生成只读索引 `community/index.json`（仓库、最近提交、是否签名、组件数、分类）；`source: github-topic:qian-plugin` 读的就是这份索引（CLI 不直接调用 GitHub 搜索 API）。社区条目默认 `trust: unverified`，`tier` 最高只接受 `community`，更高者被索引器降级并标记。

## 8.5 目录网站生成约定

- 输入：一个或多个 `marketplace.json` + 各条目的 `plugin.json`、README、`SKILL.md` front matter、`tools/*.json` 的 `name / description / sideEffect`、`policy.permissions`。
- 输出：静态站点——首页（市场 `interface` + 分类）、分类页、每条目一页（展示信息、`defaultPrompt`、表面清单、工具与副作用等级、反向权限与密钥需求、版本与 digest、安装片段 `qian add <name>` / `npx qian@latest --plugin <name> --yes`、健康徽章）、机器可读 `index.json`。
- 健康徽章来自市场仓库 CI 对每个条目运行 `qian doctor --manifest <path>`；不通过的条目仍展示但标红并禁用安装片段。
- 生成器随官方市场仓库交付（D20.8）；S3 商业市场在 `index.json` 之上叠加价格、授权、审核状态字段，不改既有字段（ADR-014 修订）。
