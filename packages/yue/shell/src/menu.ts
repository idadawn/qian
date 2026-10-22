import { Registry, type RegistrationMeta } from '@idadawn/yue-sdk';

export interface ShellMenuItem { id: string; labelKey: string; type: string; target: string; children?: readonly ShellMenuItem[] }
export interface MenuDestination { href: string; external?: boolean }
export type MenuTypeResolver = (item: ShellMenuItem) => MenuDestination;

/** 页面与跳转共用的 URL 边界；协议、凭据、控制字符与令牌参数均拒绝。 */
export function safeNavigationUrl(value: string, external = false): string {
  if (!value || [...value].some(character => character.charCodeAt(0) <= 32 || character === '\\')) throw new TypeError('shell.error.navigation');
  if (external && !/^https?:\/\//i.test(value)) throw new TypeError('shell.error.navigation');
  if (!external && (!value.startsWith('/') || value.startsWith('//'))) throw new TypeError('shell.error.navigation');
  const url = new URL(value, 'https://yue.local');
  if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) throw new TypeError('shell.error.navigation');
  const fragment = url.hash.slice(1);
  const hashQuery = fragment.includes('?') ? fragment.slice(fragment.indexOf('?') + 1) : '';
  for (const key of [...url.searchParams.keys(), ...new URLSearchParams(fragment).keys(), ...new URLSearchParams(hashQuery).keys()]) {
    if (/^(?:access_?token|refresh_?token|id_?token|selection_?token|token)$/i.test(key)) throw new TypeError('shell.error.navigation');
  }
  return external ? url.href : `${url.pathname}${url.search}${url.hash}`;
}

export function createMenuTypeRegistry() {
  const registry = new Registry<RegistrationMeta, MenuTypeResolver>();
  const register = (meta: RegistrationMeta, resolve: MenuTypeResolver) => registry.register({ meta, implementation: resolve });
  register({ id: 'page', title: 'shell.menu.page' }, item => ({ href: safeNavigationUrl(item.target) }));
  register({ id: 'external', title: 'shell.menu.external' }, item => ({ href: safeNavigationUrl(item.target, true), external: true }));
  return {
    register, toJSON: () => registry.toJSON(),
    resolve(item: ShellMenuItem): MenuDestination {
      const resolver = registry.get(item.type)?.implementation;
      if (!resolver) throw new Error('shell.error.menuType');
      const destination = resolver(item);
      return { ...destination, href: safeNavigationUrl(destination.href, destination.external) };
    },
  };
}
export type MenuTypeRegistry = ReturnType<typeof createMenuTypeRegistry>;
export const menuTypeRegistry = createMenuTypeRegistry();
