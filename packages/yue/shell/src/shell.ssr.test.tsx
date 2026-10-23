import { renderToString } from 'react-dom/server';
import { describe, expect, it, vi } from 'vitest';
import { AppShell, Authentication, ColPage, createMenuTypeRegistry, FallbackPage, initPreferences, LeftTree, OfflineBanner, Page, PageHeader, PasswordLogin, PreferenceControls, PreferencesProvider, safeNavigationUrl, SessionExpiredDialog, ShellErrorBoundary, SsoCallback, TenantSelection, TenantSwitcher, UpdateBanner, usePreferences } from './index';
import { makeI18n, makeProvider, session } from './test/fixture';

describe('菜单类型注册表', () => {
  it('页面、外链、自定义类型都经 SDK 注册；注销与重复登记可观测', () => {
    const registry = createMenuTypeRegistry();
    expect(registry.toJSON().map(item => item.id)).toEqual(['page', 'external']);
    expect(registry.resolve({ id: 'x', labelKey: 'x', type: 'page', target: '/app?a=1' })).toEqual({ href: '/app?a=1' });
    expect(registry.resolve({ id: 'x', labelKey: 'x', type: 'external', target: 'https://example.test/docs' })).toEqual({ href: 'https://example.test/docs', external: true });
    const remove = registry.register({ id: 'report', title: 'reports' }, item => ({ href: `/reports/${encodeURIComponent(item.target)}` }));
    expect(registry.resolve({ id: 'x', labelKey: 'x', type: 'report', target: 'a/b' }).href).toBe('/reports/a%2Fb');
    expect(() => registry.register({ id: 'report', title: 'duplicate' }, () => ({ href: '/' }))).toThrow();
    expect(JSON.parse(JSON.stringify(registry.toJSON()))).toHaveLength(3);
    remove(); expect(() => registry.resolve({ id: 'x', labelKey: 'x', type: 'report', target: 'a' })).toThrow();
  });
  it.each(['javascript:alert(1)', 'data:text/html,1', '//evil.test', '/\\evil.test', '/ok?access_token=secret', '/ok#refresh_token=secret', '/ok?selection_token=secret', '/ok#/callback?access_token=secret', '/ok#/callback?refresh_token=secret'])('拒绝不安全页面 %s', value => { expect(() => safeNavigationUrl(value)).toThrow(); });
  it('扩展类型也无法绕过 URL 安全边界', () => {
    const registry = createMenuTypeRegistry(); registry.register({ id: 'unsafe', title: 'unsafe' }, () => ({ href: 'javascript:alert(1)', external: true }));
    expect(() => registry.resolve({ id: 'x', labelKey: 'x', type: 'unsafe', target: '/' })).toThrow();
    expect(() => safeNavigationUrl('https://user:password@example.test', true)).toThrow();
  });
});

describe('Preferences', () => {
  it('namespace 隔离、宿主覆盖、存储读取、更新订阅和清理', () => {
    const values = new Map([['yue.preferences.a', JSON.stringify({ theme: 'dark', locale: 'en-US', sidebarCollapsed: true, accessToken: 'never-keep' })]]);
    const storage = { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => { values.set(key, value); } };
    const a = initPreferences({ namespace: 'a', storage, overrides: { locale: 'zh-CN' } });
    const b = initPreferences({ namespace: 'b', storage });
    expect(a.getSnapshot()).toEqual({ theme: 'dark', locale: 'zh-CN', sidebarCollapsed: true });
    expect(b.getSnapshot().theme).toBe('system');
    const listener = vi.fn(); const dispose = a.subscribe(listener);
    a.update({ theme: 'light', ...{ accessToken: 'new-secret' } }); expect(listener).toHaveBeenCalledTimes(1);
    expect(values.get('yue.preferences.a')).not.toContain('Token');
    dispose(); a.update({ theme: 'dark' }); expect(listener).toHaveBeenCalledTimes(1);
    a.reset(); expect(a.getSnapshot().sidebarCollapsed).toBe(false);
  });
  it('部署缺省（运行时配置的语言）低于已保存的偏好，且不写进存储，直到用户自己改了偏好', () => {
    const values = new Map<string, string>(); const writes: string[] = [];
    const storage = { getItem: (key: string) => values.get(key) ?? null, setItem: (key: string, value: string) => { writes.push(key); values.set(key, value); } };
    const fresh = initPreferences({ namespace: 'deploy', storage, defaults: { locale: 'en-US' } });
    expect(fresh.getSnapshot().locale).toBe('en-US'); expect(writes).toEqual([]);
    fresh.update({ theme: 'dark' });
    expect(JSON.parse(values.get('yue.preferences.deploy')!)).toEqual({ theme: 'dark', locale: 'en-US', sidebarCollapsed: false });
    values.set('yue.preferences.deploy', JSON.stringify({ locale: 'zh-TW' }));
    expect(initPreferences({ namespace: 'deploy', storage, defaults: { locale: 'en-US' } }).getSnapshot().locale).toBe('zh-TW');
    expect(initPreferences({ namespace: 'other', storage: null, defaults: { locale: 'en-US' } }).getServerSnapshot().locale).toBe('en-US');
  });
  it('拒绝非法 namespace，损坏或不可访问存储不阻断当前会话', () => {
    expect(() => initPreferences({ namespace: '../secret' })).toThrow();
    const store = initPreferences({ namespace: 'broken', storage: { getItem: () => '{bad', setItem: () => { throw new Error('disabled'); } } });
    store.update({ theme: 'dark' }); expect(store.getSnapshot().theme).toBe('dark');
    expect(Object.isFrozen(store.getSnapshot())).toBe(true);
  });
  it('SSR 读取注入值，不把存储快照泄漏到服务器首帧', () => {
    const store = initPreferences({ namespace: 'ssr', storage: null, overrides: { locale: 'en-US', theme: 'dark' } });
    function Read() { return <output>{usePreferences().preferences.locale}</output>; }
    expect(renderToString(<PreferencesProvider store={store}><Read /></PreferencesProvider>)).toContain('en-US');
    expect(() => renderToString(<Read />)).toThrow('shell.error.preferencesProvider');
  });
});

it('全部外壳、页面、认证与反馈组件可 SSR，密码不出现在 HTML 中', () => {
  const i18n = makeI18n(); const provider = makeProvider(); const noop = () => {};
  const html = renderToString(<PreferencesProvider store={initPreferences({ namespace: 'ssr', storage: null })}><AppShell i18n={i18n} brand="Qian" menu={[{ id: 'home', labelKey: 'test.home', type: 'page', target: '/' }]} toolbar={<PreferenceControls i18n={i18n} />} managed><Page header={<PageHeader title="页面" />}><ColPage aside={<LeftTree label="资源">树插槽</LeftTree>}>内容</ColPage></Page></AppShell></PreferencesProvider>);
  for (const slot of ['app-shell', 'page', 'page-header', 'col-page', 'left-tree', 'managed-banner']) expect(html).toContain(`data-slot="${slot}"`);
  for (const status of [403, 404, 500, 'offline'] as const) expect(renderToString(<FallbackPage i18n={i18n} status={status} />)).toContain('data-slot="fallback-page"');
  const auth = renderToString(<Authentication i18n={i18n} provider={provider} onAuthenticated={noop} onRedirect={noop} />);
  expect(auth).toContain('data-slot="authentication"');
  const password = renderToString(<PasswordLogin i18n={i18n} onSubmit={async () => {}} />);
  expect(password).toContain('type="password"'); expect(password).toContain('autoComplete="current-password"');
  expect(renderToString(<PasswordLogin i18n={i18n} onSubmit={async () => {}} />)).toContain('yue-password-login');
  expect(renderToString(<SsoCallback i18n={i18n} provider={provider} callback={{ code: 'one', state: 'two' }} onResult={noop} onBack={noop} />)).toContain('yue-sso-callback');
  expect(renderToString(<TenantSelection i18n={i18n} tenants={session.tenants} onSelect={noop} onBack={noop} />)).toContain('租户 A');
  expect(renderToString(<TenantSwitcher i18n={i18n} provider={provider} session={session} onChanged={noop} />)).toContain('yue-tenant-switcher');
  expect(renderToString(<OfflineBanner i18n={i18n} offline />)).toContain('网络连接');
  expect(renderToString(<UpdateBanner i18n={i18n} available onReload={noop} />)).toContain('新版本');
  expect(renderToString(<ShellErrorBoundary i18n={i18n}><span>可用</span></ShellErrorBoundary>)).toContain('可用');
  expect(() => renderToString(<SessionExpiredDialog i18n={i18n} open={false} onSignIn={noop} />)).not.toThrow();
});
