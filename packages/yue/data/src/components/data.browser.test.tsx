import { useState } from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import axe from 'axe-core';
import { afterEach, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { DataTable, type DataTableQuery } from './data-table';
import { Tree } from './tree';
import { Cascader, TreeSelect } from './tree-select';
import { DateRange, TimePicker } from './date-time';
import { ImagePreview } from './image-preview';
import { createDataI18n } from '../locales';

afterEach(cleanup);
const i18n = createDataI18n();
i18n.addLanguagePack('zh-CN', { 'test.name': '名称', 'test.id': '编号' });
const nodes = Array.from({ length: 10_000 }, (_, index) => ({ id: String(index), label: `节点 ${index}` }));
it('复选树支持分支与部分选中，禁用节点不被级联勾选', async () => {
  function Example() {
    const [checked, setChecked] = useState<string[]>([]);
    return <Tree nodes={[{ id: 'parent', label: '分组', children: [{ id: 'a', label: '甲' }, { id: 'b', label: '乙' }, { id: 'c', label: '禁用', disabled: true }] }]} defaultExpandedIds={['parent']} checkedIds={checked} onCheckedChange={setChecked} aria-label="权限" />;
  }
  render(<Example />);
  await userEvent.click(screen.getByRole('treeitem', { name: '分组' }));
  expect(screen.getByRole('treeitem', { name: '分组' }).getAttribute('aria-checked')).toBe('true');
  await userEvent.keyboard('{ArrowRight}');
  await userEvent.click(screen.getByRole('treeitem', { name: '甲' }));
  expect(screen.getByRole('treeitem', { name: '分组' }).getAttribute('aria-checked')).toBe('mixed');
  expect(screen.getByRole('treeitem', { name: '禁用' }).getAttribute('aria-checked')).toBe('false');
});

it('一万节点 Tree 只渲染视窗，End / Home 跨窗口定位且 ARIA 总数不丢失', async () => {
  const selected = vi.fn();
  render(<Tree nodes={nodes} aria-label="大树" onSelectedChange={selected} />);
  await vi.waitFor(() => expect(screen.getAllByRole('treeitem').length).toBeGreaterThan(0));
  expect(screen.getAllByRole('treeitem').length).toBeLessThan(40);
  const first = screen.getByRole('treeitem', { name: '节点 0' });
  expect(first.getAttribute('aria-setsize')).toBe('10000');
  first.focus();
  await userEvent.keyboard('{End}');
  await vi.waitFor(() => expect(document.activeElement?.textContent).toContain('节点 9999'));
  await userEvent.keyboard('{Enter}');
  expect(selected).toHaveBeenCalledWith('9999');
  await userEvent.keyboard('{Home}');
  await vi.waitFor(() => expect(document.activeElement?.textContent).toContain('节点 0'));
  expect(screen.getAllByRole('treeitem').length).toBeLessThan(40);
});

it('TreeSelect 与 Cascader 一万节点复用同一虚拟化树，选择值可回写', async () => {
  const select = vi.fn();
  const cascade = vi.fn();
  render(<><TreeSelect nodes={nodes} i18n={i18n} label="组织" onChange={select} /><Cascader nodes={[{ id: 'root', label: '总部', children: nodes }]} i18n={i18n} label="层级" onChange={cascade} /></>);
  await userEvent.click(screen.getByRole('button', { name: '组织' }));
  await vi.waitFor(() => expect(screen.getAllByRole('treeitem').length).toBeGreaterThan(0));
  expect(screen.getAllByRole('treeitem').length).toBeLessThan(40);
  await userEvent.click(screen.getByRole('treeitem', { name: '节点 2' }));
  expect(select).toHaveBeenCalledWith('2');
  await userEvent.click(screen.getByRole('button', { name: '层级' }));
  await userEvent.click(screen.getByRole('treeitem', { name: '总部' }));
  expect(screen.getAllByRole('treeitem').length).toBeLessThan(45);
  await userEvent.click(screen.getByRole('treeitem', { name: '节点 3' }));
  expect(cascade).toHaveBeenCalledWith(['root', '3']);
});

it('表格分页和排序只改变查询，不在客户端切掉服务端当前页；显隐与行选择有效', async () => {
  const changed = vi.fn();
  const selection = vi.fn();
  function Example() {
    const [query, setQuery] = useState<DataTableQuery>({ pageIndex: 0, pageSize: 2, sorting: [], filters: {} });
    return <DataTable columns={[{ field: 'id', titleKey: 'test.id', fixed: 'left' }, { field: 'name', titleKey: 'test.name', sortable: true, filterable: true }]} data={[{ id: 'a', name: '甲' }, { id: 'b', name: '乙' }]} total={6} query={query} onQueryChange={(next) => { setQuery(next); changed(next); }} getRowId={(row) => row.id} i18n={i18n} label="列表" selectable onSelectionChange={selection} />;
  }
  render(<Example />);
  await userEvent.click(screen.getByRole('button', { name: '下一页' }));
  expect(changed.mock.lastCall?.[0].pageIndex).toBe(1);
  expect(screen.getByText('甲', { exact: true })).toBeTruthy();
  await userEvent.click(screen.getByRole('button', { name: '名称' }));
  expect(changed.mock.lastCall?.[0].pageIndex).toBe(0);
  expect(changed.mock.lastCall?.[0].sorting[0].id).toBe('name');
  await userEvent.fill(screen.getByRole('textbox', { name: '筛选 名称' }), '甲');
  expect(changed.mock.lastCall?.[0].filters.name).toBe('甲');
  await userEvent.click(screen.getByRole('checkbox', { name: '选择第 1 行' }));
  expect(selection).toHaveBeenCalledWith(['a']);
  await userEvent.click(screen.getByText('显示列'));
  await userEvent.click(screen.getByRole('checkbox', { name: '名称' }));
  expect(screen.queryByRole('columnheader', { name: /名称/ })).toBeNull();
  const result = await axe.run(document.body);
  expect(result.violations.filter((v) => ['critical', 'serious'].includes(v.impact ?? ''))).toEqual([]);
});

it('日期/时间使用原生键盘输入；范围错误和图片对话框有可访问名称', async () => {
  const change = vi.fn();
  render(<><DateRange label="日期范围" i18n={i18n} value={['2026-10-17', '2026-10-16']} /><TimePicker label="时间" value="12:00" onChange={change} /><ImagePreview i18n={i18n} alt="示例图片" src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==" /></>);
  expect(screen.getByRole('alert').textContent).toContain('结束值不得早于开始值');
  await userEvent.fill(screen.getByLabelText('时间'), '13:30');
  expect(change).toHaveBeenCalledWith('13:30');
  await userEvent.click(screen.getByRole('button', { name: '图片预览' }));
  expect(screen.getByRole('dialog', { name: '示例图片' })).toBeTruthy();
  await userEvent.keyboard('{Escape}');
  await vi.waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
});
