// 第 2、3、4 部分每页顶部的版本说明（docs-site.md §5）。首个包族版本发布后改为版本切换。
// Kun 小节与 Kun API 参考（frontmatter project: kun）不显示：它们按 KunVersion 镜像，各页横幅已写明版本（§2b）。
import { usePage } from '@rspress/core/runtime';
import { Link } from '@rspress/core/theme';
import { VERSIONED_PARTS } from '../site';

export function PartNotice() {
  const { page } = usePage();
  const part = VERSIONED_PARTS.find((p) => page.routePath.startsWith(p.prefix));
  if (!part || page.frontmatter?.project === 'kun') return null;
  return (
    <div className="qian-part-notice">
      版本：开发版（main）。「{part.name}」随 Qian 包族的 SemVer 多版本发布，首个版本发布后在此切换版本（
      <Link href="/design/prd/specs/docs-site">文档站规范 §5</Link>）。
    </div>
  );
}
