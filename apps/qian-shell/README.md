# qian.shell 静态宿主

使用 `@idadawn/qian-runtime` 与 [前端插件 SDK](../../packages/qian-plugin-sdk/README.md) 构成真实 TS 插件树，复用 `@idadawn/yue-shell` 的 AppShell。属于 `platform` 的 shell 组合包，由 gateway 提供静态文件；不是独立 profile。

## 构建与配置

```bash
pnpm --filter @idadawn/qian-runtime build
pnpm --filter @qian/tenancy-ui build:remote
pnpm --filter qian-shell build
node apps/qian-shell/scripts/package-static.mjs
```

结果在 `dist/artifact/`：外壳 `index.html / assets / app-config.js`、官方租户远程 `remotes/qian.tenancy/`、MF 清单与类型归档、文件摘要清单。DEP-008 将此目录装入 platform 镜像并经 gateway 提供；本任务不声称完成部署或供应链验收。

`public/app-config.js` 是可替换的运行时配置：`apiBase`、`bootstrapUrl`、`auditUrl`。三个 URL 只允许同源，不允许 URL 凭据。刷新页面重新读取，不需要重新编译；其中不能写令牌、用户身份、租户授权或插件授权列表。

bootstrap 由可信宿主返回：`context`（tenant、membership、app、locale、theme）、`remotes`（组件 ID、版本、scope、ui 清单）、`mounted / entitled`（`scope|id` 键）、`permissions`。生产宿主负责身份验证及从 registry/权益/权限目录生成这些输入；缺少端口时外壳显示会话不可用。S1-YUE-001 不提供伪造登录态绕过此契约。

全局 `window.__qian.bridge` 只包含受控调用入口；应用实际渲染的远程收到宿主闭包绑定的 bridge。导航根据静态 ui 清单生成，访问页面才调用 `loadRemote`。租户运营远程由 [qian-tenancy-ui](../../packages/qian-tenancy-ui/README.md) 构建，原有独立开发入口保持可用。

## 一键浏览器验收

```bash
pnpm --filter qian-shell e2e
```

需要 .NET 10、Docker 与 Playwright Chromium。脚本自动启动独立的 [Qian.Shell.TestHost](../../tests/Qian.Shell.TestHost/Qian.Shell.TestHost.csproj)，复用共享 PostgreSQL fixture 和正式 NativeData / NativeAudit Provider；随机临时 Bearer 凭据放在仓库外临时文件，只由 Node 服务端读取。静态验收服务器只监听 `127.0.0.1`，固定审计路径通过回环代理写入真实审计链，浏览器不接触凭据。脚本结束（包括失败）向宿主发送 SIGTERM、等待容器释放并删除临时状态。

验收使用带明确权限的 fixture bootstrap 和租户数据响应；真实登录、租户权益与运营 API 端到端接线仍属于各自任务。版本拒绝的审计链是实际 PostgreSQL 写入并回读校验，不以浏览器事件或 JSONL 代替。

Playwright 检查：官方远程按页面请求；共享 React、PlatformClient、SDK 和 Button 引用一致；scope 遮蔽与卸载恢复；租户 B 未挂载时 manifest 请求为零；`engines.yue` 拒绝并在控制台和真实审计链留痕；`mf-manifest.json` 与 DTS 可下载；桥接拒绝伪造 ID 与越权；上下文深冻结；axe critical/serious 为零；首次脚本 gzip 总量不超过 250 KiB。`acceptance-results/` 保存截图、首屏资源记录与不含凭据的链回读证据。验收控制模块只由 `--mode acceptance` 引用，正式制品打包检查其不存在。

## 模型体验与延期项

静态导航与版本化插槽来自统一 SDK 目录；能力执行只走 PlatformClient，不另建后端工具目录。私有 SDK 发包归本任务的正式发布门；生产身份接线、权益源接线和镜像部署分别由 YUE-002、ENT-002、DEP-008 完成。
