import { cleanup, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { Combobox, ComboboxContent, ComboboxEmpty, ComboboxInput, ComboboxItem, ComboboxList } from './combobox';

afterEach(cleanup);

const FRUITS = ['苹果', '香蕉', '樱桃', '葡萄'];

function Demo({ onValueChange }: { onValueChange?: (v: string | null) => void }) {
  return (
    <Combobox items={FRUITS} onValueChange={onValueChange}>
      <ComboboxInput placeholder="输入筛选" aria-label="水果筛选" />
      <ComboboxContent>
        <ComboboxEmpty>没有匹配项</ComboboxEmpty>
        <ComboboxList>{(item: string) => <ComboboxItem key={item} value={item}>{item}</ComboboxItem>}</ComboboxList>
      </ComboboxContent>
    </Combobox>
  );
}

describe('Combobox（Base UI 验证门）', () => {
  it('输入即过滤，ArrowDown 高亮，Enter 选中回写输入框，Escape 关闭；无匹配显示空态', async () => {
    const onValueChange = vi.fn();
    render(<Demo onValueChange={onValueChange} />);
    const input = screen.getByRole('combobox', { name: '水果筛选' }) as HTMLInputElement;
    expect(input.getAttribute('aria-autocomplete')).toBeTruthy();
    input.focus();
    await userEvent.keyboard('樱');
    const listbox = await screen.findByRole('listbox');
    await vi.waitFor(() => expect(within(listbox).getAllByRole('option').map((o) => o.textContent)).toEqual(['樱桃']));
    await userEvent.keyboard('{ArrowDown}');
    await vi.waitFor(() => expect(within(listbox).getByRole('option', { name: '樱桃' }).hasAttribute('data-highlighted')).toBe(true));
    expect(document.activeElement).toBe(input);
    await userEvent.keyboard('{Enter}');
    expect(onValueChange).toHaveBeenCalledWith('樱桃', expect.anything());
    await vi.waitFor(() => expect(input.value).toBe('樱桃'));
    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
    await userEvent.fill(input, 'zzz');
    await screen.findByText('没有匹配项');
    await userEvent.keyboard('{Escape}');
    await vi.waitFor(() => expect(screen.queryByRole('listbox')).toBeNull());
  });

  it('axe：打开状态下无 critical / serious 问题', async () => {
    render(<Demo />);
    const input = screen.getByRole('combobox', { name: '水果筛选' });
    input.focus();
    await userEvent.keyboard('{ArrowDown}');
    await screen.findByRole('listbox');
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
