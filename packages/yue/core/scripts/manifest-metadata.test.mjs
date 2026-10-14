import assert from 'node:assert/strict';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { after, test } from 'node:test';
import ts from 'typescript';
import { updatePropsMetadata } from './manifest-metadata.mjs';

const directory = mkdtempSync(path.join(tmpdir(), 'yue-metadata-'));
after(() => rmSync(directory, { recursive: true, force: true }));
const file = path.join(directory, 'fixture.ts');
writeFileSync(file, `
export interface Fixture {
  nested?: {
    /** 嵌套值的真实注释。 */
    value: string;
    raw?: string;
  } | null;
  rows: ReadonlyArray<{
    /** 数组成员的键。 */
    key: string;
  }>;
  pair: readonly [{
    /** 元组第一项。 */
    first: string;
  }, {
    /** 元组第二项。 */
    second: number;
  }];
  choice: {
    kind: 'text';
    /** 文本分支的值。 */
    value: string;
  } | {
    kind: 'number';
    /** 数字分支的值。 */
    value: number;
    onlyNumber?: string;
  } | null;
  combined: {
    /** 交叉类型左侧。 */
    left: string;
  } & {
    /** 交叉类型右侧。 */
    right: number;
  };
}
`);
const program = ts.createProgram([file], { strict: true, target: ts.ScriptTarget.ESNext });
const checker = program.getTypeChecker();
const source = program.getSourceFile(file);
const symbol = checker.getExportsOfModule(checker.getSymbolAtLocation(source))[0];
const type = checker.getDeclaredTypeOfSymbol(symbol);
const annotate = properties => updatePropsMetadata({ type: 'object', properties }, type, checker, 'Fixture').properties;
const stale = () => ({ type: 'string', title: 'wrong', description: 'SomeComponent 的 someProp 属性。' });

test('可选对象与 ReadonlyArray 的嵌套属性来自真实符号，无注释和结构节点不保留伪元数据', () => {
  const result = annotate({
    nested: { type: 'object', properties: { value: stale(), raw: stale() } },
    rows: { type: 'array', items: { title: 'wrong', description: 'Button 的 id 属性。', properties: { key: stale() } } },
  });
  assert.equal(result.nested.properties.value.title, 'value');
  assert.equal(result.nested.properties.value.description, '嵌套值的真实注释。');
  assert.ok(!('description' in result.nested.properties.raw));
  assert.equal(result.rows.items.properties.key.description, '数组成员的键。');
  assert.ok(!('title' in result.rows.items));
  assert.ok(!('description' in result.rows.items));
  assert.doesNotMatch(JSON.stringify(result), / 的 .+ 属性。/);
});

for (const keyword of ['anyOf', 'oneOf']) {
  test(`${keyword} 按鉴别字段选择对应联合分支的注释`, () => {
    const result = annotate({ choice: { [keyword]: [
      { properties: { kind: { const: 'text' }, value: stale() } },
      { properties: { kind: { const: 'number' }, value: { type: 'number' } } },
      { type: 'null' },
    ] } });
    assert.equal(result.choice[keyword][0].properties.value.description, '文本分支的值。');
    assert.equal(result.choice[keyword][1].properties.value.description, '数字分支的值。');
    assert.equal(result.choice[keyword][0].properties.kind.title, 'kind');
  });
}

test('allOf 的每一分支都沿交叉类型属性取注释', () => {
  const result = annotate({ combined: { allOf: [
    { properties: { left: stale() } }, { properties: { right: { type: 'number' } } },
  ] } });
  assert.equal(result.combined.allOf[0].properties.left.description, '交叉类型左侧。');
  assert.equal(result.combined.allOf[1].properties.right.description, '交叉类型右侧。');
});

for (const keyword of ['items', 'prefixItems']) {
  test(`${keyword} 元组逐位置取类型，不把另一位置的属性混入`, () => {
    const result = annotate({ pair: { type: 'array', [keyword]: [
      { properties: { first: stale() } }, { properties: { second: { type: 'number' } } },
    ] } });
    assert.equal(result.pair[keyword][0].properties.first.description, '元组第一项。');
    assert.equal(result.pair[keyword][1].properties.second.description, '元组第二项。');
    assert.throws(() => annotate({ pair: { [keyword]: [{ properties: { second: stale() } }] } }), /Fixture\.pair\[0\]\.second/);
  });
}

test('不存在的属性在顶层、数组、对象与组合分支都带路径报错', () => {
  assert.throws(() => annotate({ missing: stale() }), /Fixture\.missing/);
  assert.throws(() => annotate({ nested: { properties: { missing: stale() } } }), /Fixture\.nested\.missing/);
  assert.throws(() => annotate({ rows: { items: { properties: { missing: stale() } } } }), /Fixture\.rows\[\]\.missing/);
  for (const keyword of ['anyOf', 'oneOf', 'allOf']) {
    assert.throws(() => annotate({ choice: { [keyword]: [{ properties: { kind: { const: 'text' }, onlyNumber: stale() } }] } }), new RegExp(`Fixture\\.choice\\.${keyword}\\[0\\]\\.onlyNumber`));
  }
});
