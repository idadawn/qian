import { useId, useState, type FormEvent } from 'react';
import { Alert, Button, Input, Label } from '@idadawn/yue-core';
import type { RemotePageProps } from './contracts';
import './remote.css';

/** 「新建客户」页：经 bridge.invoke 调 qian.sample.crm/customer.create（写能力，外壳补幂等键），成功后回到客户列表。 */
export default function CreateCustomerPage({ bridge }: RemotePageProps) {
  const id = useId();
  const [name, setName] = useState('');
  const [busy, setBusy] = useState(false);
  const [failed, setFailed] = useState(false);
  const submit = async (event: FormEvent) => {
    event.preventDefault();
    const value = name.trim();
    if (!value || busy) return;
    setBusy(true); setFailed(false);
    try {
      await bridge.invoke('qian.sample.crm/customer.create', { name: value });
      bridge.notify('success', '客户已创建');
      bridge.navigate('/customers');
    } catch { setFailed(true); } finally { setBusy(false); }
  };
  return <section data-testid="qian-sample-crm-create" className="space-y-4 p-4 md:p-6">
    <h1 className="text-xl font-semibold">新建客户</h1>
    <form onSubmit={event => void submit(event)} className="max-w-md space-y-3">
      <div className="space-y-1.5"><Label htmlFor={id}>客户名称</Label><Input id={id} value={name} maxLength={200} required onChange={event => setName(event.currentTarget.value)} /></div>
      {failed && <Alert tone="error" role="alert">创建失败，请稍后重试。</Alert>}
      <Button type="submit" loading={busy}>创建</Button>
    </form>
  </section>;
}
