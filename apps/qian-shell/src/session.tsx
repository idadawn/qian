import { Button, Descriptions, Icon } from '@idadawn/yue-core/shell';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { Page, PageHeader } from '@idadawn/yue-shell/layout';
import type { ShellMenuItem } from '@idadawn/yue-shell/menu';
import type { BffSession } from '@qian/shell-kit';
import type { ShellState } from './runtime';

interface SessionPageProps {
  i18n: PlatformI18n; session: BffSession; menu: readonly ShellMenuItem[]; permissions: readonly string[]; capabilities: ShellState['capabilities'];
  loading: boolean; onReload: () => void;
}
/** 会话信息（账号菜单 → 会话信息）：当前会话、有效权限与可用页面，都来自网关 bootstrap；「刷新」重拉一轮。 */
export function SessionPage({ i18n, session, menu, permissions, capabilities, loading, onReload }: SessionPageProps) {
  const card = 'flex min-w-0 flex-col gap-3.5 rounded-xl border border-border bg-card px-5 py-4.5';
  const title = 'm-0 text-base font-semibold leading-6';
  return <Page testId="shell-session-page" className="space-y-5" header={<PageHeader title={i18n.t('app.sessionInfo')} description={i18n.t('app.workspaceHelp')}
    actions={<Button variant="outline" loading={loading} onClick={onReload} testId="shell-reload"><Icon name="refresh" />{i18n.t('app.refresh')}</Button>} />}>
    <div className="grid gap-5 md:grid-cols-2">
      <section className={card} data-testid="shell-session" aria-labelledby="shell-session-title">
        <h2 id="shell-session-title" className={title}>{i18n.t('app.session')}</h2>
        <Descriptions layout="justified" items={[
          { key: 'displayName', label: i18n.t('app.displayName'), children: <span data-field="displayName">{session.displayName ?? '—'}</span> },
          { key: 'subject', label: i18n.t('app.subject'), children: <span className="font-mono text-[13px]" data-field="subject">{session.subject}</span> },
          { key: 'tenantId', label: i18n.t('app.tenant'), children: <span className="font-mono text-[13px]" data-field="tenantId">{session.tenantId}</span> },
          { key: 'kind', label: i18n.t('app.kind'), children: <span data-field="kind">{session.kind}</span> },
          { key: 'identityRevision', label: i18n.t('app.revision'), children: <span className="tabular-nums" data-field="identityRevision">{session.identityRevision === null ? '—' : String(session.identityRevision)}</span> },
        ]} />
      </section>
      <section className={card} data-testid="shell-permissions" aria-labelledby="shell-permissions-title">
        <div className="flex flex-wrap items-center justify-between gap-3"><h2 id="shell-permissions-title" className={title}>{i18n.t('app.permissions')}</h2>
          <span className="text-[13px] text-caption-foreground" data-testid="shell-capabilities">{capabilities === undefined ? i18n.t('common.loading') : capabilities === null ? i18n.t('app.capabilitiesUnavailable') : i18n.t('app.capabilities', { count: capabilities })}</span></div>
        {permissions.length === 0 ? <p className="m-0 text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="m-0 flex list-none flex-col gap-1.5 p-0 text-sm">{permissions.map(permission => <li key={permission} data-permission={permission} data-allowed="true"><code className="font-mono text-[13px]">{permission}</code></li>)}</ul>}
      </section>
      <section className={`${card} md:col-span-2`} data-testid="shell-pages" aria-labelledby="shell-pages-title">
        <h2 id="shell-pages-title" className={title}>{i18n.t('app.pages')}</h2>
        {menu.length === 0 ? <p className="m-0 text-sm text-muted-foreground">{i18n.t('common.empty')}</p>
          : <ul className="m-0 flex list-none flex-col gap-1.5 p-0 text-sm">{menu.map(item => <li key={item.id} data-route={item.target} className="flex flex-wrap items-center gap-2">{item.label ?? item.labelKey}<code className="font-mono text-[13px] text-muted-foreground">{item.target}</code></li>)}</ul>}
      </section>
    </div>
  </Page>;
}
