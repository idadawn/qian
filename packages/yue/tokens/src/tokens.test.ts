import { describe, expect, it } from 'vitest';
import { contrastRatio, inSrgb } from './colors';
import { TOKEN_REGISTRY, generateTokenCss, lintTokenCss, primitiveColors, tokenColor, tokenContrasts, validateTokenRegistry, type TokenRegistry } from './tokens';
import { createThemeInitializationScript } from './theme';

describe('三层令牌契约', () => {
  it('每个语义色在两种主题和全部品牌中均有对比配对且达到 AA', () => {
    expect(validateTokenRegistry()).toEqual([]);
    const colors = TOKEN_REGISTRY.semantics.filter(token => token.kind === 'color');
    const pairs = tokenContrasts();
    expect(pairs).toHaveLength(colors.length * 2 * TOKEN_REGISTRY.brands.length);
    for (const pair of pairs) expect(pair.ratio, `${pair.theme}/${pair.brand}/${pair.token} against ${pair.against}`).toBeGreaterThanOrEqual(4.5);
    for (const color of primitiveColors().values()) expect(inSrgb(color)).toBe(true);
    expect(contrastRatio([0, 0, 0], [1, 0, 0])).toBeCloseTo(21, 5);
  });
  it('同名异义、未登记引用和跨层引用被拒绝', () => {
    const registry = structuredClone(TOKEN_REGISTRY) as TokenRegistry;
    registry.aliases.push({ name: '--accent', target: '--yue-primary' });
    expect(validateTokenRegistry(registry).some(error => error.includes('--accent'))).toBe(true);
    expect(() => generateTokenCss(registry)).toThrow('同名异义');
    expect(lintTokenCss(':root { --accent: red; }')).toHaveLength(1);
    expect(lintTokenCss('button { background: var(--accent); }')).toEqual([]);
    const wrong = structuredClone(TOKEN_REGISTRY) as TokenRegistry;
    wrong.components.push({ name: '--yue-other-value', meaning: '错误依赖', value: 'var(--yue-button-radius)' });
    expect(validateTokenRegistry(wrong).some(error => error.includes('只能引用下层'))).toBe(true);
  });
  it('CSS 保留 alpha 能力、明确暗色选择器且品牌不依赖媒体查询', () => {
    const css = generateTokenCss();
    expect(css).toContain('html.dark, html[data-theme="dark"]');
    expect(css).toContain('html[data-brand="teal"]');
    expect(css).not.toContain('prefers-color-scheme');
    expect(tokenColor('--yue-primary', 0.4)).toBe('oklch(var(--yue-primary) / 0.4)');
    expect(() => tokenColor('--unknown')).toThrow();
    expect(() => tokenColor('--yue-primary', 2)).toThrow();
  });
  it('SSR 可生成自包含脚本，配置不能闭合 script 标签', () => {
    const script = createThemeInitializationScript({ storageKey: '</script><script>bad</script>', brand: 'teal' });
    expect(script).not.toContain('</script>');
    expect(script).toContain('data');
    expect(script).toContain('matchMedia');
    expect(() => createThemeInitializationScript({ brand: 'bad"id' })).toThrow();
  });
});
