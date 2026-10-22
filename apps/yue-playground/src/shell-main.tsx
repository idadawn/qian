import { createRoot } from 'react-dom/client';
import { lazy, Suspense } from 'react';
import scenarios from '../../../packages/yue/shell/docs/scenarios.json';
import './index.css';

// 示例按页面加载；Authentication 内部继续按认证路由懒加载密码表单。
const ShellDemo = lazy(() => import('../../../packages/yue/shell/docs/examples/shell-demo'));
const query = new URLSearchParams(location.search);
const theme = query.get('theme');
if (theme === 'dark' || theme === 'light') { document.documentElement.dataset.theme = theme; document.documentElement.classList.toggle('dark', theme === 'dark'); }
const component = query.get('component') ?? 'AppShell';
const state = query.get('state') ?? 'Default';
const scenario = scenarios.find(value => value.component === component && value.state === state) ?? scenarios.find(value => value.component === component);
createRoot(document.getElementById('root')!).render(<Suspense fallback={<p role="status">加载中</p>}><ShellDemo {...scenario?.props} /></Suspense>);
