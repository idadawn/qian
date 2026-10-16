import source from './registry.json';
import { contrastRatio, fitSrgb, type Oklch } from './colors';

export type Tone = 'ok' | 'warn' | 'error' | 'info' | 'neutral';
export type Theme = 'light' | 'dark';
export type ContrastKind = 'text' | 'boundary' | 'decorative';
export const CONTRAST_MINIMUM: Readonly<Record<ContrastKind, number>> = { text: 4.5, boundary: 3, decorative: 0 };
export interface TokenDefinition { name: string; meaning: string; value?: string; kind?: string; light?: string; dark?: string; contrastWith?: string; contrast?: ContrastKind; lightAlpha?: number; darkAlpha?: number }
export interface TokenRegistry {
  palettes: { name: string; hue: number; chroma: number }[];
  scale: { step: number; lightness: number; chromaFactor: number }[];
  primitives: TokenDefinition[]; semantics: TokenDefinition[]; components: TokenDefinition[];
  brands: { id: string; palette: string }[]; aliases: { name: string; target: string }[];
}
/** 唯一命名与角色真相源；CSS、检查器和文档均从该目录派生。 */
export const TOKEN_REGISTRY: Readonly<TokenRegistry> = source as TokenRegistry;
export const TONES: readonly Tone[] = ['ok', 'warn', 'error', 'info', 'neutral'];
export function primitiveColors(registry: TokenRegistry = TOKEN_REGISTRY): ReadonlyMap<string, Oklch> {
  const colors = new Map<string, Oklch>([['--yue-p-white', [1, 0, 0]], ['--yue-p-black', [0, 0, 0]]]);
  for (const palette of registry.palettes) for (const scale of registry.scale) colors.set(`--yue-p-${palette.name}-${scale.step}`, fitSrgb([scale.lightness, palette.chroma * scale.chromaFactor, palette.hue]));
  return colors;
}
export function tokenValue(name: string, theme: Theme, brand = 'default', registry: TokenRegistry = TOKEN_REGISTRY): Oklch {
  const palette = registry.brands.find(item => item.id === brand)?.palette;
  if (!palette) throw new Error('未知品牌令牌组。');
  const definition = registry.semantics.find(item => item.name === name);
  const reference = definition?.[theme] ?? name;
  const color = primitiveColors(registry).get(reference.replace('--yue-p-brand-', `--yue-p-${palette}-`));
  if (!color) throw new Error('该令牌不是已登记的颜色。'); return color;
}
export interface TokenContrast { theme: Theme; brand: string; token: string; against: string; ratio: number; contrast: ContrastKind; minimum: number }
export function tokenContrasts(registry: TokenRegistry = TOKEN_REGISTRY): TokenContrast[] {
  const result: TokenContrast[] = [];
  for (const theme of ['light', 'dark'] as const) for (const brand of registry.brands) for (const token of registry.semantics) {
    if (token.kind !== 'color' || !token.contrastWith) continue;
    if (!token.contrast || !(Object.hasOwn(CONTRAST_MINIMUM, token.contrast))) throw new Error(`缺少对比度分类：${token.name}`);
    result.push({ theme, brand: brand.id, token: token.name, against: token.contrastWith, contrast: token.contrast, minimum: CONTRAST_MINIMUM[token.contrast], ratio: contrastRatio(tokenValue(token.name, theme, brand.id, registry), tokenValue(token.contrastWith, theme, brand.id, registry), token[`${theme}Alpha`] ?? 1) });
  }
  return result;
}
/** 名称不能跨角色复用；所有引用必须指向正确层级，所有语义色都有明确对比背景。 */
export function validateTokenRegistry(registry: TokenRegistry = TOKEN_REGISTRY): string[] {
  const errors: string[] = [], names = new Map<string, string>(), primitives = new Set(primitiveColors(registry).keys());
  for (const scale of registry.scale) primitives.add(`--yue-p-brand-${scale.step}`);
  const add = (name: string, meaning: string): void => { if (names.has(name)) errors.push(`同名异义或重复令牌：${name}（${names.get(name)} / ${meaning}）`); names.set(name, meaning); };
  for (const name of primitives) add(name, '基础色阶');
  for (const token of registry.primitives) { add(token.name, token.meaning); primitives.add(token.name); if (!token.name.startsWith('--yue-p-')) errors.push(`primitive 命名无效：${token.name}`); }
  const semantics = new Set(registry.semantics.map(item => item.name));
  for (const token of registry.semantics) {
    add(token.name, token.meaning);
    if (!token.light || !token.dark || !primitives.has(token.light) || !primitives.has(token.dark)) errors.push(`semantic 引用未登记：${token.name}`);
    if (token.kind === 'color' && (!token.contrastWith || !semantics.has(token.contrastWith))) errors.push(`缺少语义对比色：${token.name}`);
    if (token.kind === 'color' && (!token.contrast || !(Object.hasOwn(CONTRAST_MINIMUM, token.contrast)))) errors.push(`缺少对比度分类：${token.name}`);
    for (const alpha of [token.lightAlpha, token.darkAlpha]) if (alpha !== undefined && (!Number.isFinite(alpha) || alpha < 0 || alpha > 1)) errors.push(`透明度无效：${token.name}`);
  }
  for (const token of registry.components) { add(token.name, token.meaning); for (const match of token.value?.matchAll(/var\((--[a-z0-9-]+)\)/g) ?? []) if (!primitives.has(match[1]!) && !semantics.has(match[1]!)) errors.push(`component 只能引用下层：${token.name}`); }
  for (const alias of registry.aliases) { add(alias.name, `别名 ${alias.target}`); if (!semantics.has(alias.target)) errors.push(`别名指向未登记角色：${alias.name}`); }
  return errors;
}
/** CSS 消费者不能另行定义已有角色，避免 --accent 等名字被解释成另一种含义。 */
export function lintTokenCss(css: string, registry: TokenRegistry = TOKEN_REGISTRY): string[] {
  const known = new Set([...registry.semantics, ...registry.components].map(item => item.name).concat(registry.aliases.map(item => item.name)));
  return [...css.replace(/\/\*[\s\S]*?\*\//g, '').matchAll(/(--[a-z0-9-]+)\s*:/g)].filter(match => known.has(match[1]!)).map(match => `令牌角色只能在唯一目录中定义：${match[1]}`);
}
export function tokenColor(name: string, alpha = 1): string {
  if (!source.semantics.some(token => token.name === name && token.kind === 'color') && !primitiveColors().has(name)) throw new Error('颜色令牌未登记。');
  if (!Number.isFinite(alpha) || alpha < 0 || alpha > 1) throw new Error('alpha 必须在 0–1 内。');
  const token = TOKEN_REGISTRY.semantics.find(item => item.name === name);
  return token?.lightAlpha !== undefined || token?.darkAlpha !== undefined ? `color-mix(in oklch, oklch(var(${name})) ${alpha * 100}%, transparent)` : `oklch(var(${name}) / ${alpha})`;
}
export function generateTokenCss(registry: TokenRegistry = TOKEN_REGISTRY): string {
  const errors = validateTokenRegistry(registry); if (errors.length) throw new Error(errors.join('\n'));
  const lines = ['/* 由 registry.json 生成；运行 pnpm --filter @idadawn/yue-tokens tokens:generate。 */', ':root {', '  color-scheme: light;'];
  for (const [name, color] of primitiveColors(registry)) lines.push(`  ${name}: ${color.join(' ')};`);
  for (const token of registry.primitives) lines.push(`  ${token.name}: ${token.value};`);
  const primary = registry.brands.find(item => item.id === 'default')!;
  for (const scale of registry.scale) lines.push(`  --yue-p-brand-${scale.step}: var(--yue-p-${primary.palette}-${scale.step});`);
  const semanticValue = (token: TokenDefinition, theme: Theme): string => `var(${token[theme]})${token[`${theme}Alpha`] === undefined ? '' : ` / ${token[`${theme}Alpha`]}`}`;
  for (const token of registry.semantics) lines.push(`  ${token.name}: ${semanticValue(token, 'light')};`);
  for (const token of registry.components) lines.push(`  ${token.name}: ${token.value};`);
  for (const alias of registry.aliases) { const token = registry.semantics.find(item => item.name === alias.target)!; lines.push(`  ${alias.name}: ${token.kind === 'color' ? `oklch(var(${alias.target}))` : `var(${alias.target})`};`); }
  lines.push('}', 'html.dark, html[data-theme="dark"] {', '  color-scheme: dark;');
  for (const token of registry.semantics) lines.push(`  ${token.name}: ${semanticValue(token, 'dark')};`);
  lines.push('}', 'html[data-theme="light"] { color-scheme: light; }');
  lines.push('[data-yue-density="compact"] { --yue-table-row-height: var(--yue-table-row-height-compact); }', '[data-yue-density="default"] { --yue-table-row-height: var(--yue-p-space-10); }');
  for (const brand of registry.brands) { lines.push(`html[data-brand="${brand.id}"] {`); for (const scale of registry.scale) lines.push(`  --yue-p-brand-${scale.step}: var(--yue-p-${brand.palette}-${scale.step});`); lines.push('}'); }
  lines.push('@media (prefers-reduced-motion: reduce) {', '  :root { --yue-p-duration-fast: 0ms; --yue-p-duration-normal: 0ms; --yue-p-duration-slow: 0ms; }', '}');
  return lines.join('\n') + '\n';
}

/** Tailwind v4 消费入口与色彩目录同源，支持 bg-primary/90 等透明度配方。 */
export function generateThemeCss(registry: TokenRegistry = TOKEN_REGISTRY): string {
  const lines = ['/* 由 registry.json 生成；运行 tokens:generate。 */', "@import './index.css';", "@import 'tw-animate-css';", '@custom-variant dark (&:where(.dark, .dark *, [data-theme="dark"], [data-theme="dark"] *));', '@theme inline {'];
  for (const alias of registry.aliases) if (registry.semantics.find(token => token.name === alias.target)?.kind === 'color') lines.push(`  --color-${alias.name.slice(2)}: var(${alias.name});`);
  lines.push('  --font-sans: var(--yue-p-font-sans);', '  --font-mono: var(--yue-p-font-mono);', '  --radius-sm: calc(var(--radius) - 4px);', '  --radius-md: calc(var(--radius) - 2px);', '  --radius-lg: var(--radius);', '  --radius-xl: calc(var(--radius) + 4px);');
  for (const size of ['xs', 'sm', 'md', 'lg', 'xl']) lines.push(`  --shadow-${size}: var(--yue-p-shadow-${size});`);
  for (const speed of ['fast', 'normal', 'slow']) lines.push(`  --transition-duration-${speed}: var(--yue-p-duration-${speed});`);
  lines.push('  --ease-standard: var(--yue-p-ease-standard);', '}', '@layer base {', '  body { @apply font-sans text-sm antialiased bg-background text-foreground; }', '}', '');
  return lines.join('\n');
}
