import axe, { type AxeResults, type Result } from 'axe-core';

// 验证门的通过标准是 axe critical = 0（docs/prd/specs/yue.md §4.4）；这里连 serious 一起拦，比标准更严。
export const BLOCKING_IMPACTS = new Set(['critical', 'serious']);

export async function runAxe(root: Element | Document = document): Promise<AxeResults> {
  // 等待有限的进出场过渡和随后的焦点安排，避免把动画中间帧当成稳定打开态。
  let stableFrames = 0;
  while (stableFrames < 2) {
    await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
    const finite = document.getAnimations().filter(animation => animation.playState !== 'finished' && Number.isFinite(Number(animation.effect?.getComputedTiming().endTime)));
    if (finite.length) { await Promise.all(finite.map(animation => animation.finished.catch(() => {}))); stableFrames = 0; }
    else stableFrames++;
  }
  return axe.run(root, { resultTypes: ['violations'] });
}

export function blockingViolations(results: AxeResults): Result[] {
  return results.violations.filter((v) => v.impact && BLOCKING_IMPACTS.has(v.impact));
}

export function describeViolations(violations: Result[]): string {
  return violations.map((v) => `${v.impact} ${v.id}: ${v.help} → ${v.nodes.map((n) => n.target.join(' ')).join('; ')}`).join('\n');
}
