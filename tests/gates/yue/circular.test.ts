import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

// 循环依赖门禁自测：真跑一遍 depcruise，样例 a ↔ b 必须被 no-circular 拦下；真实工作区必须通过。
const ROOT = path.resolve(__dirname, '../../..');
const BIN = path.join(ROOT, 'node_modules', '.bin', 'depcruise');

function depcruise(config: string, ...targets: string[]) {
  const r = spawnSync(BIN, ['--config', config, '--output-type', 'err', ...targets], { cwd: ROOT, encoding: 'utf8' });
  return { status: r.status, out: `${r.stdout}\n${r.stderr}` };
}

describe('循环依赖门禁（dependency-cruiser）', () => {
  it('样例 a ↔ b 被 no-circular 拦下', () => {
    const r = depcruise('tests/gates/yue/depcruise.fixture.cjs', 'tests/gates/yue/fixtures/cycle');
    expect(r.status).not.toBe(0);
    expect(r.out).toContain('no-circular');
  });

  it('真实的 packages/yue 与 apps 通过', () => {
    const r = depcruise('.dependency-cruiser.cjs', 'packages/yue', 'apps');
    expect(r.out).not.toContain('error');
    expect(r.status).toBe(0);
  });
});
