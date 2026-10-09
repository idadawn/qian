import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { sha256, type KbIndex, type KbPage } from './build-kb-index.ts';
import { SITE_SIZE_BUDGET_MB, checkMachineReadable, checkMarkdownOutput, checkSizeBudget, countFences, stripCode } from './check-site.ts';

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

describe('Markdown 产物质量（S1-DOCS-003）', () => {
  it('干净的 Markdown 通过；泛型、行内代码与围栏里的内容不算残留', () => {
    const md = '# 标题\n\n`Result<T>` 与 Result\\<T> 都不是组件，`<Badge text="x" />` 在行内代码里也不是。\n\n```xml\n<Project Sdk="Microsoft.NET.Sdk">\n```\n\n```ts\nimport { a } from \'b\';\n```\n\n- 列表\n';
    assert.deepEqual(checkMarkdownOutput('a.md', md, 4), []);
    assert.deepEqual(checkMarkdownOutput('a.md', md, 2), [], '产物的代码围栏可以比源页多（DocFX 的 HTML 代码块）');
    assert.equal(stripCode('a `x` b\n```\ncode\n```\nc'), 'a ` b\nc');
    assert.equal(countFences(md), 4);
    assert.equal(countFences('````md\n```ts\nx\n```\n````\n'), 4);
  });

  it('ESM、组件、JSX 注释、className、空白段落与丢失的代码块都报出来', () => {
    const cases: Array<[string, string]> = [
      ["import { Card } from '@theme';\n\n# x\n", 'ESM 语句残留'],
      ['# x\n\n每租户能力门面 <Badge text="S4" type="warning" />\n', '组件残留 <Badge text='],
      ['# x\n\n<LatestChanges />\n', '组件残留'],
      ['# x\n\n{/* 注释 */}\n', 'JSX 残留'],
      ['# x\n\n<div className="qian-home">\n', 'JSX 残留'],
      ['# x\n\n- \n\n正文\n', '空标题或空列表项「-」'],
      ['# x\n\n## \n\n正文\n', '空标题或空列表项「##」'],
    ];
    for (const [md, message] of cases) {
      const errors = checkMarkdownOutput('p.md', md);
      assert.ok(errors.some((e) => e.includes(message)), `${message}: ${errors.join(' | ')}`);
    }
    const lost = checkMarkdownOutput('p.md', '# x\n\n正文\n', 2);
    assert.ok(lost.some((e) => e.includes('代码围栏 0 行，少于源页的 2 行')), lost.join(' | '));
  });
});

function page(route: string, md: string, extra: Partial<KbPage> = {}): KbPage {
  const base = '/';
  const rel = route === '/' ? 'index' : route.endsWith('/') ? `${route.slice(1)}index` : route.slice(1);
  return {
    route,
    url: `${base}${rel}.html`,
    md: `${base}${rel}.md`,
    sourceFile: `${rel}.md`,
    title: rel,
    description: '',
    project: 'qian',
    part: 'start',
    audience: ['developer'],
    updated: '2026-10-09',
    sha256: sha256(md),
    bytes: Buffer.byteLength(md),
    frontmatter: {},
    ...extra,
  };
}

describe('机器可读产物与源文档同步（docs-site.md §4a、§7）', () => {
  const homeMd = '# 首页\n';
  const hostsMd = '# 宿主\n\n```bash\nqian add files\n```\n';
  const index: KbIndex = {
    version: 1,
    generatedAt: '',
    base: '/',
    siteUrl: 'http://docs.example',
    kunVersion: '2026.10.8',
    pages: [page('/', homeMd), page('/start/hosts', hostsMd)],
  };
  const files: Record<string, string> = {
    'index.html': '',
    'index.md': homeMd,
    'start/hosts.html': '',
    'start/hosts.md': hostsMd,
    'kb-index.json': JSON.stringify(index),
    'llms.txt': '# Qian 文档\n\n## 开始\n\n- [宿主](/start/hosts.md): 一句话\n',
    'llms-full.txt': '---\nurl: /index.md\n---\n# 首页\n\n---\nurl: /start/hosts.md\n---\n# 宿主\n',
    'sitemap.xml': '<urlset><url><loc>http://docs.example/</loc></url><url><loc>http://docs.example/start/hosts</loc></url></urlset>',
  };
  const sources: Record<string, string> = { 'index.md': '# 首页\n', 'start/hosts.md': '# 宿主\n\n```bash\nqian add files\n```\n' };
  const run = (overrides: Record<string, string | null> = {}) => {
    const all = { ...files, ...overrides } as Record<string, string | null>;
    const names = Object.keys(all).filter((k) => all[k] !== null);
    return checkMachineReadable(names, (rel) => all[rel] ?? '', (sourceFile) => sources[sourceFile] ?? null);
  };

  it('齐全且一致时通过', () => {
    assert.deepEqual(run(), []);
  });

  it('缺文件、索引过期、llms 漏页、sitemap 漏页、多余 .md 各报一条', () => {
    assert.ok(run({ 'llms-full.txt': null }).some((e) => e.includes('缺少 llms-full.txt')));
    assert.ok(run({ 'kb-index.json': null }).some((e) => e.includes('缺少 kb-index.json')));
    assert.ok(run({ 'kb-index.json': '{' }).some((e) => e.includes('不是合法的 JSON')));
    assert.ok(run({ 'start/hosts.md': '# 改过的宿主\n' }).some((e) => e.includes('sha256 不一致')));
    assert.ok(run({ 'start/hosts.html': null }).some((e) => e.includes('缺少 start/hosts.html')));
    assert.ok(run({ 'llms.txt': '# Qian 文档\n' }).some((e) => e.includes('llms.txt 没有列出 /start/hosts.md')));
    assert.ok(run({ 'llms-full.txt': '---\nurl: /index.md\n---\n' }).some((e) => e.includes('llms-full.txt 没有收录 /start/hosts.md')));
    assert.ok(run({ 'sitemap.xml': '<urlset></urlset>' }).some((e) => e.includes('sitemap.xml 没有 /start/hosts')));
    assert.ok(run({ 'extra.md': '# 多余\n' }).some((e) => e.includes('extra.md 不在 kb-index.json 里')));
    const stale = { ...index, version: 2 };
    assert.ok(run({ 'kb-index.json': JSON.stringify(stale) }).some((e) => e.includes('version 是 2')));
  });

  it('每页 .md 的质量问题带路径报出', () => {
    const broken = '# 宿主\n\n<Badge text="S4" type="warning" />\n';
    const stale = { ...index, pages: [page('/', homeMd), page('/start/hosts', broken)] };
    const errors = run({ 'start/hosts.md': broken, 'kb-index.json': JSON.stringify(stale) });
    assert.ok(errors.some((e) => e.startsWith('start/hosts.md：') && e.includes('组件残留')), errors.join(' | '));
    assert.ok(errors.some((e) => e.includes('代码围栏 0 行，少于源页的 2 行')), errors.join(' | '));
  });
});
