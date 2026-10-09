import { DirectionProvider } from '@idadawn/yue-core';
import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { Tree, type TreeNode, type TreeProps } from './tree';

afterEach(cleanup);

const NODES: TreeNode[] = [
  {
    id: 'hq',
    label: '总部',
    children: [
      { id: 'rd', label: '研发部', children: [{ id: 'fe', label: '前端组' }, { id: 'be', label: '后端组', disabled: true }] },
      { id: 'sales', label: '销售部' },
    ],
  },
  { id: 'branch', label: '分公司', children: [{ id: 'sh', label: '上海' }] },
];

function Demo(props: Partial<TreeProps>) {
  return <Tree nodes={NODES} aria-label="组织" defaultExpandedIds={['hq']} {...props} />;
}

const item = (name: string) => screen.getByRole('treeitem', { name });
const names = () => screen.getAllByRole('treeitem').map((el) => el.getAttribute('data-node-id'));

describe('Tree（自建，验证门第六项）', () => {
  it('ARIA：扁平 treeitem + aria-level / setsize / posinset、aria-expanded、aria-selected；只有一个 tab 停靠点', () => {
    render(<Demo defaultSelectedId="sales" />);
    expect(screen.getByRole('tree', { name: '组织' })).toBeTruthy();
    expect(names()).toEqual(['hq', 'rd', 'sales', 'branch']);
    expect(item('总部').getAttribute('aria-level')).toBe('1');
    expect(item('总部').getAttribute('aria-expanded')).toBe('true');
    expect(item('研发部').getAttribute('aria-level')).toBe('2');
    expect(item('研发部').getAttribute('aria-setsize')).toBe('2');
    expect(item('销售部').getAttribute('aria-posinset')).toBe('2');
    expect(item('研发部').getAttribute('aria-expanded')).toBe('false');
    expect(item('销售部').getAttribute('aria-expanded')).toBeNull();
    expect(item('销售部').getAttribute('aria-selected')).toBe('true');
    expect(screen.getAllByRole('treeitem').filter((el) => el.tabIndex === 0)).toEqual([item('销售部')]);
  });

  it('键盘：上下移动、Right 展开再进入、Left 折叠再回父级、Home / End、Enter 选中、禁用项不可选', async () => {
    const onSelectedChange = vi.fn();
    render(<Demo onSelectedChange={onSelectedChange} />);
    item('总部').focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(item('研发部'));
    await userEvent.keyboard('{ArrowRight}');
    expect(item('研发部').getAttribute('aria-expanded')).toBe('true');
    expect(document.activeElement).toBe(item('研发部'));
    await userEvent.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(item('前端组'));
    await userEvent.keyboard('{Enter}');
    expect(onSelectedChange).toHaveBeenCalledWith('fe');
    expect(item('前端组').getAttribute('aria-selected')).toBe('true');
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(item('后端组'));
    await userEvent.keyboard(' ');
    expect(onSelectedChange).not.toHaveBeenCalledWith('be');
    await userEvent.keyboard('{ArrowLeft}');
    expect(document.activeElement).toBe(item('研发部'));
    await userEvent.keyboard('{ArrowLeft}');
    expect(item('研发部').getAttribute('aria-expanded')).toBe('false');
    expect(names()).toEqual(['hq', 'rd', 'sales', 'branch']);
    await userEvent.keyboard('{End}');
    expect(document.activeElement).toBe(item('分公司'));
    await userEvent.keyboard('{Home}');
    expect(document.activeElement).toBe(item('总部'));
    await userEvent.keyboard('{ArrowUp}');
    expect(document.activeElement).toBe(item('总部'));
  });

  it('RTL 下 ArrowLeft 展开、ArrowRight 折叠', async () => {
    render(
      <DirectionProvider direction="rtl">
        <Demo />
      </DirectionProvider>,
    );
    item('研发部').focus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(item('研发部').getAttribute('aria-expanded')).toBe('true');
    await userEvent.keyboard('{ArrowRight}');
    expect(item('研发部').getAttribute('aria-expanded')).toBe('false');
  });

  it('受控：expandedIds / selectedId 由外部决定，键盘只触发回调', async () => {
    const onExpandedChange = vi.fn();
    const onSelectedChange = vi.fn();
    render(<Demo expandedIds={['hq']} onExpandedChange={onExpandedChange} selectedId="hq" onSelectedChange={onSelectedChange} />);
    item('研发部').focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(onExpandedChange).toHaveBeenCalledWith(['hq', 'rd']);
    expect(item('研发部').getAttribute('aria-expanded')).toBe('false');
    await userEvent.keyboard('{Enter}');
    expect(onSelectedChange).toHaveBeenCalledWith('rd');
    expect(item('总部').getAttribute('aria-selected')).toBe('true');
    expect(item('研发部').getAttribute('aria-selected')).toBe('false');
  });

  it('axe：无 critical / serious 问题', async () => {
    render(<Demo defaultSelectedId="rd" />);
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
