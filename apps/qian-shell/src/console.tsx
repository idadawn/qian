import { Button, Icon, isIconName } from '@idadawn/yue-core/shell';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { Page, PageHeader } from '@idadawn/yue-shell/layout';
import type { ShellMenuItem } from '@idadawn/yue-shell/menu';
import type { BffSession } from '@qian/shell-kit';

// 平台身份的首页（平台控制台，S1-YUE-018）。持平台超管时 bootstrap 不代管：列出平台组件交给外壳的页面（与侧栏同一份菜单，来自 Capabilities 的平台控制台目录，
// 已按 Authz 的只读判定过滤）；不持角色时是代管上下文，只有说明与会话卡片。页面里的每次操作由组件的运营端点逐次经超管旁路判定并写平台审计链，这里不做任何判定。
// 外壳按需加载本模块（React.lazy），不进租户会话的首屏；词条随本模块安装，不与外壳自己的 app.* 词条混在一起。

const installed = new WeakSet<PlatformI18n>();
function install(i18n: PlatformI18n) {
  if (installed.has(i18n)) return;
  installed.add(i18n);
  i18n.addLanguagePack('zh-CN', {
    'console.title': '平台控制台', 'console.help': '以平台超级管理员身份管理租户与开通申请；页面里的每次操作都逐次经超管旁路判定，并记入平台审计链。',
    'console.managedTitle': '平台工作台', 'console.managedHelp': '平台身份由平台代管，当前没有可用的平台页面。需要管理租户时，请联系平台管理员授予平台超级管理员角色，或切换到某个租户。',
    'console.pages': '平台页面', 'console.open': '打开', 'console.openPage': '打开{{title}}',
    'console.session': '当前会话', 'console.displayName': '显示名', 'console.subject': '主体', 'console.tenant': '租户', 'console.kind': '主体类型', 'console.revision': '身份修订',
  });
  i18n.addLanguagePack('zh-TW', {
    'console.title': '平台控制台', 'console.help': '以平台超級管理員身分管理租戶與開通申請；頁面裡的每次操作都逐次經超管旁路判定，並記入平台稽核鏈。',
    'console.managedTitle': '平台工作台', 'console.managedHelp': '平台身分由平台代管，目前沒有可用的平台頁面。需要管理租戶時，請聯絡平台管理員授予平台超級管理員角色，或切換到某個租戶。',
    'console.pages': '平台頁面', 'console.open': '開啟', 'console.openPage': '開啟{{title}}',
    'console.session': '目前工作階段', 'console.displayName': '顯示名', 'console.subject': '主體', 'console.tenant': '租戶', 'console.kind': '主體類型', 'console.revision': '身分修訂',
  });
  i18n.addLanguagePack('en-US', {
    'console.title': 'Platform console', 'console.help': 'Manage tenants and sign-up applications as a platform super administrator. Every action on these pages is authorized one by one through the super administrator bypass and recorded in the platform audit chain.',
    'console.managedTitle': 'Platform workspace', 'console.managedHelp': 'Platform identities are managed by the platform and no platform pages are available. To manage tenants, ask a platform administrator to grant you the platform super administrator role, or switch to a tenant.',
    'console.pages': 'Platform pages', 'console.open': 'Open', 'console.openPage': 'Open {{title}}',
    'console.session': 'Current session', 'console.displayName': 'Display name', 'console.subject': 'Subject', 'console.tenant': 'Tenant', 'console.kind': 'Principal kind', 'console.revision': 'Identity revision',
  });
}

export interface PlatformHomeProps {
  i18n: PlatformI18n;
  session: BffSession;
  /** 外壳侧栏的同一份菜单：控制台目录交出的平台页面（代管时为空）。 */
  menu: readonly ShellMenuItem[];
  /** bootstrap 的代管标记：true 即不持平台超管（或没有任何可用的平台页面）。 */
  managed: boolean;
  onNavigate: (href: string) => void;
}

export function PlatformHome({ i18n, session, menu, managed, onNavigate }: PlatformHomeProps) {
  install(i18n);
  const card = 'space-y-2 rounded-xl border border-border bg-card p-5';
  return <Page header={<PageHeader title={i18n.t(managed ? 'console.managedTitle' : 'console.title')} description={i18n.t(managed ? 'console.managedHelp' : 'console.help')} />}>
    <div className="grid gap-4 md:grid-cols-2">
      {!managed && <section className={`${card} md:col-span-2`} data-testid="console-pages" aria-labelledby="console-pages-title">
        <h2 id="console-pages-title" className="text-lg font-semibold">{i18n.t('console.pages')}</h2>
        {menu.length === 0 ? <p className="text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="grid gap-2 sm:grid-cols-2">{menu.map(item => {
            // 与侧栏同一规则（yue-shell 的 AppShell）：有文案用文案，否则按词条键翻译。
            const title = item.label ?? i18n.t(item.labelKey);
            return <li key={item.id} data-route={item.target} className="flex items-center justify-between gap-3 rounded-lg border border-border p-3">
              <span className="flex min-w-0 items-center gap-2"><Icon name={isIconName(item.icon) ? item.icon : 'dashboard'} /><span className="truncate">{title}</span></span>
              <Button variant="outline" size="sm" aria-label={i18n.t('console.openPage', { title })} onClick={() => onNavigate(item.target)}>{i18n.t('console.open')}</Button>
            </li>;
          })}</ul>}
      </section>}
      <section className={card} data-testid="shell-session">
        <h2 className="text-lg font-semibold">{i18n.t('console.session')}</h2>
        <dl className="grid grid-cols-[auto_minmax(0,1fr)] gap-x-4 gap-y-1 text-sm">
          <dt className="text-muted-foreground">{i18n.t('console.displayName')}</dt><dd data-field="displayName">{session.displayName ?? '—'}</dd>
          <dt className="text-muted-foreground">{i18n.t('console.subject')}</dt><dd className="truncate" data-field="subject">{session.subject}</dd>
          <dt className="text-muted-foreground">{i18n.t('console.tenant')}</dt><dd className="truncate" data-field="tenantId">{session.tenantId ?? i18n.t('shell.platform')}</dd>
          <dt className="text-muted-foreground">{i18n.t('console.kind')}</dt><dd data-field="kind">{session.kind}</dd>
          <dt className="text-muted-foreground">{i18n.t('console.revision')}</dt><dd data-field="identityRevision">{session.identityRevision === null ? '—' : String(session.identityRevision)}</dd>
        </dl>
      </section>
    </div>
  </Page>;
}
