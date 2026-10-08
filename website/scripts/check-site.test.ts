import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { SITE_SIZE_BUDGET_MB, checkSizeBudget } from './check-site.ts';

describe('产物体积预算（R55）', () => {
  it('预算内不报错', () => {
    assert.deepEqual(checkSizeBudget([['index.html', 1_000_000], ['sdk/api/kun/a.html', 2_000_000]], 10), []);
    assert.ok(SITE_SIZE_BUDGET_MB >= 50, '预算挡结构性膨胀，不挡正常增长');
  });

  it('超出预算即失败，并按前两级目录报出最大的来源', () => {
    const sizes: [string, number][] = [
      ['index.html', 1_000_000],
      ['sdk/api/kun/kun-core/a.html', 6_000_000],
      ['sdk/api/kun/kun-core/b.html', 3_000_000],
      ['static/js/async/x.js', 2_000_000],
    ];
    const errors = checkSizeBudget(sizes, 10);
    assert.equal(errors.length, 1);
    assert.match(errors[0] ?? '', /产物 12\.0 MB 超过预算 10 MB/);
    assert.match(errors[0] ?? '', /最大的目录：sdk\/api\/ 9\.0 MB、static\/js\/ 2\.0 MB、\.\/ 1\.0 MB/);
  });
});
