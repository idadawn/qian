// 生成脚本共用：仓库坐标、生成目录的写入、Markdown 链接改写。
// 只用 Node 内置模块与可擦除的 TS 语法：Node ≥ 22.18 可直接运行，Rspress 加载配置时（含 Node 20 的 jiti）也能导入。
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { DEFAULT_BRANCH, REPO_URL } from '../theme/site.ts';

export { DEFAULT_BRANCH, REPO_URL };

export const WEBSITE_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
export const REPO_ROOT = path.resolve(WEBSITE_ROOT, '..');
export const SITE_DOCS = path.join(WEBSITE_ROOT, 'docs');

/**
 * 由脚本生成、不进 Git 的目录（docs-site.md §7：生成目录不接受手改）。sdk/kun 里唯一的手写页 with-qian.mdx 进 Git，
 * 生成时保留（见 writeGenerated 的 keep）。
 */
export const GENERATED_PARTS = ['components', 'cli', 'design', 'sdk/kun', 'sdk/api'] as const;
export type GeneratedPart = (typeof GENERATED_PARTS)[number];

export interface GeneratedFile {
  /** 相对于该生成目录的路径，用 / 分隔。 */
  path: string;
  content: string;
}

export interface WriteResult {
  written: string[];
  removed: string[];
  unchanged: number;
}

/**
 * 把一组文件写进 website/docs/<part>/：内容没变的不重写，目录里多出来的旧文件删掉（keep 里的手写文件除外）。
 * 只重写变化的文件，dev 服务器监听到的就只有真实变化，不会因为重新生成而反复重启。
 */
export function writeGenerated(
  part: GeneratedPart,
  files: GeneratedFile[],
  siteDocs: string = SITE_DOCS,
  keep: readonly string[] = [],
): WriteResult {
  const dir = path.join(siteDocs, part);
  const result: WriteResult = { written: [], removed: [], unchanged: 0 };
  const wanted = new Set<string>(keep);
  for (const file of files) {
    const rel = toPosix(file.path);
    if (rel.startsWith('/') || rel.split('/').includes('..')) {
      throw new Error(`生成文件路径越界：${file.path}`);
    }
    if (keep.includes(rel)) {
      throw new Error(`生成文件会覆盖手写文件：${part}/${rel}`);
    }
    if (wanted.has(rel)) {
      throw new Error(`生成文件重复：${part}/${rel}`);
    }
    wanted.add(rel);
    const target = path.join(dir, rel);
    const current = fs.existsSync(target) ? fs.readFileSync(target, 'utf8') : null;
    if (current === file.content) {
      result.unchanged++;
      continue;
    }
    fs.mkdirSync(path.dirname(target), { recursive: true });
    fs.writeFileSync(target, file.content);
    result.written.push(rel);
  }
  for (const rel of listFiles(dir)) {
    if (!wanted.has(rel)) {
      fs.rmSync(path.join(dir, rel));
      result.removed.push(rel);
    }
  }
  removeEmptyDirs(dir);
  return result;
}

export function listFiles(dir: string): string[] {
  if (!fs.existsSync(dir)) return [];
  const out: string[] = [];
  const walk = (current: string) => {
    for (const entry of fs.readdirSync(current, { withFileTypes: true })) {
      const full = path.join(current, entry.name);
      if (entry.isDirectory()) walk(full);
      else out.push(toPosix(path.relative(dir, full)));
    }
  };
  walk(dir);
  return out.sort();
}

function removeEmptyDirs(dir: string): void {
  if (!fs.existsSync(dir)) return;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (entry.isDirectory()) removeEmptyDirs(path.join(dir, entry.name));
  }
  if (fs.readdirSync(dir).length === 0) fs.rmdirSync(dir);
}

export function toPosix(p: string): string {
  return p.split(path.sep).join('/');
}

/** 仓库内路径 → GitHub 上的地址（目录用 tree，文件用 blob）。 */
export function githubUrl(repoRelPath: string, isDir: boolean): string {
  const kind = isDir ? 'tree' : 'blob';
  const rel = toPosix(repoRelPath).replace(/\/$/, '');
  return `${REPO_URL}/${kind}/${DEFAULT_BRANCH}/${rel}`;
}

/** 把源 Markdown 里的一个链接目标映射成站内路由或外部地址；返回 null 表示保持原样。 */
export type LinkResolver = (href: string) => string | null;

const LINK_RE = /(!?\[(?:[^\]\\]|\\.)*\])\(([^)\s]+)((?:\s+"[^"]*")?)\)/g;

/**
 * 改写 Markdown 正文里的行内链接与图片，跳过代码块与行内代码。
 * 只处理 [文字](目标) 形式；docs/ 里没有引用式链接（[x]: y），出现时不改写。
 */
export function rewriteLinks(markdown: string, resolve: LinkResolver): string {
  const lines = markdown.split('\n');
  let fence: string | null = null;
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i] ?? '';
    const marker = /^\s*(`{3,}|~{3,})/.exec(line);
    if (marker) {
      const token = marker[1] ?? '';
      if (fence === null) fence = token;
      else if (token[0] === fence[0] && token.length >= fence.length) fence = null;
      continue;
    }
    if (fence !== null) continue;
    lines[i] = withInlineCodeMasked(line, (text) =>
      text.replace(LINK_RE, (whole, label: string, href: string, title: string) => {
        const next = resolve(href);
        return next === null ? whole : `${label}(${next}${title})`;
      }),
    );
  }
  return lines.join('\n');
}

const MASK = '\u0000';

/**
 * 把行内代码换成占位符后再交给 fn，结束后原样换回：行内代码里的「[x](y)」不会被当成链接，
 * 而链接文字里带行内代码的（如 [`yue.md`](yue.md)）仍是一个完整的链接。反引号串长度配对规则与 CommonMark 一致。
 */
export function withInlineCodeMasked(line: string, fn: (masked: string) => string): string {
  const spans: string[] = [];
  let masked = '';
  let pos = 0;
  while (pos < line.length) {
    const open = line.indexOf('`', pos);
    if (open === -1) break;
    let ticks = 1;
    while (line[open + ticks] === '`') ticks++;
    const delimiter = '`'.repeat(ticks);
    let close = line.indexOf(delimiter, open + ticks);
    while (close !== -1 && line[close + ticks] === '`') {
      close = line.indexOf(delimiter, close + ticks + 1);
    }
    if (close === -1) {
      // 没有配对的反引号串按普通文字处理，继续往后找
      masked += line.slice(pos, open + ticks);
      pos = open + ticks;
      continue;
    }
    spans.push(line.slice(open, close + ticks));
    masked += `${line.slice(pos, open)}${MASK}${spans.length - 1}${MASK}`;
    pos = close + ticks;
  }
  masked += line.slice(pos);
  return fn(masked).replace(new RegExp(`${MASK}(\\d+)${MASK}`, 'g'), (_, index: string) => spans[Number(index)] ?? '');
}

/** 拆出 YAML frontmatter（首行 --- 到下一个 ---），没有则 frontmatter 为空串。 */
export function splitFrontmatter(markdown: string): { frontmatter: string; body: string } {
  const m = /^---\n([\s\S]*?)\n---\n?/.exec(markdown);
  if (!m) return { frontmatter: '', body: markdown };
  return { frontmatter: m[1] ?? '', body: markdown.slice(m[0].length) };
}

export type FrontmatterValue = string | string[];

/**
 * 解析 frontmatter 的 YAML 子集（与 scripts/check_docs.py 的 parse_frontmatter 同一套规则）：
 * `key: 标量`、`key: [a, b]`、`key:` 加缩进的 `- 项` 列表、双引号（JSON）与单引号字符串、# 注释。
 * 嵌套映射、多行标量、锚点等其它写法一律抛错，提示改成上述形式。
 */
export function parseFrontmatter(frontmatter: string): Record<string, FrontmatterValue> {
  const data: Record<string, FrontmatterValue> = {};
  const lines = frontmatter.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const line = lines[i] ?? '';
    if (line.trim() === '' || line.trimStart().startsWith('#')) continue;
    if (/^[ \t]/.test(line)) throw new Error(`frontmatter 意外的缩进行：${line.trim()}`);
    const m = /^([A-Za-z_][\w-]*):(?:\s+(.*))?$/.exec(line);
    if (!m) throw new Error(`frontmatter 不是 key: value 形式：${line}`);
    const key = m[1] ?? '';
    let raw = (m[2] ?? '').trim();
    if (key in data) throw new Error(`frontmatter 字段重复：${key}`);
    if (raw.startsWith('#')) raw = '';
    if (raw === '') {
      const items: string[] = [];
      while (i + 1 < lines.length && /^[ \t]+-\s/.test(lines[i + 1] ?? '')) {
        i++;
        items.push(frontmatterScalar((lines[i] ?? '').trim().slice(2)));
      }
      data[key] = items.length > 0 ? items : '';
    } else if (raw.startsWith('[')) {
      if (!raw.endsWith(']')) throw new Error(`frontmatter 列表要写在一行内：${key}`);
      data[key] = frontmatterInlineList(raw.slice(1, -1));
    } else {
      data[key] = frontmatterScalar(raw);
    }
  }
  return data;
}

function frontmatterScalar(raw: string): string {
  const value = raw.trim();
  if (value.startsWith('"')) {
    const parsed: unknown = JSON.parse(value);
    if (typeof parsed !== 'string') throw new Error(`frontmatter 双引号里只能是字符串：${value}`);
    return parsed;
  }
  if (value.startsWith("'")) {
    if (value.length < 2 || !value.endsWith("'")) throw new Error(`frontmatter 单引号字符串不合法：${value}`);
    return value.slice(1, -1).replace(/''/g, "'");
  }
  if (/^[{&*|>]/.test(value)) throw new Error(`frontmatter 不支持的写法：${value}`);
  return value;
}

function frontmatterInlineList(inner: string): string[] {
  const items: string[] = [];
  let buffer = '';
  let quote: string | null = null;
  for (const ch of inner) {
    if (quote) {
      buffer += ch;
      if (ch === quote) quote = null;
    } else if (ch === '"' || ch === "'") {
      quote = ch;
      buffer += ch;
    } else if (ch === ',') {
      items.push(frontmatterScalar(buffer));
      buffer = '';
    } else {
      buffer += ch;
    }
  }
  if (buffer.trim() !== '' || items.length > 0) items.push(frontmatterScalar(buffer));
  return items.filter((item) => item !== '');
}

/** 今天（UTC）的 YYYY-MM-DD。 */
export function today(): string {
  return new Date().toISOString().slice(0, 10);
}

const fileDateCache = new Map<string, string>();

/**
 * 仓库文件最后一次提交的日期（YYYY-MM-DD），用作生成页 frontmatter 的 updated。
 * 没提交过、不在 git 仓库里或没有 git 时用今天，生成结果仍然可用。
 */
export function repoFileDate(repoRelPath: string, repoRoot: string = REPO_ROOT): string {
  const key = `${repoRoot}\u0000${repoRelPath}`;
  const cached = fileDateCache.get(key);
  if (cached) return cached;
  let date = '';
  try {
    date = execFileSync('git', ['-C', repoRoot, 'log', '-1', '--format=%cs', '--', repoRelPath], { stdio: ['ignore', 'pipe', 'ignore'] })
      .toString()
      .trim();
  } catch {
    date = '';
  }
  if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) date = today();
  fileDateCache.set(key, date);
  return date;
}

/** 去掉行内 Markdown 标记（链接、代码、粗斜体）并把换行并成空格，用作 description。 */
export function plainText(markdown: string): string {
  return markdown
    .replace(/!\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/`([^`]*)`/g, '$1')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/\*([^*]+)\*/g, '$1')
    .replace(/\s*\n\s*/g, ' ')
    .trim();
}

/** YAML 双引号字符串：生成的 frontmatter 只写字符串标量，统一走这里转义。 */
export function yamlString(value: string): string {
  return JSON.stringify(value);
}
