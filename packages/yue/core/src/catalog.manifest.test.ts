import { spawnSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import scenarios from '../docs/scenarios.json';
import docs from '../docs/catalog.json';
import { Ajv2020 } from 'ajv/dist/2020.js';
import { describe, expect, it } from 'vitest';
import { componentManifestSchema } from '@idadawn/yue-contracts';
import { TOKEN_REGISTRY, primitiveColors } from '@idadawn/yue-tokens';
import { CORE_COMPONENT_MANIFESTS } from './manifests';

const ajv = new Ajv2020({ allErrors: true, strict: false });
// 子进程失败时把标准输出与标准错误并入断言信息：node --test 的失败详情写在标准输出里，只看退出码无从排查。
function runNode(args: string[]) {
  const result = spawnSync(process.execPath, args, { encoding: 'utf8' });
  expect(result.error).toBeUndefined();
  expect(result.status, `node ${args.join(' ')} 退出码 ${result.status}\n--- stdout ---\n${result.stdout}\n--- stderr ---\n${result.stderr}`).toBe(0);
}
const script = (name: string) => fileURLToPath(new URL(`../scripts/${name}`, import.meta.url));
const validate = ajv.compile(componentManifestSchema);
function assertMetadata(node: unknown, name?: string) {
  if (!node || typeof node !== 'object') return;
  const schema = node as Record<string, unknown>;
  if (name) expect(schema).toHaveProperty('title', name);
  else {
    expect(schema).not.toHaveProperty('title');
    expect(schema).not.toHaveProperty('description');
  }
  if (typeof schema.description === 'string') expect(schema.description).not.toMatch(/^.+ 的 .+ 属性。$/);
  for (const [key, child] of Object.entries((schema.properties ?? {}) as Record<string, unknown>)) assertMetadata(child, key);
  if (Array.isArray(schema.items)) schema.items.forEach(child => assertMetadata(child));
  else assertMetadata(schema.items);
  for (const keyword of ['prefixItems', 'anyOf', 'oneOf', 'allOf']) {
    if (Array.isArray(schema[keyword])) schema[keyword].forEach(child => assertMetadata(child));
  }
}
describe('组件机器目录', () => {
  it('生成器覆盖联合、交叉、元组与各层不存在属性的反例', () => {
    runNode(['--test', script('manifest-metadata.test.mjs')]);
  }, 30_000);
  it('元数据由当前 TypeScript 注释生成且没有漂移', () => {
    runNode([script('export-manifests.mjs'), '--check']);
    const button = CORE_COMPONENT_MANIFESTS.find(item => item.name === 'Button')!;
    expect(button.props).toHaveProperty('properties.loading.description', '加载中时禁用操作并显示忙碌状态。');
    expect(button.props).not.toHaveProperty('properties.id.description');
    const toast = CORE_COMPONENT_MANIFESTS.find(item => item.name === 'Toast')!;
    expect(toast.props).toHaveProperty('properties.toast.properties.id.description', 'The unique identifier for the toast.');
    const breadcrumb = CORE_COMPONENT_MANIFESTS.find(item => item.name === 'Breadcrumb')!;
    expect(breadcrumb.props).toHaveProperty('properties.items.items.properties.href.title', 'href');
  }, 30_000);
  it('全部清单符合统一 Schema，名称唯一，示例通过自己的 props Schema，令牌均登记', () => {
    const names = CORE_COMPONENT_MANIFESTS.map(item => item.name);
    expect(new Set(names).size).toBe(names.length);
    expect(names).toHaveLength(51);
    const tokens = new Set([...primitiveColors().keys(), ...[...TOKEN_REGISTRY.primitives, ...TOKEN_REGISTRY.semantics, ...TOKEN_REGISTRY.components].map(item => item.name)]);
    for (const manifest of CORE_COMPONENT_MANIFESTS) {
      expect(validate(manifest), JSON.stringify(validate.errors)).toBe(true);
      expect(ajv.validateSchema(manifest.props)).toBe(true);
      assertMetadata(manifest.props);
      const props = ajv.compile(manifest.props);
      for (const example of manifest.examples) expect(props(example.props), `${manifest.name}: ${JSON.stringify(props.errors)}`).toBe(true);
      for (const token of manifest.tokens) expect(tokens.has(token), token).toBe(true);
      expect(docs).toHaveProperty(manifest.name);
      expect(scenarios.some(s => s.id === `${manifest.name}/Default`)).toBe(true);
      expect(scenarios.some(s => s.id === `${manifest.name}/Dark`)).toBe(true);
    }
  });
  it('畸形清单、未知组件属性与无效变体被拒绝', () => {
    expect(validate({ ...CORE_COMPONENT_MANIFESTS[0], slots: [] })).toBe(false);
    const button = CORE_COMPONENT_MANIFESTS.find(item => item.name === 'Button')!;
    const props = ajv.compile(button.props);
    expect(props({ variant: 'invented' })).toBe(false);
    expect(props({ dangerouslySetInnerHTML: { __html: '<script />' } })).toBe(false);
    const icon = CORE_COMPONENT_MANIFESTS.find(item => item.name === 'Icon')!;
    expect(ajv.compile(icon.props)({ name: 'unregistered' })).toBe(false);
  });
});
