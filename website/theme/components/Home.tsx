// 首页区块（docs-site.md §2a.1）。文字内容写在 docs/index.mdx 里，这里只管排版。
// 首页容器带 rp-doc 类，卡片里的 Markdown 沿用文档页样式；结构性元素标 rp-not-doc，不吃文档页的标题与段落样式。
import type { CSSProperties, ReactNode } from 'react';
import { Link } from '@rspress/core/theme';
import { LICENSE_NAME, LICENSE_URL, REPO_URL } from '../site';

function openSearch() {
  // 复用导航栏里的搜索面板（它监听 Ctrl / ⌘ + K）；首页不另放一个面板，否则一次快捷键会打开两个。
  document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyK', key: 'k', ctrlKey: true, bubbles: true }));
}

export function HomeHero({ title, tagline }: { title: string; tagline: string }) {
  return (
    <header className="qian-home-hero rp-not-doc">
      <h1>{title}</h1>
      <p>{tagline}</p>
      <button type="button" className="qian-home-search" onClick={openSearch}>
        <span>搜索文档</span>
        <kbd>Ctrl K</kbd>
      </button>
    </header>
  );
}

export function HomeSection({ id, title, lead, children }: { id: string; title: string; lead?: string; children: ReactNode }) {
  return (
    <section className="qian-home-section" id={id} aria-labelledby={`${id}-title`}>
      <h2 id={`${id}-title`} className="rp-not-doc">
        {title}
      </h2>
      {lead ? <p className="qian-home-lead rp-not-doc">{lead}</p> : null}
      {children}
    </section>
  );
}

export function CardGrid({ min, children }: { min?: string; children: ReactNode }) {
  const style = min ? ({ '--qian-card-min': min } as CSSProperties) : undefined;
  return (
    <div className="qian-card-grid" style={style}>
      {children}
    </div>
  );
}

export function Card({ title, children }: { title: ReactNode; children: ReactNode }) {
  return (
    <div className="qian-card">
      <h3 className="rp-not-doc">{title}</h3>
      {children}
    </div>
  );
}

/** 页脚只放站点地图、仓库与许可证，不放公司信息（docs-site.md §2a）。 */
export function HomeFooter() {
  return (
    <footer className="qian-home-footer rp-not-doc">
      <Link href="/sitemap">站点地图</Link>
      <a href={REPO_URL} target="_blank" rel="noopener noreferrer">
        仓库
      </a>
      <a href={LICENSE_URL} target="_blank" rel="noopener noreferrer">
        许可证：{LICENSE_NAME}
      </a>
    </footer>
  );
}
