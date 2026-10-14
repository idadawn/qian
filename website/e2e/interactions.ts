import { expect, type FrameLocator } from '@playwright/test';
const openings: Record<string, string> = { Dialog: '打开对话框', AlertDialog: '打开确认框', Sheet: '打开侧栏', Drawer: '打开抽屉', Popover: '打开浮层' };
export async function exercise(name: string, frame: FrameLocator, state = 'Default', checkOpen?: () => Promise<void>) {
  const main = frame.getByRole('main');
  if (state === 'Disabled') {
    const controls = main.locator('input,textarea,button,[role="radio"],[role="checkbox"],[role="switch"],[role="combobox"]');
    expect(await controls.count()).toBeGreaterThan(0);
    for (const control of await controls.all()) await expect(control).toBeDisabled();
    return;
  }
  if (state === 'Error') {
    await expect(main.locator('[aria-invalid="true"]').first()).toBeVisible();
    if (name === 'Field') await expect(main.getByText('请输入姓名。')).toBeVisible();
    return;
  }
  if (state === 'Hover') { await main.getByRole('button').first().hover(); await expect(main.getByRole('button').first()).toBeEnabled(); await expect.poll(() => main.getByRole('button').first().evaluate(element => element.matches(':hover'))).toBe(true); return; }
  if (openings[name]) {
    const trigger = main.getByRole('button', { name: openings[name], exact: true });
    await trigger.click();
    const dialog = frame.getByRole(name === 'AlertDialog' ? 'alertdialog' : 'dialog');
    await expect(dialog).toBeVisible();
    await checkOpen?.();
    if (name === 'Dialog') { await dialog.getByRole('textbox').fill('交互测试'); await expect(dialog.getByRole('textbox')).toHaveValue('交互测试'); }
    await dialog.getByRole('button', { name: name === 'AlertDialog' ? '取消' : '关闭', exact: true }).click();
    await expect(dialog).toBeHidden(); await expect(trigger).toBeFocused();
    await trigger.click();
    if (name === 'AlertDialog') await dialog.getByRole('button', { name: '继续', exact: true }).click();
    else await dialog.press('Escape');
    await expect(dialog).toBeHidden(); await expect(trigger).toBeFocused();
    return;
  }
  switch (name) {
    case 'Button': await main.getByRole('button', { name: '保存更改' }).click(); await expect(main.getByRole('status')).toHaveText('已保存 1 次'); break;
    case 'IconButton': await main.getByRole('button', { name: '关闭' }).click(); await expect(main.getByRole('status')).toHaveText('已关闭通知'); break;
    case 'ButtonGroup': await main.getByRole('button', { name: '取消' }).click(); await expect(main.getByRole('status')).toHaveText('已取消'); break;
    case 'Input': case 'Textarea': case 'Label': await main.getByRole('textbox').fill('示例输入'); await expect(main.getByRole('textbox')).toHaveValue('示例输入'); break;
    case 'Field': await main.getByRole('textbox').focus(); await main.getByRole('textbox').press('Tab'); await expect(main.getByText('请输入姓名。')).toBeVisible(); await main.getByRole('textbox').fill('示例输入'); await expect(main.getByText('请输入姓名。')).toBeHidden(); await expect(main.getByRole('textbox')).toHaveValue('示例输入'); break;
    case 'InputNumber': await main.getByRole('button', { name: '增加数量' }).click(); await expect(main.getByLabel('数量', { exact: true })).toHaveValue('3'); break;
    case 'InputPassword': await main.getByRole('button', { name: '显示密码' }).click(); await expect(main.getByLabel('密码', { exact: true })).toHaveAttribute('type', 'text'); await main.getByRole('button', { name: '隐藏密码' }).click(); await expect(main.getByLabel('密码', { exact: true })).toHaveAttribute('type', 'password'); break;
    case 'PinInput': await main.getByRole('textbox', { name: '验证码', exact: true }).pressSequentially('1234'); await expect(main.getByLabel('验证码第 4 位')).toHaveValue('4'); break;
    case 'Checkbox': await main.getByRole('checkbox').press('Space'); await expect(main.getByRole('checkbox')).toBeChecked(); break;
    case 'Switch': await main.getByRole('switch').press('Space'); await expect(main.getByRole('switch')).toBeChecked(); break;
    case 'Toggle': await main.getByRole('button', { name: '加粗' }).press('Space'); await expect(main.getByRole('button', { name: '加粗' })).toHaveAttribute('aria-pressed', 'true'); break;
    case 'Radio': await main.getByRole('radio').check(); await expect(main.getByRole('radio')).toBeChecked(); break;
    case 'RadioGroup': await main.getByRole('radio', { name: '每天' }).press('ArrowDown'); await expect(main.getByRole('radio', { name: '每周' })).toBeChecked(); break;
    case 'Segmented': await main.getByRole('radio', { name: '列表' }).press('ArrowRight'); await expect(main.getByRole('radio', { name: '网格' })).toBeChecked(); break;
    case 'Select': await main.getByRole('combobox').click(); await checkOpen?.(); await frame.getByRole('option', { name: '上海', exact: true }).click(); await expect(main.getByRole('combobox')).toContainText('上海'); break;
    case 'Combobox': await main.getByTestId('yue-combobox-trigger').click(); await frame.getByTestId('yue-combobox-input').fill('上海'); await checkOpen?.(); await frame.getByRole('option', { name: '上海', exact: true }).click(); await expect(main.getByTestId('yue-combobox-trigger')).toContainText('上海'); break;
    case 'Menu': case 'DropdownMenu': await main.getByRole('button', { name: '打开菜单' }).click(); await checkOpen?.(); await frame.getByRole('menuitem', { name: '编辑', exact: true }).click(); await expect(main.getByRole('status')).toHaveText('正在编辑'); break;
    case 'ContextMenu': await main.getByTestId('yue-context-menu-trigger').press('Shift+F10'); await checkOpen?.(); await frame.getByRole('menuitem', { name: '复制', exact: true }).click(); await expect(main.getByRole('status')).toHaveText('已复制示例'); break;
    case 'Tabs': await main.getByRole('tab', { name: '设置', exact: true }).click(); await expect(main.getByRole('tab', { name: '设置', exact: true })).toHaveAttribute('aria-selected', 'true'); await expect(main.getByRole('tabpanel', { name: '设置', exact: true })).toHaveText('设置内容'); break;
    case 'Accordion': await main.getByRole('button', { name: '如何使用' }).click(); await expect(main.getByText('组件可以通过 props 配置。')).toBeVisible(); break;
    case 'Tooltip': await main.getByRole('button', { name: '查看提示' }).focus(); await expect(frame.getByRole('tooltip')).toBeVisible(); break;
    case 'HoverCard': await main.getByRole('link', { name: '查看用户' }).hover(); await expect(frame.getByText('示例用户', { exact: true })).toBeVisible(); break;
    case 'Card': await main.getByRole('button', { name: '查看详情' }).click(); await expect(main.getByText('项目负责人：示例用户')).toBeVisible(); break;
    case 'Resizable': { const handle = main.getByRole('separator'); const before = await handle.getAttribute('aria-valuenow'); await handle.press('ArrowRight'); await expect(handle).not.toHaveAttribute('aria-valuenow', before!); break; }
    case 'ScrollArea': { const viewport = main.getByRole('region', { name: '滚动列表' }); await viewport.press('PageDown'); await expect.poll(() => viewport.evaluate(element => element.scrollTop)).toBeGreaterThan(0); break; }
    case 'Tag': await main.getByRole('button', { name: '移除研发标签' }).click(); await expect(main.getByText('研发', { exact: true })).toBeHidden(); break;
    case 'Toast': case 'Notification': { await main.getByRole('button', { name: '显示消息' }).click(); const title = frame.getByText(name === 'Toast' ? '保存成功' : '任务已完成', { exact: true }); await expect(title).toBeVisible(); await checkOpen?.(); await main.getByRole('button', { name: '显示消息' }).press('F6'); await frame.getByRole('button', { name: '关闭消息' }).click(); await expect(title).toBeHidden(); break; }
    case 'Empty': await main.getByRole('button', { name: '新建记录' }).click(); await expect(main.getByText('已创建第一条记录')).toBeVisible(); break;
    case 'Result': await main.getByRole('button', { name: '返回列表' }).click(); await expect(main.getByText('记录列表', { exact: true })).toBeVisible(); break;
    case 'Breadcrumb': await main.getByRole('link', { name: '首页' }).click(); await expect(main.getByRole('link', { name: '首页' })).toHaveAttribute('href', '#home'); break;
    case 'Pagination': await main.getByRole('button', { name: '下一页' }).click(); await expect(main.getByRole('button', { name: '第 2 页' })).toHaveAttribute('aria-current', 'page'); break;
    case 'BackTop': await main.getByText('长页面末尾').scrollIntoViewIfNeeded(); await expect.poll(() => main.evaluate(element => element.ownerDocument.defaultView!.scrollY)).toBeGreaterThan(0); await main.getByRole('button', { name: '返回顶部' }).click(); await expect.poll(() => main.evaluate(element => element.ownerDocument.defaultView!.scrollY)).toBe(0); break;
    case 'FullScreen': await main.getByRole('button', { name: '进入全屏' }).click(); await expect(frame.getByRole('button', { name: '退出全屏' }).or(frame.getByRole('alert'))).toBeVisible(); if (await frame.getByRole('button', { name: '退出全屏' }).isVisible()) await frame.getByRole('button', { name: '退出全屏' }).click(); break;
    case 'Tokens': await expect(main.getByRole('heading', { name: 'yue 设计令牌' })).toBeVisible(); break;
    case 'Badge': case 'Avatar': case 'Skeleton': case 'Spinner': case 'Progress': case 'Alert': case 'Icon': case 'Descriptions': case 'Separator': await expect(main.locator('[data-slot]').first()).toBeVisible(); break;
    default: throw new Error(`缺少组件交互验收 ${name}`);
  }
}
