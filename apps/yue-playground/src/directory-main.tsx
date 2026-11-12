import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { OrganizeSelect, UserSelect, createBizI18n } from '@idadawn/yue-biz';
import { PlatformClient } from '@idadawn/yue-sdk';
import './index.css';

// 真实目录验收页（S1-IDP-010 验收 2，R109）：只在 E2E 的开发服务器上用（vite.directory.config.ts），不进靶场的生产构建。
// 取数经 PlatformClient 走同源 Cookie：/api/identity 与 /api/directory 由开发服务器代理到本机真实身份宿主，不带令牌、不带租户头，租户只取自登录会话。
const client = new PlatformClient({ baseUrl: location.origin });
const i18n = createBizI18n();

function Directory() {
  const [organization, setOrganization] = useState<string | null>(null);
  const [member, setMember] = useState<string | null>(null);
  const [anyone, setAnyone] = useState<string | null>(null);
  return <main className="mx-auto grid max-w-3xl gap-6 p-8">
    <h1 className="text-2xl font-semibold">真实目录验收</h1>
    <div className="grid gap-1"><span className="text-sm font-medium">部门</span>
      <OrganizeSelect client={client} i18n={i18n} label="部门" value={organization} onChange={(next) => { setOrganization(next); setMember(null); }} /></div>
    <div className="grid gap-1"><span className="text-sm font-medium">部门成员</span>
      <UserSelect client={client} i18n={i18n} label="部门成员" organizationId={organization} value={member} onChange={setMember} /></div>
    <div className="grid gap-1"><span className="text-sm font-medium">全部成员</span>
      <UserSelect client={client} i18n={i18n} label="全部成员" value={anyone} onChange={setAnyone} /></div>
    <output aria-live="polite" data-testid="directory-state" className="text-sm text-muted-foreground">{JSON.stringify({ organization, member, anyone })}</output>
  </main>;
}

createRoot(document.getElementById('root')!).render(<Directory />);
