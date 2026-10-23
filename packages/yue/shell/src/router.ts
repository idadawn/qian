import { useSyncExternalStore } from 'react';
import { menuTypeRegistry, safeNavigationUrl, type MenuTypeRegistry, type ShellMenuItem } from './menu';

/** 外壳的最小历史路由：菜单项解析出的同源地址经 pushState 进入，popstate 回退；不复制路由引擎，页面内容由宿主按 path 决定。 */
export interface ShellRouter {
  /** 当前路径（pathname + search + hash）。 */
  getPath(): string;
  navigate(href: string, options?: { replace?: boolean }): void;
  subscribe(listener: () => void): () => void;
}

type RouterWindow = Pick<Window, 'addEventListener' | 'removeEventListener'> & { history: Pick<History, 'pushState' | 'replaceState'>; location: Pick<Location, 'pathname' | 'search' | 'hash'> };

export function createBrowserRouter(win: RouterWindow = window): ShellRouter {
  const listeners = new Set<() => void>();
  const notify = () => { for (const listener of listeners) listener(); };
  return {
    getPath: () => `${win.location.pathname}${win.location.search}${win.location.hash}`,
    navigate(href, options) {
      const target = safeNavigationUrl(href);
      if (options?.replace) win.history.replaceState(null, '', target); else win.history.pushState(null, '', target);
      notify();
    },
    subscribe(listener) {
      listeners.add(listener);
      if (listeners.size === 1) win.addEventListener('popstate', notify);
      return () => { listeners.delete(listener); if (listeners.size === 0) win.removeEventListener('popstate', notify); };
    },
  };
}

/** 订阅当前路径；SSR 首帧为根路径。 */
export function useRoutePath(router: ShellRouter): string {
  return useSyncExternalStore(router.subscribe, router.getPath, () => '/');
}

export interface MenuMatch { item: ShellMenuItem; href: string }

/** 把当前路径对到菜单项：先精确匹配 pathname，再按最长路径前缀匹配（`/customers` 命中 `/customers/42`）；外链不参与。 */
export function matchMenuItem(menu: readonly ShellMenuItem[], path: string, registry: MenuTypeRegistry = menuTypeRegistry): MenuMatch | undefined {
  const pathname = new URL(path, 'https://yue.local').pathname;
  let best: MenuMatch | undefined;
  const visit = (items: readonly ShellMenuItem[]): MenuMatch | undefined => {
    for (const item of items) {
      const destination = registry.resolve(item);
      if (!destination.external) {
        const target = new URL(destination.href, 'https://yue.local').pathname;
        if (target === pathname) return { item, href: destination.href };
        if (pathname.startsWith(target.endsWith('/') ? target : `${target}/`) && (!best || target.length > new URL(best.href, 'https://yue.local').pathname.length)) best = { item, href: destination.href };
      }
      if (item.children) { const exact = visit(item.children); if (exact) return exact; }
    }
    return undefined;
  };
  return visit(menu) ?? best;
}
