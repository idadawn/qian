import { readFileSync } from 'node:fs';
import { Ajv2020 } from 'ajv/dist/2020.js';
import { describe, expect, it } from 'vitest';
import { componentManifestSchema } from '@idadawn/yue-contracts';
import { TOKEN_REGISTRY, primitiveColors } from '@idadawn/yue-tokens';
import { CORE_COMPONENT_MANIFESTS } from './manifests';

const ajv = new Ajv2020({ allErrors: true, strict: false });
const validate = ajv.compile(componentManifestSchema);
describe('组件机器目录', () => {
  it('全部清单符合统一 Schema，名称唯一，示例通过自己的 props Schema，令牌均登记', () => {
    const names = CORE_COMPONENT_MANIFESTS.map(item => item.name);
    expect(new Set(names).size).toBe(names.length);
    expect(names).toHaveLength(51);
    const tokens = new Set([...primitiveColors().keys(), ...[...TOKEN_REGISTRY.primitives, ...TOKEN_REGISTRY.semantics, ...TOKEN_REGISTRY.components].map(item => item.name)]);
    for (const manifest of CORE_COMPONENT_MANIFESTS) {
      expect(validate(manifest), JSON.stringify(validate.errors)).toBe(true);
      expect(ajv.validateSchema(manifest.props)).toBe(true);
      const props = ajv.compile(manifest.props);
      for (const example of manifest.examples) expect(props(example.props), `${manifest.name}: ${JSON.stringify(props.errors)}`).toBe(true);
      for (const token of manifest.tokens) expect(tokens.has(token), token).toBe(true);
      expect(readFileSync(new URL(`./storybook/${manifest.name}.stories.tsx`, import.meta.url), 'utf8')).toContain('meta.story(');
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
