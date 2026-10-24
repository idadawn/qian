# @qian/sample-crm-ui（参考应用远程模块）

参考应用 `qian.sample.crm` 的界面（S1-YUE-012，私有，不发包）。MF2 远程制品暴露「客户」「新建客户」两页，由 [Qian 外壳](../../apps/qian-shell/README.md) 按菜单命中按需加载；组件制品里的 UI 清单 `ui/manifest.json` 随本包维护，联调环境的平台插件树直接读它。

## 内容

- `ui/manifest.json`：`federation.manifest` 指向 `/remotes/qian.sample.crm/mf-manifest.json`；两页分别要求 `qian.sample.crm:customer:read` / `customer:write`；`bridge` 声明插槽、可导航路由、通知与两项能力（`customer.list` 只读、`customer.create` 写）。
- `src/customers.tsx`（`CustomersPage`）：经 `bridge.invoke` 调 `qian.sample.crm/customer.list`，加载中、失败、空列表与列表四态；有写权限时显示「新建客户」。
- `src/create-customer.tsx`（`CreateCustomerPage`）：表单经 `bridge.invoke` 调 `customer.create`（写能力，幂等键由外壳补上），成功后回到列表。
- `vite.remote.config.ts`：React、`@idadawn/yue-*` 与插件 SDK 全部由外壳共享（singleton、`import: false`），制品里只有两页自己的代码；产物在 `dist/remote/`，不交付 `index.html`。

## 构建与托管

```bash
pnpm --filter @qian/sample-crm-ui build
```

联调环境（[tools/Qian.Dev.Shell](../../tools/Qian.Dev.Shell/README.md)）经网关的外壳静态托管把 `dist/remote/` 挂在 `/remotes/qian.sample.crm/`；只有本租户实际挂载了参考应用、bootstrap 列出它时，外壳才会请求这些制品。

## 已知限制

两项能力经外壳以登录用户本人执行（S1-YUE-015）：平台表面宿主把验证过的用户主体交给参考应用的身份适配器，命令 PEP 按其角色判定，审计链记 `actor_type = user`。真实流程验收断言甲方列表含开通钩子种下的「示例客户」、新建后回到列表并出现新客户，乙方只读、看不到甲方的客户。这只覆盖与平台表面同进程的参考栈；审计来源通道沿用 `api`，`ui` 归因见 S1-AUD-011。页面在调用失败时仍显示错误提示，不影响外壳。
