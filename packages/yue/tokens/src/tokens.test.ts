import { describe, expect, it } from 'vitest';
import { contrastRatio, inSrgb } from './colors';
import { TOKEN_REGISTRY, generateTokenCss, generateThemeCss, lintTokenCss, primitiveColors, tokenColor, tokenValue, tokenContrasts, validateTokenRegistry, type TokenRegistry } from './tokens';
import { createThemeInitializationScript } from './theme';

describe('三层令牌契约', () => {
  it('每个语义色在两种主题和全部品牌中均按登记类别达到阈值', () => {
    expect(validateTokenRegistry()).toEqual([]);
    const colors = TOKEN_REGISTRY.semantics.filter(token => token.kind === 'color');
    const pairs = tokenContrasts();
    expect(pairs).toHaveLength(colors.length * 2 * TOKEN_REGISTRY.brands.length);
    for (const pair of pairs) expect(pair.ratio, `${pair.theme}/${pair.brand}/${pair.token} against ${pair.against} (${pair.contrast})`).toBeGreaterThanOrEqual(pair.minimum);
    for (const color of primitiveColors().values()) expect(inSrgb(color)).toBe(true);
    expect(contrastRatio([0, 0, 0], [1, 0, 0])).toBeCloseTo(21, 5);
  });
  it.each([
    ['--yue-foreground', 'text', 4.5],
    ['--yue-input', 'boundary', 3],
    ['--yue-border', 'decorative', 0],
  ] as const)('%s 按 %s 分类使用 %s 阈值', (name, contrast, minimum) => {
    const pairs = tokenContrasts().filter(pair => pair.token === name);
    expect(pairs).toHaveLength(4);
    for (const pair of pairs) { expect(pair.contrast).toBe(contrast); expect(pair.minimum).toBe(minimum); }
    const registry = structuredClone(TOKEN_REGISTRY) as TokenRegistry;
    const token = registry.semantics.find(item => item.name === name)!;
    token.light = registry.semantics.find(item => item.name === token.contrastWith)!.light;
    const pair = tokenContrasts(registry).find(item => item.token === name && item.theme === 'light')!;
    expect(pair.ratio >= pair.minimum).toBe(contrast === 'decorative');
  });
  it('缺失分类不能悄悄豁免；暗色半透明边框按实际合成色计算', () => {
    const registry = structuredClone(TOKEN_REGISTRY) as TokenRegistry;
    delete registry.semantics.find(token => token.name === '--yue-input')!.contrast;
    expect(validateTokenRegistry(registry)).toContain('缺少对比度分类：--yue-input');
    expect(() => tokenContrasts(registry)).toThrow('缺少对比度分类');
    const inherited = JSON.parse(JSON.stringify(TOKEN_REGISTRY).replace('"contrast":"boundary"', '"contrast":"toString"')) as TokenRegistry;
    expect(validateTokenRegistry(inherited).some(error => error.includes('缺少对比度分类'))).toBe(true);
    expect(() => tokenContrasts(inherited)).toThrow('缺少对比度分类');
    expect(contrastRatio([1, 0, 0], [0, 0, 0], 0.1)).toBeCloseTo(1.2005, 3);
    expect(contrastRatio([1, 0, 0], [0.145, 0, 0], 0)).toBeCloseTo(1, 6);
    expect(() => contrastRatio([1, 0, 0], [0, 0, 0], -0.1)).toThrow();
  });
  it('Tailwind 主题映射覆盖所有语义颜色并导出系统字体与派生圆角', () => {
    const css = generateThemeCss();
    for (const alias of TOKEN_REGISTRY.aliases) if (TOKEN_REGISTRY.semantics.find(token => token.name === alias.target)?.kind === 'color') expect(css).toContain(`--color-${alias.name.slice(2)}: var(${alias.name})`);
    expect(css).toContain('--font-sans: var(--yue-p-font-sans)');
    expect(css).toContain('--radius-sm: calc(var(--radius) - 4px)');
    expect(css).toContain('--shadow-xs: var(--yue-p-shadow-xs)');
    expect(css).toContain('--transition-duration-fast: var(--yue-p-duration-fast)');
  });
  it('输入边界在允许的宿主背景上均达到 3:1，15% 白色不能误算为达标', () => {
    for (const theme of ['light', 'dark'] as const) for (const background of ['background', 'card', 'popover', 'muted', 'sidebar']) {
      const input = TOKEN_REGISTRY.semantics.find(token => token.name === '--yue-input')!;
      expect(contrastRatio(tokenValue(input.name, theme), tokenValue(`--yue-${background}`, theme), input[`${theme}Alpha`] ?? 1), `${theme}/${background}`).toBeGreaterThanOrEqual(3);
    }
    expect(contrastRatio([1, 0, 0], [0.145, 0, 0], 0.15)).toBeCloseTo(1.4739, 3);
    expect(tokenColor('--yue-input', 0.5)).toBe('color-mix(in oklch, oklch(var(--yue-input)) 50%, transparent)');
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
