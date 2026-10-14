import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { DEMOS } from '../docs/examples/catalog';
import { CORE_COMPONENT_MANIFESTS } from './manifests';

describe('全部组件 SSR', () => {
  it('组件目录与实际可渲染示例一一对应', () => {
    expect(Object.keys(DEMOS).sort()).toEqual(CORE_COMPONENT_MANIFESTS.map(item => item.name).sort());
  });
  for (const [name, Demo] of Object.entries(DEMOS)) {
    it(`${name} 在无 DOM 的 Node 中渲染`, () => {
      const html = renderToString(<Demo />);
      expect(html).toContain('data-slot=');
      expect(html).toContain('data-testid=');
    });
  }
});
