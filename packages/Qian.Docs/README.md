# Qian.Docs

[S1-RT-010](../../pm/tasks/S1-RT-010.md) 的构建期文档生成 Provider。Definition 在 `Qian.Docs.Abstractions`；本包只依赖 Runtime 与 Definition，通过 `IProfileLoader` 消费装配结果，不引用装配 Provider、不执行目标代码。

## 装配契约

文件夹包含配置、模块、事件和 seam 文档生成逻辑及 C# 语义分析器。适用于任意具名 profile，角色为构建期 `IDocumentGenerator` Provider；不是新的业务插件，不向 Runtime 强加服务键。未来可由 `qian.docs` 组件包装并经 `qian add` 安装，当前通过仓库 CLI 使用，不声明已发布包。

```bash
pnpm install
pnpm --filter qian... build
pnpm exec qian docs generate --profile platform --json
```

命令输出 `docs/generated/config-catalog.md`、`module-graph.md`、`event-graph.md`、`seam-graph.md`。配置、来源层、模块顺序与服务键连接都来自实际 profile/patch 解析；可重复 `--patch`。文档生成默认关闭 home 层，显式 `--home` 才启用，避免个人环境悄悄改变仓库文档。`--out` 只能指向工作区内的目录。

`docs/generated/` 受 G2 文档新鲜度门禁约束：documentation 工作流按当前源码重新生成后，与提交内容有任何差异（含新文件）即失败。改动 profile/patch、配置 Schema 或事件代码时，重新运行上面的命令并一并提交；也可从失败运行上传的 `derived-docs-and-events` 产物取回生成结果。生成结果不含时间、随机值或绝对路径，同一输入在 macOS 与 Linux 上逐字节一致。

配置目录包含生效配置与原始 Schema，敏感 Schema 的 default/examples/const/enum 不导出，secretRef 从不解析。模块和 seam 图是静态装配计划；业务骨架保留明确说明，不作为已运行 Provider 的证据。

## 事件代码分析

事件值使用 `QianEvent.Define<TPayload,TResult>(name, mode)` 或构造器；可用 `[QianEvent(Mode = DispatchMode.Emit)]` 标注字段/属性，特性与初始化值必须一致。生成器默认扫描 `packages`、`plugins`、`src` 中的 C# 文件，排除 tests、bin、obj、node_modules、dist；可重复 `--source <目录>` 指定扫描范围。源码目录与文件不能是符号链接。

Roslyn 语义模型绑定真实 `IPluginContext.On` / `DispatchAsync`，支持 namespace/type alias、命名参数、只读字段/属性别名和未被改写的局部变量。声明重复、静态事件注册模式不一致、静态事件的 mode 无法确定、源码语法错误均使 CLI 退出 1；返回 ErrorDetail 与已解析的生产方/消费方映射，不覆盖上一份有效文档。字符串、注释与其它类型的同名方法不当成事件调用。

动态事件参数、外部二进制事件或可写事件字段无法完整建立静态映射，输出明确 warning，不列为已验证调用。运行时仍执行自身的模式一致性检查。生成器不会执行编译产物、插件构造器或 MSBuild 目标来推测事件。

## 文档模板

CLI 的 `renderDocumentationTemplates` 生成 README 与 AGENTS，两份均含「模型体验」「已知限制与延期项」。`qian init --template skill-only --id <组件id> --name <名称>` 实际消费同一模板，生成无代码组件、技能、minimal 配置；已有文件会拒绝覆盖。代码组件模板与完整安装编排继续由 S1-RT-005 接入。

## 模型体验

`DocumentResult` 返回四份 Markdown、事件声明及生产/消费位置、带文件/行号的结构化诊断。生成内容不包含随机时间；事件模式错误可以直接定位到声明或调用处。CLI 校验失败时保留旧文档，并在标准错误中返回映射，供 CI 或模型修复。

## 已知限制与延期项

- 事件扫描目前消费 C# 源码；TS 运行时尚待 S1-RT-003，不虚构其事件 API 或调用关系。
- 静态分析不执行目标项目，也不替代完整编译；依赖外部二进制、动态参数的调用标为未解析。
- 当前 CLI 从仓库构建随附 .NET 工具，需要 .NET 10 SDK。npm 分发时的工具打包由 CLI 发布任务处理。
- 本任务接入事件模式失败门禁；生成文档新鲜度比对由 S1-QA-009 接入 documentation 工作流，README / AGENTS.md 必需节、第二份清单与 plugin.json 派生字段等其余全仓门禁由 S1-QA-006 接入。
- 未实现其它 init 模板、包安装与发布，也不把四份派生文档替代手写的模块主文档。
