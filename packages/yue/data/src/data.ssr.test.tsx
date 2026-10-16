import { renderToString } from 'react-dom/server';
import { expect, it } from 'vitest';
import { Ajv2020 } from 'ajv/dist/2020';
import { dataColumnSchema } from '@idadawn/yue-contracts';
import { PlatformClient, createMockTransport } from '@idadawn/yue-sdk';
import { Cascader, DataTable, DatePicker, DateRange, ImagePreview, TimePicker, TimeRange, Tree, TreeSelect, Upload, createDataI18n } from './index';
const i18n = createDataI18n();
it('列定义的可序列化部分由唯一 JSON Schema 校验', () => {
  const validate = new Ajv2020().compile(dataColumnSchema);
  expect(validate({ field: 'name', titleKey: 'tenant.name', sortable: true, filterable: true, fixed: 'left', width: 200 })).toBe(true);
  expect(validate({ field: 'name', width: -1 })).toBe(false);
  expect(validate({ field: 'name', titleKey: 'tenant.name', unexpected: true })).toBe(false);
});
it('表格、树选择、日期、图片与上传可 SSR，不依赖浏览器全局对象', () => {
  const nodes = [{ id: 'a', label: '甲' }];
  const markup = renderToString(<>
    <DataTable columns={[{ field: 'label', titleKey: 'common.detail' }]} data={nodes} total={1} query={{ pageIndex: 0, pageSize: 5, sorting: [], filters: {} }} onQueryChange={() => undefined} getRowId={(row) => row.id} i18n={i18n} label="表格" />
    <Tree nodes={nodes} aria-label="树" /><TreeSelect nodes={nodes} label="选择" i18n={i18n} /><Cascader nodes={nodes} label="层级" i18n={i18n} />
    <DatePicker label="日期" /><DateRange label="日期范围" i18n={i18n} /><TimePicker label="时间" /><TimeRange label="时间范围" i18n={i18n} />
    <ImagePreview src="/image.png" alt="图片" i18n={i18n} /><Upload client={new PlatformClient({ baseUrl: 'https://example.test', transport: createMockTransport([]) })} label="文件" i18n={i18n} />
  </>);
  for (const slot of ['data-table', 'tree', 'tree-select', 'cascader', 'date-picker', 'date-range', 'time-picker', 'time-range', 'image-preview', 'upload']) expect(markup).toContain(`yue-${slot}`);
});
it.each(['loading', 'empty', 'error'] as const)('表格 %s 状态有稳定可寻址出口', (state) => {
  const markup = renderToString(<DataTable columns={[{ field: 'id', titleKey: 'common.detail' }]} data={[]} total={0} query={{ pageIndex: 0, pageSize: 5, sorting: [], filters: {} }} onQueryChange={() => undefined} getRowId={(row: { id: string }) => row.id} i18n={i18n} label="表格" loading={state === 'loading'} error={state === 'error' ? i18n.t('data.error') : null} />);
  expect(markup).toContain(`data-table-${state}`);
});
