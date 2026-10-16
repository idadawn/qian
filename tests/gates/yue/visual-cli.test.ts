import { spawnSync } from 'node:child_process';
import path from 'node:path';
import { expect, it } from 'vitest';

it.each([
  ['--update-snapshots'], ['-u'], ['--update-snapshots=changed'], ['--update-snapshots=missing'],
  ['--update-snapshots=all', '--update-snapshots=changed'],
])('拒绝不完整或重复的更新模式：%j', (...args) => {
  const result = spawnSync(process.execPath, [path.resolve(import.meta.dirname, '../../../scripts/yue-visual.mjs'), ...args], { encoding: 'utf8' });
  expect(result.status).not.toBe(0);
  expect(result.stderr).toContain('刷新基线须显式使用 --update-snapshots=all');
});
