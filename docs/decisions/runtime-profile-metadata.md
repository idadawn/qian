# 真实组件清单启用后的开发 profile 与 AOT 验收

状态：已确认方案 A。产品负责人于 2026-10-22 在本次执行会话中明确回复“开发模板暂用 JIT，保留最终 AOT 门（推荐）”，由 cody 代录。任务：[S1-DEC-029](../../pm/tasks/S1-DEC-029.md)，影响 [S1-RT-033](../../pm/tasks/S1-RT-033.md) 的清单、profile 与回归验收。

## 现有证据

RT-033 已从真实代码生成 14 个组件清单，并显式分类 18 个实现构件。Profiles 测试发现原骨架清单隐藏了以下差异：

| 开发模板 | 当前文件与真实代码的差异 | 所需修正 |
|---|---|---|
| gateway | `build.aot: true`，包含 Apollo 与 Audit；两者没有本组件的 NativeAOT 发布运行证据。Audit 的真实入口必需 `Data`，组合包却没有 Data 条目 | 先决定当前开发模板是否可以暂用 JIT；无论哪种发布模式，都必须补全实际依赖 |
| standalone | 默认装入 Tenancy，真实入口必需 `Data`，组合包却没有 Data | 补 Data 条目，使用宿主自身连接配置；保持 minimal 为空 |
| identity | IdentityServer 的 `issuer` 及 OIDC 必填配置仍是空对象 | 使用已有文档中的示例地址和 secretRef 补足模板，并校验出网声明；不配置真实凭据 |

现有 AOT profile 门禁会拒绝没有兼容声明的代码组件，这是正确行为。不能为通过测试给组件虚标 `x-aot: true`，也不能删除依赖或忽略已派生的配置 Schema。

已有 [S1-DEP-008](../../pm/tasks/S1-DEP-008.md) 明确要求真实 gateway 镜像为 AOT distroless，并以真实业务宿主和完整镜像证据验收；该任务与 [S1-GW-001](../../pm/tasks/S1-GW-001.md) 仍是 todo。供应链自检中的 AOT 镜像只证明工具链，不证明 Apollo、Audit 或真实 gateway 的兼容性。

2026-10-22 确认前的整合结果：全仓 Release 构建零警告、零错误；42 个包的依赖门、57 项 SDK/工具测试、14 个组件与 18 个实现构件的清单检查均通过。全仓 1131 项测试中 1127 通过、4 项失败、零跳过，失败全部是上表三种 profile 的装配检查及 Tenancy 对相同 profile 的断言；Python 92 项门禁唯一失败也来自相同 profile 检查。网站 73 项单测、161 项浏览器验收和构建产物检查通过。这些证据支持将问题收敛到本决策，但不构成 gateway AOT 已验证的证明。

## 方案 A（已确认）：当前开发模板如实用 JIT，保留最终 AOT 必过门

1. 仅把当前 `profiles/gateway/qian.profile.yml` 的 `build.aot` 改为 `false`，并在 profile 描述、ADR-020 与组件规格 §7.7 说明当前开发装配为 JIT，尚未交付 gateway NativeAOT。
2. gateway 与 standalone 的组合包各补 `qian.data` 条目，配置使用空对象，连接由宿主按 Data 现有约定提供；identity 补合法示例配置。此修正不添加部署、数据库或凭据。
3. AOT 通用门禁保持不变，继续用正反例证明 `build.aot: true` 时拒绝未验证组件。
4. S1-GW-001 / S1-DEP-008 增补显式接续要求：确定真实 gateway 的 Config / Audit / Data 依赖组合，完成 Linux NativeAOT 发布及运行证明，再恢复默认模板 `build.aot: true` 和相应组件兼容声明。DEP-008 的 AOT distroless 验收不得删除、提前勾选或被本次 JIT 检查替代。
5. RT-033 本轮交付如实的代码清单和可校验开发模板，不声称 gateway 业务宿主、AOT 或生产发布已完成。

影响：当前开发模板的默认发布模式发生显式变更，但保留已定的 gateway 最终 AOT 目标；可继续按 RT-033 → RT-035 串行完成本轮目标。

## 方案 B（未采用）：保持当前模板 AOT，先补齐完整兼容实现和证据

保留 `build.aot: true`，先完成 gateway 所需 Apollo、Audit、Data 或替代 Provider 的真实 NativeAOT 发布运行验收，再允许组件声明兼容。不得放宽门禁或仅凭静态配置推断兼容。

影响：本轮先增加 gateway AOT 实现与验证工作；RT-033 的最终回归及其后的 RT-035 等待该前置完成。具体 Provider 边界若有调整，按既有仓库规则另行建任务和记录决策。

## 实施验收

- 三处模板与真实组件依赖、配置 Schema 一致，五个默认 profile 的检查按确认后的模式执行。
- AOT 拒绝未验证组件的反例继续有效，镜像供应链检查不以开发模板的 JIT 结果代替最终 gateway AOT 证据。
- 全仓构建、测试、清单新鲜度、文档与任务门禁通过后才合并 RT-033。

实施验证（2026-10-22）：方案 A 与新合入主线的 Identity.Client 元数据接入完成后，全仓 1191/1191、零跳过；Python 92/92、43 包依赖门、15 个组件与 19 个实现构件的清单检查通过。五个默认 profile 与 AOT 正反例均通过，网站构建、产物检查及 161 项浏览器验收通过。此结果证明开发模板与真实清单一致，不替代 S1-GW-001 / S1-DEP-008 的 gateway NativeAOT 验收。
