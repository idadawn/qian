> For AI agents: the complete documentation index is available at /pr-preview/pr-202/llms.txt, the full documentation bundle is available at /pr-preview/pr-202/llms-full.txt.

# 组件清单入口与程序集内多个插件实现

> 本页由构建脚本镜像自仓库 [`docs/decisions/component-entry-metadata.md`](https://github.com/idadawn/qian/blob/main/docs/decisions/component-entry-metadata.md)，请修改源文件，不要改本页。

状态：已确认方案 A。产品负责人于 2026-10-22 在本次执行会话中回复“采用 A：同程序集多组件（推荐）”，由 cody 代录。任务：[S1-DEC-028](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-DEC-028.md)，承接 [S1-RT-033](https://github.com/idadawn/qian/blob/main/pm/tasks/S1-RT-033.md) 验收第 3 条；实现仍由 RT-033 交付。

## 已核对的事实

- 组件规格 §3 的 `runtime.entry` 是一个实现 `IQianPlugin` 的类型名；一个组件文件夹只有一份 `.qian-plugin/plugin.json`。
- 当前生成器要求一个程序集恰好一个 `[QianPlugin]`，构建工具也只取一个程序集元数据工厂。
- `Qian.Capabilities` 中真正对应 `qian.capabilities` 的入口是 `CapabilitiesPlugin`。另四个实现分别承担 OpenAPI / gRPC 契约包装、导航包装、准入失败占位；它们接收其他组件及运行时数据，不能视为四个独立的可安装组件。
- `Qian.Config.Apollo` 同时包含 `ConfigurationPlugin` 与 `ApolloConfigurationPlugin`；仓库已有 `qian.config`、`qian.config.apollo` 两个组件目录和不同 ID。
- `DeclarativeSurfacePlugin` / `HotReloadSurfacePlugin` 是对已校验组件快照的装载适配器；它们的依赖和具名提供项随输入组件而变，仓库没有 `qian.surfaces` 组件目录。`RbacCommandPolicyPlugin` 是 Authz 的宿主组合子插件。
- 因此“所有 IQianPlugin 实现都各建一份清单”不能正确表达现有组件边界。真实入口需要生成清单；装饰器和快照适配器需要可追溯地列为实现构件。

## 方案 A（已确认）：每个组件一个入口，同程序集可含多个组件

1. 保持一个组件 ID、一份清单、一个 `runtime.entry`。不增加 `runtime.entries`，也不让同一组件 ID 指向多个互相竞争的入口。
2. 一个程序集允许多个不同 ID 的 `[QianPlugin]`。生成器逐组件生成元数据；`check|sync` 按目标清单的 ID 选择，重复 ID、找不到匹配项、跨组件能力归属均明确失败。
3. 只有作为组件发布和挂载入口的类型标 `[QianPlugin]`。契约包装器、运行时快照适配器、宿主组合子插件不新增组件 ID、不生成虚构的独立清单；在 SDK 文档及自动检查的分类清单里明确归属，新增实现不得静默漏检。
4. 某组件确需多个实现协作时，以明确的单一组合入口承担生命周期与依赖；生成器不得简单合并程序集里全部 `Inject` / `Provide`，也不得执行插件代码来猜测。
5. 对现有组件分别同步准确的 `scopes`、入口、服务键和代码派生表面。尚未通过真实 `qian add` 门的组件继续保留 `x-skeleton`，不因补齐元数据就宣称安装已验收。

影响：主要改 SDK 生成协议、工具选择逻辑和 CI 发现/分类；组件清单 Schema 与运行时入口字段保持现有格式。`qian.config` 和 `qian.config.apollo` 可以继续共用现有 Provider 包。

## 方案 B（未采用）：每个可发布入口拆一个程序集

每个组件继续只有一个清单和一个入口，但保持“一个程序集一个标注”的限制；把 `qian.config` 与 `qian.config.apollo` 的入口拆到不同包。包装器与快照适配器仍按方案 A 第 3 条分类。

影响：生成器协议改动较少，但需要增加包、调整依赖与发布路径，超出本轮清单同步的直接需要。

## 实施落点

- 把结论写入 ADR-020 补记与组件规格 §3，记录确认来源，再解除 RT-033 的此项阻塞。
- RT-033 仍在 RT-029 合入之后开始实现；RT-035 在 RT-033 合入之后开始实现。
- RT-035 的错误码目录口径按已有 `contracts/v0/README.md` §3.3 处理：组件码留在 Definition 包，不塞入 `qian.runtime` 内置目录；不改变已发布错误码的含义。此项是既有契约的遵守，不作为本次新设计选项。
