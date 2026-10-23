import { createI18n } from '@idadawn/yue-sdk';
import { installShellLanguagePacks } from '@idadawn/yue-shell';

/** 认证页族复用外壳的认证文案，只补页面自身的少量词条；语言取浏览器首选，缺省简体中文。 */
function initialLocale(): 'zh-CN' | 'zh-TW' | 'en-US' {
  const language = typeof navigator === 'undefined' ? '' : navigator.language;
  if (/^zh-(TW|HK|MO|Hant)/i.test(language)) return 'zh-TW';
  if (/^en/i.test(language)) return 'en-US';
  return 'zh-CN';
}

export const i18n = createI18n(initialLocale());
installShellLanguagePacks(i18n);
i18n.addLanguagePack('zh-CN', {
  'identity.invalidEntry': '登录入口无效', 'identity.invalidEntryHelp': '请从应用重新发起登录；本页只接受应用的授权请求。',
  'identity.connectors': '或使用以下方式登录', 'identity.connectorError': '连接器登录未完成，请重试或改用账号密码。',
  'identity.switchHelp': '选择要切换到的租户，切换后当前会话结束。', 'identity.waiting': '正在等待身份提供方返回',
  'identity.bindTitle': '确认绑定', 'identity.bindHelp': '上游身份 {{name}} 尚未绑定账号，请选择处理方式。', 'identity.verifyHelp': '先用账号密码验证身份，再完成绑定。',
  'identity.bindLink': '绑定到已验证账号', 'identity.bindCreate': '创建新账号并绑定', 'identity.bindCancel': '放弃本次登录',
  'identity.flowError': '上游登录失败或已失效。', 'identity.restart': '重新发起登录',
});
i18n.addLanguagePack('zh-TW', {
  'identity.invalidEntry': '登入入口無效', 'identity.invalidEntryHelp': '請從應用重新發起登入；本頁只接受應用的授權請求。',
  'identity.connectors': '或使用以下方式登入', 'identity.connectorError': '連接器登入未完成，請重試或改用帳號密碼。',
  'identity.switchHelp': '選擇要切換到的租戶，切換後目前工作階段結束。', 'identity.waiting': '正在等待身分提供者返回',
  'identity.bindTitle': '確認綁定', 'identity.bindHelp': '上游身分 {{name}} 尚未綁定帳號，請選擇處理方式。', 'identity.verifyHelp': '先用帳號密碼驗證身分，再完成綁定。',
  'identity.bindLink': '綁定到已驗證帳號', 'identity.bindCreate': '建立新帳號並綁定', 'identity.bindCancel': '放棄本次登入',
  'identity.flowError': '上游登入失敗或已失效。', 'identity.restart': '重新發起登入',
});
i18n.addLanguagePack('en-US', {
  'identity.invalidEntry': 'Invalid sign-in entry', 'identity.invalidEntryHelp': 'Start sign-in from the application again; this page only accepts authorization requests from applications.',
  'identity.connectors': 'Or continue with', 'identity.connectorError': 'The connector sign-in did not complete. Try again or use your account password.',
  'identity.switchHelp': 'Choose the tenant to switch to. The current session ends after switching.', 'identity.waiting': 'Waiting for the identity provider',
  'identity.bindTitle': 'Confirm binding', 'identity.bindHelp': 'The upstream identity {{name}} is not bound to an account yet. Choose how to proceed.', 'identity.verifyHelp': 'Verify with your account password first, then complete the binding.',
  'identity.bindLink': 'Bind to the verified account', 'identity.bindCreate': 'Create a new account and bind', 'identity.bindCancel': 'Abandon this sign-in',
  'identity.flowError': 'The upstream sign-in failed or expired.', 'identity.restart': 'Start sign-in again',
});
