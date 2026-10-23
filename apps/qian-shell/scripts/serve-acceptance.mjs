import { createServer } from 'node:http';
import { mkdirSync, readFileSync, statSync } from 'node:fs';
import { resolve, extname, sep } from 'node:path';
const root = resolve(import.meta.dirname, '../../..');
const app = resolve(root, 'apps/qian-shell/dist/acceptance');
const remote = resolve(root, 'packages/qian-tenancy-ui/dist/remote');
const evidence = resolve(root, 'apps/qian-shell/acceptance-results');
mkdirSync(evidence, { recursive: true });
const manifest = JSON.parse(readFileSync(resolve(root, 'plugins/qian.tenancy/ui/manifest.json'), 'utf8'));
if (!process.env.QIAN_MF_AUDIT_STATE) throw new Error('缺少真实审计验收宿主状态文件');
const audit = JSON.parse(readFileSync(process.env.QIAN_MF_AUDIT_STATE, 'utf8'));
const backend = new URL(audit.url);
if (backend.protocol !== 'http:' || !['127.0.0.1', '[::1]'].includes(backend.hostname) || backend.username || backend.password || backend.search || backend.hash) throw new Error('审计验收代理只允许无凭据的回环 HTTP 地址');
async function proxyAudit(request, response, path, token) {
  const origin = request.headers.origin;
  if (origin && origin !== `http://${request.headers.host}`) { response.writeHead(403).end(); return; }
  let body = '';
  for await (const chunk of request) { body += chunk; if (body.length > 16384) { response.writeHead(413).end(); return; } }
  const upstream = await fetch(new URL(path, backend), { method: request.method, redirect: 'error', headers: { Authorization: `Bearer ${token}`, 'Content-Type': 'application/json' }, ...(body ? { body } : {}) });
  response.writeHead(upstream.status, { 'Content-Type': upstream.headers.get('content-type') ?? 'application/json' });
  response.end(Buffer.from(await upstream.arrayBuffer()));
}
const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.json': 'application/json', '.zip': 'application/zip', '.ts': 'text/plain' };
const server = createServer(async (request, response) => {
  const url = new URL(request.url, 'http://127.0.0.1');
  const scenario = (request.headers.cookie ?? '').match(/(?:^|; )mf-scenario=([^;]+)/)?.[1] ?? 'a';
  response.setHeader('Cache-Control', 'no-store');
  if (url.pathname === '/api/shell/bootstrap') {
    const a = '11111111-1111-4111-8111-111111111111', b = '22222222-2222-4222-8222-222222222222';
    const scope = `tenant:${a}`;
    const selected = structuredClone(manifest);
    if (scenario === 'bad') selected.engines.yue = '^99.0.0';
    if (scenario === 'shadow') selected.pages[0].nav.title = '租户专属管理';
    const registrations = [{ id: 'qian.tenancy', version: '1.0.0', scope, manifest: selected }];
    if (scenario === 'shadow') registrations.unshift({ id: 'qian.tenancy', version: '1.0.0', scope: 'global', manifest });
    response.setHeader('Content-Type', 'application/json');
    response.end(JSON.stringify({ context: { tenant: { id: scenario === 'b' ? b : a, name: '验收租户' }, membership: { id: 'member-a', permissions: ['qian.tenancy:tenant:read'] }, app: null, locale: 'zh-CN', theme: 'light' },
      remotes: registrations, mounted: registrations.map(({ scope, id }) => `${scope}|${id}`), entitled: registrations.map(({ scope, id }) => `${scope}|${id}`), permissions: ['qian.tenancy:tenant:read'] })); return;
  }
  if (url.pathname === '/api/shell/audit/remote-rejected' && request.method === 'POST') {
    await proxyAudit(request, response, '/api/shell/audit/remote-rejected', audit.writerToken); return;
  }
  if (url.pathname === '/__acceptance/audit' && request.method === 'GET') { await proxyAudit(request, response, '/__acceptance/audit', audit.readerToken); return; }
  if (url.pathname === '/api/tenancy/tenants') { response.setHeader('Content-Type', 'application/json'); response.end(JSON.stringify({ items: [], total: 0 })); return; }
  if (url.pathname === '/api/tenancy/meta/state-machine') { response.setHeader('Content-Type', 'application/json'); response.end(JSON.stringify({ transitions: [], actions: [], states: [] })); return; }
  const remoteRequest = url.pathname.startsWith('/remotes/qian.tenancy/');
  const base = remoteRequest ? remote : app;
  const relative = remoteRequest ? url.pathname.slice('/remotes/qian.tenancy/'.length) : url.pathname.slice(1);
  let file = resolve(base, relative || 'index.html');
  if (!file.startsWith(base + sep)) { response.writeHead(403).end(); return; }
  try { if (!statSync(file).isFile()) throw new Error('not a file'); }
  catch { if (remoteRequest || extname(file)) { response.writeHead(404).end(); return; } file = resolve(app, 'index.html'); }
  response.setHeader('Content-Type', types[extname(file)] ?? 'application/octet-stream'); response.end(readFileSync(file));
});
server.listen(Number(process.env.QIAN_MF_PORT ?? 5180), '127.0.0.1');
const stop = () => server.close(() => process.exit(0)); process.on('SIGTERM', stop); process.on('SIGINT', stop);
