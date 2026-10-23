import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { readAppConf, type AppConf } from '@idadawn/yue-shell';
import { App, ConfigError } from './App';
import './index.css';

// 运行时配置先于一切：window.__APP_CONF__ 由同源 /app-conf.js 在模块脚本之前注入；非法时只渲染配置错误页，不发任何请求。
function configuration(): AppConf | null { try { return readAppConf(); } catch { return null; } }
const conf = configuration();
createRoot(document.getElementById('root')!).render(<StrictMode>{conf ? <App conf={conf} /> : <ConfigError />}</StrictMode>);
