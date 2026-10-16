import { createRef } from 'react';
import { cleanup, render, screen } from '@testing-library/react';
import axe from 'axe-core';
import { afterEach, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { PlatformError } from '@idadawn/yue-sdk';
import { SchemaForm, type FormApi } from './schema-form';
import { i18n, schema } from './test/fixture';

afterEach(cleanup);
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
