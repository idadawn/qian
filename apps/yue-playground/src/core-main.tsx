import { createRoot } from 'react-dom/client';
import { CORE_COMPONENT_MANIFESTS } from '@idadawn/yue-core';
import scenarios from '../../../packages/yue/core/docs/scenarios.json';
import { DEMOS } from '../../../packages/yue/core/docs/examples/catalog';
import './index.css';

const query = new URLSearchParams(window.location.search);
const state = query.get('state') ?? 'Default';
const selected = query.get('component');
const theme = query.get('theme');
if (theme === 'dark' || theme === 'light') { document.documentElement.dataset.theme = theme; document.documentElement.classList.toggle('dark', theme === 'dark'); }
const names = Object.keys(DEMOS) as (keyof typeof DEMOS)[];
createRoot(document.getElementById('root')!).render(
  <main className="mx-auto min-h-screen max-w-6xl bg-background px-6 py-10 text-foreground">
    <header className="mb-8 border-b border-border pb-6"><p className="mb-2 text-xs font-semibold tracking-widest text-muted-foreground">QIAN / YUE</p><h1 className="text-3xl font-semibold">基础组件目录</h1><p className="mt-3 text-sm text-muted-foreground">51 个组件入口 · Base UI · 可访问交互与明暗主题</p><nav aria-label="主题" className="mt-4 flex gap-4 text-sm"><a href={`?${new URLSearchParams({ ...(selected ? { component: selected } : {}), theme: 'light' })}`}>明亮</a><a href={`?${new URLSearchParams({ ...(selected ? { component: selected } : {}), theme: 'dark' })}`}>暗色</a><a href="/">验证门首页</a></nav></header>
    <div className={selected ? 'space-y-6' : 'grid gap-5 md:grid-cols-2'}>{names.filter(name => !selected || name === selected).map(name => {
      const Demo = DEMOS[name];
      const metadata = CORE_COMPONENT_MANIFESTS.find(item => item.name === name)!;
      return <section key={name} aria-labelledby={`title-${name}`} className="min-w-0 rounded-xl border border-border bg-card p-6 text-card-foreground"><div className="mb-6 flex items-baseline justify-between gap-4"><h2 id={`title-${name}`} className="font-semibold">{name}</h2><span className="text-xs text-muted-foreground">{metadata.a11y.role}</span></div><Demo {...scenarios.find(scenario => scenario.component === name && scenario.state === state)?.props} /></section>;
    })}</div>
  </main>,
);
