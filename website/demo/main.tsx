import * as React from 'react';
import { createRoot } from 'react-dom/client';
import * as Core from '@idadawn/yue-core';
import * as Form from '@idadawn/yue-form';
import * as Sdk from '@idadawn/yue-sdk';
import * as Data from '@idadawn/yue-data';
import { DATA_DEMOS } from '../../packages/yue/data/docs/examples/data-demo';
import dataScenarios from '../../packages/yue/data/docs/scenarios.json';
import SchemaFormDemo from '../../packages/yue/form/docs/examples/schema-form-demo';
import formScenarios from '../../packages/yue/form/docs/scenarios.json';
import { TOKEN_REGISTRY } from '@idadawn/yue-tokens';
import { DEMOS, type DemoProps } from '../../packages/yue/core/docs/examples/catalog';
import scenarios from '../../packages/yue/core/docs/scenarios.json';
import sources from 'yue-demo-sources';
import './index.css';

const query = new URLSearchParams(location.search);
const name = query.get('component') ?? 'Button';
const initialState = query.get('state') ?? (name === 'Tokens' ? 'Light' : 'Default');
const themes = ['light', 'dark'] as const;
const labels: Record<string, string> = { Default: '默认', Light: '明亮', Dark: '暗色', Disabled: '禁用', Error: '错误', Hover: '悬停', Readonly: '只读', Loading: '加载', Empty: '空', Compact: '紧凑' };
const modules: Record<string, unknown> = { react: React, '@idadawn/yue-core': Core, '@idadawn/yue-form': Form, '@idadawn/yue-data': Data, '@idadawn/yue-sdk': Sdk, '@idadawn/yue-tokens': { TOKEN_REGISTRY } };
const states = [...scenarios, ...formScenarios, ...dataScenarios].filter(s => s.component === name);
let editorPromise: Promise<void> | undefined;
function script(url: string) {
  return new Promise<void>((resolve, reject) => {
    const element = document.createElement('script'); element.src = url; element.onload = () => resolve(); element.onerror = () => reject(new Error('无法加载本地编辑器资源')); document.head.append(element);
  });
}
async function loadEditor() {
  editorPromise ??= (async () => {
    await script('../yue-vendor/babel.min.js');
    await script('../yue-vendor/vs/loader.js');
    window.require.config({ paths: { vs: new URL('../yue-vendor/vs', location.href).href } });
    await new Promise<void>((resolve, reject) => window.require(['vs/editor/editor.main'], resolve, reject));
  })();
  return editorPromise;
}
function Tokens() {
  return <div><h1 className="mb-4 text-xl font-semibold">yue 设计令牌</h1><div className="grid grid-cols-2 gap-3">{TOKEN_REGISTRY.semantics.filter(token => token.kind === 'color').map(token => <div key={token.name} className="rounded-lg p-4" style={token.contrast === 'text' ? { background: `oklch(var(${token.contrastWith}))`, color: `oklch(var(${token.name}))` } : { background: 'var(--background)', color: 'var(--foreground)', border: `3px solid oklch(var(${token.name}))` }}><strong className="break-all text-sm">{token.name}</strong><p className="mt-2 text-xs">{token.meaning}</p></div>)}</div><h2 className="mt-6 font-semibold">组件令牌</h2><dl>{TOKEN_REGISTRY.components.map(token => <div key={token.name}><dt>{token.name}</dt><dd>{token.meaning}：{token.value}</dd></div>)}</dl></div>;
}
class Boundary extends React.Component<{ children: React.ReactNode }, { error: string }> {
  state = { error: '' };
  static getDerivedStateFromError(error: Error) { return { error: error.message }; }
  render() { return this.state.error ? <p role="alert" className="demo-error">示例运行失败：{this.state.error}</p> : this.props.children; }
}
function App() {
  const [state, setState] = React.useState(initialState);
  const [theme, setTheme] = React.useState(query.get('theme') === 'dark' ? 'dark' : 'light');
  const [revision, setRevision] = React.useState(0);
  const [editing, setEditing] = React.useState(false);
  const [loading, setLoading] = React.useState(false);
  const [copied, setCopied] = React.useState(false);
  const [error, setError] = React.useState('');
  const [edited, setEdited] = React.useState<React.ComponentType<DemoProps> | null>(null);
  const editorHost = React.useRef<HTMLDivElement>(null);
  const editor = React.useRef<ReturnType<Window['monaco']['editor']['create']> | null>(null);
  const original = name === 'Tokens' ? Tokens : name === 'SchemaForm' ? SchemaFormDemo : DATA_DEMOS[name] ?? DEMOS[name as keyof typeof DEMOS];
  const Demo = edited ?? original;
  const scenario = states.find(s => s.state === state) ?? states[0];
  React.useEffect(() => {
    const receive = (event: MessageEvent) => {
      if (event.origin === location.origin && event.source === parent && event.data?.type === 'yue-doc-theme' && ['light', 'dark'].includes(event.data.theme)) setTheme(event.data.theme);
    };
    addEventListener('message', receive);
    return () => removeEventListener('message', receive);
  }, []);
  React.useEffect(() => {
    document.documentElement.dataset.theme = theme;
    if (window.monaco) window.monaco.editor.setTheme(theme === 'dark' ? 'vs-dark' : 'vs');
  }, [theme]);
  React.useEffect(() => {
    if (!editing) return;
    let cancelled = false;
    setLoading(true);
    void loadEditor().then(() => {
      if (cancelled || !editorHost.current) return;
      editor.current = window.monaco.editor.create(editorHost.current, { value: sources[name], language: 'typescript', theme: document.documentElement.dataset.theme === 'dark' ? 'vs-dark' : 'vs', automaticLayout: true, wordWrap: 'on', minimap: { enabled: false }, ariaLabel: `${name} 示例代码`, fontSize: 13, scrollBeyondLastLine: false });
      setLoading(false);
    }).catch((e: unknown) => { if (!cancelled) { setError(String(e)); setLoading(false); } });
    return () => { cancelled = true; editor.current?.dispose(); editor.current = null; };
  }, [editing]);
  async function copyCode() {
    const value = editor.current?.getValue() ?? sources[name] ?? '';
    try {
      if (navigator.clipboard?.writeText) await navigator.clipboard.writeText(value);
      else {
        const area = document.createElement('textarea');
        area.value = value; area.style.position = 'fixed'; area.style.opacity = '0';
        document.body.append(area); area.select();
        const copied = document.execCommand('copy'); area.remove();
        if (!copied) throw new Error('请在编辑器中选择代码后复制');
      }
      setCopied(true);
    } catch { setError('复制失败，请在编辑器中选择代码后复制。'); }
  }
  function run() {
    try {
      const code = window.Babel.transform(editor.current?.getValue() ?? sources[name] ?? '', { filename: 'example.tsx', presets: ['typescript', ['react', { runtime: 'classic' }]], plugins: ['transform-modules-commonjs'] }).code;
      const output: { exports: { default?: React.ComponentType<DemoProps> } } = { exports: {} };
      // 仅供文档本地编辑；预打包依赖白名单，不携带产品身份或租户上下文。
      new Function('require', 'module', 'exports', 'React', code)((id: string) => { if (!Object.hasOwn(modules, id)) throw new Error(`示例未提供依赖：${id}`); return modules[id]; }, output, output.exports, React);
      if (typeof output.exports.default !== 'function') throw new Error('请默认导出一个 React 组件');
      setEdited(() => output.exports.default!); setError(''); setRevision(v => v + 1);
    } catch (e) { setError(e instanceof Error ? e.message : String(e)); }
  }
  if (!Demo) return <p role="alert">找不到组件示例。</p>;
  return <><nav className="demo-toolbar" aria-label="演示设置"><label>状态 <select aria-label="示例状态" value={state} onChange={e => { setState(e.target.value); if (e.target.value === 'Dark') setTheme('dark'); setRevision(v => v + 1); }}>{states.map(s => <option key={s.id} value={s.state}>{labels[s.state]}</option>)}</select></label><label>主题 <select aria-label="示例主题" value={theme} onChange={e => setTheme(e.target.value)}>{themes.map(t => <option key={t} value={t}>{t === 'dark' ? '暗色' : '明亮'}</option>)}</select></label><button onClick={() => { setEdited(null); setRevision(v => v + 1); editor.current?.setValue(sources[name] ?? ''); setError(''); }}>重置</button>{name !== 'Tokens' && <button aria-expanded={editing} onClick={() => setEditing(v => !v)}>{editing ? '收起编辑器' : '编辑代码'}</button>}{editing && <><button disabled={loading} onClick={run}>运行</button><button disabled={loading} onClick={() => void copyCode()}>{copied ? "已复制" : "复制代码"}</button></>}</nav>{editing && <><div ref={editorHost} className="demo-editor" />{loading && <p role="status">正在加载本地编辑器…</p>}</>}{error && <p role="alert" className="demo-error">{error}</p>}<main className="demo-stage" aria-label={`${name} 交互预览`}><Boundary key={`${state}-${revision}`}><Demo {...scenario?.props} /></Boundary></main></>;
}
createRoot(document.getElementById('root')!).render(<App />);
