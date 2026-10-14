// 构建产物检查（CI 在 rspress build 之后运行）：pnpm --filter website check
// 1. 七个部分、首页、404、站点地图都在；2. 页面 lang 为 zh；
// 3. 离线可用：脚本、样式、字体、图片不引用外部地址（普通超链接不算）；
// 4. 站内引用都带部署前缀（DOCS_BASE，PR 预览为 /pr-preview/pr-<n>/）；
// 5. 全文搜索索引已生成；6. Shiki 高亮覆盖 C# / TypeScript / JSON / YAML / Bash；
// 7. Kun 小节与 Kun API 参考（docs-site.md §2b）：每页横幅里的 tag 与 Directory.Packages.props 的 KunVersion 一致，
//    sdk/kun/ 下除手写页外都是镜像页（白名单外的文件进站即失败）；
// 8. 产物总大小不超过预算（R55），挡住「同一块内容被重复渲染进每一页」这类结构性膨胀；
// 9. 机器可读产物（docs-site.md §4a）：llms.txt、llms-full.txt、每页 .md、kb-index.json、sitemap.xml 齐全且与源文档同步，
//    .md 里没有 JSX / ESM 残留与空标题空列表项，代码块不少于源页。
import fs from 'node:fs';
import path from 'node:path';
import { KB_INDEX_FILE, KB_INDEX_VERSION, SITEMAP_FILE, absoluteUrl, sha256, type KbIndex } from './build-kb-index.ts';
import { readKunVersion } from './kun.ts';
import { SITE_DOCS, WEBSITE_ROOT, listFiles, splitFrontmatter } from './repo.ts';

const outDir = path.join(WEBSITE_ROOT, 'doc_build');
const base = process.env.DOCS_BASE ?? '/';

const REQUIRED_PAGES = [
  'index.html',
  '404.html',
  'sitemap.html',
  'start/index.html',
  'start/family.html',
  'start/versions.html',
  'components/index.html',
  'sdk/index.html',
  'cli/index.html',
  'ops/index.html',
  'marketplace/index.html',
  'design/index.html',
  'sdk/kun/index.html',
  'sdk/kun/with-qian.html',
  'sdk/api/index.html',
  'sdk/api/kun/index.html',
];
/** Kun 小节里唯一的手写页（website/docs/sdk/kun/with-qian.mdx）。 */
const KUN_HANDWRITTEN_PAGES = ['sdk/kun/with-qian.html'];
const REQUIRED_LANGS = ['csharp', 'ts', 'json', 'yaml', 'bash'];

export interface SiteReport {
  errors: string[];
  pages: number;
  /** 产物总字节数。 */
  bytes: number;
  highlighted: Record<string, number>;
}

/**
 * 产物总大小预算（R55，docs-site.md §7）。S1-QA-010 实测 96.2 MB（756 个页面，含完整 API/Markdown 与 15.6 MB 离线编辑器）；
 * 按 R55 从 100 MB 调整为 120 MB，保留约 24 MB 增长空间，挡住的是结构性膨胀——
 * 例如整棵侧栏被渲染进几百个 API 页，曾让产物从 57 MB 涨到 170 MB——而不是正常增长。超出时先看报出的最大目录，
 * 确属正常增长再调高预算，并在 PR 里写明原因。
 */
export const SITE_SIZE_BUDGET_MB = 120;

const mb = (bytes: number) => `${(bytes / 1e6).toFixed(1)} MB`;

/** sizes：[相对路径, 字节数]。超出预算时报出最大的几个目录（取前两级），便于定位膨胀来源。 */
export function checkSizeBudget(sizes: ReadonlyArray<readonly [string, number]>, budgetMb: number = SITE_SIZE_BUDGET_MB): string[] {
  const total = sizes.reduce((sum, [, bytes]) => sum + bytes, 0);
  if (total <= budgetMb * 1e6) return [];
  const byDir = new Map<string, number>();
  for (const [rel, bytes] of sizes) {
    const dir = rel.split('/').slice(0, -1).slice(0, 2).join('/') || '.';
    byDir.set(dir, (byDir.get(dir) ?? 0) + bytes);
  }
  const top = [...byDir.entries()]
    .sort((a, b) => b[1] - a[1])
    .slice(0, 6)
    .map(([dir, bytes]) => `${dir}/ ${mb(bytes)}`)
    .join('、');
  return [
    `产物 ${mb(total)} 超过预算 ${budgetMb} MB（R55）。最大的目录：${top}。` +
      '先查是否有同一块内容被重复渲染进每一页（如整棵侧栏），确属正常增长再调高 SITE_SIZE_BUDGET_MB 并在 PR 里说明',
  ];
}

/** 代码围栏行数（``` 或更多反引号开头的行），源页与 .md 产物各数一遍，相等才算代码块都保留了。 */
export function countFences(markdown: string): number {
  return markdown.split('\n').filter((line) => /^\s*`{3,}/.test(line)).length;
}

const xmlEscape = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');

/** 去掉围栏代码块与行内代码，残留检查只看正文（代码里的 XML、import 都是正常内容）。 */
export function stripCode(markdown: string): string {
  const out: string[] = [];
  let fence: string | null = null;
  for (const line of markdown.split('\n')) {
    const marker = /^\s*(`{3,}|~{3,})/.exec(line)?.[1];
    if (marker && fence === null) {
      fence = marker;
      continue;
    }
    if (fence !== null) {
      if (marker && marker[0] === fence[0] && marker.length >= fence.length) fence = null;
      continue;
    }
    out.push(line.replace(/`[^`\n]*`/g, '`'));
  }
  return out.join('\n');
}

/**
 * 单页 Markdown 产物的质量（docs-site.md §5.1）：正文（代码除外）没有 ESM 语句、组件标签、JSX 注释或 className 残留；没有空标题、空列表项；
 * 给了源页的代码围栏数时产物不能更少（演示代码块必须保留；DocFX 的 HTML 代码块会多出几个围栏，所以只查不少于）。
 * 泛型写法 `Result<T>` 不算组件：组件标签带属性或自闭合。
 */
export function checkMarkdownOutput(rel: string, md: string, sourceFences: number | null = null): string[] {
  const errors: string[] = [];
  const text = stripCode(md);
  if (/^import\s.+\sfrom\s/m.test(text) || /^export\s+(const|function|default)\b/m.test(text)) errors.push(`${rel}：Markdown 里有 ESM 语句残留`);
  const component = /<[A-Z][A-Za-z]*(?:\s+[a-zA-Z-]+=|\s*\/>)/.exec(text);
  if (component) errors.push(`${rel}：Markdown 里有组件残留 ${component[0]}（JSX 容器内部的行内组件不会被渲染，改写成文字）`);
  if (/\{\/\*/.test(text) || /\bclassName=/.test(text)) errors.push(`${rel}：Markdown 里有 JSX 残留`);
  const empty = /^[ \t]*(#{1,6}|[-*+]|\d+[.)])[ \t]*$/m.exec(text);
  if (empty) errors.push(`${rel}：Markdown 里有空标题或空列表项「${empty[0].trim()}」（组件在 SSG-MD 下输出了空元素）`);
  if (sourceFences !== null) {
    const fences = countFences(md);
    if (fences < sourceFences) errors.push(`${rel}：代码围栏 ${fences} 行，少于源页的 ${sourceFences} 行，代码块丢失`);
  }
  return errors;
}

/**
 * 机器可读产物与源文档同步（docs-site.md §4a、§7）：四个文件齐全；kb-index.json 版本对得上；索引里每页的 .md 都在且
 * sha256 一致、HTML 都在；llms.txt 列出每页（首页除外）、llms-full.txt 收录每页、sitemap.xml 含每页；产物里没有索引外的 .md。
 * readSource 返回源页正文（没有源时 null），用来比对代码块数量（docs-site.md §5.1）。
 */
export function checkMachineReadable(files: readonly string[], read: (rel: string) => string, readSource: (sourceFile: string) => string | null): string[] {
  const errors: string[] = [];
  for (const required of ['llms.txt', 'llms-full.txt', KB_INDEX_FILE, SITEMAP_FILE]) {
    if (!files.includes(required)) errors.push(`缺少 ${required}`);
  }
  if (!files.includes(KB_INDEX_FILE)) return errors;
  let index: KbIndex;
  try {
    index = JSON.parse(read(KB_INDEX_FILE)) as KbIndex;
  } catch {
    return [...errors, `${KB_INDEX_FILE} 不是合法的 JSON`];
  }
  if (index.version !== KB_INDEX_VERSION) errors.push(`${KB_INDEX_FILE} 的 version 是 ${String(index.version)}，本检查器只认 ${KB_INDEX_VERSION}`);
  const llmsTxt = files.includes('llms.txt') ? read('llms.txt') : '';
  const llmsFull = files.includes('llms-full.txt') ? read('llms-full.txt') : '';
  const sitemap = files.includes(SITEMAP_FILE) ? read(SITEMAP_FILE) : '';
  const listed = new Set<string>();
  for (const page of index.pages ?? []) {
    const mdRel = page.md.startsWith(index.base) ? page.md.slice(index.base.length) : page.md.replace(/^\//, '');
    listed.add(mdRel);
    if (!files.includes(mdRel)) {
      errors.push(`${page.route}：缺少 ${mdRel}`);
      continue;
    }
    const md = read(mdRel);
    if (sha256(md) !== page.sha256) errors.push(`${page.route}：${mdRel} 与 ${KB_INDEX_FILE} 记录的 sha256 不一致（索引过期，重新构建）`);
    const htmlRel = page.url.startsWith(index.base) ? page.url.slice(index.base.length) : page.url.replace(/^\//, '');
    if (!files.includes(htmlRel)) errors.push(`${page.route}：缺少 ${htmlRel}`);
    const source = readSource(page.sourceFile);
    errors.push(...checkMarkdownOutput(mdRel, md, source === null ? null : countFences(source)));
    if (page.route !== '/' && !llmsTxt.includes(`](${page.md})`)) errors.push(`llms.txt 没有列出 ${page.md}`);
    if (!llmsFull.includes(`url: ${page.md}\n`)) errors.push(`llms-full.txt 没有收录 ${page.md}`);
    if (!sitemap.includes(`<loc>${xmlEscape(absoluteUrl(index, page.route))}</loc>`)) errors.push(`sitemap.xml 没有 ${page.route}`);
  }
  for (const rel of files) {
    if (rel.endsWith('.md') && !listed.has(rel)) errors.push(`${rel} 不在 ${KB_INDEX_FILE} 里（产物里多出来的 Markdown）`);
  }
  return errors;
}

/** Kun 页面的横幅（scripts/sync-kun-docs.ts、build-kun-api.ts 生成）都写了 tag：它必须正好是 v<KunVersion>。 */
export function checkKunPages(files: readonly string[], read: (rel: string) => string, kunVersion: string): string[] {
  const errors: string[] = [];
  const tag = `v${kunVersion}`;
  const kunPages = files.filter((f) => f.endsWith('.html') && (f.startsWith('sdk/kun/') || f.startsWith('sdk/api/kun/')));
  for (const rel of kunPages) {
    if (KUN_HANDWRITTEN_PAGES.includes(rel)) continue;
    const html = read(rel);
    const banner = /(?:本页镜像自|本页由|本节由)[\s\S]{0,2000}?<\/code>）/.exec(html)?.[0] ?? '';
    if (banner === '') {
      errors.push(`${rel}：Kun 小节里的页面不是镜像页也不是手写页（白名单外的文件不能进站，docs-site.md §2b.2）`);
      continue;
    }
    const tags = [...banner.matchAll(/<code>(v\d[^<]*)<\/code>/g)].map((m) => m[1]);
    if (!tags.includes(tag) || tags.some((t) => t !== tag)) {
      errors.push(`${rel}：横幅里的 Kun 版本 ${tags.join('、') || '（无）'} 与 KunVersion ${kunVersion} 不一致`);
    }
  }
  if (kunPages.length === 0) errors.push('没有 Kun 小节页面（sdk/kun/、sdk/api/kun/）');
  return errors;
}

export function checkSite(dir: string = outDir, basePath: string = base): SiteReport {
  const errors: string[] = [];
  if (!fs.existsSync(dir)) return { errors: [`没有构建产物：${dir}（先运行 pnpm --filter website build）`], pages: 0, bytes: 0, highlighted: {} };
  const files = listFiles(dir);
  const htmlFiles = files.filter((f) => f.endsWith('.html'));
  const sizes = files.map((rel) => [rel, fs.statSync(path.join(dir, rel)).size] as const);
  errors.push(...checkSizeBudget(sizes));

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

  errors.push(...checkKunPages(files, (rel) => fs.readFileSync(path.join(dir, rel), 'utf8'), readKunVersion()));
  errors.push(
    ...checkMachineReadable(
      files,
      (rel) => fs.readFileSync(path.join(dir, rel), 'utf8'),
      (sourceFile) => {
        const sourcePath = path.join(SITE_DOCS, sourceFile);
        return fs.existsSync(sourcePath) ? splitFrontmatter(fs.readFileSync(sourcePath, 'utf8')).body : null;
      },
    ),
  );

  return { errors, pages: htmlFiles.length, bytes: sizes.reduce((sum, [, bytes]) => sum + bytes, 0), highlighted };
}

if (import.meta.filename === process.argv[1]) {
  const report = checkSite();
  if (report.errors.length > 0) {
    console.error(report.errors.slice(0, 50).join('\n'));
    if (report.errors.length > 50) console.error(`……共 ${report.errors.length} 处`);
    process.exit(1);
  }
  const langs = Object.entries(report.highlighted).map(([l, n]) => `${l} ${n}`).join('、');
  console.log(`站点检查通过：${report.pages} 个页面（含同数的 .md、llms.txt、llms-full.txt、kb-index.json、sitemap.xml），产物 ${mb(report.bytes)}（预算 ${SITE_SIZE_BUDGET_MB} MB），前缀 ${base}；高亮代码块 ${langs}`);
}
