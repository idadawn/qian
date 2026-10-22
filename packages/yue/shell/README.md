# @idadawn/yue-shell

AppShell 单一侧栏布局、菜单 → 路由、MF2 宿主与插槽、租户切换器、认证页族与兜底页。

- 分层与允许依赖：[yue 专题规格 §3.1](../../../docs/prd/specs/yue.md#31-包结构与依赖方向)；依赖方向由 `packages/yue/boundaries.config.mjs` 与 CI 门禁强制。
- 实现任务：S1-YUE-006；库 API、共用示例与认证边界见 [组件文档源](docs/index.mdx)。

## 使用与职责

从包入口导入 AppShell、Page/PageHeader/ColPage/LeftTree、菜单注册表、Preferences 与认证页族。先用 SDK createI18n 建实例并调用 installShellLanguagePacks，再用 PreferencesProvider 注入 initPreferences 的结果。主题应用复用 tokens 的首帧脚本与 initializeTheme；不在库内复制主题或路由引擎。

AuthProvider 由宿主注入，认证页族不持有令牌。登录表单使用 form SchemaForm，租户切换成功后由宿主更新 session 与 SDK 的 getTenantId；SDK onSessionExpired 驱动 SessionExpiredDialog。Preferences 只允许非敏感字段，菜单解析校验外链协议与令牌参数。

## 模型体验

menuTypeRegistry.toJSON 可导出菜单类型元数据，注册返回可撤销的释放函数；控件暴露稳定 data-slot/data-testid。示例与验收状态登记在 docs/scenarios.json，Rspress 与靶场消费同一份 React 源码。靶场 `/shell.html` 默认展示已登录的布局，`?component=Authentication&state=MultiTenant` 可测试二次选择。请求走 SDK mock，不访问真实身份服务。

## 验证

组件级 browser/SSR 与注册表/Preferences 单测位于 src，页面交互位于 apps/yue-playground/e2e/shell.spec.ts，文档场景位于 website/e2e/shell.spec.ts。运行仓库 pnpm yue:gates；生成目录、dist、测试结果不提交。

发布形状使用 automatic JSX 与保留模块的 ESM，保持密码表单的动态 import 边界。仓库安装依赖并构建后运行 `node packages/yue/shell/scripts/verify-package.mjs`：脚本实际打包六个依赖包、在临时目录离线安装 tarball、构建独立消费者并用 Chromium 验证已登录首屏不加载密码表单、进入认证页才加载且无运行时错误。它不使用源码 alias 或 React 全局兜底；报告与消费者保留在脚本输出的临时目录。首次运行需已安装 Playwright Chromium。

## 已知限制与延期项

- 本任务是库交付；qian.shell 的 MF2 宿主、真实 OIDC code + PKCE、selection_token、Cookie 与令牌重签由 S1-YUE-001/002 接线。版本号沿用工作区 0.1.0，尚未发布。
- 验证码登录与忘记密码为 P1，Captcha 为 P2，七种布局与偏好抽屉为后续范围。
