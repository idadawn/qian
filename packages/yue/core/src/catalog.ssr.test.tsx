import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { DEMOS } from '../docs/examples/catalog';
import { CORE_COMPONENT_MANIFESTS } from './manifests';
import { Icon } from './components/icon';

describe('全部组件 SSR', () => {
  it('Icon.title 按现有实现透传到 SVG，label 仍提供可访问名称', () => {
    const html = renderToString(<Icon name="check" title="状态说明" label="已完成" />);
    expect(html).toContain('title="状态说明"');
    expect(html).toContain('aria-label="已完成"');
  });
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
