import { useEffect, useRef, useState } from 'react';
import { withBase } from '@rspress/core/runtime';

function InteractiveDemo({ name }: { name: string }) {
  const [initialTheme, setInitialTheme] = useState<string | null>(null);
  const frame = useRef<HTMLIFrameElement>(null);
  const sendTheme = () => frame.current?.contentWindow?.postMessage({ type: 'yue-doc-theme', theme: document.documentElement.classList.contains('dark') ? 'dark' : 'light' }, location.origin);
  useEffect(() => {
    const sync = () => {
      setInitialTheme(value => value ?? (document.documentElement.classList.contains('dark') ? 'dark' : 'light'));
      sendTheme();
    };
    sync();
    const observer = new MutationObserver(sync);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    return () => observer.disconnect();
  }, []);
  return <section aria-label={`${name} 演示`} style={{ border: '1px solid var(--rp-c-divider)', borderRadius: 12, overflow: 'hidden', margin: '24px 0' }}>
    {initialTheme ? <iframe ref={frame} onLoad={sendTheme} title={`${name} 交互预览`} src={withBase(`/yue-demo/index.html?component=${encodeURIComponent(name)}&theme=${initialTheme}`)} style={{ width: '100%', height: name === 'AppShell' ? 760 : name === 'Tokens' ? 660 : 520, border: 0, display: 'block' }} allow="fullscreen" sandbox={name === 'Authentication' || name === 'SchemaForm' ? 'allow-scripts allow-same-origin allow-forms' : 'allow-scripts allow-same-origin'} /> : <p style={{ padding: 24 }}>正在加载交互演示…</p>}
  </section>;
}
export function ComponentDemo({ name }: { name: string }) {
  if (import.meta.env.SSG_MD) return <p>本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。</p>;
  return <InteractiveDemo key={name} name={name} />;
}
