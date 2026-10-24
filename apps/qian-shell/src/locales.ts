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
  });
  i18n.addLanguagePack('zh-TW', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力導覽、有效權限與工作階段都來自真實平台介面；頁面內容由遠端模組按需載入。',
    'app.platformHelp': '平台身分由平台代管：沒有租戶的選單、權限與能力目錄；切換到某個租戶後再查看。',
    'app.session': '目前工作階段', 'app.displayName': '顯示名', 'app.subject': '主體', 'app.tenant': '租戶', 'app.kind': '主體類型', 'app.revision': '身分修訂',
    'app.permissions': '有效權限', 'app.capabilities': '可見能力 {{count}} 項', 'app.pages': '可用頁面', 'app.refresh': '重新整理',
    'app.remoteFailed': '外掛未能載入，請聯絡管理員核對版本與權限。', 'app.capabilitiesUnavailable': '可見能力暫不可用',
    'app.signedOut': '已登出。', 'app.configError': '執行期設定無效', 'app.configErrorHelp': '請檢查 /app-config.json：apiBase、authBase、bootstrapUrl、auditUrl 必須是同源路徑，brand、locale、managed 取值合法，且沒有多餘欄位。',
  });
  i18n.addLanguagePack('en-US', {
    'app.workspace': 'Workspace', 'app.workspaceHelp': 'Navigation, effective permissions and the session all come from real platform APIs; page content is loaded on demand from remote modules.',
    'app.platformHelp': 'Platform identities are managed by the platform: there is no tenant navigation, permissions or capability catalog. Switch to a tenant to see them.',
    'app.session': 'Current session', 'app.displayName': 'Display name', 'app.subject': 'Subject', 'app.tenant': 'Tenant', 'app.kind': 'Principal kind', 'app.revision': 'Identity revision',
    'app.permissions': 'Effective permissions', 'app.capabilities': '{{count}} visible capabilities', 'app.pages': 'Available pages', 'app.refresh': 'Refresh',
    'app.remoteFailed': 'The plugin could not be loaded. Ask an administrator to check its version and permissions.', 'app.capabilitiesUnavailable': 'Visible capabilities are unavailable',
    'app.signedOut': 'You have signed out.', 'app.configError': 'Invalid runtime configuration', 'app.configErrorHelp': 'Check /app-config.json: apiBase, authBase, bootstrapUrl and auditUrl must be same-origin paths, brand, locale and managed must be valid, and no other fields are allowed.',
  });
}
