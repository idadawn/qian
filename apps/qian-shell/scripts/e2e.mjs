import { spawn } from 'node:child_process';
import { existsSync, mkdtempSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { resolve } from 'node:path';
const root = new URL('../../..', import.meta.url);
const run = (command, args, env = process.env) => new Promise((resolve, reject) => {
  const child = spawn(command, args, { cwd: root, stdio: 'inherit', env });
  child.once('error', reject); child.once('exit', (code, signal) => code === 0 ? resolve() : reject(new Error(`${command} 退出：${code ?? signal}`)));
});
let host, temporary;
try {
  let state = process.env.QIAN_MF_AUDIT_STATE;
  if (!state) {
    await run('dotnet', ['build', 'tests/Qian.Shell.TestHost/Qian.Shell.TestHost.csproj', '-c', 'Release', '--nologo']);
    temporary = mkdtempSync(resolve(tmpdir(), 'qian-mf-audit-')); state = resolve(temporary, 'state.json');
    host = spawn('dotnet', ['tests/Qian.Shell.TestHost/bin/Release/net10.0/Qian.Shell.TestHost.dll', '--state', state], { cwd: root, stdio: ['ignore', 'inherit', 'inherit'], env: process.env });
    const deadline = Date.now() + 60_000;
    while (!existsSync(state)) {
      if (host.exitCode !== null) throw new Error('真实审计验收宿主提前退出');
      if (Date.now() > deadline) throw new Error('真实审计验收宿主未及时就绪');
      await new Promise((resolve) => setTimeout(resolve, 200));
    }
  }
  const env = { ...process.env, QIAN_MF_AUDIT_STATE: state };
  await run('node', ['apps/qian-shell/scripts/audit-contract.mjs'], env);
  for (const args of [['--filter', '@idadawn/qian-runtime', 'build'], ['--filter', '@qian/tenancy-ui', 'build:remote'], ['--filter', 'qian-shell', 'exec', 'vite', 'build', '--mode', 'acceptance', '--outDir', 'dist/acceptance'], ['--filter', 'qian-shell', 'exec', 'playwright', 'test']]) await run('pnpm', args, env);
} catch (error) { console.error(error.message); process.exitCode = 1; }
finally {
  if (host && host.exitCode === null) { host.kill('SIGTERM'); await new Promise((resolve) => host.once('exit', resolve)); }
  if (temporary) rmSync(temporary, { recursive: true, force: true });
}
