import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT } from './repo.ts';
import { describe, it } from 'node:test';
import { readYueDocs, collectYuePages } from './yue-docs.ts';
import { compactTables } from './compact-yue-markdown.ts';

describe('真实组件文档契约', () => {
  const data = readYueDocs();
  const pages = collectYuePages();
  it('51 个入口、全部公开组合部件与 126 条历史场景都可寻址', () => {
    assert.equal(data.docs.length, 51); assert.equal(data.scenarios.length, 126);
    assert.equal(fs.existsSync(path.join(REPO_ROOT, 'apps/yue-storybook')), false);
    assert.doesNotMatch(fs.readFileSync(path.join(REPO_ROOT, 'pnpm-lock.yaml'), 'utf8'), /@storybook\/|^\s+storybook@/m);
    for (const doc of data.docs) assert.ok(pages.some(p => p.path === `yue/core/${doc.name.toLowerCase()}.mdx`));
    for (const api of data.apis) assert.ok(pages.some(p => p.content.includes(`### ${api.name}\n`)), api.name);
  });
  it('解析真实属性、默认值、继承属性、泛型和必填回调', () => {
    const props = (name: string) => data.apis.find(a => a.name === name)!.props;
    for (const name of ['variant', 'size', 'loading', 'disabled']) assert.ok(props('Button').some(p => p.name === name));
    assert.equal(props('Button').find(p => p.name === 'variant')?.defaultValue, "'primary'");
    assert.match(props('Button').find(p => p.name === 'variant')!.type, /destructive/);
    assert.match(props('Dialog').find(p => p.name === 'onOpenChange')!.type, /boolean/);
    assert.equal(props('FullScreen').find(p => p.name === 'onError')!.required, true);
    assert.ok(props('Button').some(p => p.name === 'onClick' && p.native));
    assert.ok(props('Combobox').some(p => p.name === 'items'));
  });
  it('复用相同 DOM 属性而不删属性，HTML 与 Markdown 来源相同', () => {
    const button = pages.find(p => p.path === 'yue/core/button.mdx')!.content;
    assert.match(button, /disabled/); assert.match(button, /\/components\/yue\/api\/native-/);
    for (const name of ['onClick', 'aria-label', 'tabIndex']) assert.ok(pages.some(p => p.path.startsWith('yue/api/') && p.content.includes(`| ${name} |`)), name);
  });
});

it('压缩表格填充不改变类型、转义竖线与代码块', () => {
  const source = '| 属性     | 类型                |\n| a       | `"a" \\| "b"`       |\n```txt\n| keep   | padding   |\n```';
  assert.equal(compactTables(source), '| 属性 | 类型 |\n| a | `"a" \\| "b"` |\n```txt\n| keep   | padding   |\n```');
});
