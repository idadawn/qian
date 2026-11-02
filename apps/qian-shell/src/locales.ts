import type { PlatformI18n } from '@idadawn/yue-sdk';

/** 外壳自身的词条；外壳与通用文案来自 @idadawn/yue-shell / @idadawn/yue-sdk 的语言包。 */
export function installAppLanguagePacks(i18n: PlatformI18n): void {
  i18n.addLanguagePack('zh-CN', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力导航、有效权限与会话都来自真实平台接口；页面内容由远程模块按需加载。',
    'app.platformHelp': '平台身份由平台代管：没有租户的菜单、权限与能力目录；切换到某个租户后再查看。',
    'app.session': '当前会话', 'app.displayName': '显示名', 'app.subject': '主体', 'app.tenant': '租户', 'app.kind': '主体类型', 'app.revision': '身份修订',
    'app.permissions': '有效权限', 'app.capabilities': '可见能力 {{count}} 项', 'app.pages': '可用页面', 'app.refresh': '刷新',
    'app.remoteFailed': '插件未能加载，请联系管理员核对版本与权限。', 'app.capabilitiesUnavailable': '可见能力暂不可用',
    'app.signedOut': '已退出登录。', 'app.configError': '运行时配置无效', 'app.configErrorHelp': '请检查 /app-config.json：apiBase、authBase、bootstrapUrl、auditUrl 必须是同源路径，brand、locale、managed 取值合法，且没有多余字段。',
    'app.welcome.greeting': '欢迎，{{name}}', 'app.welcome.intro': '{{company}}已开通，下面几步帮你开始使用。', 'app.welcome.onboarding': '开通引导', 'app.welcome.done': '（已完成）', 'app.welcome.todo': '（待完成）',
    'app.welcome.stepAdmin': '创建管理员账号', 'app.welcome.stepAdminHelp': '{{name}} 是企业的管理员。', 'app.welcome.stepMembers': '邀请同事加入', 'app.welcome.stepMembersHelp': '当前 {{count}} 人。',
    'app.welcome.usageUnavailable': '暂时无法读取成员数。', 'app.welcome.stepApps': '开始使用应用', 'app.welcome.stepAppsHelp': '从左侧菜单打开已经安装的应用。', 'app.welcome.stepAppsNone': '还没有可用的应用，安装后会出现在左侧菜单。', 'app.welcome.open': '打开{{name}}',
    'app.welcome.trialTitle': '试用中，还剩 {{count}} 天', 'app.welcome.trialText': '试用到期日 {{date}}。到期后进入宽限期，期间仍可使用；如需正式开通，请联系平台运营。',
    'app.welcome.graceTitle': '已进入宽限期', 'app.welcome.graceText': '有效期已于 {{date}} 到期，宽限期内仍可使用，请尽快联系平台运营续期。',
    'app.welcome.company': '企业信息', 'app.welcome.status.pending': '待激活', 'app.welcome.status.trial': '试用中', 'app.welcome.status.active': '正常', 'app.welcome.status.grace': '宽限期', 'app.welcome.status.suspended': '已冻结',
    'app.welcome.name': '企业名称', 'app.welcome.code': '租户号', 'app.welcome.validUntil': '到期日', 'app.welcome.noExpiry': '长期有效', 'app.welcome.users': '用户', 'app.welcome.unlimited': '不限', 'app.welcome.admin': '管理员',
  });
  i18n.addLanguagePack('zh-TW', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力導覽、有效權限與工作階段都來自真實平台介面；頁面內容由遠端模組按需載入。',
    'app.platformHelp': '平台身分由平台代管：沒有租戶的選單、權限與能力目錄；切換到某個租戶後再查看。',
    'app.session': '目前工作階段', 'app.displayName': '顯示名', 'app.subject': '主體', 'app.tenant': '租戶', 'app.kind': '主體類型', 'app.revision': '身分修訂',
    'app.permissions': '有效權限', 'app.capabilities': '可見能力 {{count}} 項', 'app.pages': '可用頁面', 'app.refresh': '重新整理',
    'app.remoteFailed': '外掛未能載入，請聯絡管理員核對版本與權限。', 'app.capabilitiesUnavailable': '可見能力暫不可用',
    'app.signedOut': '已登出。', 'app.configError': '執行期設定無效', 'app.configErrorHelp': '請檢查 /app-config.json：apiBase、authBase、bootstrapUrl、auditUrl 必須是同源路徑，brand、locale、managed 取值合法，且沒有多餘欄位。',
    'app.welcome.greeting': '歡迎，{{name}}', 'app.welcome.intro': '{{company}}已開通，下面幾步幫你開始使用。', 'app.welcome.onboarding': '開通引導', 'app.welcome.done': '（已完成）', 'app.welcome.todo': '（待完成）',
    'app.welcome.stepAdmin': '建立管理員帳號', 'app.welcome.stepAdminHelp': '{{name}} 是企業的管理員。', 'app.welcome.stepMembers': '邀請同事加入', 'app.welcome.stepMembersHelp': '目前 {{count}} 人。',
    'app.welcome.usageUnavailable': '暫時無法讀取成員數。', 'app.welcome.stepApps': '開始使用應用', 'app.welcome.stepAppsHelp': '從左側選單開啟已經安裝的應用。', 'app.welcome.stepAppsNone': '還沒有可用的應用，安裝後會出現在左側選單。', 'app.welcome.open': '開啟{{name}}',
    'app.welcome.trialTitle': '試用中，還剩 {{count}} 天', 'app.welcome.trialText': '試用到期日 {{date}}。到期後進入寬限期，期間仍可使用；如需正式開通，請聯絡平台營運。',
    'app.welcome.graceTitle': '已進入寬限期', 'app.welcome.graceText': '有效期已於 {{date}} 到期，寬限期內仍可使用，請儘快聯絡平台營運續期。',
    'app.welcome.company': '企業資訊', 'app.welcome.status.pending': '待啟用', 'app.welcome.status.trial': '試用中', 'app.welcome.status.active': '正常', 'app.welcome.status.grace': '寬限期', 'app.welcome.status.suspended': '已凍結',
    'app.welcome.name': '企業名稱', 'app.welcome.code': '租戶號', 'app.welcome.validUntil': '到期日', 'app.welcome.noExpiry': '長期有效', 'app.welcome.users': '使用者', 'app.welcome.unlimited': '不限', 'app.welcome.admin': '管理員',
  });
  i18n.addLanguagePack('en-US', {
    'app.workspace': 'Workspace', 'app.workspaceHelp': 'Navigation, effective permissions and the session all come from real platform APIs; page content is loaded on demand from remote modules.',
    'app.platformHelp': 'Platform identities are managed by the platform: there is no tenant navigation, permissions or capability catalog. Switch to a tenant to see them.',
    'app.session': 'Current session', 'app.displayName': 'Display name', 'app.subject': 'Subject', 'app.tenant': 'Tenant', 'app.kind': 'Principal kind', 'app.revision': 'Identity revision',
    'app.permissions': 'Effective permissions', 'app.capabilities': '{{count}} visible capabilities', 'app.pages': 'Available pages', 'app.refresh': 'Refresh',
    'app.remoteFailed': 'The plugin could not be loaded. Ask an administrator to check its version and permissions.', 'app.capabilitiesUnavailable': 'Visible capabilities are unavailable',
    'app.signedOut': 'You have signed out.', 'app.configError': 'Invalid runtime configuration', 'app.configErrorHelp': 'Check /app-config.json: apiBase, authBase, bootstrapUrl and auditUrl must be same-origin paths, brand, locale and managed must be valid, and no other fields are allowed.',
    'app.welcome.greeting': 'Welcome, {{name}}', 'app.welcome.intro': '{{company}} is ready. These steps help you get started.', 'app.welcome.onboarding': 'Getting started', 'app.welcome.done': '(done)', 'app.welcome.todo': '(to do)',
    'app.welcome.stepAdmin': 'Create the administrator account', 'app.welcome.stepAdminHelp': '{{name}} is the company administrator.', 'app.welcome.stepMembers': 'Invite your colleagues',
    'app.welcome.stepMembersHelp_one': '{{count}} person so far.', 'app.welcome.stepMembersHelp_other': '{{count}} people so far.',
    'app.welcome.usageUnavailable': 'The member count is unavailable.', 'app.welcome.stepApps': 'Start using an app', 'app.welcome.stepAppsHelp': 'Open an installed app from the menu.', 'app.welcome.stepAppsNone': 'No apps yet; installed apps appear in the menu.', 'app.welcome.open': 'Open {{name}}',
    'app.welcome.trialTitle_one': '{{count}} day left in your trial', 'app.welcome.trialTitle_other': '{{count}} days left in your trial',
    'app.welcome.trialText': 'Your trial ends on {{date}}. A grace period follows in which you can keep working; contact the platform team to switch to a paid plan.',
    'app.welcome.graceTitle': 'Grace period', 'app.welcome.graceText': 'Your subscription ended on {{date}}. You can keep working during the grace period; contact the platform team to renew.',
    'app.welcome.company': 'Company', 'app.welcome.status.pending': 'Pending', 'app.welcome.status.trial': 'Trial', 'app.welcome.status.active': 'Active', 'app.welcome.status.grace': 'Grace period', 'app.welcome.status.suspended': 'Suspended',
    'app.welcome.name': 'Name', 'app.welcome.code': 'Tenant code', 'app.welcome.validUntil': 'Valid until', 'app.welcome.noExpiry': 'No end date', 'app.welcome.users': 'Users', 'app.welcome.unlimited': 'no limit', 'app.welcome.admin': 'Administrator',
  });
}
