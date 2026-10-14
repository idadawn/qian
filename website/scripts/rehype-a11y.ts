// Rspress 2.0.23 的标题锚点默认 aria-hidden 但仍可聚焦；给链接命名并保留键盘入口。
interface Node { type: string; tagName?: string; value?: string; properties?: Record<string, unknown>; children?: Node[] }
export function rehypeAccessibleAnchors() {
  return (tree: Node) => {
    const text = (node: Node): string => node.value ?? (node.children ?? []).map(text).join('');
    const visit = (node: Node) => {
      if (/^h[1-6]$/.test(node.tagName ?? '')) {
        const label = (node.children ?? []).filter(child => !String(child.properties?.class ?? child.properties?.className).includes('rp-header-anchor')).map(text).join('').trim();
        for (const child of node.children ?? []) {
          if (child.tagName === 'a' && String(child.properties?.class ?? child.properties?.className).includes('rp-header-anchor')) {
            delete child.properties!.ariaHidden;
            child.properties!.ariaLabel = `链接到 ${label}`;
          }
        }
      }
      node.children?.forEach(visit);
    };
    visit(tree);
  };
}
