import { describe, expect, it } from 'vitest';
import { builtinLanguagePacks, createI18n } from './i18n';

describe('i18n', () => {
  it('三种内置语言 key 一致且实例互不影响', async () => {
    const first = createI18n();
    const second = createI18n('en-US');
    expect(first.t('common.save')).toBe('保存');
    expect(second.t('common.save')).toBe('Save');
    await first.changeLanguage('zh-TW');
    expect(first.t('common.save')).toBe('儲存');
    expect(second.t('common.save')).toBe('Save');
    for (const pack of Object.values(builtinLanguagePacks)) expect(Object.keys(pack).sort()).toEqual(Object.keys(builtinLanguagePacks['zh-CN']).sort());
  });

  it('加载远程语言包、覆盖与插值，不替换调用方 t(key)', async () => {
    const i18n = createI18n('en-US');
    await i18n.loadLanguagePack('en-US', async () => ({ 'page.welcome': 'Welcome, {{name}}', 'common.save': 'Save changes' }));
    expect(i18n.t('page.welcome', { name: 'Cody' })).toBe('Welcome, Cody');
    expect(i18n.t('common.save')).toBe('Save changes');
    expect(i18n.t('common.cancel')).toBe('Cancel');
  });

  it('远程失败保留已有语言包；结构错误不会装载', async () => {
    const i18n = createI18n();
    await expect(i18n.loadLanguagePack('zh-CN', async () => { throw new Error('offline'); })).rejects.toThrow('offline');
    expect(i18n.t('common.save')).toBe('保存');
    expect(() => i18n.addLanguagePack('zh-CN', { invalid: 1 } as unknown as Record<string, string>)).toThrow('sdk.error.languagePack');
  });
});
