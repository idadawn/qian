import { createRoot } from 'react-dom/client';
import scenarios from '../../../packages/yue/data/docs/scenarios.json';
import DataDemo from '../../../packages/yue/data/docs/examples/data-demo';
import './index.css';
const theme = new URLSearchParams(location.search).get('theme');
if (theme === 'dark' || theme === 'light') { document.documentElement.dataset.theme = theme; document.documentElement.classList.toggle('dark', theme === 'dark'); }
const kind = new URLSearchParams(location.search).get('kind') ?? 'DataTable';
createRoot(document.getElementById('root')!).render(<main className="mx-auto max-w-4xl p-8"><h1 className="mb-6 text-2xl font-semibold">数据组件验证</h1><DataDemo kind={kind} {...scenarios.find(scenario => scenario.component === kind && scenario.state === (new URLSearchParams(location.search).get('state') ?? 'Default'))?.props} /></main>);
