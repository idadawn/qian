// 保持 Rspress 的存储键与 ThemeContext；在首次客户端渲染读取偏好，避免 hydration 覆盖首帧主题。
import { useCallback, useEffect, useLayoutEffect, useState } from 'react';
import { useSite } from '@rspress/core/runtime';
import { getDefaultDarkModeValue, isDarkModeSwitchEnabled } from '@rspress/shared';
const KEY = 'rspress-theme-appearance';
type Theme = 'light' | 'dark';
type Preference = Theme | 'auto';
const valid = (value: string | null): value is Preference => value === 'light' || value === 'dark' || value === 'auto';
const systemTheme = (): Theme => typeof window !== 'undefined' && matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
function apply(theme: Theme) {
  document.documentElement.classList.toggle('dark', theme === 'dark');
  document.documentElement.classList.toggle('rp-dark', theme === 'dark');
  document.documentElement.style.colorScheme = theme;
}
export function useThemeState(): readonly [Theme, (theme: Theme) => void] {
  const { site } = useSite();
  const fallback = getDefaultDarkModeValue(site.themeConfig.darkMode);
  const enabled = isDarkModeSwitchEnabled(site.themeConfig.darkMode);
  const read = (): Preference => {
    try { const value = enabled && typeof window !== 'undefined' ? localStorage.getItem(KEY) : null; return valid(value) ? value : fallback; }
    catch { return fallback; }
  };
  const [preference, setPreference] = useState<Preference>(read);
  const [system, setSystem] = useState<Theme>(systemTheme);
  const choice = enabled ? preference : fallback;
  const theme = choice === 'auto' ? system : choice;
  useLayoutEffect(() => { apply(theme); }, [theme]);
  useEffect(() => {
    const media = matchMedia('(prefers-color-scheme: dark)');
    const mediaChanged = () => setSystem(systemTheme());
    const storageChanged = (event: StorageEvent) => { if (event.key === KEY || event.key === null) setPreference(read()); };
    media.addEventListener('change', mediaChanged); window.addEventListener('storage', storageChanged);
    return () => { media.removeEventListener('change', mediaChanged); window.removeEventListener('storage', storageChanged); };
  }, [fallback, enabled]);
  const setTheme = useCallback((value: Theme) => {
    if (!enabled) return;
    const next = value === system ? 'auto' : value;
    try { localStorage.setItem(KEY, next); } catch { /* 无持久存储时仍支持当前页面切换。 */ }
    setPreference(next); apply(value);
  }, [enabled, system]);
  return [theme, setTheme];
}
