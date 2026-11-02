import { useId, useSyncExternalStore, type ComponentProps, type ReactNode } from 'react';
import { Alert, Badge, cn, Icon, IconButton, isIconName, Menu, MenuContent, MenuItem, MenuRadioGroup, MenuRadioItem, MenuSeparator, MenuTrigger, type IconName } from '@idadawn/yue-core/shell';
import type { Locale, PlatformI18n } from '@idadawn/yue-sdk';
import { menuTypeRegistry, type MenuTypeRegistry, type ShellMenuItem } from './menu';
import { usePreferences, type Preferences } from './preferences';

export interface ShellPartProps { children?: ReactNode; className?: string; testId?: string }
export interface PageProps extends ShellPartProps { header?: ReactNode }
export function Page({ header, children, className, testId = 'yue-page' }: PageProps) {
  return <section data-slot="page" data-testid={testId} className={cn('min-w-0 space-y-4 p-4 md:p-6', className)}>{header}{children}</section>;
}
export interface PageHeaderProps extends ShellPartProps { title: string; description?: string; actions?: ReactNode; breadcrumb?: ReactNode }
/** 页头：粗标题与一行说明，操作靠右、与标题块底边对齐。 */
export function PageHeader({ title, description, actions, breadcrumb, children, className, testId = 'yue-page-header' }: PageHeaderProps) {
  return <header data-slot="page-header" data-testid={testId} className={cn('space-y-3', className)}>{breadcrumb}<div className="flex flex-wrap items-end justify-between gap-4"><div className="min-w-0 space-y-1"><h1 className="text-[22px] font-bold leading-[30px] tracking-tight">{title}</h1>{description && <p className="text-sm text-muted-foreground">{description}</p>}</div>{actions && <div className="flex flex-wrap items-center gap-2">{actions}</div>}</div>{children}</header>;
}
export interface ColPageProps extends ShellPartProps { aside: ReactNode }
export function ColPage({ aside, children, className, testId = 'yue-col-page' }: ColPageProps) {
  return <div data-slot="col-page" data-testid={testId} className={cn('grid min-w-0 gap-6 md:grid-cols-[14rem_minmax(0,1fr)]', className)}>{aside}<div className="min-w-0">{children}</div></div>;
}
/** 左栏容器；树的键盘与选择行为由消费方传入同一 data Tree，不复制树引擎。 */
export interface LeftTreeProps extends ShellPartProps { label: string }
export function LeftTree({ label, children, className, testId = 'yue-left-tree' }: LeftTreeProps) {
  return <aside aria-label={label} data-slot="left-tree" data-testid={testId} className={cn('min-w-0 rounded-xl border border-border bg-card p-4', className)}>{children}</aside>;
}

export function useShellLocale(i18n: PlatformI18n): string {
  return useSyncExternalStore(listener => { i18n.instance.on('languageChanged', listener); return () => { i18n.instance.off('languageChanged', listener); }; }, () => i18n.instance.language, () => i18n.instance.language);
}

const themeIcons: Record<Preferences['theme'], IconName> = { light: 'sun', dark: 'moon', system: 'monitor' };
/** 语言用各自的本名列出（语言包里三种界面语言给的是同一组本名）。 */
const locales: readonly Locale[] = ['zh-CN', 'zh-TW', 'en-US'];
const quietTrigger = 'border-transparent bg-transparent px-0 shadow-none';

export interface PreferenceControlsProps extends ShellPartProps { i18n: PlatformI18n }
/** 顶栏偏好：主题与语言各一个图标按钮，展开为单选菜单，选中即关闭；当前值有勾号，读屏读作「已选中」。 */
export function PreferenceControls({ i18n, className, testId = 'yue-preference-controls' }: PreferenceControlsProps) {
  const { preferences, updatePreferences } = usePreferences();
  useShellLocale(i18n);
  const trigger = cn(quietTrigger, 'size-9 text-foreground/80 hover:bg-accent hover:text-foreground data-[popup-open]:bg-accent');
  return <div data-slot="preference-controls" data-testid={testId} className={cn('flex items-center gap-1', className)}>
    <Menu>
      <MenuTrigger aria-label={i18n.t('shell.theme')} testId={`${testId}-theme`} className={trigger}><Icon name={themeIcons[preferences.theme]} /></MenuTrigger>
      <MenuContent align="end" className="w-auto">
        <MenuRadioGroup value={preferences.theme} onValueChange={(theme: Preferences['theme']) => updatePreferences({ theme })}>
          {(Object.keys(themeIcons) as Preferences['theme'][]).map(theme => <MenuRadioItem key={theme} value={theme} closeOnClick><span className="inline-flex items-center gap-2"><Icon name={themeIcons[theme]} />{i18n.t(`shell.${theme}`)}</span></MenuRadioItem>)}
        </MenuRadioGroup>
      </MenuContent>
    </Menu>
    <Menu>
      <MenuTrigger aria-label={i18n.t('shell.language')} testId={`${testId}-locale`} className={trigger}><Icon name="languages" /></MenuTrigger>
      <MenuContent align="end" className="w-auto">
        <MenuRadioGroup value={preferences.locale} onValueChange={(locale: Locale) => { updatePreferences({ locale }); void i18n.changeLanguage(locale); }}>
          {locales.map(locale => <MenuRadioItem key={locale} value={locale} lang={locale} closeOnClick>{i18n.t(`shell.locale.${locale}`)}</MenuRadioItem>)}
        </MenuRadioGroup>
      </MenuContent>
    </Menu>
  </div>;
}

export interface AppShellProps extends ShellPartProps {
  i18n: PlatformI18n; brand: ReactNode; menu: readonly ShellMenuItem[]; activePath?: string;
  registry?: MenuTypeRegistry; onNavigate?: (href: string) => void;
  toolbar?: ReactNode; userName?: string; onSignOut?: () => void;
  managed?: boolean; managedLabel?: string; userMenu?: ReactNode;
  /** 品牌标识，缺省为主色方块；只作装饰，可访问名称由 brand 提供。 */
  logo?: ReactNode;
  /** 品牌下方的工作区卡片，例如当前租户与切换入口；侧栏收起时隐藏。 */
  workspace?: ReactNode;
  /** 顶栏左侧，通常放面包屑。 */
  breadcrumb?: ReactNode;
  /** 侧栏底部用户名下的一行说明，例如角色或所在租户。 */
  userDescription?: string;
}

function Mark() {
  return <svg viewBox="0 0 36 36" className="size-7" aria-hidden="true" focusable="false"><rect width="36" height="36" rx="9" className="fill-primary" />{[10, 16.5, 23].map(y => <rect key={y} x="9" y={y} width="18" height="3" rx="1.5" className="fill-primary-foreground" />)}</svg>;
}

/** 相邻同组的菜单项归到一个分组标题下；没有 group 的项不出标题。 */
function sections(items: readonly ShellMenuItem[]): { group?: string; items: ShellMenuItem[] }[] {
  const result: { group?: string; items: ShellMenuItem[] }[] = [];
  for (const item of items) {
    const last = result.at(-1);
    if (last && last.group === item.group) last.items.push(item); else result.push({ group: item.group, items: [item] });
  }
  return result;
}

/** 单一侧栏布局：侧栏放品牌、工作区、分组导航与账号；顶栏放侧栏开关、面包屑与工具；主区是画布底色。 */
export function AppShell({ i18n, brand, menu, activePath, registry = menuTypeRegistry, onNavigate, toolbar, userName, onSignOut, managed, managedLabel, userMenu, logo, workspace, breadcrumb, userDescription, children, className, testId = 'yue-app-shell' }: AppShellProps) {
  const { preferences, updatePreferences } = usePreferences();
  useShellLocale(i18n);
  const contentId = useId();
  const navigationId = useId();
  const groupId = useId();
  const collapsed = preferences.sidebarCollapsed;
  const renderItems = (items: readonly ShellMenuItem[], nested = false) => <ul className={cn('space-y-0.5', nested && !collapsed && 'mt-0.5 ps-4')}>{items.map(item => {
    const destination = registry.resolve(item);
    const label = item.label ?? i18n.t(item.labelKey);
    const navigate: ComponentProps<'a'>['onClick'] = event => {
      if (!destination.external && onNavigate && event.button === 0 && !event.ctrlKey && !event.metaKey && !event.shiftKey && !event.altKey) { event.preventDefault(); onNavigate(destination.href); }
    };
    const icon: IconName = isIconName(item.icon) ? item.icon : destination.external ? 'externalLink' : 'fileText';
    return <li key={item.id}><a href={destination.href} target={destination.external ? '_blank' : undefined} rel={destination.external ? 'noopener noreferrer' : undefined} onClick={navigate} aria-current={!destination.external && destination.href === activePath ? 'page' : undefined} title={collapsed ? label : undefined}
      className={cn('relative flex h-9 items-center gap-2.5 rounded-lg px-2.5 text-sm text-sidebar-foreground/80 transition-colors duration-fast hover:bg-sidebar-accent hover:text-sidebar-accent-foreground aria-[current=page]:bg-info-bg aria-[current=page]:font-semibold aria-[current=page]:text-info focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring motion-reduce:transition-none', collapsed && 'justify-center px-0')} data-slot="shell-menu-item" data-testid={`${testId}-menu-item`}>
      <Icon name={icon} className="size-[18px]" /><span className={collapsed ? 'sr-only' : 'min-w-0 flex-1 truncate'}>{label}</span>
      {destination.external && !collapsed && icon !== 'externalLink' && <Icon name="externalLink" className="size-3.5 text-muted-foreground" />}
      {item.badge !== undefined && <Badge tone="info" testId={`${testId}-menu-badge`} className={collapsed ? 'sr-only' : 'h-5 px-1.5'}>{item.badge}</Badge>}
      {item.badge !== undefined && collapsed && <span aria-hidden="true" className="absolute end-2 top-1.5 size-1.5 rounded-full bg-primary" />}
    </a>{item.children && renderItems(item.children, true)}</li>;
  })}</ul>;
  const initial = Array.from(userName?.trim() ?? '')[0]?.toUpperCase();
  const avatar = <span aria-hidden="true" className="inline-flex size-8 shrink-0 items-center justify-center rounded-full bg-info-bg text-[13px] font-bold text-info">{initial ?? <Icon name="user" />}</span>;
  return <div data-slot="app-shell" data-testid={testId} className={cn('relative flex min-h-dvh bg-canvas text-foreground', className)}>
    <a href={`#${contentId}`} className="sr-only z-50 rounded-md bg-background p-3 focus:not-sr-only focus:absolute">{i18n.t('shell.skip')}</a>
    <aside className={cn('shrink-0 border-e border-sidebar-border bg-sidebar text-sidebar-foreground', collapsed ? 'w-16' : 'w-52 md:w-60')} data-slot="shell-sidebar" data-testid={`${testId}-sidebar`}>
      <div className={cn('sticky top-0 flex h-dvh flex-col gap-4 py-4', collapsed ? 'px-2' : 'px-3')}>
        <div className={cn('flex h-9 shrink-0 items-center gap-2.5', collapsed ? 'justify-center' : 'px-1.5')} data-slot="shell-brand" data-testid={`${testId}-brand`}><span aria-hidden="true" className="inline-flex shrink-0">{logo ?? <Mark />}</span><span className={collapsed ? 'sr-only' : 'min-w-0 truncate text-base font-bold'}>{brand}</span></div>
        {workspace && !collapsed && <div className="shrink-0" data-slot="shell-workspace" data-testid={`${testId}-workspace`}>{workspace}</div>}
        <nav id={navigationId} aria-label={i18n.t('shell.nav')} className="min-h-0 flex-1 overflow-y-auto">{sections(menu).map((section, index) => section.group
          ? <div key={`${index}-${section.group}`} role="group" aria-labelledby={`${groupId}-${index}`} className="pt-2 first:pt-0"><p id={`${groupId}-${index}`} className={collapsed ? 'sr-only' : 'px-2.5 pt-2 pb-1.5 text-xs font-semibold leading-4 text-muted-foreground'}>{section.group}</p>{collapsed && index > 0 && <span aria-hidden="true" className="mx-2 mb-2 block h-px bg-sidebar-border" />}{renderItems(section.items)}</div>
          : <div key={`${index}-items`}>{renderItems(section.items)}</div>)}</nav>
        {(userName || onSignOut || userMenu) && <div className={cn('flex shrink-0 items-center gap-2.5 border-t border-sidebar-border pt-3', collapsed ? 'justify-center' : 'px-1')} data-slot="shell-user" data-testid={`${testId}-user`}>
          {!collapsed && <>{avatar}<span className="flex min-w-0 flex-1 flex-col leading-[18px]"><span className="truncate text-sm font-semibold">{userName ?? i18n.t('shell.user')}</span>{userDescription && <span className="truncate text-xs text-muted-foreground">{userDescription}</span>}</span></>}
          <Menu><MenuTrigger aria-label={i18n.t('shell.user')} className={cn(quietTrigger, 'hover:bg-sidebar-accent data-[popup-open]:bg-sidebar-accent', collapsed ? 'size-9 rounded-full' : 'size-8')}>{collapsed ? avatar : <Icon name="more" />}</MenuTrigger>
            <MenuContent side="top" align={collapsed ? 'start' : 'end'} className="w-auto">{userMenu}{userMenu && onSignOut && <MenuSeparator />}{onSignOut && <MenuItem onClick={onSignOut}><Icon name="logOut" />{i18n.t('shell.signOut')}</MenuItem>}</MenuContent></Menu>
        </div>}
      </div>
    </aside>
    <div className="flex min-w-0 flex-1 flex-col">
      <header className="sticky top-0 z-30 flex min-h-14 shrink-0 flex-wrap items-center gap-3 border-b border-border bg-background px-3 py-2 md:px-6" data-slot="shell-toolbar" data-testid={`${testId}-toolbar`}>
        <IconButton variant="ghost" label={i18n.t(collapsed ? 'shell.expand' : 'shell.collapse')} aria-expanded={!collapsed} aria-controls={navigationId} onClick={() => updatePreferences({ sidebarCollapsed: !collapsed })} testId={`${testId}-toggle`} className="size-9 shrink-0 text-foreground/80 md:-ms-2"><Icon name={collapsed ? 'panelLeftOpen' : 'panelLeftClose'} /></IconButton>
        <div className="min-w-0 flex-1" data-slot="shell-breadcrumb" data-testid={`${testId}-breadcrumb`}>{breadcrumb}</div>
        {toolbar && <div className="flex shrink-0 flex-wrap items-center gap-1">{toolbar}</div>}
      </header>
      {managed && <div data-slot="managed-banner" data-testid={`${testId}-managed`}><Alert tone="warn" className="rounded-none border-x-0 border-t-0">{managedLabel ?? i18n.t('shell.managed')}</Alert></div>}
      <main id={contentId} tabIndex={-1} className="min-w-0 flex-1 outline-none" data-slot="shell-content" data-testid={`${testId}-content`}>{children}</main>
    </div>
  </div>;
}
