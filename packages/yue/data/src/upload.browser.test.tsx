import { expect, it } from 'vitest';
import { createMockTransport, PlatformClient } from '@idadawn/yue-sdk';
import { fileSha256, uploadFile, type UploadProgress } from './upload';

const file = () => new File(['abcdefgh'], 'sample.txt', { type: 'text/plain' });
const hash = '9c56cc51b374c3ba189210d5b6d4bf57790d351c96c47c02190ecf1e430635ab';
function service({ failPart = 0, existing = false } = {}) {
  const parts = new Map<number, string>();
  const requested: number[] = [];
  let fail = failPart;
  let initialized = 0;
  let completed = 0;
  const object = { id: 'file-1', name: 'sample.txt', size: 8, contentType: 'text/plain' };
  const client = new PlatformClient({ baseUrl: 'https://upload.mock', transport: createMockTransport([
    { method: 'GET', path: '/uploads:lookup', respond: (request) => { expect(new URL(request.url).searchParams.get('sha256')).toBe(hash); return existing ? Response.json(object) : new Response(null, { status: 404 }); } },
    { method: 'POST', path: '/uploads:init', respond: async (request) => { initialized++; expect(await request.json()).toMatchObject({ sha256: hash, size: 8, name: 'sample.txt' }); return Response.json({ uploadId: 'session', partSize: 3, uploadedParts: [...parts.keys()] }); } },
    { method: 'PUT', path: /^\/uploads\/session\/parts\/(\d+)$/, respond: async (request, match) => { const n = Number(match![1]); requested.push(n); if (n === fail) { fail = 0; return new Response(null, { status: 503 }); } expect(request.headers.get('Content-Type')).toBe('application/octet-stream'); parts.set(n, await request.text()); return new Response(null, { status: 204 }); } },
    { method: 'POST', path: '/uploads/session:complete', respond: async (request) => { completed++; expect(await request.json()).toEqual({ sha256: hash, partCount: 3 }); expect([...parts.values()].join('')).toBe('abcdefgh'); return Response.json(object); } },
  ]) });
  return { client, parts, requested, counts: () => ({ initialized, completed }) };
}

it('真实浏览器 SHA-256 与分片体正确，进度以服务端确认字节递增', async () => {
  expect(await fileSha256(file())).toBe(hash);
  const backend = service();
  const progress: UploadProgress[] = [];
  expect((await uploadFile(backend.client, file(), { onProgress: (next) => progress.push(next) })).id).toBe('file-1');
  expect(backend.requested).toEqual([1, 2, 3]);
  expect(progress.filter((entry) => entry.stage === 'uploading').map((entry) => entry.loaded)).toEqual([0, 3, 6, 8]);
  expect(progress.at(-1)?.stage).toBe('complete');
});

it('分片失败后重入只发送剩余分片，不重发已确认部分', async () => {
  const backend = service({ failPart: 2 });
  await expect(uploadFile(backend.client, file())).rejects.toMatchObject({ status: 503 });
  expect(backend.counts().completed).toBe(0);
  await uploadFile(backend.client, file());
  expect(backend.requested).toEqual([1, 2, 2, 3]);
  expect(backend.counts()).toEqual({ initialized: 2, completed: 1 });
});

it('取消时不 complete，恢复查询服务端已接收分片后继续', async () => {
  const backend = service();
  const controller = new AbortController();
  await expect(uploadFile(backend.client, file(), { signal: controller.signal, onProgress: (progress) => { if (progress.loaded === 3) controller.abort(); } })).rejects.toMatchObject({ name: 'AbortError' });
  expect(backend.counts().completed).toBe(0);
  await uploadFile(backend.client, file());
  expect(backend.requested).toEqual([1, 2, 3]);
});

it('秒传命中直接返回已有 id，不初始化或传输分片', async () => {
  const backend = service({ existing: true });
  const progress: UploadProgress[] = [];
  const object = await uploadFile(backend.client, file(), { onProgress: (next) => progress.push(next) });
  expect(object.id).toBe('file-1');
  expect(backend.requested).toEqual([]);
  expect(backend.counts()).toEqual({ initialized: 0, completed: 0 });
  expect(progress.at(-1)?.deduplicated).toBe(true);
});
