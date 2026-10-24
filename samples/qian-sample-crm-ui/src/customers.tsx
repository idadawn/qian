import { useEffect, useState } from 'react';
import { Alert, Button, Spinner } from '@idadawn/yue-core';
import { CUSTOMER_WRITE, type CustomerPage, type RemotePageProps } from './contracts';
import './remote.css';

type View = { status: 'loading' } | { status: 'ready'; page: CustomerPage } | { status: 'failed' };
const LOADING: View = { status: 'loading' };

/** 「客户」页：经 bridge.invoke 调 qian.sample.crm/customer.list（外壳校验清单声明与权限，平台按调用者主体与租户判定并执行）。 */
export default function CustomersPage({ bridge }: RemotePageProps) {
  // 结果按「第几次加载」记账：刷新时版本号变了，旧结果自动视为加载中，不在副作用里同步改状态。
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{ revision: number; view: View }>();
  useEffect(() => {
    const controller = new AbortController();
    bridge.invoke<CustomerPage>('qian.sample.crm/customer.list', { skip: 0, take: 100 }, { signal: controller.signal })
      .then(page => { if (!controller.signal.aborted) setResult({ revision, view: { status: 'ready', page } }); })
      .catch(() => { if (!controller.signal.aborted) setResult({ revision, view: { status: 'failed' } }); });
    return () => controller.abort();
  }, [bridge, revision]);
  const view = result?.revision === revision ? result.view : LOADING;
  const context = bridge.getContext();
  const writable = context.membership?.permissions.includes(CUSTOMER_WRITE) ?? false;
  return <section data-testid="qian-sample-crm-customers" data-tenant={context.tenant?.id ?? ''} className="space-y-4 p-4 md:p-6">
    <header className="flex flex-wrap items-center justify-between gap-3">
      <h1 className="text-xl font-semibold">客户</h1>
      <div className="flex gap-2">
        <Button variant="outline" onClick={() => setRevision(value => value + 1)}>刷新</Button>
        {writable && <Button onClick={() => bridge.navigate('/customers/new')}>新建客户</Button>}
      </div>
    </header>
    {view.status === 'loading' ? <Spinner label="加载中" />
      : view.status === 'failed' ? <Alert tone="error" role="alert">客户列表加载失败，请稍后重试。</Alert>
      : view.page.items.length === 0 ? <p role="status" className="text-sm text-muted-foreground">暂无客户</p>
      : <ul aria-label="客户列表" className="divide-y divide-border rounded-md border border-border">
        {view.page.items.map(item => <li key={item.id} data-customer={item.id} className="px-3 py-2 text-sm">{item.name}</li>)}
      </ul>}
  </section>;
}
