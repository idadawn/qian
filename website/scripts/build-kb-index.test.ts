import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { KB_INDEX_VERSION, absoluteUrl, buildKbIndex, listPageSources, pageRoute, routeHtml, routeMd, sha256, sitemapXml } from './build-kb-index.ts';

function write(root: string, rel: string, content: string): void {
  const target = path.join(root, rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content);
}

const PAGE = (title: string, extra = '') =>
  `---\ntitle: ${title}\ndescription: 一句话。\npart: start\nproject: qian\naudience: [developer]\nupdated: 2026-10-09\n${extra}---\n\n# ${title}\n`;

describe('路由与产物路径', () => {
  it('index 文件对应目录路由，其余去掉扩展名', () => {
    assert.equal(pageRoute('index.mdx'), '/');
    assert.equal(pageRoute('start/index.md'), '/start/');
    assert.equal(pageRoute('start/hosts.md'), '/start/hosts');
    assert.equal(pageRoute('sdk/api/kun/kun-core-results/result-1.md'), '/sdk/api/kun/kun-core-results/result-1');
    assert.equal(routeHtml('/'), 'index.html');
    assert.equal(routeMd('/start/'), 'start/index.md');
    assert.equal(routeMd('/start/hosts'), 'start/hosts.md');
    assert.equal(absoluteUrl({ siteUrl: 'http://docs.example/', base: '/' }, '/'), 'http://docs.example/');
    assert.equal(absoluteUrl({ siteUrl: 'http://docs.example', base: '/pr-preview/pr-6/' }, '/start/hosts'), 'http://docs.example/pr-preview/pr-6/start/hosts');
  });
});

describe('知识库索引（docs-site.md §4a）', () => {
  it('从源页与 .md 产物拼出索引，并在缺页、缺字段、多余 .md 时报错', () => {
    const docs = fs.mkdtempSync(path.join(os.tmpdir(), 'qian-kb-docs-'));
    const out = fs.mkdtempSync(path.join(os.tmpdir(), 'qian-kb-out-'));
    write(docs, 'index.mdx', PAGE('首页', 'pageType: custom\n'));
    write(docs, 'start/index.md', PAGE('开始'));
    write(docs, 'start/hosts.md', PAGE('宿主', 'version: "1.2.3"\nsource: "idadawn/qian@main:docs/x.md"\n'));
    write(docs, 'start/_meta.json', '[]');
    write(docs, 'public/logo.svg', '<svg/>');
    write(docs, 'ops/missing-md.md', PAGE('没产物'));
    write(docs, 'ops/bad.md', '---\ntitle: 坏页\nnested:\n  key: value\n---\n');
    write(docs, 'ops/thin.md', '---\ntitle: 只有标题\n---\n# 只有标题\n');
    assert.deepEqual(listPageSources(docs), ['index.mdx', 'ops/bad.md', 'ops/missing-md.md', 'ops/thin.md', 'start/hosts.md', 'start/index.md']);
    for (const rel of ['index', 'start/index', 'start/hosts', 'ops/thin']) {
      write(out, `${rel}.md`, `# ${rel}\n`);
      write(out, `${rel}.html`, '<html lang="zh"></html>');
    }
    write(out, 'orphan.md', '# 没有来源\n');
    const { index, errors } = buildKbIndex({ siteDocs: docs, outDir: out, base: '/', siteUrl: 'http://docs.example', kunVersion: '2026.10.8', now: new Date('2026-10-09T00:00:00Z') });
    assert.equal(index.version, KB_INDEX_VERSION);
    assert.equal(index.generatedAt, '2026-10-09T00:00:00.000Z');
    assert.deepEqual(
      index.pages.map((p) => p.route),
      ['/', '/ops/thin', '/start/', '/start/hosts'],
    );
    const hosts = index.pages.find((p) => p.route === '/start/hosts');
    assert.ok(hosts);
    assert.equal(hosts.url, '/start/hosts.html');
    assert.equal(hosts.md, '/start/hosts.md');
    assert.equal(hosts.sourceFile, 'start/hosts.md');
    assert.equal(hosts.title, '宿主');
    assert.equal(hosts.project, 'qian');
    assert.deepEqual(hosts.audience, ['developer']);
    assert.equal(hosts.version, '1.2.3');
    assert.equal(hosts.source, 'idadawn/qian@main:docs/x.md');
    assert.equal(hosts.updated, '2026-10-09');
    assert.equal(hosts.sha256, sha256('# start/hosts\n'));
    assert.equal(hosts.bytes, Buffer.byteLength('# start/hosts\n'));
    assert.equal(hosts.frontmatter.pageType, undefined);
    const home = index.pages.find((p) => p.route === '/');
    assert.equal(home?.url, '/index.html');
    assert.equal(home?.frontmatter.pageType, 'custom');
    assert.equal(home?.version, undefined);
    assert.ok(errors.some((e) => e.includes('ops/missing-md.md') && e.includes('没有 ops/missing-md.md')), errors.join('\n'));
    assert.ok(errors.some((e) => e.includes('ops/bad.md') && e.includes('frontmatter')), errors.join('\n'));
    assert.ok(errors.some((e) => e.includes('ops/thin.md') && e.includes('缺少 description')), errors.join('\n'));
    assert.ok(errors.some((e) => e.includes('orphan.md')), errors.join('\n'));
    assert.equal(errors.length, 4 + 3, errors.join('\n')); // thin 缺 description、part、project、updated 四条 + 另三类各一条
  });

  it('sitemap.xml 用绝对地址，lastmod 取 updated，首页优先级最高', () => {
    const xml = sitemapXml({
      version: 1,
      generatedAt: '',
      base: '/pr-preview/pr-6/',
      siteUrl: 'http://docs.example',
      kunVersion: '2026.10.8',
      pages: [
        { route: '/', url: '/pr-preview/pr-6/index.html', md: '/pr-preview/pr-6/index.md', sourceFile: 'index.mdx', title: '首页', description: '', project: 'qian', part: 'start', audience: [], updated: '2026-10-09', sha256: '', bytes: 0, frontmatter: {} },
        { route: '/a&b/', url: '', md: '', sourceFile: 'a&b/index.md', title: '', description: '', project: 'qian', part: 'start', audience: [], updated: '2026-10-01', sha256: '', bytes: 0, frontmatter: {} },
      ],
    });
    assert.match(xml, /^<\?xml version="1\.0" encoding="UTF-8"\?>\n<urlset xmlns="http:\/\/www\.sitemaps\.org\/schemas\/sitemap\/0\.9">\n/);
    assert.ok(xml.includes('<url><loc>http://docs.example/pr-preview/pr-6/</loc><lastmod>2026-10-09</lastmod><priority>1.0</priority></url>'), xml);
    assert.ok(xml.includes('<url><loc>http://docs.example/pr-preview/pr-6/a&amp;b/</loc><lastmod>2026-10-01</lastmod><priority>0.5</priority></url>'), xml);
  });
});
