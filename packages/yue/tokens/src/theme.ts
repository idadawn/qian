import type { Theme } from './tokens';
export type ThemePreference = Theme | 'system';
export interface ThemeOptions { storageKey?: string; defaultTheme?: ThemePreference; brand?: string }
export interface ThemeState { preference: ThemePreference; theme: Theme; brand: string }
interface BootstrapSettings { storageKey: string; defaultTheme: ThemePreference; brand?: string }
/** 自包含函数同时用于首帧脚本和运行时，避免两套解析规则漂移。 */
function bootstrap(settings: BootstrapSettings): ThemeState {
  const root = document.documentElement;
  const validTheme = (value: unknown): value is ThemePreference => value === 'light' || value === 'dark' || value === 'system';
  const validBrand = (value: unknown): value is string => typeof value === 'string' && /^[a-z][a-z0-9-]{0,63}$/.test(value);
  let stored: string | null = null;
  try { stored = window.localStorage.getItem(settings.storageKey); } catch { /* 隐私模式或拒绝存储时使用默认值。 */ }
  const preference = validTheme(stored) ? stored : settings.defaultTheme;
  const theme = preference === 'system' ? window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light' : preference;
  const brand = validBrand(settings.brand) ? settings.brand : validBrand(root.dataset.brand) ? root.dataset.brand : 'default';
  root.dataset.theme = theme; root.dataset.themePreference = preference; root.dataset.brand = brand;
  root.classList.toggle('dark', theme === 'dark'); root.style.colorScheme = theme;
  return { preference, theme, brand };
}
function settings(options: ThemeOptions): BootstrapSettings {
  const defaultTheme = options.defaultTheme ?? 'system';
  if (!['light', 'dark', 'system'].includes(defaultTheme)) throw new Error('主题模式无效。');
  if (options.brand !== undefined && !/^[a-z][a-z0-9-]{0,63}$/.test(options.brand)) throw new Error('品牌标识无效。');
  return { storageKey: options.storageKey ?? 'yue-theme', defaultTheme, brand: options.brand };
}
/** 放在 head 中、样式和应用模块之前；服务端须按 CSP 提供 nonce/hash。 */
export function createThemeInitializationScript(options: ThemeOptions = {}): string {
  const config = JSON.stringify(settings(options)).replaceAll('<', '\\u003c').replaceAll('\u2028', '\\u2028').replaceAll('\u2029', '\\u2029');
  return `;(${bootstrap.toString()})(${config});`;
}
export interface ThemeController { getState(): ThemeState; setTheme(preference: ThemePreference): ThemeState; setBrand(brand: string): ThemeState; dispose(): void }
/** 浏览器运行时同步更新 DOM；品牌来自服务端/租户上下文，不随媒体查询切换。 */
export function initializeTheme(options: ThemeOptions = {}): ThemeController {
  if (typeof window === 'undefined' || typeof document === 'undefined') throw new Error('运行时主题控制器仅用于浏览器；SSR 使用首帧脚本。');
  const config = settings(options), root = document.documentElement;
  let state = bootstrap(config), disposed = false;
  const media = window.matchMedia('(prefers-color-scheme: dark)');
  const apply = (): ThemeState => {
    const theme = state.preference === 'system' ? media.matches ? 'dark' : 'light' : state.preference;
    state = { ...state, theme }; root.dataset.theme = theme; root.dataset.themePreference = state.preference; root.dataset.brand = state.brand;
    root.classList.toggle('dark', theme === 'dark'); root.style.colorScheme = theme; return { ...state };
  };
  const change = (): void => { if (!disposed && state.preference === 'system') apply(); };
  media.addEventListener('change', change);
  return {
    getState: () => ({ ...state }),
    setTheme(preference) { if (disposed || !['light', 'dark', 'system'].includes(preference)) throw new Error('主题控制器已释放或模式无效。'); state = { ...state, preference }; try { window.localStorage.setItem(config.storageKey, preference); } catch { /* DOM 仍同步更新。 */ } return apply(); },
    setBrand(brand) { if (disposed || !/^[a-z][a-z0-9-]{0,63}$/.test(brand)) throw new Error('主题控制器已释放或品牌标识无效。'); state = { ...state, brand }; return apply(); },
    dispose() { if (!disposed) { disposed = true; media.removeEventListener('change', change); } },
  };
}
