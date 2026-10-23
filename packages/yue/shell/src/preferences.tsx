import { createContext, useContext, useSyncExternalStore, type ReactNode } from 'react';
import type { Locale } from '@idadawn/yue-sdk';

export interface Preferences { theme: 'light' | 'dark' | 'system'; locale: Locale; sidebarCollapsed: boolean }
export interface PreferenceStorage { getItem(key: string): string | null; setItem(key: string, value: string): void }
export interface PreferencesStore {
  readonly namespace: string;
  getSnapshot(): Readonly<Preferences>;
  getServerSnapshot(): Readonly<Preferences>;
  subscribe(listener: () => void): () => void;
  update(patch: Partial<Preferences>): void;
  reset(): void;
}
const defaults: Preferences = { theme: 'system', locale: 'zh-CN', sidebarCollapsed: false };

/** 只接受三项非敏感偏好；即使 JS 调用方传入 token，也不会持久化。 */
function pick(value: unknown): Partial<Preferences> {
  if (!value || typeof value !== 'object') return {};
  const source = value as Record<string, unknown>;
  return {
    ...(['light', 'dark', 'system'].includes(String(source.theme)) ? { theme: source.theme as Preferences['theme'] } : {}),
    ...(['zh-CN', 'zh-TW', 'en-US'].includes(String(source.locale)) ? { locale: source.locale as Locale } : {}),
    ...(typeof source.sidebarCollapsed === 'boolean' ? { sidebarCollapsed: source.sidebarCollapsed } : {}),
  };
}
function browserStorage(): PreferenceStorage | undefined { try { return typeof window === 'undefined' ? undefined : window.localStorage; } catch { return undefined; } }

/**
 * `overrides` 总是生效（高于已保存的偏好）；`defaults` 是部署缺省（例如运行时配置的语言）：低于已保存的偏好，也不写进存储。
 * 存储只记用户自己改过的键（`update` 增量合并），`reset` 写空对象。
 */
export function initPreferences({ namespace, overrides, defaults: deployment, storage = browserStorage() }: { namespace: string; overrides?: Partial<Preferences>; defaults?: Partial<Preferences>; storage?: PreferenceStorage | null }): PreferencesStore {
  if (!/^[a-zA-Z0-9._-]+$/.test(namespace)) throw new TypeError('shell.error.preferenceNamespace');
  const key = `yue.preferences.${namespace}`;
  const initial = Object.freeze({ ...defaults, ...pick(deployment), ...pick(overrides) });
  let saved: Partial<Preferences> = {};
  try { saved = pick(JSON.parse(storage?.getItem(key) ?? '{}')); } catch { /* 存储损坏或被禁用时仍可使用当前会话。 */ }
  let current = Object.freeze({ ...initial, ...saved, ...pick(overrides) });
  const listeners = new Set<() => void>();
  // 存储只记用户自己写过的键（增量合并）：部署缺省与宿主覆盖不进存储，reset 写空对象。部署缺省以后变了，用户没改过的那几项跟着变。
  const persist = (next: Partial<Preferences>) => {
    saved = next;
    try { storage?.setItem(key, JSON.stringify(saved)); } catch { /* 浏览器禁用存储时偏好保留在内存。 */ }
  };
  const replace = (next: Preferences) => {
    if (JSON.stringify(next) === JSON.stringify(current)) return;
    current = Object.freeze(next);
    listeners.forEach(listener => listener());
  };
  return {
    namespace, getSnapshot: () => current, getServerSnapshot: () => initial,
    subscribe(listener) { listeners.add(listener); return () => { listeners.delete(listener); }; },
    update(patch) {
      const written = pick(patch);
      if (JSON.stringify({ ...saved, ...written }) !== JSON.stringify(saved)) persist({ ...saved, ...written });
      replace({ ...current, ...written });
    },
    reset() { persist({}); replace({ ...initial }); },
  };
}

const PreferencesContext = createContext<PreferencesStore | null>(null);
export function PreferencesProvider({ store, children }: { store: PreferencesStore; children: ReactNode }) { return <PreferencesContext value={store}>{children}</PreferencesContext>; }
export function usePreferences(): { preferences: Readonly<Preferences>; updatePreferences: PreferencesStore['update'] } {
  const store = useContext(PreferencesContext);
  if (!store) throw new Error('shell.error.preferencesProvider');
  const preferences = useSyncExternalStore(store.subscribe, store.getSnapshot, store.getServerSnapshot);
  return { preferences, updatePreferences: store.update };
}
