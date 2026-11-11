import { createRoot } from 'react-dom/client';
import scenarios from '../../../packages/yue/biz/docs/scenarios.json';
import BizDemo from '../../../packages/yue/biz/docs/examples/biz-demo';
import './index.css';
const query = new URLSearchParams(location.search);
const theme = query.get('theme');
if (theme === 'dark' || theme === 'light') { document.documentElement.dataset.theme = theme; document.documentElement.classList.toggle('dark', theme === 'dark'); }
// 默认是角色授予的组合页（S1-YUE-010 靶场 E2E）；?kind=<组件>&state=<场景> 打开与文档站相同的单组件场景。
const kind = query.get('kind') ?? 'RoleGrant';
const scenario = scenarios.find((item) => item.component === kind && item.state === (query.get('state') ?? 'Default'));
createRoot(document.getElementById('root')!).render(<main className="mx-auto max-w-4xl p-8"><h1 className="mb-6 text-2xl font-semibold">业务选择器验证</h1><BizDemo kind={kind} {...scenario?.props} /></main>);
