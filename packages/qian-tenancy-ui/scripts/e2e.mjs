import { spawn } from 'node:child_process';
import { createServer } from 'node:net';
import { mkdtemp, mkdir, open, access, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { randomUUID } from 'node:crypto';

const root = resolve(import.meta.dirname, '../../..');
const state = await mkdtemp(resolve(tmpdir(), 'cody-tenant-ui-'));
const script = resolve(root, 'scripts/dev-tenant-ops.sh');
async function port() {
  const server = createServer();
  await new Promise((ok, fail) => { server.once('error', fail); server.listen(0, '127.0.0.1', ok); });
  const address = server.address();
  await new Promise((ok) => server.close(ok));
  return address.port;
}
const [apiPort, pgPort, uiPort] = await Promise.all([port(), port(), port()]);
const env = { ...process.env, QIAN_DEV_STATE: state, QIAN_DEV_PG_CONTAINER: 'cody-ten005-' + randomUUID().slice(0, 8), QIAN_DEV_PG_PORT: String(pgPort), QIAN_DEV_OPS_URL: `http://127.0.0.1:${apiPort}`, QIAN_DEV_UI_PORT: String(uiPort), QIAN_UI_STOP_FILE: resolve(state, 'stop-host'), QIAN_UI_STOPPED_FILE: resolve(state, 'host-stopped') };
const log = await open(resolve(state, 'host.log'), 'w', 0o600);
const host = spawn('bash', [script, 'up'], { cwd: root, env, stdio: ['ignore', log.fd, log.fd] });
let ended = false;
const hostDone = new Promise((ok) => host.once('exit', () => { ended = true; ok(); }));
let stopping;
function stopHost() {
  return stopping ??= (async () => {
    if (!ended) { host.kill('SIGTERM'); await Promise.race([hostDone, delay(10_000)]); if (!ended) { host.kill('SIGKILL'); await hostDone; } }
    await writeFile(env.QIAN_UI_STOPPED_FILE, 'stopped\n');
  })();
}
// 开发代理守卫自测：代理默认以平台超管身份转发，命令行把监听地址改成非回环时，dev 与 preview 都必须拒绝启动。
async function refusesNonLoopback(args) {
  const vite = spawn('pnpm', ['exec', 'vite', ...args, '--host', '0.0.0.0', '--port', String(await port()), '--strictPort'], { cwd: resolve(root, 'packages/qian-tenancy-ui'), env, stdio: ['ignore', 'pipe', 'pipe'], detached: true });
  let output = '';
  for (const stream of [vite.stdout, vite.stderr]) stream.on('data', (chunk) => { output += chunk; });
  const code = await Promise.race([new Promise((ok) => vite.once('exit', ok)), delay(30_000)]);
  if (code === undefined) process.kill(-vite.pid, 'SIGKILL');
  if (code === undefined || code === 0 || !output.includes('只允许监听回环地址')) throw new Error(`开发代理守卫失效：vite ${[...args, '--host', '0.0.0.0'].join(' ')} 没有拒绝启动`);
}
let watch;
let succeeded = false;
try {
  for (const args of [[], ['preview']]) await refusesNonLoopback(args);
  console.log('开发代理守卫自测通过：dev 与 preview 均拒绝非回环监听地址');
  console.log('启动独立真实宿主：', env.QIAN_DEV_OPS_URL);
  let ready = false;
  for (let i = 0; i < 300; i++) {
    if (ended) throw new Error(`宿主提前结束；本机诊断日志：${state}/host.log`);
    try { ready = (await fetch(env.QIAN_DEV_OPS_URL + '/api/tenancy/meta/state-machine', { signal: AbortSignal.timeout(1500) })).status === 401; } catch { /* 启动中 */ }
    if (ready) break;
    await delay(2000);
  }
  if (!ready) throw new Error('真实宿主未在 600 秒内就绪');
  watch = setInterval(() => { void access(env.QIAN_UI_STOP_FILE).then(stopHost).catch(() => {}); }, 100);
  await mkdir(resolve(root, 'TestResults/tenant-ui'), { recursive: true });
  const test = spawn('pnpm', ['exec', 'playwright', 'test'], { cwd: resolve(root, 'packages/qian-tenancy-ui'), env, stdio: 'inherit' });
  const code = await new Promise((ok, fail) => { test.once('error', fail); test.once('exit', ok); });
  if (code !== 0) throw new Error('真实页面验收失败，参见 Playwright 产物');
  succeeded = true;
} finally {
  clearInterval(watch);
  await stopHost(); await log.close();
  const down = spawn('bash', [script, 'down'], { cwd: root, env, stdio: 'inherit' });
  await new Promise((ok) => down.once('exit', ok));
  if (succeeded) await rm(state, { recursive: true, force: true });
  else console.error('失败日志保留在本机私有目录：', state);
}
