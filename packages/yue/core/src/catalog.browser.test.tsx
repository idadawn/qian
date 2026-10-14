import { cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import * as C from './index';
import { DEMOS } from '../docs/examples/catalog';
import { blockingViolations, describeViolations, runAxe } from './test/axe';

const opening: Record<string, string> = { Dialog: '打开对话框', AlertDialog: '打开确认框', Sheet: '打开侧栏', Drawer: '打开抽屉', Popover: '打开浮层', Menu: '打开菜单', DropdownMenu: '打开菜单', Toast: '显示消息', Notification: '显示消息', Accordion: '如何使用' };
afterEach(() => { cleanup(); vi.restoreAllMocks(); });
async function openDemo(name: string) {
  if (opening[name]) await userEvent.click(screen.getByRole('button', { name: opening[name] }));
  if (name === 'Select') await userEvent.click(screen.getByRole('combobox'));
  if (name === 'Combobox') await userEvent.click(screen.getByTestId('yue-combobox-trigger'));
  if (name === 'Tooltip') { screen.getByRole('button', { name: '查看提示' }).focus(); await screen.findByRole('tooltip'); }
  if (name === 'HoverCard') { await userEvent.hover(screen.getByRole('link')); await screen.findByText('示例用户'); }
  if (name === 'ContextMenu') { screen.getByTestId('yue-context-menu-trigger').focus(); await userEvent.keyboard('{Shift>}{F10}{/Shift}'); }
  if (['Dialog', 'Sheet', 'Drawer', 'Popover'].includes(name)) await screen.findByRole('dialog');
  if (name === 'AlertDialog') await screen.findByRole('alertdialog');
  if (['Menu', 'DropdownMenu', 'ContextMenu'].includes(name)) await screen.findByRole('menu');
  if (['Select', 'Combobox'].includes(name)) await screen.findByRole('listbox');
  if (name === 'Toast') await screen.findByText('保存成功');
  if (name === 'Notification') await screen.findByText('任务已完成');
}

describe('全组件部件与无障碍', () => {
  for (const [name, Demo] of Object.entries(DEMOS)) {
    it(`${name} 打开态有可寻址部件且 axe 无 critical/serious`, async () => {
      render(<Demo />);
      await openDemo(name);
      const manifest = C.CORE_COMPONENT_MANIFESTS.find(item => item.name === name)!;
      expect(manifest.slots.some(slot => document.querySelector(`[data-slot="${slot}"]`)), name).toBe(true);
      for (const part of document.querySelectorAll('[data-slot]')) expect(part.getAttribute('data-testid'), part.outerHTML).toBeTruthy();
      const violations = blockingViolations(await runAxe());
      expect(violations, describeViolations(violations)).toHaveLength(0);
    });
  }
});

describe('基础交互', () => {
  it('testId 可以覆盖默认标识，data-slot 保持组件身份', () => {
    render(<><C.Button data-slot="injected" data-testid="injected" testId="save-button">保存</C.Button><C.Select items={[{ value: 'a', label: 'A' }]}><C.SelectTrigger aria-label="选择" data-slot="injected" testId="select-control" /></C.Select></>);
    expect(screen.getByTestId('save-button').getAttribute('data-slot')).toBe('button');
    expect(screen.getByTestId('select-control').getAttribute('data-slot')).toBe('select-trigger');
    expect(screen.queryByTestId('injected')).toBeNull();
  });
  it('Checkbox / Switch / Toggle 支持 Space、受控状态与禁用', async () => {
    const onChecked = vi.fn();
    const view = render(<C.Checkbox aria-label="选项" checked={false} onCheckedChange={onChecked} />);
    screen.getByRole('checkbox').focus(); await userEvent.keyboard(' ');
    expect(onChecked).toHaveBeenCalledWith(true, expect.anything());
    expect(screen.getByRole('checkbox').getAttribute('aria-checked')).toBe('false');
    view.rerender(<C.Checkbox aria-label="选项" disabled checked={false} onCheckedChange={onChecked} />);
    await userEvent.keyboard(' '); expect(onChecked).toHaveBeenCalledTimes(1);
    cleanup(); render(<><C.Switch aria-label="提醒" /><C.Toggle aria-label="加粗">B</C.Toggle></>);
    screen.getByRole('switch').focus(); await userEvent.keyboard(' ');
    expect(screen.getByRole('switch').getAttribute('aria-checked')).toBe('true');
    screen.getByRole('button', { name: '加粗' }).focus(); await userEvent.keyboard(' ');
    expect(screen.getByRole('button', { name: '加粗' }).getAttribute('aria-pressed')).toBe('true');
  });
  it('Radio 与 Segmented 使用方向键选择', async () => {
    render(<DEMOS.RadioGroup />); screen.getByRole('radio', { name: '每天' }).focus(); await userEvent.keyboard('{ArrowDown}');
    expect(screen.getByRole('radio', { name: '每周' }).getAttribute('aria-checked')).toBe('true');
    cleanup(); render(<DEMOS.Segmented />); screen.getByRole('radio', { name: '列表' }).focus(); await userEvent.keyboard('{ArrowRight}');
    expect(screen.getByRole('radio', { name: '网格' }).getAttribute('aria-checked')).toBe('true');
  });
  it('密码显隐保持原值并更新可访问名称', async () => {
    render(<DEMOS.InputPassword />);
    await userEvent.click(screen.getByRole('button', { name: '显示密码' }));
    expect((screen.getByLabelText('密码') as HTMLInputElement).type).toBe('text');
    expect((screen.getByLabelText('密码') as HTMLInputElement).value).toBe('test-password');
    await userEvent.click(screen.getByRole('button', { name: '隐藏密码' }));
    expect((screen.getByLabelText('密码') as HTMLInputElement).type).toBe('password');
  });
  it('InputNumber 遵守步进与边界，PinInput 自动移焦并回传完整值', async () => {
    render(<DEMOS.InputNumber />);
    await userEvent.click(screen.getByRole('button', { name: '增加数量' }));
    expect((screen.getByLabelText('数量') as HTMLInputElement).value).toBe('3');
    cleanup(); const changed = vi.fn();
    render(<C.PinInput label="验证码" length={4} getSlotLabel={index => `第 ${index + 1} 位`} onValueComplete={changed} />);
    screen.getByRole('textbox', { name: '验证码' }).focus(); await userEvent.keyboard('1234');
    expect(changed).toHaveBeenCalledWith('1234', expect.anything());
    expect((screen.getByLabelText('第 4 位') as HTMLInputElement).value).toBe('4');
  });
  for (const name of ['Dialog', 'AlertDialog', 'Sheet', 'Drawer', 'Popover'] as const) {
    it(`${name} Escape 关闭并恢复触发器焦点`, async () => {
      const Demo = DEMOS[name]; render(<Demo />); const trigger = screen.getByRole('button', { name: opening[name] });
      await openDemo(name); await userEvent.keyboard('{Escape}');
      await vi.waitFor(() => expect(screen.queryByRole(name === 'AlertDialog' ? 'alertdialog' : 'dialog')).toBeNull());
      expect(document.activeElement).toBe(trigger);
    });
  }
  it('对话框 Tab 在模态内容内循环，关闭按钮可用', async () => {
    render(<DEMOS.Dialog />); await openDemo('Dialog');
    screen.getByRole('button', { name: '关闭' }).focus(); await userEvent.tab();
    expect(screen.getByRole('dialog').contains(document.activeElement)).toBe(true);
    await userEvent.click(screen.getByRole('button', { name: '关闭' }));
    await vi.waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });
  it('Resizable 方向键改变面板占比，aria 控制目标有效', async () => {
    render(<DEMOS.Resizable />); const handle = screen.getByRole('separator', { name: '调整面板宽度' });
    const before = handle.getAttribute('aria-valuenow'); handle.focus(); await userEvent.keyboard('{ArrowRight}');
    await vi.waitFor(() => expect(handle.getAttribute('aria-valuenow')).not.toBe(before));
    for (const id of handle.getAttribute('aria-controls')!.split(' ')) expect(document.getElementById(id)).not.toBeNull();
  });
  it('Pagination 限制前后边界、改变当前页，并支持受控输入', async () => {
    const changed = vi.fn();
    const props = { label: '分页', previousLabel: '上一页', nextLabel: '下一页', getPageLabel: (page: number) => `第 ${page} 页`, totalPages: 3, onPageChange: changed };
    const view = render(<C.Pagination {...props} />);
    expect((screen.getByRole('button', { name: '上一页' }) as HTMLButtonElement).disabled).toBe(true);
    await userEvent.click(screen.getByRole('button', { name: '下一页' }));
    expect(changed).toHaveBeenCalledWith(2);
    expect(screen.getByRole('button', { name: '第 2 页' }).getAttribute('aria-current')).toBe('page');
    view.rerender(<C.Pagination {...props} page={3} />);
    expect((screen.getByRole('button', { name: '下一页' }) as HTMLButtonElement).disabled).toBe(true);
  });
  it('Toast / Notification 共用消息出口并可关闭，Tag 执行移除回调', async () => {
    render(<DEMOS.Toast />); await openDemo('Toast');
    await userEvent.keyboard('{F6}');
    await userEvent.click(await screen.findByRole('button', { name: '关闭消息' }));
    await vi.waitFor(() => expect(screen.queryByText('保存成功')).toBeNull());
    cleanup(); render(<DEMOS.Tag />); await userEvent.click(screen.getByRole('button', { name: '移除研发标签' }));
    expect(screen.queryByText('研发')).toBeNull();
  });
  it('Field 错误通过描述关联输入，FullScreen 失败交由宿主呈现', async () => {
    render(<DEMOS.Field invalid />);
    const input = screen.getByRole('textbox', { name: '姓名' });
    expect(input.getAttribute('aria-invalid')).toBe('true');
    const ids = input.getAttribute('aria-describedby')!.split(' ');
    expect(ids.some(id => document.getElementById(id)?.textContent?.includes('请输入姓名'))).toBe(true);
    cleanup(); const rejected = vi.fn();
    vi.spyOn(document.documentElement, 'requestFullscreen').mockRejectedValue(new Error('unsupported'));
    render(<C.FullScreen enterLabel="全屏" exitLabel="退出" onError={rejected} />);
    await userEvent.click(screen.getByRole('button', { name: '全屏' }));
    await vi.waitFor(() => expect(rejected).toHaveBeenCalledOnce());
  });
});
