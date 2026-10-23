import { useEffect, useState, useSyncExternalStore, type ComponentType } from 'react';
import { Alert, Button } from '@idadawn/yue-core/shell';
import { createI18n, type PlatformClient } from '@idadawn/yue-sdk';
import { AppShell, Page, PageHeader } from '@idadawn/yue-shell/layout';
import { PreferencesProvider, initPreferences } from '@idadawn/yue-shell/preferences';
import { ShellErrorBoundary } from '@idadawn/yue-shell/fallback';
import { installShellLanguagePacks } from '@idadawn/yue-shell/locales';
import type { RemoteBridge, ShellHost, SlotContribution } from '@idadawn/qian-plugin-sdk';

const i18n = createI18n(); installShellLanguagePacks(i18n);
const preferences = initPreferences({ namespace: 'qian-shell' });
type PageComponent = ComponentType<{ client: PlatformClient; bridge: RemoteBridge }>;

export function ShellApplication({ host, client }: { host: ShellHost; client: PlatformClient }) {
  const [path, setPath] = useState(location.pathname);
  const [page, setPage] = useState<SlotContribution<PageComponent>>();
  const [error, setError] = useState(false);
  const revision = useSyncExternalStore(host.subscribe, host.getRevision, host.getRevision);
  const menu = host.catalog('qian.shell.nav/v1');
  for (const item of menu) i18n.addLanguagePack('zh-CN', { [item.descriptor.id]: item.descriptor.title });
  useEffect(() => { const changed = () => setPath(location.pathname); addEventListener('popstate', changed); return () => removeEventListener('popstate', changed); }, []);
  useEffect(() => {
    let cancelled = false;
    const descriptor = host.catalog('qian.shell.nav/v1').find((item) => item.descriptor.route === path);
    if (!descriptor) return;
    void host.loadSlot('qian.shell.nav/v1', descriptor.descriptor.id).then(() => {
      if (!cancelled) { setError(false); setPage(host.list<PageComponent>('qian.shell.nav/v1').find((item) => item.descriptor.id === descriptor.descriptor.id)); }
    }).catch(() => { if (!cancelled) { setPage(undefined); setError(true); } });
    return () => { cancelled = true; };
  }, [path, host, revision]);
  const navigate = (target: string) => { history.pushState(null, '', target); setPage(undefined); setPath(target); };
  const visiblePage = menu.some((item) => item.descriptor.route === path && item.scope === page?.scope && item.remoteId === page.remoteId) ? page : undefined;
  const RemotePage = visiblePage?.implementation;
  return <PreferencesProvider store={preferences}><AppShell i18n={i18n} brand="Qian 控制台" activePath={path} menu={menu.map(({ descriptor }) => ({ id: descriptor.id, labelKey: descriptor.id, type: 'page', target: descriptor.route! }))} onNavigate={navigate}>
    <ShellErrorBoundary i18n={i18n}><Page header={visiblePage ? undefined : <PageHeader title="工作台" />}>
      {error ? <Alert tone="error" role="alert">插件未能加载，请联系管理员核对版本与权限。</Alert>
        : RemotePage && visiblePage ? <RemotePage key={`${visiblePage.scope}:${visiblePage.remoteId}`} client={client} bridge={visiblePage.bridge} />
          : <p role="status">请选择已授权的应用页面。</p>}
      {path !== '/' && <Button variant="ghost" onClick={() => navigate('/')}>返回工作台</Button>}
    </Page></ShellErrorBoundary>
  </AppShell></PreferencesProvider>;
}
