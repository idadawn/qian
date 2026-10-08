// 第 3 部分「Kun 框架」小节（docs-site.md §2b，ADR-021 D21.4）：按 KunVersion 把 kun 仓库 tag 上白名单内的文档镜像到 docs/sdk/kun/。
// 只镜像不改写：正文原样保留，只做站点需要的机械处理——链接改写、来源横幅、frontmatter、
// 把正文里形如 <T> 的非 HTML 尖括号转义（否则会被当成 HTML 标签吞掉）、按 GFM 规则给少列的表格行补空单元格。
// 唯一的手写页 with-qian.mdx 不由这里生成，写入时保留。
import path from 'node:path';
import { rewriteLinks, withInlineCodeMasked, yamlString, type GeneratedFile, type LinkResolver } from './repo.ts';
import {
  KUN_REPO_URL,
  kunFileDate,
  kunGithubUrl,
  kunSource,
  listKunFiles,
  readKunFile,
  type KunCheckout,
} from './kun.ts';

/** 手写页（docs-site.md §2b.2 表最后一行），生成时保留、不覆盖。 */
export const KUN_HANDWRITTEN = ['with-qian.mdx'] as const;
export const KUN_ROUTE_BASE = '/sdk/kun/';

interface WhitelistRule {
  /** 源文件路径模式：* 只匹配一段路径内的字符，区分大小写。 */
  pattern: string;
  /** 站内目录（相对 sdk/kun/），空串为小节根目录。 */
  dir: string;
  /** 固定页名；不写则取源文件名的 slug。 */
  name?: string;
}

/** docs-site.md §2b.2 的镜像白名单。白名单外的文件不进站；要扩白名单先改规范的表。 */
export const KUN_WHITELIST: readonly WhitelistRule[] = [
  { pattern: 'README.md', dir: '', name: 'index' },
  { pattern: 'docs/architecture/module-map.md', dir: '', name: 'modules' },
  { pattern: 'docs/getting-started.md', dir: '', name: 'getting-started' },
  { pattern: 'docs/guides/*.md', dir: 'guides' },
  { pattern: 'docs/observability/*.md', dir: 'guides' },
  { pattern: 'docs/schema-per-service.md', dir: 'guides' },
  { pattern: 'docs/security/*.md', dir: 'security' },
  { pattern: 'docs/architecture/why-*.md', dir: 'architecture' },
  { pattern: 'docs/architecture/when-to-use-*.md', dir: 'architecture' },
  { pattern: 'docs/architecture/ADR-*.md', dir: 'architecture' },
  // Kun 的 ADR 文件名大小写不统一（adr-p2d-multitenancy.md 是已 Accepted 的 ADR），两种都收（R58）
  { pattern: 'docs/architecture/adr-*.md', dir: 'architecture' },
  { pattern: 'docs/whitepaper.md', dir: '', name: 'whitepaper' },
  { pattern: 'CHANGELOG.md', dir: 'releases', name: 'index' },
  { pattern: 'docs/releases/*.md', dir: 'releases' },
];

const SECTION_LABELS: Record<string, string> = {
  guides: '指南',
  security: '安全',
  architecture: '设计说明与 ADR',
  releases: '变更日志与发布说明',
};

function matches(pattern: string, file: string): boolean {
  const p = pattern.split('/');
  const f = file.split('/');
  if (p.length !== f.length) return false;
  return p.every((seg, i) => new RegExp(`^${seg.replace(/[.+?^${}()|[\]\\]/g, '\\$&').replace(/\*/g, '[^/]*')}$`).test(f[i] ?? ''));
}

/** slug：源文件名小写，. 与空格换成 -（docs-site.md §2b.3，ADR-003-Kun.Audit-module.md → adr-003-kun-audit-module）。 */
export function kunSlug(fileName: string): string {
  return path.posix
    .basename(fileName)
    .replace(/\.md$/i, '')
    .toLowerCase()
    .replace(/[.\s]+/g, '-');
}

/** 白名单模式之外再排除的文件：各级 AGENTS.md / CLAUDE.md 是给编码代理的，不进站（docs-site.md §2b.2）。 */
const KUN_EXCLUDED = /(?:^|\/)(?:AGENTS|CLAUDE)\.md$/;

/** 源文件 → sdk/kun/ 下的页面路径；不在白名单里返回 null。 */
export function kunPagePath(file: string): string | null {
  if (KUN_EXCLUDED.test(file)) return null;
  const rule = KUN_WHITELIST.find((r) => matches(r.pattern, file));
  if (!rule) return null;
  const name = rule.name ?? kunSlug(file);
  return `${rule.dir ? `${rule.dir}/` : ''}${name}.md`;
}

export function kunRoute(pagePath: string): string {
  const page = pagePath.replace(/\.mdx?$/, '');
  return KUN_ROUTE_BASE + (page === 'index' ? '' : page.endsWith('/index') ? page.slice(0, -'index'.length) : page);
}

// ---------------------------------------------------------------- 文本处理（只动代码块外）

interface Segment {
  text: string;
  code: boolean;
}

/** 按围栏代码块切段；代码段原样保留。围栏配对规则与 repo.ts 的 rewriteLinks 相同。 */
export function splitFenced(markdown: string): Segment[] {
  const out: Segment[] = [];
  let fence: string | null = null;
  let buf: string[] = [];
  const flush = (code: boolean) => {
    if (buf.length > 0) out.push({ text: buf.join('\n'), code });
    buf = [];
  };
  for (const line of markdown.split('\n')) {
    const marker = /^\s*(`{3,}|~{3,})/.exec(line)?.[1];
    if (marker && fence === null) {
      flush(false);
      fence = marker;
      buf.push(line);
    } else if (marker && fence !== null && marker[0] === fence[0] && marker.length >= fence.length) {
      buf.push(line);
      flush(true);
      fence = null;
    } else {
      buf.push(line);
    }
  }
  flush(fence !== null);
  return out;
}

function joinSegments(segments: Segment[]): string {
  return segments.map((s) => s.text).join('\n');
}

const HTML_TAGS = new Set(
  (
    'a abbr b blockquote br code dd del details div dl dt em figcaption figure h1 h2 h3 h4 h5 h6 hr i img ins kbd li ol p ' +
    'picture pre q s samp small source span strong sub summary sup table tbody td tfoot th thead tr tt u ul var video'
  ).split(' '),
);
const TAG_RE = /<(\/?)([A-Za-z][\w.-]*)(?=[\s/>])/g;

/**
 * 正文（代码块与行内代码之外）里不是 HTML 标签的尖括号转义成 &lt;：Kun 文档常在正文里写 Result<T>，
 * 按 HTML 解析会变成一个 <t> 元素。行内代码可以跨行，所以按段落（空行分隔）遮蔽行内代码。
 */
export function escapeStrayTags(markdown: string): string {
  return joinSegments(
    splitFenced(markdown).map((seg) =>
      seg.code
        ? seg
        : {
            code: false,
            text: seg.text
              .split(/(\n[ \t]*\n)/)
              .map((part, i) =>
                i % 2 === 1
                  ? part
                  : withInlineCodeMasked(part, (masked) =>
                      masked.replace(TAG_RE, (whole, slash: string, name: string) =>
                        HTML_TAGS.has(name.toLowerCase()) ? whole : `&lt;${slash}${name}`,
                      ),
                    ),
              )
              .join(''),
          },
    ),
  );
}

/** 与 scripts/check_docs.py 一致的单元格计数：去掉首尾竖线后按未转义的 | 切分。 */
export function tableCells(line: string): number {
  return line.trim().replace(/^\|+|\|+$/g, '').split(/(?<!\\)\|/).length;
}

/**
 * 表格行比表头少列时补空单元格。GFM 渲染时本来就会补齐，所以显示不变，只是让 check_docs.py 的列数检查通过；
 * 多出来的列在 GFM 里会被丢掉，不补、只告警（check_docs.py 会报错，要到 kun 仓库修）。
 */
export function padShortTableRows(markdown: string, source: string, warn: (message: string) => void): string {
  const lines = markdown.split('\n');
  let fence: string | null = null;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i] ?? '';
    const marker = /^\s*(`{3,}|~{3,})/.exec(line)?.[1];
    if (marker) {
      if (fence === null) fence = marker;
      else if (marker[0] === fence[0] && marker.length >= fence.length) fence = null;
      continue;
    }
    if (fence !== null || !line.trimStart().startsWith('|') || !/^\s*\|?\s*:?-{2,}/.test(lines[i + 1] ?? '')) continue;
    const width = tableCells(line);
    let j = i + 2;
    for (; j < lines.length && (lines[j] ?? '').trimStart().startsWith('|'); j++) {
      const row = lines[j] ?? '';
      const cells = tableCells(row);
      if (cells < width) {
        const closed = row.trimEnd().endsWith('|') ? row.trimEnd() : `${row.trimEnd()} |`;
        lines[j] = closed + ' |'.repeat(width - cells);
        warn(`${source}:${j + 1}：表格行只有 ${cells} 列（表头 ${width} 列），已补空单元格`);
      } else if (cells > width) {
        warn(`${source}:${j + 1}：表格行有 ${cells} 列，多于表头 ${width} 列，GFM 会丢掉多出的列`);
      }
    }
    i = j - 1;
  }
  return lines.join('\n');
}

/** Markdown → 纯文本（frontmatter 的 title / description 与侧栏名）。 */
export function plainText(markdown: string): string {
  return markdown
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    // 只去掉真正的 HTML 标签；Result<T> 这类泛型（含 DocFX 写的 Result<T\>）保留
    .replace(/<\/?([A-Za-z][\w-]*)(?:\s[^<>]*)?\/?>/g, (tag, name: string) => (HTML_TAGS.has(name.toLowerCase()) ? ' ' : tag))
    .replace(/`+([^`]*?)`+/g, '$1')
    .replace(/\*\*|__/g, '')
    .replace(/(^|[^\w*])\*([^*\n]+)\*/g, '$1$2')
    .replace(/(^|\s)_([^_\n]+)_(?=\s|$|[，。,.;；:：])/g, '$1$2')
    .replace(/^[ \t]*>[ \t]?/gm, '')
    .replace(/\\([\\`*_{}[\]()#+\-.!<>|])/g, '$1')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&amp;/g, '&')
    // 中文软换行不留空格：换行两边都是中日韩字符或全角标点时直接相连
    .replace(/([\u3000-\u303f\u4e00-\u9fff\uff00-\uffef])[ \t]*\n\s*(?=[\u3000-\u303f\u4e00-\u9fff\uff00-\uffef])/g, '$1')
    .replace(/\s+/g, ' ')
    .trim();
}

export function truncate(text: string, max = 120): string {
  const chars = [...text];
  return chars.length <= max ? text : `${chars.slice(0, max - 1).join('')}…`;
}

/** 首段：跳过标题、代码、表格、列表、分隔线、HTML 与只有「**键**: 值」的元数据段（ADR 头部）。 */
export function firstParagraph(markdown: string): string {
  for (const seg of splitFenced(markdown)) {
    if (seg.code) continue;
    for (const block of seg.text.split(/\n[ \t]*\n/)) {
      const text = block.trim();
      if (text === '' || /^(#|\||<|!\[|\[!\[|[-*_]{3,}\s*$|[-*+] |\d+[.)] )/.test(text)) continue;
      const lines = text.split('\n');
      if (lines.every((l) => /^\s*\*\*[^*]+\*\*\s*[:：]/.test(l))) continue;
      return text;
    }
  }
  return '';
}

/** 第一个一级标题（代码块外）。 */
function findH1(markdown: string): { before: string; heading: string; after: string } | null {
  const lines = markdown.split('\n');
  let fence: string | null = null;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i] ?? '';
    const marker = /^\s*(`{3,}|~{3,})/.exec(line)?.[1];
    if (marker) {
      if (fence === null) fence = marker;
      else if (marker[0] === fence[0] && marker.length >= fence.length) fence = null;
      continue;
    }
    if (fence === null && /^# \S/.test(line)) {
      return { before: lines.slice(0, i).join('\n'), heading: line, after: lines.slice(i + 1).join('\n') };
    }
  }
  return null;
}

// ---------------------------------------------------------------- 链接

/**
 * kun 文档里的相对链接：目标在白名单内改写为站内路径，否则改写为 kun 仓库该 tag 上的文件地址（docs-site.md §2b.3）。
 * 越出仓库根目录的相对路径（上游笔误）去掉多余的 ../ 再找；找不到的仍指向该 tag 上的地址，并给出告警。
 */
export function makeKunResolver(kun: KunCheckout, source: string, files: ReadonlySet<string>, warn: (m: string) => void): LinkResolver {
  const dirs = new Set<string>();
  for (const f of files) {
    for (let d = path.posix.dirname(f); d !== '.'; d = path.posix.dirname(d)) dirs.add(d);
  }
  return (href) => {
    if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('#') || href.startsWith('/')) return null;
    const hash = href.indexOf('#');
    const target = hash === -1 ? href : href.slice(0, hash);
    const anchor = hash === -1 ? '' : href.slice(hash);
    if (target === '') return null;
    let resolved = path.posix.normalize(path.posix.join(path.posix.dirname(source), decodeURI(target))).replace(/\/$/, '');
    if (resolved === '..' || resolved.startsWith('../')) {
      const clamped = resolved.replace(/^(?:\.\.\/)+/, '').replace(/^\.\.$/, '');
      warn(`${source}：链接 ${href} 越出仓库根目录，按 ${clamped || '仓库根目录'} 处理`);
      resolved = clamped;
    }
    if (resolved === '' || resolved === '.') return kunGithubUrl(kun, '', true) + anchor;
    if (files.has(resolved)) {
      const page = kunPagePath(resolved);
      return (page ? kunRoute(page) : kunGithubUrl(kun, resolved)) + anchor;
    }
    if (dirs.has(resolved)) return kunGithubUrl(kun, resolved, true) + anchor;
    warn(`${source}：链接 ${href} 指向 ${kun.tag} 上不存在的文件`);
    return kunGithubUrl(kun, resolved) + anchor;
  };
}

// ---------------------------------------------------------------- 页面

function banner(kun: KunCheckout, source: string, lead: string): string {
  return [
    ':::info 镜像页',
    `${lead} [\`${source}\`](${kunGithubUrl(kun, source)})（\`${kun.tag}\`）。` +
      'Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship)）；' +
      'Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian)。改动请提到 kun 仓库。',
    ':::',
  ].join('\n');
}

function frontmatter(kun: KunCheckout, source: string, title: string, description: string): string {
  return [
    '---',
    `title: ${yamlString(title)}`,
    `description: ${yamlString(truncate(description || title))}`,
    'part: sdk',
    'project: kun',
    `version: ${yamlString(kun.version)}`,
    `source: ${yamlString(kunSource(kun, source))}`,
    `updated: ${kunFileDate(kun, source)}`,
    'audience: [developer]',
    '---',
    '',
  ].join('\n');
}

/** 镜像正文的机械处理：链接、表格补列、尖括号转义。 */
function processBody(kun: KunCheckout, source: string, markdown: string, files: ReadonlySet<string>, warn: (m: string) => void): string {
  const linked = rewriteLinks(markdown, makeKunResolver(kun, source, files, warn));
  return escapeStrayTags(padShortTableRows(linked, source, warn));
}

export interface MirroredPage {
  source: string;
  page: string;
  title: string;
  content: string;
}

/** 镜像一个源文件；不在白名单里的文件不进站，直接报错（docs-site.md §2b.2）。 */
export function mirrorPage(kun: KunCheckout, source: string, files: ReadonlySet<string>, warn: (m: string) => void): MirroredPage {
  const page = kunPagePath(source);
  if (page === null || source === 'README.md') {
    throw new Error(`${source} 不在 Kun 镜像白名单里（docs-site.md §2b.2），不能进站；要收录先改规范的白名单表`);
  }
  const body = processBody(kun, source, readKunFile(kun, source), files, warn);
  const h1 = findH1(body);
  const title = h1 ? plainText(h1.heading.slice(2)) : kunSlug(source);
  const rest = h1 ? h1.after : body;
  const description = plainText(firstParagraph(rest));
  const head = h1 ? `${h1.before ? `${h1.before}\n` : ''}${h1.heading}` : `# ${title}`;
  const content = `${frontmatter(kun, source, title, description)}\n${head}\n\n${banner(kun, source, '本页镜像自 kun 仓库')}\n${rest}`;
  return { source, page, title, content: content.endsWith('\n') ? content : `${content}\n` };
}

/** 侧栏名：ADR 写成「ADR-003 Introduce …」（「ADR: P2-D …」写成「ADR P2-D …」），其余用标题。 */
export function kunLabel(title: string): string {
  return title.replace(/^(ADR(?:-[A-Z0-9]+)?)[:：]\s*/, '$1 ');
}

/** 概览页由 README 的一句话定位与模块矩阵拼装（docs-site.md §2b.2 第一行），加上版本与本节目录。 */
function overviewPage(kun: KunCheckout, files: ReadonlySet<string>, pages: MirroredPage[], packages: number, warn: (m: string) => void): string {
  const readme = readKunFile(kun, 'README.md');
  const tagline = /^\*([^*\s][^*]*?)\*\s*$/m.exec(readme)?.[1]?.trim() ?? '';
  if (tagline === '') warn('README.md：没找到斜体的一句话定位，概览页不写定位');
  const lines = readme.split('\n');
  const start = lines.findIndex((l) => /^##\s.*模块矩阵/.test(l));
  let matrix = '';
  if (start === -1) {
    warn('README.md：没找到「模块矩阵」一节，概览页只列本节目录');
  } else {
    const end = lines.findIndex((l, i) => i > start && /^##\s/.test(l));
    matrix = processBody(kun, 'README.md', lines.slice(start + 1, end === -1 ? undefined : end).join('\n').trim(), files, warn);
  }

  const byDir = (dir: string) => pages.filter((p) => p.page.startsWith(`${dir}/`) && !p.page.endsWith('/index.md'));
  const link = (p: MirroredPage) => `[${kunLabel(p.title)}](${kunRoute(p.page)})`;
  const find = (page: string) => pages.find((p) => p.page === page);
  const toc: string[] = ['- [与 Qian 的关系](/sdk/kun/with-qian)：Qian 自写的唯一一页——Kun 模块与平台插件、seam 的对应，canonical API，强制装配与禁用项，补丁包状态'];
  const gettingStarted = find('getting-started.md');
  if (gettingStarted) toc.push(`- 上手：${link(gettingStarted)}`);
  const modules = find('modules.md');
  if (modules) toc.push(`- 模块地图：${link(modules)}`);
  for (const dir of ['guides', 'security', 'architecture']) {
    const items = byDir(dir);
    if (items.length > 0) toc.push(`- ${SECTION_LABELS[dir]}：${items.map(link).join('、')}`);
  }
  const whitepaper = find('whitepaper.md');
  if (whitepaper) toc.push(`- 白皮书：${link(whitepaper)}`);
  if (find('releases/index.md')) {
    const notes = byDir('releases');
    toc.push(`- [变更日志](/sdk/kun/releases/)${notes.length > 0 ? `；发布说明：${notes.map(link).join('、')}` : ''}`);
  }
  toc.push(`- [API 参考](/sdk/api/kun/)：DocFX 从 ${packages} 个 \`Kun.*\` 包生成`);

  const description = tagline || 'Qian 的底座框架 Kun：按锁定版本镜像的文档与 API 参考';
  return [
    frontmatter(kun, 'README.md', 'Kun 框架', description),
    '# Kun 框架',
    '',
    banner(kun, 'README.md', '本页由 kun 仓库的定位与模块矩阵拼装，来源'),
    '',
    ...(tagline ? [`> ${tagline}`, ''] : []),
    'Kun 是 Qian 的底座框架：Qian 平台基于 Kun 框架构建，前端使用 yue 组件库（[ADR-021](/design/adr/adr-021-product-family-structure)）。' +
      `本节按仓库 \`Directory.Packages.props\` 的 \`KunVersion\` 镜像 kun 仓库 **${kun.tag}** 的文档，与 Qian 实际依赖的 Kun 是同一版本；` +
      `Kun 的代码与文档改动去 [kun 仓库](${KUN_REPO_URL})提。`,
    '',
    '## 本节内容',
    '',
    ...toc,
    '',
    ...(matrix ? ['## 模块矩阵', '', matrix, ''] : []),
  ].join('\n');
}

function metaJson(items: unknown[]): string {
  return `${JSON.stringify(items, null, 2)}\n`;
}

function sectionOrder(page: string): string {
  const name = path.posix.basename(page);
  if (page.startsWith('architecture/')) {
    const group = name.startsWith('why-') ? 0 : name.startsWith('when-to-use-') ? 1 : 2;
    return `${group}-${name}`;
  }
  return name;
}

export interface KunDocsResult {
  files: GeneratedFile[];
  pages: MirroredPage[];
  warnings: string[];
}

/** 生成 docs/sdk/kun/ 的全部文件（with-qian.mdx 除外）。packages 是 API 参考覆盖的包数，写进概览。 */
export function syncKunDocs(kun: KunCheckout, packages: number): KunDocsResult {
  const warnings: string[] = [];
  const warn = (m: string) => warnings.push(m);
  const all = listKunFiles(kun);
  const files = new Set(all);
  const pages: MirroredPage[] = [];
  for (const source of all) {
    if (kunPagePath(source) === null || source === 'README.md') continue;
    pages.push(mirrorPage(kun, source, files, warn));
  }
  if (!files.has('README.md')) throw new Error(`kun 仓库 ${kun.tag} 没有 README.md，无法拼装概览页`);
  const seen = new Map<string, string>();
  for (const p of pages) {
    const other = seen.get(p.page);
    if (other) throw new Error(`两个源文件映射到同一页面 ${p.page}：${other}、${p.source}`);
    if ((KUN_HANDWRITTEN as readonly string[]).some((h) => h.replace(/\.mdx$/, '.md') === p.page)) {
      throw new Error(`${p.source} 会覆盖手写页 ${p.page}`);
    }
    seen.set(p.page, p.source);
  }

  const out: GeneratedFile[] = pages.map((p) => ({ path: p.page, content: p.content }));
  out.push({ path: 'index.md', content: overviewPage(kun, files, pages, packageCount(packages), warn) });

  const root: unknown[] = [
    { type: 'file', name: 'index', label: '概览' },
    { type: 'file', name: 'with-qian', label: '与 Qian 的关系' },
  ];
  const single = (page: string, label: string) => {
    if (pages.some((p) => p.page === page)) root.push({ type: 'file', name: page.replace(/\.md$/, ''), label });
  };
  const dir = (name: string) => {
    const items = pages
      .filter((p) => p.page.startsWith(`${name}/`))
      .sort((a, b) => (a.page.endsWith('/index.md') ? -1 : b.page.endsWith('/index.md') ? 1 : sectionOrder(a.page).localeCompare(sectionOrder(b.page))));
    if (items.length === 0) return;
    root.push({ type: 'dir', name, label: SECTION_LABELS[name] ?? name, collapsible: true, collapsed: true });
    out.push({
      path: `${name}/_meta.json`,
      content: metaJson(
        items.map((p) => ({
          type: 'file',
          name: path.posix.basename(p.page, '.md'),
          label: p.page === 'releases/index.md' ? '变更日志' : kunLabel(p.title),
        })),
      ),
    });
  };
  single('getting-started.md', '上手');
  single('modules.md', '模块地图');
  dir('guides');
  dir('security');
  dir('architecture');
  single('whitepaper.md', '白皮书');
  dir('releases');
  root.push({ type: 'custom-link', label: 'API 参考', link: '/sdk/api/kun/' });
  out.push({ path: '_meta.json', content: metaJson(root) });
  return { files: out, pages, warnings };
}

function packageCount(n: number): number {
  if (!Number.isInteger(n) || n <= 0) throw new Error(`Kun API 参考的包数不对：${n}`);
  return n;
}
