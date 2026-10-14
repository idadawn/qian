import { useContext } from 'react';
import { ThemeContext, useSite, withBase, socialLinks as icons } from '@rspress/core/runtime';
import { useMdUrl, IconSun, IconMoon, SvgWrapper } from '@rspress/core/theme-original';
import type { SocialLink } from '@rspress/core';

export function LlmsViewOptions() {
  const { pathname } = useMdUrl();
  if (import.meta.env.SSG_MD) return null;
  return <a className="rp-llms-button" href={withBase(pathname)} aria-label="查看本页 Markdown">Markdown 链接</a>;
}
export function SocialLinks({ socialLinks }: { socialLinks?: SocialLink[] }) {
  const { site } = useSite();
  if (import.meta.env.SSG_MD) return null;
  return <div className="rp-social-links">{(site.themeConfig.socialLinks ?? socialLinks ?? []).filter(link => !link.mode || link.mode === 'link').map(link => {
    const svg = typeof link.icon === 'string' ? icons[link.icon] : link.icon?.svg;
    return <a key={link.content} className="rp-social-links__item" href={link.content} target="_blank" rel="noopener noreferrer" aria-label={link.icon === 'github' ? 'GitHub 源码仓库' : '项目链接'}><span className="rp-social-links__icon" aria-hidden="true" dangerouslySetInnerHTML={{ __html: svg ?? '' }} /></a>;
  })}</div>;
}

export function SwitchAppearance({ onClick }: { onClick?: () => void }) {
  const { theme, setTheme } = useContext(ThemeContext);
  return <button type="button" className="rp-switch-appearance" aria-label="切换主题" onClick={() => { setTheme?.(theme === 'dark' ? 'light' : 'dark'); onClick?.(); }}><span aria-hidden="true"><SvgWrapper className="rp-switch-appearance__icon--sun" icon={IconSun} /><SvgWrapper className="rp-switch-appearance__icon--moon" icon={IconMoon} /></span></button>;
}
