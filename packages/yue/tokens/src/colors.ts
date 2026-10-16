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
export function contrastRatio(first: Oklch, second: Oklch, alpha = 1): number {
  if (!Number.isFinite(alpha) || alpha < 0 || alpha > 1) throw new Error('alpha 必须在 0–1 内。');
  const clamp = (value: number): number => Math.max(0, Math.min(1, value));
  const encode = (value: number): number => value <= 0.0031308 ? value * 12.92 : 1.055 * value ** (1 / 2.4) - 0.055;
  const decode = (value: number): number => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  const background = oklchToLinearSrgb(second).map(clamp);
  // CSS 透明色先在 sRGB 通道上合成，再转换线性亮度；不能直接线性混合亮度。
  const foreground = oklchToLinearSrgb(first).map((value, index) => decode(encode(clamp(value)) * alpha + encode(background[index]!) * (1 - alpha)));
  const luminance = (rgb: readonly number[]): number => 0.2126 * rgb[0]! + 0.7152 * rgb[1]! + 0.0722 * rgb[2]!;
  const a = luminance(foreground), b = luminance(background); return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}
