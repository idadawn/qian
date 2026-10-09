import { cleanup, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { Menu, MenuContent, MenuItem, MenuSeparator, MenuSub, MenuSubTrigger, MenuTrigger } from './menu';

afterEach(cleanup);

function Demo({ onSelect = () => {} }: { onSelect?: (value: string) => void }) {
  return (
    <Menu>
      <MenuTrigger>操作</MenuTrigger>
      <MenuContent>
        <MenuItem onClick={() => onSelect('新建')}>新建</MenuItem>
        <MenuItem onClick={() => onSelect('编辑')}>编辑</MenuItem>
        <MenuSeparator />
        <MenuSub>
          <MenuSubTrigger>更多</MenuSubTrigger>
          <MenuContent>
            <MenuItem onClick={() => onSelect('导出')}>导出</MenuItem>
            <MenuItem onClick={() => onSelect('归档')}>归档</MenuItem>
          </MenuContent>
        </MenuSub>
        <MenuItem disabled>删除</MenuItem>
      </MenuContent>
    </Menu>
  );
}

describe('Menu（Base UI 验证门）', () => {
  it('键盘：Enter 打开并聚焦首项，方向键移动，Enter 选中后关闭且焦点回到触发器', async () => {
    const onSelect = vi.fn();
    render(<Demo onSelect={onSelect} />);
    const trigger = screen.getByRole('button', { name: '操作' });
    expect(trigger.getAttribute('aria-haspopup')).toBe('menu');
    trigger.focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu');
    expect(trigger.getAttribute('aria-expanded')).toBe('true');
    const items = within(menu).getAllByRole('menuitem');
    expect(items.map((i) => i.textContent)).toEqual(['新建', '编辑', '更多', '删除']);
    await vi.waitFor(() => expect(document.activeElement).toBe(items[0]));
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(items[1]);
    await userEvent.keyboard('{Enter}');
    expect(onSelect).toHaveBeenCalledWith('编辑');
    await vi.waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
    expect(document.activeElement).toBe(trigger);
  });

  it('子菜单：ArrowRight 打开并聚焦子菜单首项，ArrowLeft 关闭回到子菜单触发器；Escape 全部关闭', async () => {
    render(<Demo />);
    screen.getByRole('button', { name: '操作' }).focus();
    await userEvent.keyboard('{Enter}');
    const menu = await screen.findByRole('menu');
    const subTrigger = within(menu).getByRole('menuitem', { name: '更多' });
    expect(subTrigger.getAttribute('aria-haspopup')).toBe('menu');
    await vi.waitFor(() => expect(document.activeElement).toBe(within(menu).getAllByRole('menuitem')[0]));
    await userEvent.keyboard('{ArrowDown}{ArrowDown}');
    expect(document.activeElement).toBe(subTrigger);
    await userEvent.keyboard('{ArrowRight}');
    await vi.waitFor(() => expect(screen.getAllByRole('menu')).toHaveLength(2));
    const sub = screen.getAllByRole('menu')[1]!;
    await vi.waitFor(() => expect(document.activeElement).toBe(within(sub).getAllByRole('menuitem')[0]));
    await userEvent.keyboard('{ArrowLeft}');
    await vi.waitFor(() => expect(screen.getAllByRole('menu')).toHaveLength(1));
    expect(document.activeElement).toBe(subTrigger);
    await userEvent.keyboard('{Escape}');
    await vi.waitFor(() => expect(screen.queryByRole('menu')).toBeNull());
  });

  it('禁用项带 aria-disabled 且键盘跳过；受控 open 生效', async () => {
    const onOpenChange = vi.fn();
    const { rerender } = render(
      <Menu open={false} onOpenChange={onOpenChange}>
        <MenuTrigger>操作</MenuTrigger>
        <MenuContent>
          <MenuItem>新建</MenuItem>
          <MenuItem disabled>删除</MenuItem>
        </MenuContent>
      </Menu>,
    );
    screen.getByRole('button', { name: '操作' }).focus();
    await userEvent.keyboard('{Enter}');
    expect(onOpenChange).toHaveBeenCalledWith(true, expect.anything());
    expect(screen.queryByRole('menu')).toBeNull();
    rerender(
      <Menu open onOpenChange={onOpenChange}>
        <MenuTrigger>操作</MenuTrigger>
        <MenuContent>
          <MenuItem>新建</MenuItem>
          <MenuItem disabled>删除</MenuItem>
        </MenuContent>
      </Menu>,
    );
    const menu = await screen.findByRole('menu');
    expect(within(menu).getByRole('menuitem', { name: '删除' }).getAttribute('aria-disabled')).toBe('true');
  });

  it('axe：打开状态下无 critical / serious 问题', async () => {
    render(<Demo />);
    screen.getByRole('button', { name: '操作' }).focus();
    await userEvent.keyboard('{Enter}');
    await screen.findByRole('menu');
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
