import { spawn } from 'node:child_process';
import { randomUUID } from 'node:crypto';
import { mkdir, mkdtemp, open, rm } from 'node:fs/promises';
import { createServer } from 'node:net';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

// 真实目录 E2E 的启动器（S1-IDP-010 验收 2）：在本机私有临时目录与随机端口上起 dev-tenant-ops.sh 装配的真实宿主
// （PostgreSQL 容器、运营宿主与身份发行方），自测开发服务器的回环守卫，跑 playwright.directory.config.ts，最后停宿主、删容器。
const app = resolve(import.meta.dirname, '..');
const root = resolve(app, '../..');
const script = resolve(root, 'scripts/dev-tenant-ops.sh');
const state = await mkdtemp(resolve(tmpdir(), 'qian-directory-e2e-'));
async function port() {
  const server = createServer();
  await new Promise((ok, fail) => { server.once('error', fail); server.listen(0, '127.0.0.1', ok); });
  const address = server.address();
  await new Promise((ok) => server.close(ok));
  return address.port;
}
const [opsPort, identityPort, pgPort, uiPort] = await Promise.all([port(), port(), port(), port()]);
const env = {
  ...process.env, QIAN_DEV_STATE: state, QIAN_DEV_PG_CONTAINER: 'qian-directory-e2e-' + randomUUID().slice(0, 8), QIAN_DEV_PG_PORT: String(pgPort),
  QIAN_DEV_OPS_URL: `http://127.0.0.1:${opsPort}`, QIAN_DEV_IDENTITY_URL: `http://127.0.0.1:${identityPort}`, QIAN_DIRECTORY_UI_PORT: String(uiPort),
};
const log = await open(resolve(state, 'host.log'), 'w', 0o600);
const host = spawn('bash', [script, 'up'], { cwd: root, env, stdio: ['ignore', log.fd, log.fd] });
let ended = false;
const hostDone = new Promise((ok) => host.once('exit', () => { ended = true; ok(); }));
async function stopHost() {
  if (ended) return;
  host.kill('SIGTERM');
  await Promise.race([hostDone, delay(10_000)]);
  if (!ended) { host.kill('SIGKILL'); await hostDone; }
}

// 开发服务器只代理会话 Cookie，但仍只许监听回环地址：命令行把监听地址改成非回环时 dev 与 preview 都必须拒绝启动。
async function refusesNonLoopback(args) {
  const vite = spawn('pnpm', ['exec', 'vite', ...args, '--config', 'vite.directory.config.ts', '--host', '0.0.0.0', '--port', String(await port()), '--strictPort'], { cwd: app, env, stdio: ['ignore', 'pipe', 'pipe'], detached: true });
  let output = '';
  for (const stream of [vite.stdout, vite.stderr]) stream.on('data', (chunk) => { output += chunk; });
  const code = await Promise.race([new Promise((ok) => vite.once('exit', ok)), delay(30_000)]);
  if (code === undefined) process.kill(-vite.pid, 'SIGKILL');
  if (code === undefined || code === 0 || !output.includes('只允许监听回环地址')) throw new Error(`回环守卫失效：vite ${[...args, '--host', '0.0.0.0'].join(' ')} 没有拒绝启动\n${output.slice(-2000)}`);
}

async function ready(url, expected) {
  try { return (await fetch(url, { signal: AbortSignal.timeout(1500) })).status === expected; } catch { return false; }
}

let succeeded = false;
try {
  for (const args of [[], ['preview']]) await refusesNonLoopback(args);
  console.log('回环守卫自测通过：dev 与 preview 均拒绝非回环监听地址');
  console.log('启动独立真实宿主：', env.QIAN_DEV_IDENTITY_URL);
  let up = false;
  for (let i = 0; i < 300 && !up; i++) {
    if (ended) throw new Error(`宿主提前结束；本机诊断日志：${state}/host.log`);
    up = await ready(env.QIAN_DEV_OPS_URL + '/api/tenancy/meta/state-machine', 401) && await ready(env.QIAN_DEV_IDENTITY_URL + '/.well-known/openid-configuration', 200);
    if (!up) await delay(2000);
  }
  if (!up) throw new Error('真实宿主未在 600 秒内就绪');
  await mkdir(resolve(root, 'TestResults/directory-e2e'), { recursive: true });
  const test = spawn('pnpm', ['exec', 'playwright', 'test', '--config', 'playwright.directory.config.ts'], { cwd: app, env, stdio: 'inherit' });
  const code = await new Promise((ok, fail) => { test.once('error', fail); test.once('exit', ok); });
  if (code !== 0) throw new Error('真实目录验收失败，参见 Playwright 产物（TestResults/directory-e2e）');
  succeeded = true;
} finally {
  await stopHost(); await log.close();
  const down = spawn('bash', [script, 'down'], { cwd: root, env, stdio: 'inherit' });
  await new Promise((ok) => down.once('exit', ok));
  if (succeeded) await rm(state, { recursive: true, force: true });
  else console.error('失败日志保留在本机私有目录：', state);
}
