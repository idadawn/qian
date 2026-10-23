import { spawn } from 'node:child_process';
import { request } from 'node:https';
import { createServer } from 'node:net';
import { mkdtemp, mkdir, open, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { randomUUID } from 'node:crypto';

// 真实流程验收入口（S1-YUE-002）：独立临时状态目录、随机回环端口与自有 PostgreSQL / Dex 容器，用 scripts/dev-shell.sh up 拉起真实 qian.identity +
// 上游 Dex + 网关 BFF，等就绪后跑 Playwright，最后停宿主、删容器。失败时保留状态目录（宿主日志）。CI 用同一入口。
const root = resolve(import.meta.dirname, '../../..');
const script = resolve(root, 'scripts/dev-shell.sh');
const state = await mkdtemp(resolve(tmpdir(), 'qian-shell-auth-'));
async function port() {
  const server = createServer();
  await new Promise((ok, fail) => { server.once('error', fail); server.listen(0, '127.0.0.1', ok); });
  const { port } = server.address();
  await new Promise(ok => server.close(ok));
  return port;
}
const [apiPort, gatewayPort, pgPort, dexPort] = await Promise.all([port(), port(), port(), port()]);
const runId = randomUUID().slice(0, 8);
const env = {
  ...process.env, QIAN_DEV_STATE: state, QIAN_DEV_PG_CONTAINER: 'qian-shell-auth-' + runId, QIAN_DEV_PG_PORT: String(pgPort),
  QIAN_DEV_DEX_CONTAINER: 'qian-shell-auth-dex-' + runId, QIAN_DEV_DEX_PORT: String(dexPort),
  QIAN_DEV_SHELL_API_URL: `http://127.0.0.1:${apiPort}`, QIAN_DEV_SHELL_URL: `https://localhost:${gatewayPort}`, QIAN_DEV_SHELL_SKIP_UI_BUILD: '1',
};
function run(command, args, options = {}) {
  return new Promise((ok, fail) => {
    const child = spawn(command, args, { cwd: root, env, stdio: 'inherit', ...options });
    child.once('error', fail); child.once('exit', code => code === 0 ? ok() : fail(new Error(`${command} ${args.join(' ')} 退出码 ${code}`)));
  });
}
// 网关就绪的判据：匿名 GET /auth/session 应答 401（自签名证书，探测不校验）。
function probe(url) {
  return new Promise(ok => {
    const req = request(url, { method: 'GET', rejectUnauthorized: false, timeout: 1500 }, response => { response.resume(); ok(response.statusCode); });
    req.once('error', () => ok(0)); req.once('timeout', () => { req.destroy(); ok(0); }); req.end();
  });
}
// 前端构建产物先于宿主：宿主启动时检查两个 dist 存在；构建只在这里做一次，up 不重复构建。
await run('pnpm', ['--filter', '@qian/identity-ui', 'build']);
await run('pnpm', ['--filter', 'qian-shell-auth', 'build']);
const log = await open(resolve(state, 'host.log'), 'w', 0o600);
const host = spawn('bash', [script, 'up'], { cwd: root, env, stdio: ['ignore', log.fd, log.fd] });
let ended = false;
const hostDone = new Promise(ok => host.once('exit', () => { ended = true; ok(); }));
async function stopHost() {
  if (ended) return;
  host.kill('SIGTERM'); await Promise.race([hostDone, delay(15_000)]);
  if (!ended) { host.kill('SIGKILL'); await hostDone; }
}
let succeeded = false;
try {
  console.log('启动真实身份 / 业务宿主、上游 Dex 与网关 BFF：', env.QIAN_DEV_SHELL_API_URL, `http://127.0.0.1:${dexPort}/dex`, env.QIAN_DEV_SHELL_URL);
  let ready = false;
  for (let i = 0; i < 300 && !ready; i++) {
    if (ended) throw new Error(`宿主提前结束；日志：${state}/host.log`);
    ready = await probe(env.QIAN_DEV_SHELL_URL + '/auth/session') === 401;
    if (!ready) await delay(2000);
  }
  if (!ready) throw new Error('真实宿主未在 600 秒内就绪');
  await mkdir(resolve(root, 'TestResults/shell-auth'), { recursive: true });
  await run('pnpm', ['exec', 'playwright', 'test'], { cwd: resolve(root, 'apps/qian-shell-auth') });
  succeeded = true;
} finally {
  await stopHost(); await log.close();
  // down 会删容器、docker logs 随之不可见：失败时先把两个容器的日志留进状态目录（容器起不来时也可用 docker events 看 die 的退出码）。
  if (!succeeded) {
    for (const [name, file] of [[env.QIAN_DEV_PG_CONTAINER, 'postgres.log'], [env.QIAN_DEV_DEX_CONTAINER, 'dex.log']]) {
      const out = await open(resolve(state, file), 'w', 0o600);
      await new Promise(ok => spawn('docker', ['logs', name], { stdio: ['ignore', out.fd, out.fd] }).once('exit', ok).once('error', ok));
      await out.close();
    }
  }
  await new Promise(ok => spawn('bash', [script, 'down'], { cwd: root, env, stdio: 'inherit' }).once('exit', ok));
  if (succeeded) await rm(state, { recursive: true, force: true });
  else console.error('失败日志保留在本机私有目录：', state);
}
