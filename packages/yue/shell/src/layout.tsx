import { useId, useSyncExternalStore, type ComponentProps, type ReactNode } from 'react';
import { Alert, cn, Icon, IconButton, Menu, MenuContent, MenuItem, MenuTrigger, Select, SelectContent, SelectItem, SelectTrigger } from '@idadawn/yue-core/shell';
import type { Locale, PlatformI18n } from '@idadawn/yue-sdk';
import { menuTypeRegistry, type MenuTypeRegistry, type ShellMenuItem } from './menu';
import { usePreferences, type Preferences } from './preferences';

export interface ShellPartProps { children?: ReactNode; className?: string; testId?: string }
export interface PageProps extends ShellPartProps { header?: ReactNode }
export function Page({ header, children, className, testId = 'yue-page' }: PageProps) {
  return <section data-slot="page" data-testid={testId} className={cn('min-w-0 space-y-6 p-4 md:p-6', className)}>{header}{children}</section>;
}
export interface PageHeaderProps extends ShellPartProps { title: string; description?: string; actions?: ReactNode; breadcrumb?: ReactNode }
export function PageHeader({ title, description, actions, breadcrumb, children, className, testId = 'yue-page-header' }: PageHeaderProps) {
  return <header data-slot="page-header" data-testid={testId} className={cn('space-y-3', className)}>{breadcrumb}<div className="flex flex-wrap items-start justify-between gap-4"><div className="min-w-0"><h1 className="text-2xl font-semibold tracking-tight">{title}</h1>{description && <p className="mt-2 text-sm text-muted-foreground">{description}</p>}</div>{actions && <div className="flex flex-wrap gap-2">{actions}</div>}</div>{children}</header>;
}
export interface ColPageProps extends ShellPartProps { aside: ReactNode }
export function ColPage({ aside, children, className, testId = 'yue-col-page' }: ColPageProps) {
  return <div data-slot="col-page" data-testid={testId} className={cn('grid min-w-0 gap-6 md:grid-cols-[14rem_minmax(0,1fr)]', className)}>{aside}<div className="min-w-0">{children}</div></div>;
}
/** 左栏容器；树的键盘与选择行为由消费方传入同一 data Tree，不复制树引擎。 */
export interface LeftTreeProps extends ShellPartProps { label: string }
export function LeftTree({ label, children, className, testId = 'yue-left-tree' }: LeftTreeProps) {
  return <aside aria-label={label} data-slot="left-tree" data-testid={testId} className={cn('min-w-0 rounded-lg border border-border bg-card p-4', className)}>{children}</aside>;
}

export function useShellLocale(i18n: PlatformI18n): string {
  return useSyncExternalStore(listener => { i18n.instance.on('languageChanged', listener); return () => { i18n.instance.off('languageChanged', listener); }; }, () => i18n.instance.language, () => i18n.instance.language);
}

export interface PreferenceControlsProps extends ShellPartProps { i18n: PlatformI18n }
export function PreferenceControls({ i18n, className, testId = 'yue-preference-controls' }: PreferenceControlsProps) {
  const { preferences, updatePreferences } = usePreferences();
  useShellLocale(i18n);
  const themes: Preferences['theme'][] = ['light', 'dark', 'system'];
  const locales: Locale[] = ['zh-CN', 'zh-TW', 'en-US'];
  return <div data-slot="preference-controls" data-testid={testId} className={cn('flex flex-wrap gap-2', className)}>
    <Select items={themes.map(value => ({ value, label: i18n.t(`shell.${value}`) }))} value={preferences.theme} onValueChange={theme => { if (theme) updatePreferences({ theme }); }}>
      <SelectTrigger aria-label={i18n.t('shell.theme')} testId={`${testId}-theme`} className="min-w-28" />
      <SelectContent listLabel={i18n.t('shell.theme')}>{themes.map(theme => <SelectItem key={theme} value={theme}>{i18n.t(`shell.${theme}`)}</SelectItem>)}</SelectContent>
    </Select>
    <Select items={locales.map(value => ({ value, label: value }))} value={preferences.locale} onValueChange={locale => { if (locale) { updatePreferences({ locale }); void i18n.changeLanguage(locale); } }}>
      <SelectTrigger aria-label={i18n.t('shell.language')} testId={`${testId}-locale`} className="min-w-28" />
      <SelectContent listLabel={i18n.t('shell.language')}>{locales.map(locale => <SelectItem key={locale} value={locale}>{locale}</SelectItem>)}</SelectContent>
    </Select>
  </div>;
}

export interface AppShellProps extends ShellPartProps {
  i18n: PlatformI18n; brand: ReactNode; menu: readonly ShellMenuItem[]; activePath?: string;
  registry?: MenuTypeRegistry; onNavigate?: (href: string) => void;
  toolbar?: ReactNode; userName?: string; onSignOut?: () => void;
  managed?: boolean; managedLabel?: string; userMenu?: ReactNode;
}
export function AppShell({ i18n, brand, menu, activePath, registry = menuTypeRegistry, onNavigate, toolbar, userName, onSignOut, managed, managedLabel, userMenu, children, className, testId = 'yue-app-shell' }: AppShellProps) {
  const { preferences, updatePreferences } = usePreferences();
  useShellLocale(i18n);
  const contentId = useId();
  const navigationId = useId();
  const collapsed = preferences.sidebarCollapsed;
  const renderItems = (items: readonly ShellMenuItem[]) => <ul className="space-y-1">{items.map(item => {
    const destination = registry.resolve(item);
    const navigate: ComponentProps<'a'>['onClick'] = event => {
      if (!destination.external && onNavigate && event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) { event.preventDefault(); onNavigate(destination.href); }
    };
    return <li key={item.id}><a href={destination.href} target={destination.external ? '_blank' : undefined} rel={destination.external ? 'noopener noreferrer' : undefined} onClick={navigate} aria-current={!destination.external && destination.href === activePath ? 'page' : undefined} title={collapsed ? item.label ?? i18n.t(item.labelKey) : undefined} className="flex min-h-10 items-center gap-2 rounded-md px-3 py-2 text-sm text-sidebar-foreground hover:bg-sidebar-accent aria-[current=page]:bg-sidebar-accent aria-[current=page]:font-medium focus-visible:outline-2 focus-visible:outline-ring" data-slot="shell-menu-item" data-testid={`${testId}-menu-item`}><Icon name={destination.external ? 'arrowUp' : 'chevronRight'} /><span className={collapsed ? 'sr-only' : 'truncate'}>{item.label ?? i18n.t(item.labelKey)}</span></a>{item.children && <div className={collapsed ? undefined : 'pl-3'}>{renderItems(item.children)}</div>}</li>;
  })}</ul>;
  return <div data-slot="app-shell" data-testid={testId} className={cn('relative flex min-h-dvh bg-background text-foreground', className)}>
    <a href={`#${contentId}`} className="sr-only z-50 rounded-md bg-background p-3 focus:not-sr-only focus:absolute">{i18n.t('shell.skip')}</a>
    <aside className={cn('shrink-0 border-r border-sidebar-border bg-sidebar', collapsed ? 'w-16' : 'w-48 md:w-60')} data-slot="shell-sidebar" data-testid={`${testId}-sidebar`}>
      <div className="flex h-16 items-center justify-between gap-2 border-b border-sidebar-border px-3"><span className={collapsed ? 'sr-only' : 'truncate font-semibold'}>{brand}</span><IconButton variant="ghost" label={i18n.t(collapsed ? 'shell.expand' : 'shell.collapse')} aria-expanded={!collapsed} aria-controls={navigationId} onClick={() => updatePreferences({ sidebarCollapsed: !collapsed })} testId={`${testId}-toggle`}><Icon name={collapsed ? 'chevronRight' : 'chevronLeft'} /></IconButton></div>
      <nav id={navigationId} aria-label={i18n.t('shell.nav')} className="p-2">{renderItems(menu)}</nav>
    </aside>
    <div className="min-w-0 flex-1"><header className="flex min-h-16 flex-wrap items-center justify-end gap-3 border-b border-border px-4 py-3" data-slot="shell-toolbar" data-testid={`${testId}-toolbar`}>{toolbar}{(userName || onSignOut || userMenu) && <Menu><MenuTrigger aria-label={i18n.t('shell.user')}>{userName ?? i18n.t('shell.user')}</MenuTrigger><MenuContent>{userMenu}{onSignOut && <MenuItem onClick={onSignOut}>{i18n.t('shell.signOut')}</MenuItem>}</MenuContent></Menu>}</header>
      {managed && <div data-slot="managed-banner" data-testid={`${testId}-managed`}><Alert tone="warn" className="rounded-none border-x-0">{managedLabel ?? i18n.t('shell.managed')}</Alert></div>}
      <main id={contentId} tabIndex={-1} className="outline-none" data-slot="shell-content" data-testid={`${testId}-content`}>{children}</main>
    </div>
  </div>;
}
