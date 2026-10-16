import { useEffect, useMemo, useState } from 'react';
import { createMockTransport, PlatformClient } from '@idadawn/yue-sdk';
import { Cascader, DataTable, DatePicker, DateRange, ImagePreview, TimePicker, TimeRange, Tree, TreeSelect, Upload, createDataI18n, fileSha256, type DataTableQuery } from '@idadawn/yue-data';

interface Row { id: string; name: string }
interface Paths { '/rows': { get: { parameters: { query: { skip: number; take: number; name?: string; sort?: string; desc?: boolean } }; responses: { 200: { content: { 'application/json': { items: Row[]; total: number } } } } } } }
const i18n = createDataI18n();
i18n.addLanguagePack('zh-CN', { 'demo.id': '编号', 'demo.name': '名称' });
export const DATA_COMPONENTS = ['DataTable', 'Tree', 'TreeSelect', 'Cascader', 'DatePicker', 'DateRange', 'TimePicker', 'TimeRange', 'Upload', 'ImagePreview'] as const;

export default function DataDemo({ kind = 'DataTable', loading = false, empty = false, invalid = false }: { kind?: string; loading?: boolean; empty?: boolean; invalid?: boolean }) {
  const [query, setQuery] = useState<DataTableQuery>({ pageIndex: 0, pageSize: 5, sorting: [], filters: {} });
  const [response, setResponse] = useState<{ items: Row[]; total: number; loading: boolean; error?: string }>({ items: [], total: 0, loading: true });
  const [selected, setSelected] = useState<string | null>(null);
  const [path, setPath] = useState<string[]>([]);
  const [date, setDate] = useState('2026-10-17');
  const [time, setTime] = useState('09:00');
  const [range, setRange] = useState<[string, string]>(['2026-10-17', '2026-10-18']);
  const [timeRange, setTimeRange] = useState<[string, string]>(['09:00', '17:00']);
  const [uploaded, setUploaded] = useState('');
  const nodes = useMemo(() => Array.from({ length: 10_000 }, (_, n) => ({ id: `node-${n}`, label: `节点 ${n}` })), []);
  const [client] = useState(() => {
    const rows = Array.from({ length: 24 }, (_, n) => ({ id: String(n + 1), name: `客户 ${String(n + 1).padStart(2, '0')}` }));
    const parts = new Map<number, Uint8Array>();
    let metadata = { name: '', size: 0, contentType: '', sha256: '' };
    let object: { id: string; name: string; size: number; contentType: string } | null = null;
    let failed = false;
    return new PlatformClient({ baseUrl: 'https://data.mock', transport: createMockTransport([
      { method: 'GET', path: '/rows', respond: (request) => {
        const params = new URL(request.url).searchParams;
        let items = rows.filter((row) => row.name.includes(params.get('name') ?? ''));
        if (params.get('sort') === 'name') items = [...items].sort((a, b) => a.name.localeCompare(b.name) * (params.get('desc') === 'true' ? -1 : 1));
        return Response.json({ total: items.length, items: items.slice(Number(params.get('skip')), Number(params.get('skip')) + Number(params.get('take'))) });
      } },
      { method: 'GET', path: '/uploads:lookup', respond: (request) => object && new URL(request.url).searchParams.get('sha256') === metadata.sha256 ? Response.json(object) : new Response(null, { status: 404 }) },
      { method: 'POST', path: '/uploads:init', respond: async (request) => { const next = await request.json() as typeof metadata; if (next.sha256 !== metadata.sha256) { parts.clear(); object = null; } metadata = next; return Response.json({ uploadId: 'demo', partSize: 3, uploadedParts: [...parts.keys()] }); } },
      { method: 'PUT', path: /^\/uploads\/demo\/parts\/(\d+)$/, respond: async (request, match) => { const n = Number(match![1]); if (invalid && n === 2 && !failed) { failed = true; return new Response(null, { status: 503 }); } parts.set(n, new Uint8Array(await request.arrayBuffer())); return new Response(null, { status: 204 }); } },
      { method: 'POST', path: '/uploads/demo:complete', respond: async () => {
        const ordered = [...parts.entries()].sort(([a], [b]) => a - b).map(([, bytes]) => bytes as Uint8Array<ArrayBuffer>);
        if (await fileSha256(new Blob(ordered)) !== metadata.sha256) return new Response(null, { status: 400 });
        object = { id: 'file-demo', name: metadata.name, size: metadata.size, contentType: metadata.contentType }; return Response.json(object);
      } },
    ]) });
  });
  useEffect(() => {
    if (kind !== 'DataTable') return;
    const controller = new AbortController();
    void client.api<Paths>().GET('/rows', { params: { query: { skip: query.pageIndex * query.pageSize, take: query.pageSize, name: query.filters.name, sort: query.sorting[0]?.id, desc: query.sorting[0]?.desc } }, signal: controller.signal }).then(({ data }) => {
      if (!controller.signal.aborted && data) setResponse({ ...data, loading: false });
    }).catch(() => { if (!controller.signal.aborted) setResponse({ items: [], total: 0, loading: false, error: '加载失败' }); });
    return () => controller.abort();
  }, [client, kind, query]);

  if (kind === 'Tree') return <Tree nodes={nodes} aria-label="一万节点" onSelectedChange={setSelected} selectedId={selected} />;
  if (kind === 'TreeSelect') return <TreeSelect nodes={nodes} i18n={i18n} label="组织选择" value={selected} onChange={setSelected} />;
  if (kind === 'Cascader') return <Cascader nodes={[{ id: 'root', label: '总部', children: nodes }]} i18n={i18n} label="层级选择" value={path} onChange={setPath} />;
  if (kind === 'DatePicker') return <DatePicker label="日期" value={date} onChange={setDate} />;
  if (kind === 'DateRange') return <DateRange label="日期范围" value={range} onChange={setRange} i18n={i18n} />;
  if (kind === 'TimePicker') return <TimePicker label="时间" value={time} onChange={setTime} />;
  if (kind === 'TimeRange') return <TimeRange label="时间范围" value={timeRange} onChange={setTimeRange} i18n={i18n} />;
  if (kind === 'Upload') return <><Upload client={client} i18n={i18n} label="上传文件" onComplete={(file) => setUploaded(file.id)} /><p role="status">{uploaded}</p></>;
  if (kind === 'ImagePreview') return <ImagePreview i18n={i18n} alt="示例图片" src="data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///ywAAAAAAQABAAACAUwAOw==" />;
  return <DataTable columns={[{ field: 'id', titleKey: 'demo.id', fixed: 'left', width: 100 }, { field: 'name', titleKey: 'demo.name', sortable: true, filterable: true }]} data={empty ? [] : response.items} total={empty ? 0 : response.total} query={query} onQueryChange={(next) => { setResponse((current) => ({ ...current, loading: true })); setQuery(next); }} getRowId={(row) => row.id} i18n={i18n} label="客户列表" loading={loading || response.loading} error={invalid ? '加载失败' : response.error} selectable />;
}

export const DATA_DEMOS = Object.fromEntries(DATA_COMPONENTS.map((name) => [name, DataDemo]));
