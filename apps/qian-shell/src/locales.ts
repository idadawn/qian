import type { PlatformI18n } from '@idadawn/yue-sdk';

/** 外壳自身的词条；外壳与通用文案来自 @idadawn/yue-shell / @idadawn/yue-sdk 的语言包。 */
export function installAppLanguagePacks(i18n: PlatformI18n): void {
  i18n.addLanguagePack('zh-CN', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力导航、有效权限与会话都来自真实平台接口；页面内容由远程模块按需加载。',
    'app.session': '当前会话', 'app.displayName': '显示名', 'app.subject': '主体', 'app.tenant': '租户', 'app.kind': '主体类型', 'app.revision': '身份修订',
    'app.permissions': '有效权限', 'app.capabilities': '可见能力 {{count}} 项', 'app.pages': '可用页面', 'app.refresh': '刷新',
    'app.remoteFailed': '插件未能加载，请联系管理员核对版本与权限。', 'app.capabilitiesUnavailable': '可见能力暂不可用',
    'app.signedOut': '已退出登录。', 'app.configError': '运行时配置无效', 'app.configErrorHelp': '请检查 /app-config.json：apiBase、authBase、bootstrapUrl、auditUrl 必须是同源路径，brand、locale、managed 取值合法，且没有多余字段。',
    'app.sessionInfo': '会话信息', 'app.breadcrumb': '面包屑', 'app.switchWorkspace': '切换工作空间', 'app.platformConsole': '平台控制台', 'app.platformOperations': '平台运营', 'app.tenantWorkspace': '企业工作空间',
    'app.workspaceStatus.pending': '待激活', 'app.workspaceStatus.trial': '试用', 'app.workspaceStatus.active': '正式', 'app.workspaceStatus.grace': '宽限期', 'app.workspaceStatus.suspended': '已冻结',
    'app.welcome.greeting': '你好，{{name}}', 'app.welcome.intro': '{{company}}已开通。完成下面几步，团队就可以开始使用。', 'app.welcome.introGeneric': '从左侧菜单打开要用的应用。',
    'app.welcome.onboarding': '开通引导', 'app.welcome.progress': '已完成 {{done}} / {{total}}', 'app.welcome.done': '（已完成）', 'app.welcome.todo': '（待完成）', 'app.welcome.doneLabel': '已完成',
    'app.welcome.stepAdmin': '创建管理员账号', 'app.welcome.stepAdminHelp': '{{name}} 是企业的管理员。', 'app.welcome.stepMembers': '添加成员', 'app.welcome.stepMembersHelp': '当前 {{count}} / {{limit}} 人', 'app.welcome.stepMembersCount': '当前 {{count}} 人',
    'app.welcome.usageUnavailable': '暂时无法读取成员数。', 'app.welcome.stepApps': '开启第一个应用', 'app.welcome.stepAppsHelp': '从左侧菜单打开已经开启的应用。', 'app.welcome.stepAppsNone': '还没有可用的应用，开启后会出现在左侧菜单。', 'app.welcome.open': '打开{{name}}',
    'app.welcome.trialText': '试用中，还剩 {{count}} 天（{{date}} 到期）。到期后进入宽限期，宽限期内新登录会被拒绝。',
    'app.welcome.graceText': '有效期已于 {{date}} 到期，现处于宽限期：新登录会被拒绝，当前会话也会很快结束。请联系平台运营续期。',
    'app.welcome.company': '企业信息', 'app.welcome.statusLabel': '状态', 'app.welcome.status.pending': '待激活', 'app.welcome.status.trial': '试用', 'app.welcome.status.active': '正常', 'app.welcome.status.grace': '宽限期', 'app.welcome.status.suspended': '已冻结',
    'app.welcome.code': '租户号', 'app.welcome.validUntil': '到期日', 'app.welcome.noExpiry': '长期有效', 'app.welcome.users': '用户', 'app.welcome.unlimited': '不限', 'app.welcome.admin': '管理员',
  });
  i18n.addLanguagePack('zh-TW', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力導覽、有效權限與工作階段都來自真實平台介面；頁面內容由遠端模組按需載入。',
    'app.session': '目前工作階段', 'app.displayName': '顯示名', 'app.subject': '主體', 'app.tenant': '租戶', 'app.kind': '主體類型', 'app.revision': '身分修訂',
    'app.permissions': '有效權限', 'app.capabilities': '可見能力 {{count}} 項', 'app.pages': '可用頁面', 'app.refresh': '重新整理',
    'app.remoteFailed': '外掛未能載入，請聯絡管理員核對版本與權限。', 'app.capabilitiesUnavailable': '可見能力暫不可用',
    'app.signedOut': '已登出。', 'app.configError': '執行期設定無效', 'app.configErrorHelp': '請檢查 /app-config.json：apiBase、authBase、bootstrapUrl、auditUrl 必須是同源路徑，brand、locale、managed 取值合法，且沒有多餘欄位。',
    'app.sessionInfo': '工作階段資訊', 'app.breadcrumb': '麵包屑', 'app.switchWorkspace': '切換工作空間', 'app.platformConsole': '平台控制台', 'app.platformOperations': '平台營運', 'app.tenantWorkspace': '企業工作空間',
    'app.workspaceStatus.pending': '待啟用', 'app.workspaceStatus.trial': '試用', 'app.workspaceStatus.active': '正式', 'app.workspaceStatus.grace': '寬限期', 'app.workspaceStatus.suspended': '已凍結',
    'app.welcome.greeting': '你好，{{name}}', 'app.welcome.intro': '{{company}}已開通。完成下面幾步，團隊就可以開始使用。', 'app.welcome.introGeneric': '從左側選單開啟要用的應用。',
    'app.welcome.onboarding': '開通引導', 'app.welcome.progress': '已完成 {{done}} / {{total}}', 'app.welcome.done': '（已完成）', 'app.welcome.todo': '（待完成）', 'app.welcome.doneLabel': '已完成',
    'app.welcome.stepAdmin': '建立管理員帳號', 'app.welcome.stepAdminHelp': '{{name}} 是企業的管理員。', 'app.welcome.stepMembers': '新增成員', 'app.welcome.stepMembersHelp': '目前 {{count}} / {{limit}} 人', 'app.welcome.stepMembersCount': '目前 {{count}} 人',
    'app.welcome.usageUnavailable': '暫時無法讀取成員數。', 'app.welcome.stepApps': '開啟第一個應用', 'app.welcome.stepAppsHelp': '從左側選單開啟已經啟用的應用。', 'app.welcome.stepAppsNone': '還沒有可用的應用，啟用後會出現在左側選單。', 'app.welcome.open': '開啟{{name}}',
    'app.welcome.trialText': '試用中，還剩 {{count}} 天（{{date}} 到期）。到期後進入寬限期，寬限期內新登入會被拒絕。',
    'app.welcome.graceText': '有效期已於 {{date}} 到期，現處於寬限期：新登入會被拒絕，目前的工作階段也會很快結束。請聯絡平台營運續期。',
    'app.welcome.company': '企業資訊', 'app.welcome.statusLabel': '狀態', 'app.welcome.status.pending': '待啟用', 'app.welcome.status.trial': '試用', 'app.welcome.status.active': '正常', 'app.welcome.status.grace': '寬限期', 'app.welcome.status.suspended': '已凍結',
    'app.welcome.code': '租戶號', 'app.welcome.validUntil': '到期日', 'app.welcome.noExpiry': '長期有效', 'app.welcome.users': '使用者', 'app.welcome.unlimited': '不限', 'app.welcome.admin': '管理員',
  });
  i18n.addLanguagePack('en-US', {
    'app.workspace': 'Workspace', 'app.workspaceHelp': 'Navigation, effective permissions and the session all come from real platform APIs; page content is loaded on demand from remote modules.',
    'app.session': 'Current session', 'app.displayName': 'Display name', 'app.subject': 'Subject', 'app.tenant': 'Tenant', 'app.kind': 'Principal kind', 'app.revision': 'Identity revision',
    'app.permissions': 'Effective permissions', 'app.capabilities': '{{count}} visible capabilities', 'app.pages': 'Available pages', 'app.refresh': 'Refresh',
    'app.remoteFailed': 'The plugin could not be loaded. Ask an administrator to check its version and permissions.', 'app.capabilitiesUnavailable': 'Visible capabilities are unavailable',
    'app.signedOut': 'You have signed out.', 'app.configError': 'Invalid runtime configuration', 'app.configErrorHelp': 'Check /app-config.json: apiBase, authBase, bootstrapUrl and auditUrl must be same-origin paths, brand, locale and managed must be valid, and no other fields are allowed.',
    'app.sessionInfo': 'Session info', 'app.breadcrumb': 'Breadcrumb', 'app.switchWorkspace': 'Switch workspace', 'app.platformConsole': 'Platform console', 'app.platformOperations': 'Platform operations', 'app.tenantWorkspace': 'Company workspace',
    'app.workspaceStatus.pending': 'Pending', 'app.workspaceStatus.trial': 'Trial', 'app.workspaceStatus.active': 'Active', 'app.workspaceStatus.grace': 'Grace period', 'app.workspaceStatus.suspended': 'Suspended',
    'app.welcome.greeting': 'Hello, {{name}}', 'app.welcome.intro': '{{company}} is ready. Finish the steps below and your team can get started.', 'app.welcome.introGeneric': 'Open an app from the menu.',
    'app.welcome.onboarding': 'Getting started', 'app.welcome.progress': '{{done}} of {{total}} done', 'app.welcome.done': '(done)', 'app.welcome.todo': '(to do)', 'app.welcome.doneLabel': 'Done',
    'app.welcome.stepAdmin': 'Create the administrator account', 'app.welcome.stepAdminHelp': '{{name}} is the company administrator.', 'app.welcome.stepMembers': 'Add members',
    'app.welcome.stepMembersHelp': '{{count}} of {{limit}} seats in use', 'app.welcome.stepMembersCount_one': '{{count}} person so far', 'app.welcome.stepMembersCount_other': '{{count}} people so far',
    'app.welcome.usageUnavailable': 'The member count is unavailable.', 'app.welcome.stepApps': 'Turn on your first app', 'app.welcome.stepAppsHelp': 'Open an app that is already on from the menu.', 'app.welcome.stepAppsNone': 'No apps yet; apps appear in the menu once they are turned on.', 'app.welcome.open': 'Open {{name}}',
    'app.welcome.trialText_one': 'Trial: {{count}} day left (ends {{date}}). A grace period follows, during which new sign-ins are refused.',
    'app.welcome.trialText_other': 'Trial: {{count}} days left (ends {{date}}). A grace period follows, during which new sign-ins are refused.',
    'app.welcome.graceText': 'Your subscription ended on {{date}} and is now in its grace period: new sign-ins are refused and current sessions end shortly. Contact the platform team to renew.',
    'app.welcome.company': 'Company', 'app.welcome.statusLabel': 'Status', 'app.welcome.status.pending': 'Pending', 'app.welcome.status.trial': 'Trial', 'app.welcome.status.active': 'Active', 'app.welcome.status.grace': 'Grace period', 'app.welcome.status.suspended': 'Suspended',
    'app.welcome.code': 'Tenant code', 'app.welcome.validUntil': 'Valid until', 'app.welcome.noExpiry': 'No end date', 'app.welcome.users': 'Users', 'app.welcome.unlimited': 'no limit', 'app.welcome.admin': 'Administrator',
  });
}
