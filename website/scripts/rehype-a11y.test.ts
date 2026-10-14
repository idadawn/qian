import assert from 'node:assert/strict';
import { it } from 'node:test';
import { rehypeAccessibleAnchors } from './rehype-a11y.ts';
it('标题链接可命名且保持键盘入口，正文链接保持原样', () => {
  const anchor = { type: 'element', tagName: 'a', properties: { class: 'rp-header-anchor', ariaHidden: 'true', href: '#example' } as Record<string, unknown>, children: [{ type: 'text', value: '#' }] };
  const link = { type: 'element', tagName: 'a', properties: { href: '/docs' }, children: [{ type: 'text', value: '示例' }] };
  rehypeAccessibleAnchors()({ type: 'root', children: [{ type: 'element', tagName: 'h2', children: [anchor, link] }] });
  assert.equal(anchor.properties.ariaHidden, undefined);
  assert.equal(anchor.properties.ariaLabel, '链接到 示例');
  assert.equal(anchor.properties.href, '#example');
  assert.deepEqual(link.properties, { href: '/docs' });
});
