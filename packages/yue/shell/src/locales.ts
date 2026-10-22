import type { LanguagePack, Locale, PlatformI18n } from '@idadawn/yue-sdk';

export const shellLanguagePacks: Record<Locale, LanguagePack> = {
  'zh-CN': {
    'shell.nav': '主导航', 'shell.skip': '跳到页面内容', 'shell.collapse': '收起侧栏', 'shell.expand': '展开侧栏',
    'shell.theme': '主题', 'shell.light': '明亮', 'shell.dark': '暗色', 'shell.system': '跟随系统', 'shell.language': '语言',
    'shell.user': '用户菜单', 'shell.signOut': '退出登录', 'shell.tenant': '切换租户', 'shell.managed': '当前处于平台代管模式',
    'shell.login': '登录', 'shell.loginHelp': '使用账号密码或企业单点登录。', 'shell.username': '账号', 'shell.password': '密码',
    'shell.sso': '企业单点登录', 'shell.ssoRedirect': '正在跳转到身份提供方', 'shell.callback': '正在完成登录',
    'shell.authError': '登录失败，请检查账号或重新发起登录。', 'shell.ssoError': '单点登录未完成，请重新发起登录。',
    'shell.chooseTenant': '选择租户', 'shell.chooseTenantHelp': '选择本次登录要使用的租户。', 'shell.backLogin': '返回登录',
    'shell.expired': '登录已过期', 'shell.expiredHelp': '会话刷新失败，请重新登录后继续。', 'shell.reauthenticate': '重新登录',
    'shell.switchError': '租户切换失败，仍保留当前租户。', 'shell.forbidden': '无权访问此页面', 'shell.forbiddenHelp': '请联系管理员确认访问权限。',
    'shell.notFound': '页面不存在', 'shell.notFoundHelp': '页面可能已移动，请返回工作台。', 'shell.serverError': '页面暂时无法使用',
    'shell.serverErrorHelp': '请稍后重试。', 'shell.offline': '网络连接已断开', 'shell.offlineHelp': '检查网络后重试，当前页面内容会保留。',
    'shell.home': '返回工作台', 'shell.update': '有新版本可用', 'shell.reload': '刷新页面',
  },
  'zh-TW': {
    'shell.nav': '主要導覽', 'shell.skip': '跳到頁面內容', 'shell.collapse': '收起側欄', 'shell.expand': '展開側欄',
    'shell.theme': '主題', 'shell.light': '明亮', 'shell.dark': '暗色', 'shell.system': '跟隨系統', 'shell.language': '語言',
    'shell.user': '使用者選單', 'shell.signOut': '登出', 'shell.tenant': '切換租戶', 'shell.managed': '目前處於平台代管模式',
    'shell.login': '登入', 'shell.loginHelp': '使用帳號密碼或企業單一登入。', 'shell.username': '帳號', 'shell.password': '密碼',
    'shell.sso': '企業單一登入', 'shell.ssoRedirect': '正在前往身分提供者', 'shell.callback': '正在完成登入',
    'shell.authError': '登入失敗，請檢查帳號或重新登入。', 'shell.ssoError': '單一登入未完成，請重新登入。',
    'shell.chooseTenant': '選擇租戶', 'shell.chooseTenantHelp': '選擇這次登入要使用的租戶。', 'shell.backLogin': '返回登入',
    'shell.expired': '登入已過期', 'shell.expiredHelp': '工作階段更新失敗，請重新登入後繼續。', 'shell.reauthenticate': '重新登入',
    'shell.switchError': '租戶切換失敗，仍保留目前租戶。', 'shell.forbidden': '無權存取此頁面', 'shell.forbiddenHelp': '請聯絡管理員確認存取權限。',
    'shell.notFound': '頁面不存在', 'shell.notFoundHelp': '頁面可能已移動，請返回工作台。', 'shell.serverError': '頁面暫時無法使用',
    'shell.serverErrorHelp': '請稍後重試。', 'shell.offline': '網路連線已中斷', 'shell.offlineHelp': '檢查網路後重試，目前頁面內容會保留。',
    'shell.home': '返回工作台', 'shell.update': '有新版本可用', 'shell.reload': '重新整理頁面',
  },
  'en-US': {
    'shell.nav': 'Main navigation', 'shell.skip': 'Skip to page content', 'shell.collapse': 'Collapse sidebar', 'shell.expand': 'Expand sidebar',
    'shell.theme': 'Theme', 'shell.light': 'Light', 'shell.dark': 'Dark', 'shell.system': 'System', 'shell.language': 'Language',
    'shell.user': 'User menu', 'shell.signOut': 'Sign out', 'shell.tenant': 'Switch tenant', 'shell.managed': 'Platform administration mode is active',
    'shell.login': 'Sign in', 'shell.loginHelp': 'Use your account or enterprise single sign-on.', 'shell.username': 'Account', 'shell.password': 'Password',
    'shell.sso': 'Enterprise single sign-on', 'shell.ssoRedirect': 'Redirecting to your identity provider', 'shell.callback': 'Completing sign-in',
    'shell.authError': 'Sign-in failed. Check your account or try again.', 'shell.ssoError': 'Single sign-on failed. Start sign-in again.',
    'shell.chooseTenant': 'Choose a tenant', 'shell.chooseTenantHelp': 'Choose a tenant for this session.', 'shell.backLogin': 'Back to sign-in',
    'shell.expired': 'Session expired', 'shell.expiredHelp': 'Your session could not be refreshed. Sign in to continue.', 'shell.reauthenticate': 'Sign in again',
    'shell.switchError': 'Could not switch tenants. Your current tenant is unchanged.', 'shell.forbidden': 'Access denied', 'shell.forbiddenHelp': 'Contact your administrator to check your access.',
    'shell.notFound': 'Page not found', 'shell.notFoundHelp': 'This page may have moved. Return to your workspace.', 'shell.serverError': 'This page is temporarily unavailable',
    'shell.serverErrorHelp': 'Please try again later.', 'shell.offline': 'You are offline', 'shell.offlineHelp': 'Check your connection and retry. Your page content is preserved.',
    'shell.home': 'Back to workspace', 'shell.update': 'An update is available', 'shell.reload': 'Reload page',
  },
};

/** 文案显式注入宿主的 SDK i18n，不创建第二个语言实例。 */
export function installShellLanguagePacks(i18n: PlatformI18n): void {
  for (const locale of Object.keys(shellLanguagePacks) as Locale[]) i18n.addLanguagePack(locale, shellLanguagePacks[locale]);
}
