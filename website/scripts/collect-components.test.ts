import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { PLATFORM_COMPONENTS, collectComponents } from './collect-components.ts';

describe('collectComponents（S1-DOCS-002 之前的占位目录）', () => {
  const files = collectComponents();
  const byPath = new Map(files.map((f) => [f.path, f.content]));

  it('每个规划中的组件一页，目录页列出全部组件', () => {
    const index = byPath.get('platform/index.md') ?? '';
    for (const c of PLATFORM_COMPONENTS) {
      assert.ok(byPath.has(`platform/${c.id}.md`), c.id);
      assert.ok(index.includes(`(/components/platform/${c.id})`), `目录页缺少 ${c.id}`);
    }
  });

  it('S2 及以后的组件在侧栏打阶段标记，S1 不打', () => {
    assert.match(byPath.get('platform/qian.workflow.md') ?? '', /^tag: S2$/m);
    assert.doesNotMatch(byPath.get('platform/qian.files.md') ?? '', /^tag:/m);
  });

  it('首页与其他部分链接到的组件页都存在', () => {
    for (const id of ['qian.gateway', 'qian.audit', 'qian.observability', 'qian.workflow', 'qian.identity', 'qian.authz', 'qian.files']) {
      assert.ok(byPath.has(`platform/${id}.md`), id);
    }
  });

  it('组件 id 重复时报错', () => {
    const first = PLATFORM_COMPONENTS[0];
    assert.ok(first);
    PLATFORM_COMPONENTS.push({ ...first });
    try {
      assert.throws(() => collectComponents(), /重复/);
    } finally {
      PLATFORM_COMPONENTS.pop();
    }
  });

  it('外壳页来自包目录且在索引、侧栏与三个共用示例中可发现', () => {
    const shell = byPath.get('yue/shell.mdx') ?? '';
    for (const name of ['AppShell', 'Authentication', 'FallbackPage']) assert.ok(shell.includes(`name="${name}"`));
    assert.ok(byPath.get('yue/index.md')?.includes('/components/yue/shell'));
    assert.ok(JSON.parse(byPath.get('yue/_meta.json') ?? '[]').includes('shell'));
  });
});
