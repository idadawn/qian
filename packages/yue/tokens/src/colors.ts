/** 可直接存为 CSS oklch 通道，并在使用处添加 alpha。 */
export type Oklch = readonly [lightness: number, chroma: number, hue: number];
/** 线性 sRGB；用于色域检查与 WCAG 相对亮度，不把越界通道当成有效颜色。 */
export function oklchToLinearSrgb([lightness, chroma, hue]: Oklch): readonly [number, number, number] {
  const angle = hue * Math.PI / 180, a = chroma * Math.cos(angle), b = chroma * Math.sin(angle);
  const l = (lightness + 0.3963377774 * a + 0.2158037573 * b) ** 3;
  const m = (lightness - 0.1055613458 * a - 0.0638541728 * b) ** 3;
  const s = (lightness - 0.0894841775 * a - 1.291485548 * b) ** 3;
  return [4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s];
}
export function inSrgb(color: Oklch): boolean { return oklchToLinearSrgb(color).every(value => value >= -1e-7 && value <= 1 + 1e-7); }
/** 保持亮度/色相，降低 chroma 映射至 sRGB；与生成 CSS 使用同一量化精度。 */
export function fitSrgb(color: Oklch): Oklch {
  let lower = 0, upper = color[1];
  for (let step = 0; step < 28; step++) { const middle = (lower + upper) / 2; if (inSrgb([color[0], middle, color[2]])) lower = middle; else upper = middle; }
  return [color[0], Math.floor(lower * 100000) / 100000, color[2]];
}
export function contrastRatio(first: Oklch, second: Oklch): number {
  const luminance = (color: Oklch): number => { const [r, g, b] = oklchToLinearSrgb(color); return 0.2126 * Math.max(0, Math.min(1, r)) + 0.7152 * Math.max(0, Math.min(1, g)) + 0.0722 * Math.max(0, Math.min(1, b)); };
  const a = luminance(first), b = luminance(second); return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}
