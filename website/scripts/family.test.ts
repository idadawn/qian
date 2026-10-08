import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { describe, it } from 'node:test';
import { REPO_ROOT, SITE_DOCS } from './repo.ts';

/** 取 Markdown 里第一张以「| 名字 |」开头的表的数据行，每行按单元格拆开并去掉链接、反引号、首尾空白。 */
function familyRows(markdown: string): string[][] {
  const lines = markdown.split('\n');
  const start = lines.findIndex((l) => /^\|\s*名字\s*\|/.test(l));
  assert.ok(start >= 0, '没有找到「产品族」表');
  const rows: string[][] = [];
  for (let i = start + 2; i < lines.length && lines[i]?.startsWith('|'); i++) {
    const cells = (lines[i] ?? '')
      .trim()
      .replace(/^\||\|$/g, '')
      .split(/(?<!\\)\|/)
      .map((c) => c.replace(/\[([^\]]*)\]\([^)]*\)/g, '$1').replace(/`/g, '').trim());
    rows.push(cells);
  }
  return rows;
}

describe('产品族页与仓库 README 的「产品族」表同一内容（docs-site.md §2b.4）', () => {
  it('名字、层、是什么、仓库、版本与许可五列逐行一致，文档位置一列各自指向站内与仓库', () => {
    const readme = familyRows(fs.readFileSync(path.join(REPO_ROOT, 'README.md'), 'utf8'));
    const family = familyRows(fs.readFileSync(path.join(SITE_DOCS, 'start', 'family.md'), 'utf8'));
    assert.equal(family.length, readme.length, '行数不同');
    for (let i = 0; i < readme.length; i++) {
      assert.deepEqual(family[i]?.slice(0, 5), readme[i]?.slice(0, 5), `第 ${i + 1} 行不一致`);
      assert.ok((family[i]?.[5] ?? '').length > 0, `第 ${i + 1} 行缺「文档在哪」`);
    }
  });
});
