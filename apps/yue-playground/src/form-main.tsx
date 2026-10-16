import { createRoot } from 'react-dom/client';
import SchemaFormDemo from '../../../packages/yue/form/docs/examples/schema-form-demo';
import './index.css';

createRoot(document.getElementById('root')!).render(<main className="mx-auto max-w-3xl p-8"><h1 className="mb-6 text-2xl font-semibold">SchemaForm 验证</h1><SchemaFormDemo /></main>);
