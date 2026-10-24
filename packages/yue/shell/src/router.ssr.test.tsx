import { describe, expect, it } from 'vitest';
import { createBrowserRouter, matchMenuItem, type ShellMenuItem } from './index';

describe('菜单 → 路由', () => {
  const menu: ShellMenuItem[] = [
    { id: 'customers', labelKey: 'customers', label: '客户', type: 'page', target: '/customers' },
    { id: 'customers-new', labelKey: 'customers-new', label: '新建客户', type: 'page', target: '/customers/new' },
  ];
  it('当前路径精确或按最长前缀命中，外链不参与', () => {
    const withExternal = [...menu, { id: 'docs', labelKey: 'docs', type: 'external', target: 'https://docs.example/customers' }];
    expect(matchMenuItem(withExternal, '/customers')?.item.label).toBe('客户');
    expect(matchMenuItem(withExternal, '/customers/new?x=1')?.item.label).toBe('新建客户');
    expect(matchMenuItem(withExternal, '/customers/42')?.item.label).toBe('客户');
    expect(matchMenuItem(withExternal, '/orders')).toBeUndefined();
    expect(() => matchMenuItem([{ id: 'x', labelKey: 'x', type: 'page', target: 'javascript:alert(1)' }], '/')).toThrow();
  });
  it('历史路由：导航校验同源地址并通知订阅者，popstate 回退', () => {
    const listeners = new Map<string, () => void>(); const history: string[] = [];
    const win = { location: { pathname: '/', search: '', hash: '' }, history: { pushState: (_: unknown, __: string, url?: string | URL | null) => { history.push('push:' + url); win.location.pathname = String(url); }, replaceState: (_: unknown, __: string, url?: string | URL | null) => { history.push('replace:' + url); } },
      addEventListener: (type: string, listener: EventListenerOrEventListenerObject) => { listeners.set(type, listener as () => void); }, removeEventListener: (type: string) => { listeners.delete(type); } };
    const router = createBrowserRouter(win);
    const seen: string[] = []; const stop = router.subscribe(() => seen.push(router.getPath()));
    router.navigate('/customers'); expect(history).toEqual(['push:/customers']); expect(seen).toEqual(['/customers']);
    expect(() => router.navigate('https://evil.test')).toThrow('shell.error.navigation'); expect(() => router.navigate('/x?access_token=1')).toThrow();
    win.location.pathname = '/'; listeners.get('popstate')!(); expect(seen).toEqual(['/customers', '/']);
    router.navigate('/again', { replace: true }); expect(history.at(-1)).toBe('replace:/again');
    stop(); expect(listeners.has('popstate')).toBe(false);
  });
});
