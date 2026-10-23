import { execFileSync } from 'node:child_process';
import { readFileSync, writeFileSync, rmSync } from 'node:fs';
const packageRoot = new URL('../', import.meta.url);
const consumer = new URL('dist/remote/consumer.ts', packageRoot), config = new URL('dist/remote/tsconfig.consumer.json', packageRoot);
const source = readFileSync(new URL('test/remote-types.ts.txt', packageRoot), 'utf8').replaceAll('../dist/remote/@mf-types/TenantPage', './@mf-types/TenantPage');
try {
  writeFileSync(consumer, source);
  writeFileSync(config, JSON.stringify({ extends: '../../tsconfig.json', compilerOptions: { skipLibCheck: false }, include: ['consumer.ts'] }));
  execFileSync('pnpm', ['exec', 'tsc', '-p', config.pathname], { cwd: packageRoot, stdio: 'inherit' });
} finally { rmSync(consumer, { force: true }); rmSync(config, { force: true }); }
