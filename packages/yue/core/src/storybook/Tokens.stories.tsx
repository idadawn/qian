import { TOKEN_REGISTRY } from '@idadawn/yue-tokens';
import preview from './preview';
function Tokens() {
  return <div><h1 className="mb-2 text-2xl font-semibold">yue 设计令牌</h1><p className="mb-6 text-sm text-[var(--muted-foreground)]">三层令牌由同一目录生成，使用工具栏切换明暗主题。</p><div className="grid grid-cols-2 gap-3 md:grid-cols-4">{TOKEN_REGISTRY.semantics.filter(token => token.kind === 'color').map(token => <div key={token.name} className="rounded-lg p-4" style={{ background: `oklch(var(${token.contrastWith}))`, color: `oklch(var(${token.name}))` }}><strong className="break-all text-sm">{token.name}</strong><p className="mt-2 text-xs">{token.meaning}</p></div>)}</div><h2 className="mt-8 text-lg font-semibold">组件令牌</h2><dl className="mt-3 grid grid-cols-2 gap-2">{TOKEN_REGISTRY.components.map(token => <div key={token.name}><dt className="font-mono text-sm">{token.name}</dt><dd className="text-sm">{token.meaning}：{token.value}</dd></div>)}</dl></div>;
}
const meta = preview.meta({ title: 'Foundations/Tokens', component: Tokens });
export const Light = meta.story({});
export const Dark = Light.extend({ globals: { theme: 'dark' } });
