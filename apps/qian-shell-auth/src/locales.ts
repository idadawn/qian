import type { PlatformI18n } from '@idadawn/yue-sdk';

/** 薄入口自身的词条；外壳与通用文案来自 @idadawn/yue-shell / @idadawn/yue-sdk 的语言包。 */
export function installAppLanguagePacks(i18n: PlatformI18n): void {
  i18n.addLanguagePack('zh-CN', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力导航、有效权限与会话都来自真实平台接口；页面内容由远程模块按需加载。',
    'app.session': '当前会话', 'app.displayName': '显示名', 'app.subject': '主体', 'app.tenant': '租户', 'app.kind': '主体类型', 'app.revision': '身份修订',
    'app.permissions': '有效权限', 'app.capabilities': '可见能力 {{count}} 项', 'app.pages': '可用页面', 'app.refresh': '刷新',
    'app.remotePending': '页面内容由该组件的远程模块按需加载；远程模块宿主与插槽由 S1-YUE-001 接线。',
    'app.signedOut': '已退出登录。', 'app.configError': '运行时配置无效', 'app.configErrorHelp': '请检查 /app-conf.js 的 apiBase、authBase、brand、locale 与 managed 字段。',
  });
  i18n.addLanguagePack('zh-TW', {
    'app.workspace': '工作台', 'app.workspaceHelp': '能力導覽、有效權限與工作階段都來自真實平台介面；頁面內容由遠端模組按需載入。',
    'app.session': '目前工作階段', 'app.displayName': '顯示名', 'app.subject': '主體', 'app.tenant': '租戶', 'app.kind': '主體類型', 'app.revision': '身分修訂',
    'app.permissions': '有效權限', 'app.capabilities': '可見能力 {{count}} 項', 'app.pages': '可用頁面', 'app.refresh': '重新整理',
    'app.remotePending': '頁面內容由該元件的遠端模組按需載入；遠端模組宿主與插槽由 S1-YUE-001 接線。',
    'app.signedOut': '已登出。', 'app.configError': '執行期設定無效', 'app.configErrorHelp': '請檢查 /app-conf.js 的 apiBase、authBase、brand、locale 與 managed 欄位。',
  });
  i18n.addLanguagePack('en-US', {
    'app.workspace': 'Workspace', 'app.workspaceHelp': 'Navigation, effective permissions and the session all come from real platform APIs; page content is loaded on demand from remote modules.',
    'app.session': 'Current session', 'app.displayName': 'Display name', 'app.subject': 'Subject', 'app.tenant': 'Tenant', 'app.kind': 'Principal kind', 'app.revision': 'Identity revision',
    'app.permissions': 'Effective permissions', 'app.capabilities': '{{count}} visible capabilities', 'app.pages': 'Available pages', 'app.refresh': 'Refresh',
    'app.remotePending': 'Page content is loaded on demand from the component remote module; the remote host and slots are wired by S1-YUE-001.',
    'app.signedOut': 'You have signed out.', 'app.configError': 'Invalid runtime configuration', 'app.configErrorHelp': 'Check the apiBase, authBase, brand, locale and managed fields of /app-conf.js.',
  });
}
