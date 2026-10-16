import { createInstance, type i18n } from 'i18next';

export type Locale = 'zh-CN' | 'zh-TW' | 'en-US';
export type LanguagePack = Record<string, string>;
export type I18nKey = string;
export const builtinLanguagePacks: Record<Locale, LanguagePack> = {
  'zh-CN': { 'common.loading': '加载中', 'common.empty': '暂无数据', 'common.error': '加载失败', 'common.retry': '重试', 'common.save': '保存', 'common.cancel': '取消', 'common.confirm': '确认', 'common.close': '关闭', 'common.search': '搜索', 'common.create': '新建', 'common.edit': '编辑', 'common.detail': '详情', 'common.required': '此项必填', 'common.invalid': '输入不符合要求', 'common.conflict': '数据已更新，请刷新后重试', 'common.forbidden': '无权限访问', 'common.unauthorized': '请先登录', 'sdk.error.http': '请求失败', 'sdk.error.contextChanged': '当前租户已切换，请重试' },
  'zh-TW': { 'common.loading': '載入中', 'common.empty': '暫無資料', 'common.error': '載入失敗', 'common.retry': '重試', 'common.save': '儲存', 'common.cancel': '取消', 'common.confirm': '確認', 'common.close': '關閉', 'common.search': '搜尋', 'common.create': '新增', 'common.edit': '編輯', 'common.detail': '詳情', 'common.required': '此項必填', 'common.invalid': '輸入不符合要求', 'common.conflict': '資料已更新，請重新整理後重試', 'common.forbidden': '無權限存取', 'common.unauthorized': '請先登入', 'sdk.error.http': '請求失敗', 'sdk.error.contextChanged': '目前租戶已切換，請重試' },
  'en-US': { 'common.loading': 'Loading', 'common.empty': 'No data', 'common.error': 'Unable to load', 'common.retry': 'Retry', 'common.save': 'Save', 'common.cancel': 'Cancel', 'common.confirm': 'Confirm', 'common.close': 'Close', 'common.search': 'Search', 'common.create': 'Create', 'common.edit': 'Edit', 'common.detail': 'Details', 'common.required': 'This field is required', 'common.invalid': 'Invalid input', 'common.conflict': 'Data has changed. Refresh and try again.', 'common.forbidden': 'Access denied', 'common.unauthorized': 'Sign in required', 'sdk.error.http': 'Request failed', 'sdk.error.contextChanged': 'The current tenant changed. Please retry.' },
};

export interface PlatformI18n {
  instance: i18n;
  t(key: I18nKey, values?: Record<string, string | number>): string;
  changeLanguage(locale: Locale): Promise<void>;
  addLanguagePack(locale: Locale, pack: LanguagePack): void;
  loadLanguagePack(locale: Locale, load: () => Promise<LanguagePack>): Promise<void>;
}

/** 远程加载器由宿主注入，可经 PlatformClient 取数；不产生第二条网络路径。 */
export function createI18n(locale: Locale = 'zh-CN'): PlatformI18n {
  const instance = createInstance();
  void instance.init({ lng: locale, fallbackLng: 'zh-CN', initAsync: false, keySeparator: false,
    interpolation: { escapeValue: false }, resources: Object.fromEntries(Object.entries(builtinLanguagePacks).map(([key, translation]) => [key, { translation: { ...translation } }])) });
  const addLanguagePack = (language: Locale, pack: LanguagePack) => {
    if (!pack || typeof pack !== 'object' || Array.isArray(pack) || Object.values(pack).some((value) => typeof value !== 'string')) throw new TypeError('sdk.error.languagePack');
    instance.addResourceBundle(language, 'translation', pack, true, true);
  };
  return {
    instance,
    t: (key, values) => instance.t(key, values),
    async changeLanguage(language) { await instance.changeLanguage(language); },
    addLanguagePack,
    async loadLanguagePack(language, load) { addLanguagePack(language, await load()); },
  };
}
