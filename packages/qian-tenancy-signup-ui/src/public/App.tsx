import { useEffect } from 'react';
import { Button, Result } from '@idadawn/yue-core';
import { FeedbackProvider } from '@idadawn/yue-form';
import type { PlatformClient } from '@idadawn/yue-sdk';
import { i18n } from '../i18n';
import { SignupPage } from './SignupPage';
import { StatusPage } from './StatusPage';
import { browser } from './navigation';

/** 公开页只有两个落点：/signup 与 /signup/status；其余路径给出返回入口。 */
export function App({ client }: { client: PlatformClient }) {
  const path = location.pathname.replace(/\/+$/, '') || '/';
  const page = path === '/signup' ? 'apply' : path === '/signup/status' ? 'status' : 'missing';
  useEffect(() => {
    document.documentElement.lang = i18n.instance.language;
    document.title = i18n.t(page === 'status' ? 'signup.statusDocumentTitle' : 'signup.documentTitle');
  }, [page]);
  return <FeedbackProvider i18n={i18n}>
    {page === 'apply' ? <SignupPage client={client} /> : page === 'status' ? <StatusPage client={client} />
      : <main className="flex min-h-dvh items-center justify-center bg-canvas p-4"><Result tone="warn" icon="alert" title={i18n.t('signup.missing')} description={i18n.t('signup.missingText')}
        action={<Button onClick={() => browser.assign('/signup')}>{i18n.t('signup.applyNow')}</Button>} /></main>}
  </FeedbackProvider>;
}
