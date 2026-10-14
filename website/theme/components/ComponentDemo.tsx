import { useEffect, useState } from 'react';
import { withBase } from '@rspress/core/runtime';

export function ComponentDemo({ name }: { name: string }) {
  const [theme, setTheme] = useState<string | null>(null);
  useEffect(() => {
    const sync = () => setTheme(document.documentElement.classList.contains('dark') ? 'dark' : 'light');
    sync();
    const observer = new MutationObserver(sync);
    observer.observe(document.documentElement, { attributes: true, attributeFilter: ['class'] });
    return () => observer.disconnect();
  }, []);
  if (import.meta.env.SSG_MD) return <p>本页交互预览支持状态、主题切换与本地代码编辑。示例源码见下方「用法与源码」。</p>;
  return <section aria-label={`${name} 演示`} style={{ border: '1px solid var(--rp-c-divider)', borderRadius: 12, overflow: 'hidden', margin: '24px 0' }}>
    {theme ? <iframe key={`${name}-${theme}`} title={`${name} 交互预览`} src={withBase(`/yue-demo/index.html?component=${encodeURIComponent(name)}&theme=${theme}`)} style={{ width: '100%', height: name === 'Tokens' ? 660 : 520, border: 0, display: 'block' }} allow="fullscreen" sandbox="allow-scripts allow-same-origin" /> : <p style={{ padding: 24 }}>正在加载交互演示…</p>}
  </section>;
}
