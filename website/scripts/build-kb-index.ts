// 知识库导入契约（docs-site.md §4a，S1-DOCS-003）：rspress build 之后运行，从页面源文件与 SSG-MD 产物拼出
// kb-index.json（每页的 url、.md 地址、标题、frontmatter 元数据、.md 内容 sha256、更新时间）与 sitemap.xml（lastmod 取 updated）。
// 用法：pnpm --filter website build（末尾自动运行）或 pnpm --filter website kb-index。
import crypto from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { DEFAULT_SITE_URL } from '../theme/site.ts';
import { readKunVersion } from './kun.ts';
import { SITE_DOCS, WEBSITE_ROOT, listFiles, parseFrontmatter, splitFrontmatter, type FrontmatterValue } from './repo.ts';

export const KB_INDEX_VERSION = 1;
export const KB_INDEX_FILE = 'kb-index.json';
export const SITEMAP_FILE = 'sitemap.xml';

export interface KbPage {
  /** 站内路由，如 /start/hosts、/start/（目录首页）、/（首页）。 */
  route: string;
  /** HTML 页面地址（带部署前缀）。 */
  url: string;
  /** 同一页的 Markdown 地址（带部署前缀）；内容由 SSG-MD 在构建期渲染。 */
  md: string;
  /** 源文件（相对 website/docs）；生成页的源不进 Git，但路径稳定。 */
  sourceFile: string;
  title: string;
  description: string;
  project: string;
  part: string;
  audience: string[];
  version?: string;
  source?: string;
  updated: string;
  /** .md 文件内容的 SHA-256（十六进制），变了就是内容变了。 */
  sha256: string;
  bytes: number;
  /** 完整 frontmatter（docs-site.md §4 的字段与页面自带的其它字段）。 */
  frontmatter: Record<string, FrontmatterValue>;
}

export interface KbIndex {
  version: number;
  generatedAt: string;
  /** 部署前缀（DOCS_BASE），正式站点为 /，PR 预览为 /pr-preview/pr-<n>/。 */
  base: string;
  /** 站点入口，与 base 拼成绝对地址；sitemap.xml 用它。 */
  siteUrl: string;
  kunVersion: string;
  pages: KbPage[];
}

/** 页面源文件 → 路由：index 文件对应目录路由（以 / 结尾），其余去掉扩展名。 */
export function pageRoute(sourceFile: string): string {
  const noExt = sourceFile.replace(/\.(md|mdx)$/, '');
  if (noExt === 'index') return '/';
  if (noExt.endsWith('/index')) return `/${noExt.slice(0, -'index'.length)}`;
  return `/${noExt}`;
}

/** 路由 → 产物里的 HTML 相对路径。 */
export function routeHtml(route: string): string {
  return routeFile(route, 'html');
}

/** 路由 → 产物里的 Markdown 相对路径（与 Rspress SSG-MD 的命名一致）。 */
export function routeMd(route: string): string {
  return routeFile(route, 'md');
}

function routeFile(route: string, ext: string): string {
  if (route === '/') return `index.${ext}`;
  const rel = route.replace(/^\//, '');
  return rel.endsWith('/') ? `${rel}index.${ext}` : `${rel}.${ext}`;
}

/** website/docs 下会成为页面的源文件：.md / .mdx，排除 public/ 与下划线开头的目录或文件。 */
export function listPageSources(siteDocs: string = SITE_DOCS): string[] {
  return listFiles(siteDocs).filter((rel) => /\.(md|mdx)$/.test(rel) && !rel.startsWith('public/') && !rel.split('/').some((seg) => seg.startsWith('_')));
}

export function sha256(content: string): string {
  return crypto.createHash('sha256').update(content, 'utf8').digest('hex');
}

export interface BuildOptions {
  siteDocs: string;
  outDir: string;
  base: string;
  siteUrl: string;
  kunVersion: string;
  now?: Date;
}

/** 拼出索引；错误（缺 .md、frontmatter 不合法、产物里有没来源的 .md）收集在 errors 里，不抛。 */
export function buildKbIndex(options: BuildOptions): { index: KbIndex; errors: string[] } {
  const { siteDocs, outDir, base, siteUrl, kunVersion } = options;
  const errors: string[] = [];
  const pages: KbPage[] = [];
  const known = new Set<string>();
  for (const sourceFile of listPageSources(siteDocs)) {
    const route = pageRoute(sourceFile);
    const mdRel = routeMd(route);
    const htmlRel = routeHtml(route);
    known.add(mdRel);
    const { frontmatter } = splitFrontmatter(fs.readFileSync(path.join(siteDocs, sourceFile), 'utf8'));
    let data: Record<string, FrontmatterValue>;
    try {
      data = parseFrontmatter(frontmatter);
    } catch (error) {
      errors.push(`${sourceFile}：${error instanceof Error ? error.message : String(error)}`);
      continue;
    }
    const mdPath = path.join(outDir, mdRel);
    if (!fs.existsSync(mdPath)) {
      errors.push(`${sourceFile}：产物里没有 ${mdRel}（SSG-MD 未生成这一页）`);
      continue;
    }
    if (!fs.existsSync(path.join(outDir, htmlRel))) errors.push(`${sourceFile}：产物里没有 ${htmlRel}`);
    const md = fs.readFileSync(mdPath, 'utf8');
    const scalar = (key: string): string => {
      const value = data[key];
      return typeof value === 'string' ? value : '';
    };
    const list = (key: string): string[] => {
      const value = data[key];
      return Array.isArray(value) ? value : [];
    };
    for (const key of ['title', 'description', 'part', 'project', 'updated']) {
      if (!scalar(key)) errors.push(`${sourceFile}：frontmatter 缺少 ${key}（docs-site.md §4）`);
    }
    const page: KbPage = {
      route,
      url: `${base}${htmlRel}`,
      md: `${base}${mdRel}`,
      sourceFile,
      title: scalar('title'),
      description: scalar('description'),
      project: scalar('project'),
      part: scalar('part'),
      audience: list('audience'),
      updated: scalar('updated'),
      sha256: sha256(md),
      bytes: Buffer.byteLength(md, 'utf8'),
      frontmatter: data,
    };
    if (scalar('version')) page.version = scalar('version');
    if (scalar('source')) page.source = scalar('source');
    pages.push(page);
  }
  for (const rel of listFiles(outDir)) {
    if (rel.endsWith('.md') && !known.has(rel)) errors.push(`产物里的 ${rel} 没有对应的页面源文件`);
  }
  pages.sort((a, b) => a.route.localeCompare(b.route));
  const generatedAt = (options.now ?? new Date()).toISOString();
  return { index: { version: KB_INDEX_VERSION, generatedAt, base, siteUrl, kunVersion, pages }, errors };
}

/** 页面的绝对地址：siteUrl + base + 路由（首页为 siteUrl + base）。 */
export function absoluteUrl(index: Pick<KbIndex, 'siteUrl' | 'base'>, route: string): string {
  return `${index.siteUrl.replace(/\/$/, '')}${index.base}${route === '/' ? '' : route.replace(/^\//, '')}`;
}

/** sitemap.xml：loc 为路由的绝对地址（不带 .html，服务器会补），lastmod 取 frontmatter 的 updated。 */
export function sitemapXml(index: KbIndex): string {
  const escape = (text: string) => text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const urls = index.pages.map((page) => {
    const priority = page.route === '/' ? '1.0' : '0.5';
    return `<url><loc>${escape(absoluteUrl(index, page.route))}</loc><lastmod>${escape(page.updated)}</lastmod><priority>${priority}</priority></url>`;
  });
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${urls.join('\n')}\n</urlset>\n`;
}

export function writeKbIndex(outDir: string, index: KbIndex): void {
  fs.writeFileSync(path.join(outDir, KB_INDEX_FILE), `${JSON.stringify(index, null, 2)}\n`);
  fs.writeFileSync(path.join(outDir, SITEMAP_FILE), sitemapXml(index));
}

if (import.meta.filename === process.argv[1]) {
  const outDir = path.join(WEBSITE_ROOT, 'doc_build');
  if (!fs.existsSync(outDir)) {
    console.error(`没有构建产物：${outDir}（先运行 rspress build）`);
    process.exit(1);
  }
  const { index, errors } = buildKbIndex({
    siteDocs: SITE_DOCS,
    outDir,
    base: process.env.DOCS_BASE ?? '/',
    siteUrl: process.env.DOCS_SITE_URL ?? DEFAULT_SITE_URL,
    kunVersion: readKunVersion(),
  });
  if (errors.length > 0) {
    console.error(errors.join('\n'));
    process.exit(1);
  }
  writeKbIndex(outDir, index);
  const byProject = new Map<string, number>();
  for (const page of index.pages) byProject.set(page.project, (byProject.get(page.project) ?? 0) + 1);
  const summary = [...byProject.entries()].map(([project, count]) => `${project} ${count}`).join('、');
  console.log(`知识库索引：${index.pages.length} 页（${summary}）→ ${KB_INDEX_FILE}、${SITEMAP_FILE}`);
}
