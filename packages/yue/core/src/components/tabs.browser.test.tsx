import { cleanup, render, screen } from '@testing-library/react';
import { DirectionProvider } from '@base-ui/react/direction-provider';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { Tabs, TabsList, TabsPanel, TabsTab } from './tabs';

afterEach(cleanup);

function Demo(props: { value?: string; defaultValue?: string; onValueChange?: (v: string) => void; orientation?: 'horizontal' | 'vertical' }) {
  return (
    <Tabs defaultValue="a" {...props}>
      <TabsList aria-label="设置">
        <TabsTab value="a">基本</TabsTab>
        <TabsTab value="b">高级</TabsTab>
        <TabsTab value="c" disabled>
          危险
        </TabsTab>
      </TabsList>
      <TabsPanel value="a">基本内容</TabsPanel>
      <TabsPanel value="b">高级内容</TabsPanel>
      <TabsPanel value="c">危险内容</TabsPanel>
    </Tabs>
  );
}

const tab = (name: string) => screen.getByRole('tab', { name });

describe('Tabs（Base UI 验证门）', () => {
  it('ARIA：tablist / tab / tabpanel 互相关联；只有当前 tab 可 Tab 到（roving tabindex）', () => {
    render(<Demo />);
    const list = screen.getByRole('tablist', { name: '设置' });
    expect(list.getAttribute('aria-orientation') ?? 'horizontal').toBe('horizontal');
    const panel = screen.getByRole('tabpanel');
    expect(tab('基本').getAttribute('aria-selected')).toBe('true');
    expect(tab('基本').getAttribute('aria-controls')).toBe(panel.id);
    expect(panel.getAttribute('aria-labelledby')).toBe(tab('基本').id);
    expect(tab('基本').tabIndex).toBe(0);
    expect(tab('高级').tabIndex).toBe(-1);
    expect(tab('危险').getAttribute('aria-disabled')).toBe('true');
  });

  it('键盘：方向键在 tab 间循环并激活；禁用 tab 可聚焦但不激活（Base UI 的可发现性策略）；Home / End', async () => {
    render(<Demo />);
    tab('基本').focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(tab('高级'));
    await vi.waitFor(() => expect(tab('高级').getAttribute('aria-selected')).toBe('true'));
    expect(screen.getByRole('tabpanel').textContent).toBe('高级内容');
    await userEvent.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(tab('危险'));
    expect(tab('危险').getAttribute('aria-selected')).toBe('false');
    expect(screen.getByRole('tabpanel').textContent).toBe('高级内容');
    await userEvent.keyboard('{ArrowRight}');
    expect(document.activeElement).toBe(tab('基本'));
    await userEvent.keyboard('{End}');
    expect(document.activeElement).toBe(tab('危险'));
    await userEvent.keyboard('{Home}');
    expect(document.activeElement).toBe(tab('基本'));
  });

  it('RTL 下 ArrowLeft 向后；竖向用 ArrowDown', async () => {
    render(
      <DirectionProvider direction="rtl">
        <Demo />
      </DirectionProvider>,
    );
    tab('基本').focus();
    await userEvent.keyboard('{ArrowLeft}');
    expect(document.activeElement).toBe(tab('高级'));
    cleanup();
    render(<Demo orientation="vertical" />);
    expect(screen.getByRole('tablist').getAttribute('aria-orientation')).toBe('vertical');
    tab('基本').focus();
    await userEvent.keyboard('{ArrowDown}');
    expect(document.activeElement).toBe(tab('高级'));
  });

  it('受控：value 决定当前 tab，键盘只触发 onValueChange', async () => {
    const onValueChange = vi.fn();
    render(<Demo value="a" onValueChange={onValueChange} />);
    tab('基本').focus();
    await userEvent.keyboard('{ArrowRight}');
    expect(onValueChange).toHaveBeenCalledWith('b', expect.anything());
    expect(tab('基本').getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('tabpanel').textContent).toBe('基本内容');
  });

  it('axe：无 critical / serious 问题', async () => {
    render(<Demo />);
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});

describe('TabsList variant="line"', () => {
  it('线形标签带计数：计数在 tab 的可访问名称里，选中项有 data-active，axe 通过', async () => {
    render(<Tabs defaultValue="pending"><TabsList variant="line" aria-label="申请状态"><TabsTab value="pending" count={3}>待审核</TabsTab><TabsTab value="all" count={18}>全部</TabsTab></TabsList>
      <TabsPanel value="pending">待审核列表</TabsPanel><TabsPanel value="all">全部列表</TabsPanel></Tabs>);
    expect(screen.getByRole('tablist').getAttribute('data-variant')).toBe('line');
    expect(tab('待审核 3').hasAttribute('data-active')).toBe(true);
    expect(screen.getAllByTestId('yue-tabs-tab-count').map(node => node.textContent)).toEqual(['3', '18']);
    await userEvent.click(tab('全部 18'));
    expect(screen.getByRole('tabpanel').textContent).toBe('全部列表');
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });
});
