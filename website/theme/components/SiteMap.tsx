// 站点地图：按七个部分列出全部页面，数据来自构建时的页面清单，新增页面自动出现。
import { routePathToMdPath, usePages } from '@rspress/core/runtime';
import { Link } from '@rspress/core/theme';
import { PARTS } from '../site';

export function SiteMap() {
  const { pages } = usePages();
  return (
    <div className="qian-sitemap">
      {PARTS.map((part) => {
        const list = pages
          .filter((p) => p.routePath.startsWith(part.prefix))
          .sort((a, b) => a.routePath.localeCompare(b.routePath));
        return (
          <section key={part.prefix}>
            <h2>{part.name}</h2>
            <ul>
              {list.map((p) => (
                <li key={p.routePath}>
                  {/* SSG-MD 那一遍链到每页的 .md，读 Markdown 的代理顺着走不会掉回 HTML */}
                  {import.meta.env.SSG_MD ? (
                    <a href={routePathToMdPath(p.routePath)}>{p.title || p.routePath}</a>
                  ) : (
                    <Link href={p.routePath}>{p.title || p.routePath}</Link>
                  )}
                </li>
              ))}
            </ul>
          </section>
        );
      })}
    </div>
  );
}
