// 构建产物检查（CI 在 rspress build 之后运行）：pnpm --filter website check
// 1. 七个部分、首页、404、站点地图都在；2. 页面 lang 为 zh；
// 3. 离线可用：脚本、样式、字体、图片不引用外部地址（普通超链接不算）；
// 4. 站内引用都带部署前缀（DOCS_BASE，PR 预览为 /pr-preview/pr-<n>/）；
// 5. 全文搜索索引已生成；6. Shiki 高亮覆盖 C# / TypeScript / JSON / YAML / Bash。
import fs from 'node:fs';
import path from 'node:path';
import { WEBSITE_ROOT, listFiles } from './repo.ts';

const outDir = path.join(WEBSITE_ROOT, 'doc_build');
const base = process.env.DOCS_BASE ?? '/';

const REQUIRED_PAGES = [
  'index.html',
  '404.html',
  'sitemap.html',
  'start/index.html',
  'components/index.html',
  'sdk/index.html',
  'cli/index.html',
  'ops/index.html',
  'marketplace/index.html',
  'design/index.html',
];
const REQUIRED_LANGS = ['csharp', 'ts', 'json', 'yaml', 'bash'];

export interface SiteReport {
  errors: string[];
  pages: number;
  highlighted: Record<string, number>;
}

export function checkSite(dir: string = outDir, basePath: string = base): SiteReport {
  const errors: string[] = [];
  if (!fs.existsSync(dir)) return { errors: [`没有构建产物：${dir}（先运行 pnpm --filter website build）`], pages: 0, highlighted: {} };
  const files = listFiles(dir);
  const htmlFiles = files.filter((f) => f.endsWith('.html'));

  for (const page of REQUIRED_PAGES) {
    if (!files.includes(page)) errors.push(`缺少页面 ${page}`);
  }
  if (!files.some((f) => /^static\/search_index\.[^/]+\.json$/.test(f))) errors.push('缺少全文搜索索引 static/search_index.*.json');

  const highlighted: Record<string, number> = Object.fromEntries(REQUIRED_LANGS.map((l) => [l, 0]));
  for (const rel of htmlFiles) {
    const html = fs.readFileSync(path.join(dir, rel), 'utf8');
    if (!/<html[^>]*\blang="zh"/.test(html)) errors.push(`${rel}：<html> 的 lang 不是 zh`);

    // 资源类引用：<script src>、<link href>（样式、预加载、图标）、<img src>、<source src>
    for (const m of html.matchAll(/<(script|link|img|source|iframe)\b[^>]*?\b(?:src|href)="([^"]+)"/g)) {
      const url = m[2] ?? '';
      if (/^(?:https?:)?\/\//.test(url)) errors.push(`${rel}：<${m[1]}> 引用外部资源 ${url}`);
    }
    for (const m of html.matchAll(/\b(?:src|href)="(\/[^"]*)"/g)) {
      const url = m[1] ?? '';
      if (!url.startsWith('//') && !url.startsWith(basePath)) errors.push(`${rel}：站内引用缺少前缀 ${basePath}：${url}`);
    }
    for (const m of html.matchAll(/<pre class="shiki[^"]*"[^>]*\blang="([^"]+)"[^>]*>([\s\S]*?)<\/pre>/g)) {
      const lang = m[1] ?? '';
      if (lang in highlighted && (m[2] ?? '').includes('var(--shiki-token-')) highlighted[lang] = (highlighted[lang] ?? 0) + 1;
    }
  }
  for (const [lang, count] of Object.entries(highlighted)) {
    if (count === 0) errors.push(`没有任何 ${lang} 代码块被 Shiki 高亮`);
  }

  for (const rel of files.filter((f) => f.endsWith('.css'))) {
    const css = fs.readFileSync(path.join(dir, rel), 'utf8');
    for (const m of css.matchAll(/url\(\s*['"]?((?:https?:)?\/\/[^'")\s]+)/g)) errors.push(`${rel}：样式引用外部资源 ${m[1]}`);
    for (const m of css.matchAll(/@import\s+(?:url\()?\s*['"]?((?:https?:)?\/\/[^'")\s]+)/g)) errors.push(`${rel}：样式导入外部地址 ${m[1]}`);
  }

  return { errors, pages: htmlFiles.length, highlighted };
}

if (import.meta.filename === process.argv[1]) {
  const report = checkSite();
  if (report.errors.length > 0) {
    console.error(report.errors.slice(0, 50).join('\n'));
    if (report.errors.length > 50) console.error(`……共 ${report.errors.length} 处`);
    process.exit(1);
  }
  const langs = Object.entries(report.highlighted).map(([l, n]) => `${l} ${n}`).join('、');
  console.log(`站点检查通过：${report.pages} 个页面，前缀 ${base}；高亮代码块 ${langs}`);
}
