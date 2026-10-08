import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from './tooltip';

afterEach(cleanup);

function Demo() {
  return (
    <TooltipProvider delay={50}>
      <button type="button">前一个</button>
      <Tooltip>
        <TooltipTrigger>保存</TooltipTrigger>
        <TooltipContent>保存当前更改</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

describe('Tooltip（Base UI 验证门）', () => {
  it('键盘聚焦即显示并通过 aria-describedby 关联，Escape 关闭；悬停也能打开', async () => {
    render(<Demo />);
    screen.getByRole('button', { name: '前一个' }).focus();
    await userEvent.tab();
    const trigger = screen.getByRole('button', { name: '保存' });
    expect(document.activeElement).toBe(trigger);
    const tooltip = await screen.findByRole('tooltip');
    expect(tooltip.textContent).toBe('保存当前更改');
    expect(trigger.getAttribute('aria-describedby')).toBe(tooltip.id);
    await userEvent.keyboard('{Escape}');
    await vi.waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull());
    expect(trigger.hasAttribute('aria-describedby')).toBe(false);
    await userEvent.hover(screen.getByRole('button', { name: '前一个' }));
    await userEvent.hover(trigger);
    await screen.findByRole('tooltip');
    await userEvent.unhover(trigger);
    await vi.waitFor(() => expect(screen.queryByRole('tooltip')).toBeNull());
  });

  it('axe：显示状态下无 critical / serious 问题', async () => {
    render(<Demo />);
    screen.getByRole('button', { name: '前一个' }).focus();
    await userEvent.tab();
    await screen.findByRole('tooltip');
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
