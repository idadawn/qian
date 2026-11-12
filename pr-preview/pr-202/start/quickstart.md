> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# 30 分钟上手

目标：一个空白 ASP.NET Core 项目，**不部署 Qian 平台**，装 `identity`、`authz`、`files` 三个组件，照组件 README 在 30 分钟内跑通文件上传与权限检查。这是 S1 的验收标准之一（[ADR-019 D19.8](/pr-preview/pr-202/design/adr/adr-019-library-first-packages.md)），样例工程为 `samples/standalone-minimal`。

:::warning 尚未可用
`qian` CLI 与 `Qian.Files`、`Qian.Authz`、`Qian.Identity.Client` 包随 S1 任务交付。下面的命令描述交付后的用法，字段与行为以 [qian 命令](/pr-preview/pr-202/cli/commands.md) 为准。
:::

## 1. 建项目

```bash
qian init --template dotnet-webapi
```

生成 standalone profile 的 ASP.NET Core 项目：`.qian/config.yml`、`profiles/standalone/qian.profile.yml`、`README.md` 与 `AGENTS.md` 骨架。

## 2. 装三个组件

```bash
qian add identity authz files
```

`qian add` 分五步：在市场里定位组件 → 验签并校验清单 → 打印变更集（复制哪些表面文件、加哪些 NuGet 包、往 `qian.patch.yml` 写哪些条目）→ 执行并记日志 → 健康检查并打印「下一步」。安装结果记进 `.qian/lock.json`，需要提交到仓库。

## 3. 配置、检查与运行

```bash
qian dump-config     # 打印层叠后的整棵插件树
qian doctor          # 校验清单、依赖、secretRef 与文档新鲜度
qian dev --aspire    # 本机运行当前 profile 的宿主，并拉起 PG / Redis / RabbitMQ / Apollo 容器
```

`qian add` 打印的「下一步」会列出待填写的 `secretRef` 与推荐 profile；配置一律写进 `qian.patch.yml`，不改组件源码。

## 下一步

- 三个组件各自怎么配、暴露了什么：[qian.identity](/pr-preview/pr-202/components/platform/qian.identity.md)、[qian.authz](/pr-preview/pr-202/components/platform/qian.authz.md)、[qian.files](/pr-preview/pr-202/components/platform/qian.files.md)
- 同一组包放进平台运行：[宿主与框架](/pr-preview/pr-202/start/hosts.md)
- profile 与 patch 怎么叠加：[profile 与 patch](/pr-preview/pr-202/start/concepts/profile.md)
