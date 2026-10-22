import { initializeTheme, TONES, TOKEN_REGISTRY, tokenColor, type ThemePreference } from '@idadawn/yue-tokens';
import './tokens.css';

const controller = initializeTheme();
const theme = document.querySelector<HTMLSelectElement>('#theme')!;
const brand = document.querySelector<HTMLSelectElement>('#brand')!;
theme.value = controller.getState().preference; brand.value = controller.getState().brand;
theme.addEventListener('change', () => controller.setTheme(theme.value as ThemePreference));
brand.addEventListener('change', () => controller.setBrand(brand.value));
const labels = { ok: '成功', warn: '警告', error: '错误', info: '信息', neutral: '中性' };
for (const tone of TONES) {
  const badge = document.createElement('span'); badge.textContent = labels[tone]; badge.dataset.tone = tone;
  badge.style.color = tokenColor(`--yue-${tone}`); badge.style.backgroundColor = tokenColor(`--yue-${tone}-bg`);
  document.querySelector('#tones')!.append(badge);
}
for (const token of TOKEN_REGISTRY.semantics.filter(item => item.kind === 'color' && !item.name.endsWith('-foreground') && !item.name.endsWith('-bg'))) {
  const swatch = document.createElement('div'); swatch.className = 'token-swatch';
  if (token.contrast === 'text') { swatch.style.backgroundColor = tokenColor(token.name); swatch.style.color = tokenColor(token.contrastWith!); }
  else { swatch.style.backgroundColor = 'var(--background)'; swatch.style.color = 'var(--foreground)'; swatch.style.borderColor = tokenColor(token.name); swatch.style.borderWidth = '3px'; }
  const label = document.createElement('span'); label.textContent = token.meaning; swatch.append(label); document.querySelector('#palette')!.append(swatch);
}
window.addEventListener('pagehide', () => controller.dispose(), { once: true });
