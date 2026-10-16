import type { Page } from '@playwright/test';

/** 按浏览器实际动画完成状态取样，不禁用动效或无障碍规则。 */
export async function settleVisuals(page: Page): Promise<void> {
  for (const frame of page.frames()) await frame.evaluate(async () => {
    await document.fonts.ready;
    let stableFrames = 0;
    while (stableFrames < 2) {
      await new Promise<void>(resolve => requestAnimationFrame(() => resolve()));
      const finite = document.getAnimations().filter(animation => animation.playState !== 'finished' && Number.isFinite(Number(animation.effect?.getComputedTiming().endTime)));
      if (finite.length) { await Promise.all(finite.map(animation => animation.finished.catch(() => {}))); stableFrames = 0; }
      else stableFrames++;
    }
  });
}
