import path from 'node:path';
import { rehypeAccessibleAnchors } from './scripts/rehype-a11y.ts';
import { fileURLToPath } from 'node:url';
import { defineConfig } from '@rspress/core';
import mermaid from 'rspress-plugin-mermaid';
import { renderLlmsTxt } from './scripts/llms.ts';
import { qianDocsSources } from './scripts/sources-plugin.ts';
import { REPO_URL } from './scripts/repo.ts';

const here = path.dirname(fileURLToPath(import.meta.url));

// 正式站点挂在根路径；PR 预览挂在 /pr-preview/pr-<n>/，由 CI 经 DOCS_BASE 传入（见 README「托管与预览」）。
const base = process.env.DOCS_BASE ?? '/';

// 明暗两态：Rspress 在首帧前给 html 加 dark 类，@idadawn/yue-tokens 的语义层同时认 html.dark 与 html[data-theme="dark"]
// （specs/yue.md §3.5），所以不再需要把 dark 类同步到 data-theme 的桥接脚本（S1-DOCS-004）。

export default defineConfig({
  root: path.join(here, 'docs'),
  base,
  lang: 'zh',
  title: 'Qian 文档',
  description: 'Qian 平台基于 Kun 框架构建，前端使用 yue 组件库。这里是它的组件、SDK、CLI、运维与设计文档。',
  icon: '/logo.svg',
  logo: '/logo.svg',
  logoText: 'Qian 文档',
  // 机器可读（docs-site.md §2a.1、S1-DOCS-003）：SSG-MD 在构建期把每页渲染成 .md，并产出 llms.txt 与 llms-full.txt；
  // kb-index.json 与 sitemap.xml 由 scripts/build-kb-index.ts 在构建后产出；llms.txt 先按 project 分（Qian → Kun → yue）再按部分分节（ADR-021 D21.5）。
  llms: { llmsTxt: renderLlmsTxt },
  route: {
    // 不用 cleanUrls：产物里的链接带 .html，任何静态服务器（含 python3 -m http.server）都能直接打开。
    cleanUrls: false,
  },
  markdown: {
    rehypePlugins: [rehypeAccessibleAnchors],
    link: { checkDeadLinks: true },
    shiki: {
      // 验收要求覆盖 C# / TypeScript / JSON / YAML / Bash；其余为 docs/ 里实际出现的语言。
      langs: ['csharp', 'ts', 'tsx', 'js', 'json', 'jsonc', 'yaml', 'bash', 'sql', 'xml', 'markdown'],
    },
  },
  plugins: [
    qianDocsSources(),
    mermaid({ mermaidConfig: { securityLevel: 'strict' } }),
  ],
  themeConfig: {
    // 导航写在 docs/_nav.json：配置里一旦写 nav，Rspress 就不再按目录自动生成各部分的侧栏。
    socialLinks: [{ icon: 'github', mode: 'link', content: REPO_URL }],
    enableScrollToTop: true,
    // 每页标题下的「复制为 Markdown」与「复制 Markdown 链接」；不放外部 AI 服务的入口（站内「问 AI」是 S2-DOCS-001）。
    llmsUI: { viewOptions: ['markdownLink'] },
  },
});
