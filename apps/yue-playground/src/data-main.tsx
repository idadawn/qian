import { createRoot } from 'react-dom/client';
import DataDemo from '../../../packages/yue/data/docs/examples/data-demo';
import './index.css';
const kind = new URLSearchParams(location.search).get('kind') ?? 'DataTable';
createRoot(document.getElementById('root')!).render(<main className="mx-auto max-w-4xl p-8"><h1 className="mb-6 text-2xl font-semibold">数据组件验证</h1><DataDemo kind={kind} /></main>);
