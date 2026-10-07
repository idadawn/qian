// 主题覆盖：yue 令牌、建设期横幅、版本化部分的版本说明，以及首页与站点地图组件。
// 其余一律沿用 Rspress 默认主题；后续「复制为 Markdown」（S1-DOCS-003）与 AskDocs（S2-DOCS-001）也挂在这里。
import './index.css';
import { MDXProvider } from '@mdx-js/react';
import { Banner, Layout as BasicLayout, getCustomMDXComponent } from '@rspress/core/theme-original';
import { PartNotice } from './components/PartNotice';

// 文档页由 DocContent 提供 MDX 组件映射（代码块、链接加 base 等）；首页是 pageType: custom，
// Rspress 不为它提供映射，这里在最外层补上同一套。文档页内层的映射照常生效。
const mdxComponents = getCustomMDXComponent();

function Layout() {
  return (
    <MDXProvider components={mdxComponents}>
      <BasicLayout
        beforeNav={
          <Banner
            href="/design/roadmap"
            storageKey="qian-docs-s1-banner"
            message="S1 建设中：文档按设计目标编写，组件与命令随 S1 任务陆续可用；标 S2–S4 的能力尚未开始。查看路线图 →"
          />
        }
        beforeDocContent={<PartNotice />}
      />
    </MDXProvider>
  );
}

export { Layout };
export * from '@rspress/core/theme-original';
export { Card, CardGrid, HomeFooter, HomeHero, HomeSection } from './components/Home';
export { LatestChanges } from './components/LatestChanges';
export { SiteMap } from './components/SiteMap';
