import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { REPO_URL } from './repo.ts';
import {
  CLI_PAGES,
  designPagePath,
  designRoute,
  listDocsSources,
  makeDocsResolver,
  promoteHeadings,
  sidebarLabel,
  splitSections,
  syncCli,
  syncDesign,
} from './sync-specs.ts';

/** 正文（代码块外）里还剩下的相对链接：生成页只应有站内绝对路由、锚点与外部地址。 */
function relativeLinks(markdown: string): string[] {
  const noFence = markdown.replace(/^(`{3,}|~{3,})[^\n]*\n[\s\S]*?^\1\s*$/gm, '');
  const noCode = noFence.replace(/`[^`\n]*`/g, '');
  return [...noCode.matchAll(/\]\(([^)\s]+)/g)]
    .map((m) => m[1] ?? '')
    .filter((href) => !/^(?:[a-z][a-z0-9+.-]*:|\/|#)/i.test(href));
}

describe('路径与路由', () => {
  it('小写短横线，README 变 index', () => {
    assert.equal(designPagePath('docs/adr/ADR-015-model-gateway.md'), 'adr/adr-015-model-gateway.md');
    assert.equal(designPagePath('docs/README.md'), 'index.md');
    assert.equal(designPagePath('docs/prd/specs/yue.md'), 'prd/specs/yue.md');
    assert.equal(designRoute('docs/README.md'), '/design/');
    assert.equal(designRoute('docs/adr/README.md'), '/design/adr/');
    assert.equal(designRoute('docs/ROADMAP.md'), '/design/roadmap');
  });

  it('不是 docs/ 下的 Markdown 时报错', () => {
    assert.throws(() => designPagePath('pm/README.md'));
  });
});

describe('makeDocsResolver', () => {
  const repo = fs.mkdtempSync(path.join(os.tmpdir(), 'qian-repo-'));
  for (const [file, text] of [
    ['docs/a/README.md', '# A'],
    ['docs/a/B.md', '# B'],
    ['docs/c.md', '# C'],
    ['docs/d/e.md', '# E'],
    ['pm/x.md', '# X'],
  ] as const) {
    fs.mkdirSync(path.dirname(path.join(repo, file)), { recursive: true });
    fs.writeFileSync(path.join(repo, file), text);
  }
  const resolve = makeDocsResolver('docs/a/B.md', repo);

  it('docs/ 内的文件与带 README 的目录 → 站内路由，保留锚点', () => {
    assert.equal(resolve('README.md#x'), '/design/a/#x');
    assert.equal(resolve('../c.md'), '/design/c');
    assert.equal(resolve('./'), '/design/a/');
  });

  it('docs/ 之外与没有 README 的目录 → GitHub 地址', () => {
    assert.equal(resolve('../../pm/x.md'), `${REPO_URL}/blob/main/pm/x.md`);
    assert.equal(resolve('../d/'), `${REPO_URL}/tree/main/docs/d`);
  });

  it('外部地址与纯锚点不改', () => {
    assert.equal(resolve('https://example.com/a.md'), null);
    assert.equal(resolve('#section'), null);
    assert.equal(resolve('/abs'), null);
  });

  it('目标不存在或越出仓库时报错', () => {
    assert.throws(() => resolve('missing.md'), /不存在/);
    assert.throws(() => resolve('../../../outside.md'), /仓库之外/);
  });
});

describe('splitSections 与 promoteHeadings', () => {
  const spec = ['前言', '## 1. 甲', '正文', '```md', '## 不是标题', '```', '### 1.1 子节', '## 2. 乙', '尾'].join('\n');

  it('按二级标题切分，代码块里的 ## 不算', () => {
    const sections = splitSections(spec);
    assert.deepEqual(sections.map((s) => [s.number, s.heading]), [[1, '1. 甲'], [2, '2. 乙']]);
    assert.ok(sections[0]?.body.includes('## 不是标题'));
  });

  it('标题升一级，代码块内不动', () => {
    assert.equal(promoteHeadings(['### a', '```', '### b', '```', '#### c'].join('\n')), ['## a', '```', '### b', '```', '### c'].join('\n'));
  });
});

describe('sidebarLabel', () => {
  it('ADR 取编号与冒号前的短标题，其余取一级标题', () => {
    assert.equal(sidebarLabel('docs/adr/ADR-015-model-gateway.md', '---\ntitle: 模型服务网关：并入现有设施\n---\n# ADR-015\n'), 'ADR-015 模型服务网关');
    assert.equal(sidebarLabel('docs/ROADMAP.md', '# 路线图\n'), '路线图');
  });
});

describe('对真实仓库生成', () => {
  it('design/：docs/ 每个文件都有页面，每个目录都有 _meta.json，正文不剩相对链接', () => {
    const files = syncDesign();
    const paths = new Set(files.map((f) => f.path));
    for (const source of listDocsSources()) {
      assert.ok(paths.has(designPagePath(source)), `缺少 ${source} 的镜像页`);
    }
    for (const dir of ['', 'adr/', 'prd/', 'prd/specs/', 'migration/']) {
      assert.ok(paths.has(`${dir}_meta.json`), `缺少 ${dir}_meta.json`);
    }
    for (const file of files.filter((f) => f.path.endsWith('.md'))) {
      assert.deepEqual(relativeLinks(file.content), [], file.path);
      assert.match(file.content, /本页由构建脚本镜像自仓库/, file.path);
    }
  });

  it('cli/：每个规格章节一页，正文不剩相对链接', () => {
    const files = syncCli();
    for (const page of CLI_PAGES) {
      const file = files.find((f) => f.path === `${page.slug}.md`);
      assert.ok(file, page.slug);
      assert.ok(file.content.length > 500, `${page.slug} 内容过短`);
      assert.deepEqual(relativeLinks(file.content), [], page.slug);
      assert.doesNotMatch(file.content, /^## \d+\.\s/m, `${page.slug} 里混进了下一章`);
    }
    assert.ok(files.some((f) => f.path === 'index.md'));
  });
});
