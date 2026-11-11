import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { initializeTheme } from '@idadawn/yue-tokens';
import { App } from './App';
import { i18n } from './i18n';
import { preferences } from './preferences';
import './index.css';

// 主题跟随偏好；保存过的语言在首帧前生效。
const theme = initializeTheme({ defaultTheme: preferences.getSnapshot().theme });
if (i18n.instance.language !== preferences.getSnapshot().locale) void i18n.changeLanguage(preferences.getSnapshot().locale);
preferences.subscribe(() => theme.setTheme(preferences.getSnapshot().theme));

createRoot(document.getElementById('root')!).render(<StrictMode><App /></StrictMode>);
