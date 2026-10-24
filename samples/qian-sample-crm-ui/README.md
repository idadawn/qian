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

参考应用的命令运行时只认机器主体（服务 / 代理），用户主体经外壳调用两项能力都会被拒绝（403 `AUTHZ_DENIED`）；真实流程验收据此断言页面真实渲染、调用确实经网关发出并被拒绝、页面优雅降级。用户主体执行应用能力不在 S1 范围内。
