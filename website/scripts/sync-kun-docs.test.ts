import assert from 'node:assert/strict';
import { after, before, describe, it } from 'node:test';
import fs from 'node:fs';
import { makeKunRepo, makeRepoRoot } from './fixtures.ts';
import { kunFileDate, openKun, readKunVersion, type KunCheckout } from './kun.ts';
import { splitFrontmatter } from './repo.ts';
import {
  escapeStrayTags,
  firstParagraph,
  kunLabel,
  kunPagePath,
  kunRoute,
  kunSlug,
  makeKunResolver,
  mirrorPage,
  padShortTableRows,
  plainText,
  syncKunDocs,
  tableCells,
  truncate,
} from './sync-kun-docs.ts';

const README = `<div align="center">

# Kun Framework

*一款为 .NET 10 打造的现代化云原生微服务开发框架*

</div>

## 📦 模块矩阵

完整矩阵请见 [module-map](docs/architecture/module-map.md)：

| 模块 | 职责 |
| :--- | :--- |
| \`Kun.Core\` | 领域抽象 |

## Quick start

不进概览。
`;

const ADR = `# ADR-003: Introduce \`Kun.Audit\` Module
**Status**: Accepted
**Date**: 2026-04-22

## Context

Handlers return Result<T> and \`IHandler<Result<T>>\` stays code; see [why](why-outbox.md#tl-dr),
[roadmap](../roadmap.md), [src](../../src/Kun.Core/) and [missing](../nope.md).

\`\`\`csharp
var r = Result<T>.Ok();
\`\`\`
`;

function fixture(): string {
  return makeKunRepo([
    {
      date: '2026-04-01',
      files: {
        'README.md': README,
        'docs/architecture/ADR-003-Kun.Audit-module.md': ADR,
        'docs/architecture/why-outbox.md': '# Why Outbox\n\nBecause dual writes.\n',
        'docs/architecture/adr-p2d-multitenancy.md': '# ADR: P2-D SaaS 多租户 3-策略架构\n\n**Status**: Accepted\n\n## Context\n\n共享库、独立 Schema、独立库三种隔离策略。\n',
        'docs/roadmap.md': '# Roadmap\n',
        'docs/AGENTS.md': '# 给代理\n',
        'docs/guides/AGENTS.md': '# 给代理\n',
        'src/Kun.Core/Kun.Core.csproj': '<Project />\n',
      },
    },
    {
      date: '2026-09-13',
      files: {
        'CHANGELOG.md': '# Changelog\n\nSee [schema](../docs/guides/table.md).\n',
        'docs/guides/table.md': '# 表格\n\n> 一段说明。\n\n| a | b | c |\n|---|---|---|\n| 1 | 2 |\n| 1 | 2 | 3 |\n',
        'docs/architecture/module-map.md': '# 模块地图\n\n> 版本说明。\n',
      },
    },
  ]);
}

describe('KunVersion 与 kun 仓库', () => {
  it('KunVersion 缺失即失败，不回退 main', () => {
    assert.throws(() => readKunVersion(makeRepoRoot('<PropertyGroup />')), /没有 <KunVersion>/);
    assert.throws(() => readKunVersion(makeRepoRoot('<PropertyGroup><KunVersion></KunVersion></PropertyGroup>')), /没有 <KunVersion>/);
    assert.throws(() => readKunVersion(makeRepoRoot('<PropertyGroup><KunVersion>main</KunVersion></PropertyGroup>')), /CalVer/);
    assert.equal(readKunVersion(makeRepoRoot('<PropertyGroup><KunVersion>2026.9.13</KunVersion></PropertyGroup>')), '2026.9.13');
  });

  it('tag 不存在即失败', () => {
    const dir = makeKunRepo([{ date: '2026-09-13', files: { 'README.md': README } }], 'v2026.6.12');
    assert.throws(() => openKun('2026.9.13', { KUN_DOCS_PATH: dir }), /没有 tag v2026\.9\.13/);
    assert.equal(openKun('2026.6.12', { KUN_DOCS_PATH: dir }).tag, 'v2026.6.12');
  });

  it('KUN_DOCS_PATH 不是 git 仓库时失败', () => {
    assert.throws(() => openKun('2026.9.13', { KUN_DOCS_PATH: makeRepoRoot('') }), /不是 git 仓库/);
  });
});

describe('白名单、slug 与路由', () => {
  it('按 §2b.2 的白名单映射，slug 小写并把 . 换成 -', () => {
    assert.equal(kunSlug('docs/architecture/ADR-003-Kun.Audit-module.md'), 'adr-003-kun-audit-module');
    assert.equal(kunPagePath('docs/architecture/ADR-003-Kun.Audit-module.md'), 'architecture/adr-003-kun-audit-module.md');
    assert.equal(kunPagePath('docs/observability/tracing-tour.md'), 'guides/tracing-tour.md');
    assert.equal(kunPagePath('docs/schema-per-service.md'), 'guides/schema-per-service.md');
    assert.equal(kunPagePath('docs/releases/v0.1.0-phase1.md'), 'releases/v0-1-0-phase1.md');
    assert.equal(kunPagePath('CHANGELOG.md'), 'releases/index.md');
    assert.equal(kunPagePath('README.md'), 'index.md');
    assert.equal(kunRoute('releases/index.md'), '/sdk/kun/releases/');
    assert.equal(kunRoute('index.md'), '/sdk/kun/');
    assert.equal(kunRoute('guides/tracing-tour.md'), '/sdk/kun/guides/tracing-tour');
  });

  it('Kun 的 ADR 文件名大小写都收（R58），侧栏名去掉冒号', () => {
    assert.equal(kunPagePath('docs/architecture/adr-p2d-multitenancy.md'), 'architecture/adr-p2d-multitenancy.md');
    assert.equal(kunPagePath('docs/architecture/ADR-S02-Kun.Sandbox-module.md'), 'architecture/adr-s02-kun-sandbox-module.md');
    assert.equal(kunLabel('ADR-003: Introduce Kun.Audit Module'), 'ADR-003 Introduce Kun.Audit Module');
    assert.equal(kunLabel('ADR: P2-D SaaS 多租户 3-策略架构'), 'ADR P2-D SaaS 多租户 3-策略架构');
    assert.equal(kunLabel('Why Outbox'), 'Why Outbox');
  });

  it('白名单外的文件不进站：路线图、差距分析、AOT 审计、各级 AGENTS.md', () => {
    for (const file of ['docs/roadmap.md', 'docs/neuroagent-phase1-gap.md', 'docs/AGENTS.md', 'docs/guides/AGENTS.md', 'docs/architecture/AGENTS.md', 'docs/architecture/AOT-audit-2026-04-23.md', 'src/Kun.Core/README.md']) {
      assert.equal(kunPagePath(file), null, file);
    }
  });
});

describe('正文处理', () => {
  it('正文里的非 HTML 尖括号转义，代码与 HTML 标签不动', () => {
    const md = 'Result<T> and <br> and <div align="center"> and <https://x.dev>\n\n`Result<T>` and `a\nList<T>`\n\n```cs\nResult<T>\n```\n';
    const out = escapeStrayTags(md);
    assert.match(out, /^Result&lt;T> and <br> and <div align="center"> and <https:\/\/x\.dev>/);
    assert.match(out, /`Result<T>` and `a\nList<T>`/);
    assert.match(out, /```cs\nResult<T>\n```/);
  });

  it('少列的表格行补空单元格并告警，多列的只告警', () => {
    const warnings: string[] = [];
    const out = padShortTableRows('| a | b | c |\n|---|---|---|\n| 1 | 2 |\n| 1 | 2 | 3 | 4 |\n', 'x.md', (m) => warnings.push(m));
    const rows = out.split('\n');
    assert.equal(tableCells(rows[2] ?? ''), 3);
    assert.equal(rows[3], '| 1 | 2 | 3 | 4 |');
    assert.equal(warnings.length, 2);
  });

  it('首段跳过 ADR 头部的元数据；纯文本保留泛型', () => {
    assert.equal(firstParagraph('**Status**: ok\n**Date**: x\n\n## Context\n\nReal text.\n'), 'Real text.');
    assert.equal(plainText('ADR-011: `Result<T>` composition'), 'ADR-011: Result<T> composition');
    assert.equal(plainText('Struct Result<T\\>'), 'Struct Result<T>');
    assert.equal(truncate('一'.repeat(130)).length, 120);
  });
});

describe('同步样例 kun 仓库', () => {
  let dir = '';
  let kun: KunCheckout;
  before(() => {
    dir = fixture();
    kun = openKun('2026.9.13', { KUN_DOCS_PATH: dir });
  });
  after(() => fs.rmSync(dir, { recursive: true, force: true }));

  it('相对链接：白名单内改站内路径，其余改 kun 仓库该 tag 的地址，越界的去掉多余的 ../', () => {
    const warnings: string[] = [];
    const files = new Set(['docs/architecture/why-outbox.md', 'docs/roadmap.md', 'src/Kun.Core/Kun.Core.csproj', 'docs/guides/table.md']);
    const resolve = makeKunResolver(kun, 'docs/architecture/ADR-003-Kun.Audit-module.md', files, (m) => warnings.push(m));
    assert.equal(resolve('why-outbox.md#tl-dr'), '/sdk/kun/architecture/why-outbox#tl-dr');
    assert.equal(resolve('../roadmap.md'), 'https://github.com/idadawn/kun/blob/v2026.9.13/docs/roadmap.md');
    assert.equal(resolve('../../src/Kun.Core/'), 'https://github.com/idadawn/kun/tree/v2026.9.13/src/Kun.Core');
    assert.equal(resolve('https://example.com'), null);
    assert.equal(resolve('#local'), null);
    assert.equal(makeKunResolver(kun, 'CHANGELOG.md', files, (m) => warnings.push(m))('../docs/guides/table.md'), '/sdk/kun/guides/table');
    assert.equal(resolve('../nope.md'), 'https://github.com/idadawn/kun/blob/v2026.9.13/docs/nope.md');
    assert.equal(warnings.length, 2, warnings.join('\n'));
  });

  it('白名单外的文件不能镜像', () => {
    assert.throws(() => mirrorPage(kun, 'docs/roadmap.md', new Set(['docs/roadmap.md']), () => {}), /不在 Kun 镜像白名单里/);
  });

  it('镜像页：横幅、frontmatter（project: kun、version、source、updated 取该文件的提交日期）', () => {
    const result = syncKunDocs(kun, 2);
    const byPath = new Map(result.files.map((f) => [f.path, f.content]));
    assert.deepEqual(
      [...byPath.keys()].sort(),
      [
        '_meta.json',
        'architecture/_meta.json',
        'architecture/adr-003-kun-audit-module.md',
        'architecture/adr-p2d-multitenancy.md',
        'architecture/why-outbox.md',
        'guides/_meta.json',
        'guides/table.md',
        'index.md',
        'modules.md',
        'releases/_meta.json',
        'releases/index.md',
      ],
    );
    const adr = byPath.get('architecture/adr-003-kun-audit-module.md') ?? '';
    const { frontmatter, body } = splitFrontmatter(adr);
    assert.match(frontmatter, /^title: "ADR-003: Introduce Kun\.Audit Module"$/m);
    assert.match(frontmatter, /^description: "Handlers return Result<T> and IHandler<Result<T>> stays code; see why, roadmap, src and missing\."$/m);
    assert.match(frontmatter, /^project: kun$/m);
    assert.match(frontmatter, /^part: sdk$/m);
    assert.match(frontmatter, /^version: "2026\.9\.13"$/m);
    assert.match(frontmatter, /^source: "idadawn\/kun@v2026\.9\.13:docs\/architecture\/ADR-003-Kun\.Audit-module\.md"$/m);
    assert.match(frontmatter, /^updated: 2026-04-01$/m);
    assert.match(body, /^# ADR-003: Introduce `Kun\.Audit` Module\n\n:::info 镜像页\n本页镜像自 kun 仓库/m);
    assert.match(body, /（`v2026\.9\.13`）/);
    assert.match(body, /\[与 Qian 的关系\]\(\/sdk\/kun\/with-qian\)/);
    assert.match(body, /Handlers return Result&lt;T>/);
    assert.match(body, /var r = Result<T>\.Ok\(\);/);
    assert.match(byPath.get('architecture/adr-p2d-multitenancy.md') ?? '', /^description: "共享库、独立 Schema、独立库三种隔离策略。"$/m);
    const architecture = JSON.parse(byPath.get('architecture/_meta.json') ?? '[]') as { name: string; label: string }[];
    assert.deepEqual(
      architecture.map((i) => [i.name, i.label]),
      [
        ['why-outbox', 'Why Outbox'],
        ['adr-003-kun-audit-module', 'ADR-003 Introduce Kun.Audit Module'],
        ['adr-p2d-multitenancy', 'ADR P2-D SaaS 多租户 3-策略架构'],
      ],
    );
    assert.match(byPath.get('guides/table.md') ?? '', /^updated: 2026-09-13$/m);
    assert.equal(kunFileDate(kun, 'docs/guides/table.md'), '2026-09-13');
    assert.equal(result.warnings.length, 3, result.warnings.join('\n'));
  });

  it('概览页拼装一句话定位、版本、模块矩阵与本节目录；侧栏含手写页与 API 参考', () => {
    const result = syncKunDocs(kun, 2);
    const overview = result.files.find((f) => f.path === 'index.md')?.content ?? '';
    assert.match(overview, /^description: "一款为 \.NET 10 打造的现代化云原生微服务开发框架"$/m);
    assert.match(overview, /\*\*v2026\.9\.13\*\*/);
    assert.match(overview, /## 模块矩阵\n\n完整矩阵请见 \[module-map\]\(\/sdk\/kun\/modules\)/);
    assert.doesNotMatch(overview, /不进概览/);
    assert.match(overview, /DocFX 从 2 个 `Kun\.\*` 包生成/);
    const meta = JSON.parse(result.files.find((f) => f.path === '_meta.json')?.content ?? '[]') as { name?: string; link?: string }[];
    assert.deepEqual(meta.slice(0, 2).map((i) => i.name), ['index', 'with-qian']);
    assert.equal(meta.at(-1)?.link, '/sdk/api/kun/');
  });
});
