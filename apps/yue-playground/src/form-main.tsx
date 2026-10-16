import { createRoot } from 'react-dom/client';
import scenarios from '../../../packages/yue/form/docs/scenarios.json';
import SchemaFormDemo from '../../../packages/yue/form/docs/examples/schema-form-demo';
import './index.css';
const theme = new URLSearchParams(location.search).get('theme');
if (theme === 'dark' || theme === 'light') { document.documentElement.dataset.theme = theme; document.documentElement.classList.toggle('dark', theme === 'dark'); }

createRoot(document.getElementById('root')!).render(<main className="mx-auto max-w-3xl p-8"><h1 className="mb-6 text-2xl font-semibold">SchemaForm 验证</h1><SchemaFormDemo {...scenarios.find(scenario => scenario.state === (new URLSearchParams(location.search).get('state') ?? 'Default'))?.props} /></main>);
