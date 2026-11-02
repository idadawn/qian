import { createRef } from 'react';
import { cleanup, render, screen, within } from '@testing-library/react';
import axe from 'axe-core';
import { afterEach, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { PlatformError } from '@idadawn/yue-sdk';
import type { FormSchema } from '@idadawn/yue-contracts';
import { SchemaForm, type FormApi } from './schema-form';
import { useDrawer } from './overlays';
import { credentialSchema, i18n, schema } from './test/fixture';

afterEach(cleanup);
it('密码字段沿用 Schema 校验与提交，浏览器语义可访问且只读不暴露密码', async () => {
  const submit = vi.fn();
  const view = render(<SchemaForm schema={credentialSchema} i18n={i18n} onSubmit={submit} />);
  const username = screen.getByLabelText('账号 *');
  const password = screen.getByLabelText('密码 *');
  expect(username.getAttribute('autocomplete')).toBe('username');
  expect(password.getAttribute('type')).toBe('password');
  expect(password.getAttribute('autocomplete')).toBe('current-password');
  await userEvent.fill(username, 'alice');
  await userEvent.tab();
  expect(document.activeElement).toBe(password);
  await userEvent.fill(password, 'short');
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).not.toHaveBeenCalled();
  expect(password.getAttribute('aria-invalid')).toBe('true');
  await userEvent.fill(password, 'example-password');
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).toHaveBeenCalledTimes(1);
  expect(submit.mock.calls[0]?.[0]).toEqual({ username: 'alice', password: 'example-password' });
  const result = await axe.run(document.body);
  expect(result.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious')).toEqual([]);
  view.rerender(<SchemaForm schema={credentialSchema} i18n={i18n} mode="readonly" />);
  expect(screen.getByTestId('yue-schema-form-readonly-password').textContent).toBe('••••');
  expect(document.body.textContent).not.toContain('example-password');
});

it('校验定位首个字段，合法数据才提交；键盘和 ARIA 无严重问题', async () => {
  const submit = vi.fn();
  render(<SchemaForm schema={schema} i18n={i18n} onSubmit={submit} />);
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).not.toHaveBeenCalled();
  expect(screen.getByLabelText('编码 *').getAttribute('aria-invalid')).toBe('true');
  expect(document.activeElement).toBe(screen.getByLabelText('编码 *'));
  await userEvent.fill(screen.getByLabelText('编码 *'), 'BAD!');
  await userEvent.fill(screen.getByLabelText('名称 *'), '甲');
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).not.toHaveBeenCalled();
  await userEvent.fill(screen.getByLabelText('编码 *'), 'abc');
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).toHaveBeenCalledTimes(1);
  expect(submit.mock.calls[0]?.[0]).toMatchObject({ code: 'abc', name: '甲' });
  const result = await axe.run(document.body);
  expect(result.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious')).toEqual([]);
});

it('dependencies 联动的必填与取消由同一校验器决定', async () => {
  const submit = vi.fn();
  render(<SchemaForm schema={schema} i18n={i18n} initialValues={{ code: 'a', name: '甲' }} onSubmit={submit} />);
  await userEvent.click(screen.getByRole('checkbox', { name: '启用联系' }));
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).not.toHaveBeenCalled();
  expect(screen.getByLabelText('电话 *').getAttribute('aria-invalid')).toBe('true');
  await userEvent.click(screen.getByRole('checkbox', { name: '启用联系' }));
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).toHaveBeenCalledTimes(1);
});

it('服务端 ErrorDetail 的 pointer 定位字段，编辑和只读不切换引擎', async () => {
  const apiRef = createRef<FormApi>();
  const props = { schema, i18n, apiRef, initialValues: { code: 'a', name: '甲', phone: '13812345678' }, onSubmit: () => { throw new PlatformError({ type: 'https://qian.dev/problems/protocol/INPUT_INVALID', title: '输入无效', code: 'INPUT_INVALID', status: 400, message: '名称被保留', trace_id: 'a'.repeat(32), pointer: '#/name' }); } };
  const view = render(<SchemaForm {...props} />);
  await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(screen.getByText('名称被保留')).toBeTruthy();
  expect(document.activeElement).toBe(screen.getByLabelText('名称 *'));
  view.rerender(<SchemaForm {...props} mode="readonly" />);
  expect(screen.queryByRole('textbox')).toBeNull();
  expect(screen.getByText('••••5678')).toBeTruthy();
  expect(apiRef.current?.getValues().name).toBe('甲');
});

it('FormApi 支持赋值、校验、重置和命令式提交', async () => {
  const apiRef = createRef<FormApi>();
  const submit = vi.fn();
  render(<SchemaForm schema={schema} i18n={i18n} apiRef={apiRef} onSubmit={submit} />);
  apiRef.current!.reset({ code: 'x', name: '乙' });
  expect(await apiRef.current!.validate()).toBe(true);
  await apiRef.current!.submit();
  expect(submit.mock.calls[0]?.[0]).toMatchObject({ name: '乙' });
  apiRef.current!.setValue('name', '丙');
  expect(apiRef.current!.getValues().name).toBe('丙');
});

it('OpenAPI 可空嵌套对象与数字联合类型沿用同一字段引擎', async () => {
  const submit = vi.fn();
  const nullable: typeof schema = { ...schema, data: { type: 'object', additionalProperties: false, properties: { quotas: { type: ['object', 'null'], properties: { users: { type: ['string', 'integer', 'null'], minimum: 0 } } } } }, ui: { layout: [{ field: 'quotas', children: [{ field: 'users', labelKey: 'test.name' }] }] }, rules: {} };
  const view = render(<SchemaForm schema={nullable} i18n={i18n} initialValues={{ quotas: { users: null } }} onSubmit={submit} />);
  const input = screen.getByLabelText('名称');
  expect(input.getAttribute('type')).toBe('number');
  await userEvent.fill(input, '-1'); await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit).not.toHaveBeenCalled();
  expect(input.getAttribute('aria-invalid')).toBe('true');
  await userEvent.fill(input, '25'); await userEvent.click(screen.getByRole('button', { name: '保存' }));
  expect(submit.mock.calls[0]?.[0]).toEqual({ quotas: { users: 25 } });
  view.rerender(<SchemaForm schema={nullable} i18n={i18n} initialValues={{ quotas: { users: 25 } }} mode="readonly" />);
  expect(screen.getByText('25').tagName).toBe('OUTPUT');
});

it('可空布尔字段与布尔字段同样横排', () => {
  const nullable: typeof schema = { ...schema, data: { type: 'object', additionalProperties: false, properties: { enabled: { type: ['boolean', 'null'] } } }, ui: { layout: [{ field: 'enabled', labelKey: 'test.enabled' }] }, rules: {} };
  render(<SchemaForm schema={nullable} i18n={i18n} testId="nullable" />);
  render(<SchemaForm schema={schema} i18n={i18n} testId="plain" />);
  const field = screen.getByTestId('nullable-field-enabled');
  expect(field.className).toContain('grid-cols-');
  expect(field.className).toBe(screen.getByTestId('plain-field-enabled').className);
});

it('枚举按 optionLabelKey 取文案；qian.segmented 渲染分段控件；抽屉里的提交按钮进固定页脚并仍提交本表单', async () => {
  i18n.addLanguagePack('zh-CN', { 'test.plan': '开通方式', 'test.size': '企业规模', 'test.plans.trial': '试用', 'test.plans.formal': '正式', 'test.sizes.small': '1–20 人', 'test.sizes.large': '500 人以上' });
  const enumSchema: FormSchema = {
    $schema: 'https://schema.qian.dev/form/v1', version: '1.0.0', id: 'qian.test.enum',
    data: { type: 'object', additionalProperties: false, required: ['plan', 'size'], properties: { plan: { type: 'string', enum: ['trial', 'formal'] }, size: { type: 'string', enum: ['small', 'large'] } } },
    ui: { layout: [{ field: 'plan', labelKey: 'test.plan', widget: 'qian.segmented', props: { optionLabelKey: 'test.plans' } }, { field: 'size', labelKey: 'test.size', props: { optionLabelKey: 'test.sizes' } }] },
    rules: {}, binding: {}, auth: {},
  };
  const submit = vi.fn();
  function Host() {
    const drawer = useDrawer(i18n);
    return <><button type="button" onClick={() => drawer.open({ title: '审核申请', size: 'md', content: <SchemaForm schema={enumSchema} i18n={i18n} initialValues={{ plan: 'trial', size: 'small' }} onSubmit={submit} submitLabel="确认开通" /> })}>打开</button>{drawer.holder}</>;
  }
  render(<Host />);
  await userEvent.click(screen.getByRole('button', { name: '打开' }));
  const dialog = await screen.findByRole('dialog', { name: '审核申请' });
  expect(within(dialog).getByRole('radio', { name: '试用' }).getAttribute('aria-checked')).toBe('true');
  await userEvent.click(within(dialog).getByRole('radio', { name: '正式' }));
  await userEvent.click(within(dialog).getByRole('combobox', { name: /企业规模/ }));
  await userEvent.click(await screen.findByRole('option', { name: '500 人以上' }));
  const footer = within(dialog).getByTestId('yue-sheet-footer');
  const confirm = within(footer).getByRole('button', { name: '确认开通' });
  expect(within(footer).getByRole('button', { name: '取消' })).toBeTruthy();
  expect(confirm.getAttribute('form')).toBe(dialog.querySelector('form')?.id);
  await userEvent.click(confirm);
  await vi.waitFor(() => expect(submit).toHaveBeenCalledTimes(1));
  expect(submit.mock.calls[0]?.[0]).toEqual({ plan: 'formal', size: 'large' });
});

it('抽屉里的子步骤表单（inlineActions）按钮紧跟表单、不带取消，页脚照常只有关闭', async () => {
  const submit = vi.fn();
  const lookup: FormSchema = { ...credentialSchema, data: { type: 'object', additionalProperties: false, required: ['username'], properties: { username: { type: 'string', minLength: 1 } } }, ui: { layout: [{ field: 'username', labelKey: 'test.username' }] } };
  function Host() {
    const drawer = useDrawer(i18n);
    return <><button type="button" onClick={() => drawer.open({ title: '选择管理员', content: <SchemaForm schema={lookup} i18n={i18n} inlineActions onSubmit={submit} submitLabel="查找账号" testId="lookup" /> })}>打开</button>{drawer.holder}</>;
  }
  render(<Host />);
  await userEvent.click(screen.getByRole('button', { name: '打开' }));
  const dialog = await screen.findByRole('dialog', { name: '选择管理员' });
  const form = within(dialog).getByTestId('lookup');
  const footer = within(dialog).getByTestId('yue-sheet-footer');
  expect(within(form).queryByRole('button', { name: '取消' })).toBeNull();
  expect(within(footer).queryByRole('button', { name: '查找账号' })).toBeNull();
  expect(within(footer).getByRole('button', { name: '关闭' })).toBeTruthy();
  await userEvent.fill(within(form).getByLabelText('账号 *'), 'adminc');
  await userEvent.click(within(form).getByRole('button', { name: '查找账号' }));
  await vi.waitFor(() => expect(submit).toHaveBeenCalledTimes(1));
  expect(submit.mock.calls[0]?.[0]).toEqual({ username: 'adminc' });
  // 等抽屉入场动画与提交态结束再查对比度，免得取到半透明的中间帧。
  await vi.waitFor(() => expect(within(form).getByRole('button', { name: '查找账号' }).hasAttribute('disabled')).toBe(false));
  await Promise.all(document.getAnimations().map((animation) => animation.finished));
  const result = await axe.run(dialog);
  expect(result.violations.filter((v) => v.impact === 'critical' || v.impact === 'serious')).toEqual([]);
});
