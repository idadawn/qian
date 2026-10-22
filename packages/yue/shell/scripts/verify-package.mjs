// 仓库根运行：先 pnpm yue:build，再 node packages/yue/shell/scripts/verify-package.mjs。
// 使用真实 tarball 和独立消费者，不设源码 alias，也不向浏览器注入 React 全局变量。
import { mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join, resolve } from 'node:path';
import { createRequire } from 'node:module';
import { spawnSync } from 'node:child_process';

const root = resolve(import.meta.dirname, '../../../..');
const output = mkdtempSync(join(tmpdir(), 'yue-shell-package-'));
const appRequire = createRequire(join(root, 'apps/yue-playground/package.json'));
const installed = name => JSON.parse(readFileSync(join(root, name === 'playwright' ? 'packages/yue/shell' : 'apps/yue-playground', 'node_modules', name, 'package.json'), 'utf8')).version;
const viteRequire = createRequire(appRequire.resolve('vite/package.json'));
const rolldown = JSON.parse(readFileSync(viteRequire.resolve('rolldown/package.json'), 'utf8')).version;
function run(args, cwd = root) {
  const result = spawnSync('pnpm', args, { cwd, encoding: 'utf8', timeout: 120_000 });
  if (result.error || result.status !== 0) throw new Error(`${args.join(' ')}\n${result.error ?? ''}\n${result.stdout}\n${result.stderr}`);
  return result.stdout;
}
const overrides = {};
for (const name of ['shell', 'core', 'form', 'sdk', 'tokens', 'contracts']) {
  const pkg = `@idadawn/yue-${name}`;
  const version = JSON.parse(readFileSync(join(root, `packages/yue/${name}/package.json`), 'utf8')).version;
  run(['--filter', pkg, 'pack', '--pack-destination', output]);
  overrides[pkg] = `file:${join(output, `idadawn-yue-${name}-${version}.tgz`)}`;
}
writeFileSync(join(output, 'package.json'), JSON.stringify({ name: 'yue-shell-package-consumer', private: true, type: 'module', packageManager: JSON.parse(readFileSync(join(root, 'package.json'), 'utf8')).packageManager, dependencies: { ...overrides, ...Object.fromEntries(['react', 'react-dom', 'vite', '@vitejs/plugin-react', 'playwright'].map(name => [name, installed(name)])) } }, null, 2));
writeFileSync(join(output, 'pnpm-workspace.yaml'), `overrides:\n${Object.entries({ ...overrides, rolldown }).map(([name, value]) => `  ${JSON.stringify(name)}: ${JSON.stringify(value)}`).join('\n')}\n`);
writeFileSync(join(output, 'index.html'), '<!doctype html><html><head><meta charset="utf-8"><title>Package proof</title></head><body><div id="root"></div><script type="module" src="/main.jsx"></script></body></html>');
writeFileSync(join(output, 'main.jsx'), `
import { useState } from 'react';
import { createRoot } from 'react-dom/client';
import { createI18n } from '@idadawn/yue-sdk';
import { AppShell, Authentication, Page, PageHeader, PreferencesProvider, initPreferences, installShellLanguagePacks } from '@idadawn/yue-shell';
const i18n = createI18n(); installShellLanguagePacks(i18n);
const preferences = initPreferences({ namespace: 'packed', storage: null });
const provider = { signIn: async () => ({ kind: 'authenticated', session: { subjectId: 'test', displayName: 'test', tenantId: 'a', tenants: [] } }), selectTenant: async () => { throw new Error('unused'); }, switchTenant: async () => { throw new Error('unused'); } };
function App() { const [login, setLogin] = useState(false); return <PreferencesProvider store={preferences}>{login ? <Authentication i18n={i18n} provider={provider} onAuthenticated={() => setLogin(false)} onRedirect={() => {}} /> : <AppShell i18n={i18n} brand="Packed consumer" menu={[]}><Page header={<PageHeader title="已登录外壳" />}><button onClick={() => setLogin(true)}>进入登录页</button></Page></AppShell>}</PreferencesProvider>; }
createRoot(document.getElementById('root')).render(<App />);
`);
writeFileSync(join(output, 'verify.mjs'), `
import assert from 'node:assert/strict';
import { readFile, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { resolve, join, sep } from 'node:path';
import { build } from 'vite';
import react from '@vitejs/plugin-react';
import { chromium } from 'playwright';
const root = import.meta.dirname;
await build({ root, configFile: false, plugins: [react()], build: { manifest: true } });
const dist = join(root, 'dist');
const manifest = JSON.parse(await readFile(join(dist, '.vite/manifest.json'), 'utf8'));
const passwordEntry = Object.values(manifest).find(entry => /password-login/.test(entry.name ?? ''));
assert.ok(passwordEntry?.isDynamicEntry, '必须保留密码表单动态入口');
const server = createServer(async (request, response) => {
  try {
    let file = resolve(dist, '.' + new URL(request.url, 'http://local').pathname);
    assert.ok(file === dist || file.startsWith(dist + sep));
    if ((await stat(file)).isDirectory()) file = join(file, 'index.html');
    response.setHeader('Content-Type', file.endsWith('.js') ? 'text/javascript' : 'text/html'); response.end(await readFile(file));
  } catch { response.writeHead(404); response.end(); }
});
await new Promise(done => server.listen(0, '127.0.0.1', done));
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage(); page.setDefaultTimeout(10000);
  const requests = []; const errors = []; page.on('request', request => requests.push(new URL(request.url()).pathname)); page.on('pageerror', error => errors.push(error.message));
  await page.goto('http://127.0.0.1:' + server.address().port + '/'); await page.getByRole('heading', { name: '已登录外壳' }).waitFor();
  const initialRequests = [...requests]; assert.ok(!initialRequests.includes('/' + passwordEntry.file), '首屏不得请求密码表单');
  await page.getByRole('button', { name: '进入登录页' }).click(); await page.getByLabel(/密码/).waitFor();
  assert.ok(requests.includes('/' + passwordEntry.file), '认证页应真正加载密码表单'); assert.deepEqual(errors, []);
  const report = { source: 'pnpm pack + 独立消费者安装构建 + Chromium', passwordChunk: passwordEntry.file, initialRequests, afterLoginRequests: requests, errors, result: 'passed' };
  await writeFile(join(root, 'report.json'), JSON.stringify(report, null, 2)); console.log(JSON.stringify(report, null, 2));
} finally { await browser.close(); await new Promise(done => server.close(done)); }
`);
run(['install', '--offline', '--ignore-scripts'], output);
process.stdout.write(run(['exec', 'node', 'verify.mjs'], output));
process.stdout.write(`\n包消费验证通过，证据保留于 ${join(output, 'report.json')}\n`);
