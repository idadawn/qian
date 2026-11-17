> For AI agents: the complete documentation index is available at /pr-preview/pr-50/llms.txt, the full documentation bundle is available at /pr-preview/pr-50/llms-full.txt.

# scope

**scope** 是任一注册的归属：`global`，或恰好一个 `<kind>:<id>`，`kind` 取 `tenant`、`app`、`agent-session`。

- **两层扁平**：没有父指针。`app:crm-7` 属于哪个租户是数据（由租户组件回答），不是 scope 结构。
- **遮蔽**：在某个 scope 里解析服务时先找该 scope 内的注册，命中即遮蔽全局项，未命中再找全局。
- **restrict**：为某个 scope 过滤全局集合；被过滤的项不出现在列表、能力目录与提示词里，调用时与「不存在」返回完全相同的结果。
- **不可区分**：被 scope 过滤或未挂载的组件，对业务与模型表面一律 404；只有持管理权限的调用者能在管理面看到原因。
- **无继承**：`agent-session:s-9` 不会自动看到 `tenant:t-001` 的注册，需要时显式复制。
- **租户开关**：启用 = 在 `tenant:<id>` 挂载条目，停用 = `disabled: true`，定制 = 替换该条目的 `config`。

详见[组件模型规格](/pr-preview/pr-50/design/prd/specs/component-model.md) §5.5。
