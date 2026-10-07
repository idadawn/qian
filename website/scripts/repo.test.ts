import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { listFiles, rewriteLinks, splitFrontmatter, withInlineCodeMasked, writeGenerated } from './repo.ts';

const toUpper = (href: string) => href.toUpperCase();

describe('rewriteLinks', () => {
  it('改写正文里的链接与图片，保留标题', () => {
    assert.equal(rewriteLinks('见 [a](x.md) 与 ![图](y.png "说明")', toUpper), '见 [a](X.MD) 与 ![图](Y.PNG "说明")');
  });

  it('跳过代码块与行内代码', () => {
    const src = ['```md', '[a](x.md)', '```', '`[b](y.md)` 与 [c](z.md)', '~~~', '[d](w.md)', '~~~'].join('\n');
    const out = rewriteLinks(src, toUpper);
    assert.equal(out, ['```md', '[a](x.md)', '```', '`[b](y.md)` 与 [c](Z.MD)', '~~~', '[d](w.md)', '~~~'].join('\n'));
  });

  it('链接文字里带行内代码时仍是一个链接', () => {
    assert.equal(rewriteLinks('格式以 [`yue.md`](yue.md) 为准', toUpper), '格式以 [`yue.md`](YUE.MD) 为准');
  });

  it('resolver 返回 null 时保持原样', () => {
    assert.equal(rewriteLinks('[a](https://example.com)', () => null), '[a](https://example.com)');
  });

  it('长围栏里出现短围栏不算结束', () => {
    const src = ['````md', '```', '[a](x.md)', '```', '````', '[b](y.md)'].join('\n');
    assert.equal(rewriteLinks(src, toUpper).split('\n').at(-1), '[b](Y.MD)');
    assert.ok(rewriteLinks(src, toUpper).includes('[a](x.md)'));
  });
});

describe('withInlineCodeMasked', () => {
  it('没有配对的反引号按普通文字处理', () => {
    assert.equal(withInlineCodeMasked('a ` b `c` d', (s) => s.replace(/d/g, 'D')), 'a ` b `c` D');
  });

  it('双反引号包住单反引号', () => {
    assert.equal(withInlineCodeMasked('x ``a`b`` y', (s) => s.replace(/[aby]/g, '_')), 'x ``a`b`` _');
  });
});

describe('splitFrontmatter', () => {
  it('拆出 frontmatter', () => {
    assert.deepEqual(splitFrontmatter('---\ntitle: x\n---\n# 标题\n'), { frontmatter: 'title: x', body: '# 标题\n' });
  });

  it('没有 frontmatter 时原样返回', () => {
    assert.deepEqual(splitFrontmatter('# 标题\n'), { frontmatter: '', body: '# 标题\n' });
  });
});

describe('writeGenerated', () => {
  const tmp = () => fs.mkdtempSync(path.join(os.tmpdir(), 'qian-docs-'));

  it('只写变化的文件，删除多余的旧文件与空目录', () => {
    const root = tmp();
    const first = writeGenerated('cli', [{ path: 'a.md', content: '1' }, { path: 'x/b.md', content: '2' }], root);
    assert.deepEqual(first.written.sort(), ['a.md', 'x/b.md']);

    const second = writeGenerated('cli', [{ path: 'a.md', content: '1' }, { path: 'x/b.md', content: '2' }], root);
    assert.deepEqual(second, { written: [], removed: [], unchanged: 2 });

    const third = writeGenerated('cli', [{ path: 'a.md', content: '1!' }], root);
    assert.deepEqual(third.written, ['a.md']);
    assert.deepEqual(third.removed, ['x/b.md']);
    assert.deepEqual(listFiles(path.join(root, 'cli')), ['a.md']);
    assert.equal(fs.existsSync(path.join(root, 'cli', 'x')), false);
  });

  it('拒绝越界与重复的路径', () => {
    const root = tmp();
    assert.throws(() => writeGenerated('cli', [{ path: '../a.md', content: '' }], root), /越界/);
    assert.throws(() => writeGenerated('cli', [{ path: 'a.md', content: '' }, { path: 'a.md', content: '' }], root), /重复/);
  });
});
