import { cleanup, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { Select, SelectContent, SelectItem, SelectTrigger } from './select';

afterEach(cleanup);

const FRUITS = ['苹果', '香蕉', '樱桃'];

function Demo(props: { value?: string | null; defaultValue?: string | null; onValueChange?: (v: string | null) => void }) {
  return (
    <Select items={FRUITS.map((f) => ({ label: f, value: f }))} {...props}>
      <SelectTrigger placeholder="选一种水果" aria-label="水果" />
      <SelectContent>
        {FRUITS.map((f) => (
          <SelectItem key={f} value={f}>
            {f}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

const trigger = () => screen.getByTestId('yue-select-trigger');
const highlighted = () => screen.getAllByRole('option').find((o) => o.hasAttribute('data-highlighted'));

describe('Select（Base UI 验证门）', () => {
  it('键盘：ArrowDown 打开 listbox，方向键高亮，Enter 选中并回写触发器，Escape 关闭回焦', async () => {
    const onValueChange = vi.fn();
    render(<Demo onValueChange={onValueChange} />);
    expect(trigger().getAttribute('aria-haspopup')).toBe('listbox');
    expect(trigger().textContent).toContain('选一种水果');
    trigger().focus();
    await userEvent.keyboard('{ArrowDown}');
    const listbox = await screen.findByRole('listbox');
    expect(within(listbox).getAllByRole('option').map((o) => o.textContent)).toEqual(['苹果', '香蕉', '樱桃']);
    await userEvent.keyboard('{ArrowDown}');
    await vi.waitFor(() => expect(highlighted()).toBeDefined());
    const picked = highlighted()!.textContent!;
    await userEvent.keyboard('{Enter}');
    expect(onValueChange).toHaveBeenCalledWith(picked, expect.anything());
    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    expect(trigger().textContent).toContain(picked);
    expect(document.activeElement).toBe(trigger());
    trigger().focus();
    await userEvent.keyboard('{Enter}');
    await screen.findByRole('listbox');
    await userEvent.keyboard('{Escape}');
    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    expect(document.activeElement).toBe(trigger());
  });

  it('受控：value 决定显示与选中项，内部不自行改值；非受控 defaultValue 生效', async () => {
    const onValueChange = vi.fn();
    render(<Demo value="樱桃" onValueChange={onValueChange} />);
    expect(trigger().textContent).toContain('樱桃');
    trigger().focus();
    await userEvent.keyboard('{Enter}');
    const listbox = await screen.findByRole('listbox');
    expect(within(listbox).getByRole('option', { name: '樱桃' }).getAttribute('aria-selected')).toBe('true');
    await userEvent.keyboard('{ArrowUp}{Enter}');
    expect(onValueChange).toHaveBeenCalledWith('香蕉', expect.anything());
    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    expect(trigger().textContent).toContain('樱桃');
    cleanup();
    render(<Demo defaultValue="香蕉" />);
    expect(trigger().textContent).toContain('香蕉');
  });

  it('axe：打开状态下无 critical / serious 问题', async () => {
    render(<Demo />);
    trigger().focus();
    await userEvent.keyboard('{Enter}');
    await screen.findByRole('listbox');
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
