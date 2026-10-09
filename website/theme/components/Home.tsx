// 首页区块（docs-site.md §2a.1）。文字内容写在 docs/index.mdx 里，这里只管排版。
// SSG-MD（import.meta.env.SSG_MD）那一遍只输出语义文本：标题、段落、列表与链接，不输出按钮、分隔符这类界面元素。
// 首页容器带 rp-doc 类，卡片里的 Markdown 沿用文档页样式；结构性元素标 rp-not-doc，不吃文档页的标题与段落样式。
import type { CSSProperties, ReactNode } from 'react';
import { withBase } from '@rspress/core/runtime';
import { Link, LlmsContainer, LlmsCopyButton, LlmsViewOptions } from '@rspress/core/theme';
import { FAMILY_REPOS, LICENSES } from '../site';

function openSearch() {
  // 复用导航栏里的搜索面板（它监听 Ctrl / ⌘ + K）；首页不另放一个面板，否则一次快捷键会打开两个。
  document.dispatchEvent(new KeyboardEvent('keydown', { code: 'KeyK', key: 'k', ctrlKey: true, bubbles: true }));
}

export function HomeHero({ title, tagline }: { title: string; tagline: string }) {
  if (import.meta.env.SSG_MD) {
    return (
      <>
        <h1>{title}</h1>
        <p>{tagline}</p>
      </>
    );
  }
  return (
    <header className="qian-home-hero rp-not-doc">
      <h1>{title}</h1>
      <p>{tagline}</p>
      <button type="button" className="qian-home-search" onClick={openSearch}>
        <span>搜索文档</span>
        <kbd>Ctrl K</kbd>
      </button>
      {/* 首页是 pageType: custom，Rspress 不会在标题下自动放「复制为 Markdown」（文档页由 themeConfig.llmsUI 注入），这里手动放一组 */}
      <LlmsContainer>
        <LlmsCopyButton />
        <LlmsViewOptions />
      </LlmsContainer>
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

function FooterGroup({ label, items }: { label: string; items: ReactNode[] }) {
  return (
    <span className="qian-home-footer-group">
      <span className="qian-home-footer-label">{label}</span>
      {items.flatMap((item, i) => (i === 0 ? [item] : [<span key={`sep-${i}`} aria-hidden="true">·</span>, item]))}
    </span>
  );
}

/** 页脚只放站点地图、产品族仓库与许可证，不放公司信息（docs-site.md §2a.1）。 */
export function HomeFooter() {
  if (import.meta.env.SSG_MD) {
    const repos = FAMILY_REPOS.map((repo) => {
      const text = repo.note ? `${repo.name}（${repo.note}）` : repo.name;
      return repo.url ? <a key={repo.name} href={repo.url}>{text}</a> : <span key={repo.name}>{text}（待建）</span>;
    });
    const licenses = LICENSES.map((license) => (
      <a key={license.project} href={license.url}>
        {license.project} {license.name}
      </a>
    ));
    return (
      <ul>
        <li>
          <a href={withBase('/sitemap.md')}>站点地图</a>
        </li>
        <li>产品族仓库：{repos.flatMap((item, i) => (i === 0 ? [item] : ['、', item]))}</li>
        <li>许可证：{licenses.flatMap((item, i) => (i === 0 ? [item] : ['、', item]))}</li>
      </ul>
    );
  }
  const repos = FAMILY_REPOS.map((repo) => {
    const text = repo.note ? `${repo.name}（${repo.note}）` : repo.name;
    return repo.url ? (
      <a key={repo.name} href={repo.url} target="_blank" rel="noopener noreferrer">
        {text}
      </a>
    ) : (
      <span key={repo.name}>{text}（待建）</span>
    );
  });
  const licenses = LICENSES.map((license) => (
    <a key={license.project} href={license.url} target="_blank" rel="noopener noreferrer">
      {license.project} {license.name}
    </a>
  ));
  return (
    <footer className="qian-home-footer rp-not-doc">
      <Link href="/sitemap">站点地图</Link>
      <FooterGroup label="产品族仓库" items={repos} />
      <FooterGroup label="许可证" items={licenses} />
    </footer>
  );
}
