import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { Tree } from './index';

describe('SSR', () => {
  it('Tree 可在 Node 里渲染，展开态与选中态进 HTML', () => {
    const html = renderToString(
      <Tree nodes={[{ id: 'a', label: '甲', children: [{ id: 'b', label: '乙' }] }]} aria-label="树" defaultExpandedIds={['a']} defaultSelectedId="b" />,
    );
    expect(html).toContain('role="tree"');
    expect(html).toContain('aria-expanded="true"');
    expect(html).toContain('data-node-id="b"');
    expect(html).toContain('data-testid="yue-tree"');
  });
});
