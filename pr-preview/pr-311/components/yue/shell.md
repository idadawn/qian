> For AI agents: the complete documentation index is available at /pr-preview/pr-311/llms.txt, the full documentation bundle is available at /pr-preview/pr-311/llms-full.txt.

# 外壳、页面与认证

`@idadawn/yue-shell` 是独立 React 库。以下示例与靶场共用源码，请求均经 SDK 的 mock transport。认证 provider 可替换为真实宿主实现；当前示例不代表真实身份服务已接通。Authentication 仅在显示密码登录时懒加载密码表单，不把它放进 AppShell 首屏；需要同步 SSR 时直接使用 PasswordLogin。

## AppShell 与页面族


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


单一侧栏宽 232px：品牌行（含收起开关）、工作空间卡片、按 `group` 分组的导航（条目可带 `badge` 角标）与底部账号区（姓名、一行说明与账号菜单）；顶栏左侧放面包屑，右侧放通知、主题等工具；语言收在账号菜单的「语言」子菜单（LocaleMenu）。示例里点工作空间卡片切到另一个租户，页头的 TenantSwitcher 同样可以切换；还可以读取受保护数据验证请求上下文、模拟会话过期、模拟渲染失败。预览工具栏可切换默认、暗色、平台代管与离线状态。平台代管横幅常驻；主题与语言偏好由宿主注入。

`Page` 使用 `section`，`AppShell` 拥有唯一 `main`。`PageHeader` 提供标题、说明、面包屑和操作区；`ColPage` 提供左右列；`LeftTree` 是左栏容器，实际树由消费方插入现有 data Tree，不复制树引擎。

### 安装与用法

在项目中安装 `@idadawn/yue-shell` 与 React 19，加载 tokens 的主题 CSS，并让 Tailwind 扫描 yue 源码。私有包源配置沿用 yue 工作区约定。

```tsx
import { createI18n } from '@idadawn/yue-sdk';
import { AppShell, Page, PageHeader, PreferencesProvider, initPreferences, installShellLanguagePacks } from '@idadawn/yue-shell';

const i18n = createI18n();
installShellLanguagePacks(i18n);
const preferences = initPreferences({ namespace: 'my-app' });

export default function Workspace() {
  return <PreferencesProvider store={preferences}>
    <AppShell i18n={i18n} brand="工作台" menu={[]}>
      <Page header={<PageHeader title="概览" />}>页面内容</Page>
    </AppShell>
  </PreferencesProvider>;
}
```

### 布局 API

| 入口 | 属性与约定 |
| ------------------ | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AppShell | 必填 i18n、brand、menu；可选 activePath、registry、onNavigate、logo、workspace（品牌下方，侧栏收起时隐藏）、breadcrumb（顶栏左侧）、toolbar、userName、userDescription、onSignOut、userMenu、managed、managedLabel、children、className、testId；menu 条目可带 group（相邻同组归到一个分组标题下）与 badge |
| Page | header、children、className、testId；默认不增加 main 地标 |
| PageHeader | 必填 title；可选 description、actions、breadcrumb、children、className、testId |
| ColPage | 必填 aside；可选 children、className、testId；窄屏上下排列 |
| LeftTree | 必填 label；可选 children、className、testId；只负责容器 |
| PreferenceControls | 必填 i18n；可选 locale（是否显示语言按钮，语言放进账号菜单时传 false，默认 true）、localeStyle（icon 或 label，label 显示地球图标、当前语言名与箭头，用在认证页）、className、testId；消费 PreferencesProvider |
| WorkspaceCard | 必填 title、tile、label；可选 description、href、onClick、className、testId；按钮名称为 label 加 title 与 description，tile 只作装饰 |
| LocaleMenu | 必填 i18n；可选 testId；放进 AppShell 的 userMenu，与顶栏语言按钮共用同一份偏好 |
| AuthLayout | 必填 brand、headline；可选 brandHref、description、aside、footer、toolbar、legal、variant（sign-in：侧栏 440px 起、内容 400px 宽并垂直居中；form：侧栏 360px 起、内容 720px 宽）、children、className、testId；sign-in 的侧栏标题在品牌行与底部之间垂直居中，传不传 footer 都不会压住底部装饰条；窄屏时品牌面折到上方 |
| PlainLayout | 必填 brand；可选 account（顶栏右侧）、width（sm 600px、md 760px）、children、className、testId |
| BrandMark | 可选 size、inverse、className、testId（默认 yue-brand-mark，data-slot 为 brand-mark）；装饰图形，可访问名称由旁边的品牌名提供 |

### 菜单与 Preferences

`createMenuTypeRegistry()` 复用 SDK Registry，内置 page 与 external。`register(meta, resolver)` 返回注销函数，`resolve(item)` 解析路由，`toJSON()` 导出可索引元数据。menu 条目包含 id、labelKey、type、target 与可选 children。宿主可注入隔离的 registry，默认使用导出的 `menuTypeRegistry`。外链仅允许 HTTP(S)，带 noopener/noreferrer；页面必须是站内绝对路径。插件自定义 resolver 的输出同样校验，URL 凭据与令牌参数被拒绝。权限筛选由宿主提供的菜单决定，前端路由不构成授权边界。

`initPreferences({ namespace, overrides, defaults, storage })` 返回 getSnapshot、getServerSnapshot、subscribe、update、reset。namespace 隔离应用；overrides 优先于存储；defaults 是部署缺省（如运行时配置的语言），低于已保存的偏好。存储只记用户自己改过的键（update 增量合并，reset 写空对象），部署缺省与 overrides 都不写入。只保存 theme（light/dark/system）、locale（zh-CN/zh-TW/en-US）和 sidebarCollapsed，未知字段永不写入存储。storage 可注入或设 null；损坏、受限存储回退到当前会话。SSR 使用稳定初始快照；宿主用 tokens 的首帧脚本与 initializeTheme 应用主题，按需监听 store 更新。PreferencesProvider 注入 store，usePreferences 返回 preferences 与 updatePreferences。

## 认证页族


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


示例用 `frame` 把登录与回调套进 AuthLayout、把选择工作空间套进顶栏带「返回登录」的 PlainLayout，与身份服务登录页是同一组合。演示账号为 `demo`，密码为 `demo-password`。默认/暗色场景可完成密码登录或点击「单点登录」；SSO 实际跳转到本地回调地址，只有 code/state。多租户场景登录后出现工作空间卡片的二次选择；回调失败场景显示错误态并可返回登录。密码登录是 44px 的独立表单，不再依赖 SchemaForm：密码字段掩码并可切换显示，必填校验聚焦到首个错误字段，提交中禁用按钮，失败时清空密码并显示表单内提示。

### 认证 API

| 入口 | 属性与约定 |
| -------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| AuthProvider | signIn(credentials, signal)、selectTenant(id, signal)、switchTenant(id, signal)；可选 beginSso(signal)、completeSso(callback, signal) |
| AuthResult | kind 为 authenticated 时携带 session；kind 为 select-tenant 时携带 tenants；不包含令牌 |
| AuthSession | subjectId、displayName、tenantId、tenants；tenant 条目为 id/name，可选 code（租户号）、platform（平台身份）、admin（企业管理员）、displayName |
| AuthCallback | code、state、error；真实 code/PKCE/state 校验由 provider 完成 |
| Authentication | 必填 provider、i18n、onAuthenticated、onRedirect；可选 callback、onCallbackConsumed、frame（按视图 login / select-tenant / change-password / callback / redirecting 包页面框架）、loginProps、selectionProps、className、testId；不传 frame 时登录等视图限宽 400px、选择工作空间 600px 居中 |
| PasswordLogin | 必填 i18n、onSubmit；可选 onSso、busy、error、title、description、rememberKey（「记住账号」，只把账号存进本机这个键，不存密码）、forgotHref（不传不显示「忘记密码？」）、alternatives（「其他登录方式」下的附加入口）、footer、className、testId；控件 44px，提交后清空密码 |
| SsoCallback | 必填 i18n、provider、callback、onResult、onBack；可选 className、testId；不输出原始异常 |
| TenantSelection | 必填 i18n、tenants、onSelect、onBack；可选 busy、error、applyHref（虚线的「申请开通新企业」入口）、hideBack（宿主把「返回登录」放进顶栏时隐藏列表下方的按钮）、className、testId；每个工作空间一张卡片，卡片按钮的可访问名称就是企业名，租户号与角色作为说明读出 |
| TenantSwitcher | 必填 i18n、provider、session、onChanged；可选 className、testId；失败保留当前租户 |
| SessionExpiredDialog | 必填 open、i18n、onSignIn；可选 testId；显式重新登录后由宿主关闭 |

AuthProvider 内部持有访问令牌与租户选择凭据；UI 不写 localStorage、URL 或 iframe。宿主把 SDK `onSessionExpired` 接到弹窗 open；SDK 负责刷新合并，UI 不再实现刷新队列。认证视图卸载或替换操作会中止旧请求，忽略迟到结果。令牌刷新使用 HttpOnly cookie 的真实接线、OIDC code + PKCE、selection_token、令牌重签与 MF2 宿主由后续外壳任务负责。

## 403、404、500 与离线兜底


本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。


预览工具栏包含四种独立场景，均提供可操作的重试/返回出口；暗色主题也可切换。`FallbackPage` 的 status 为 403、404、500 或 offline；i18n 必填，onRetry/onHome、children、className、testId 可选。错误页不输出堆栈。

`OfflineBanner` 默认监听浏览器 online/offline 事件，宿主也可注入 offline，保留当前页面内容；可选 onRetry。`UpdateBanner` 接收 available 与 onReload。两者都接收 i18n、className、testId。`ShellErrorBoundary` 接收 i18n、children，可选 resetKey、onError、onReset；resetKey 变化或重试可恢复，原始异常只交给宿主 onError。

## 键盘与无障碍

所有控件保留稳定 data-slot/data-testid，并接受字符串 className。侧栏用普通导航链接与 aria-current；折叠按钮有 aria-expanded、aria-controls。Tab 可到跳过链接、租户/偏好选择器与用户菜单；选择器和弹窗复用 core 的键盘与焦点语义。登录验证定位第一个错误字段，多租户选择可用 Tab/Enter；会话过期使用有标题与描述的 alertdialog。场景与靶场都执行真实浏览器交互和 axe critical/serious 零违规检查。

## 适用边界

库本身不提供平台业务页面，不直接接网关或身份服务器，不部署 profile；qian.shell 消费此库。验证码登录与 Captcha 留待对应可选范围；「忘记密码？」只在宿主传 forgotHref 时出现，重置流程由宿主提供。七种布局与偏好抽屉不在 S1。
