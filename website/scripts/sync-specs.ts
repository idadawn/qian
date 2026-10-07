// 第 4 部分「CLI 与规格」与第 7 部分「设计与路线」的生成器（docs-site.md §2、§6）。
// - design/：逐文件镜像仓库 docs/，路径改为小写短横线、README.md 改为 index.md，链接改写为站内路由；
// - cli/：把 docs/prd/specs/component-model.md 按二级标题拆成页面，不另写内容。
// 两个目录都不进 Git，构建前由 sources-plugin.ts 调用重新生成。
import fs from 'node:fs';
import path from 'node:path';
import {
  REPO_ROOT,
  githubUrl,
  rewriteLinks,
  splitFrontmatter,
  toPosix,
  yamlString,
  type GeneratedFile,
  type LinkResolver,
} from './repo.ts';

export const COMPONENT_MODEL_SPEC = 'docs/prd/specs/component-model.md';

/** docs/ 的二级目录在侧栏里的名字与顺序；没列出的目录排在后面、用目录名。 */
const DESIGN_DIRS: Array<{ name: string; label: string }> = [
  { name: 'adr', label: '架构决策（ADR）' },
  { name: 'prd', label: '产品需求（PRD）' },
  { name: 'migration', label: '迁移方案' },
  { name: 'decisions', label: '决策清单' },
  { name: 'research', label: '调研基线' },
];

/** docs/ 根目录下的文件在侧栏里的名字与顺序。 */
const DESIGN_ROOT_FILES: Array<{ file: string; label: string }> = [
  { file: 'README.md', label: '文档索引' },
  { file: 'ROADMAP.md', label: '路线图' },
  { file: 'CHANGELOG.md', label: '变更日志' },
];

/** component-model.md 的哪些二级标题成为 /cli/ 下的哪一页；其余章节（运行时、seam、安全等）只在设计镜像里。 */
export const CLI_PAGES: Array<{ section: number; slug: string; title: string; description: string }> = [
  { section: 9, slug: 'commands', title: 'qian 命令', description: 'qian CLI 的十个命令、npx 一键形式、安装过程、幂等与回滚、管理面端点。' },
  { section: 3, slug: 'plugin-json', title: '组件清单 plugin.json', description: '.qian-plugin/plugin.json 的示例、字段逐项表与 JSON Schema 骨架。' },
  { section: 2, slug: 'layout', title: '组件文件夹布局', description: '组件文件夹的目录树、表面一览、无代码组件与带代码组件。' },
  { section: 4, slug: 'surfaces', title: '声明式表面的文件格式', description: 'skills、agents、commands、hooks、MCP、tools、ui 与 patch 的文件格式。' },
  { section: 7, slug: 'profile-patch', title: 'profile、组合包与 patch', description: '分层模型、qian.profile.yml、组合包、qian.patch.yml 与 dump-config 输出。' },
  { section: 8, slug: 'marketplace-json', title: '目录市场 marketplace.json', description: '.qian/marketplace.json 的示例、字段表、多市场优先级与社区发现。' },
  { section: 10, slug: 'validation', title: '校验与 TCK', description: '清单与表面校验器、运行时语义 TCK 与 CI 门禁。' },
];

// ---------------------------------------------------------------- 路径与路由

/** docs/ 下的源文件 → design/ 下的页面路径，如 docs/adr/ADR-015-x.md → adr/adr-015-x.md。 */
export function designPagePath(repoRelPath: string): string {
  const rel = toPosix(repoRelPath);
  if (!rel.startsWith('docs/') || !rel.endsWith('.md')) {
    throw new Error(`不是 docs/ 下的 Markdown：${repoRelPath}`);
  }
  const parts = rel.slice('docs/'.length).split('/');
  const file = parts.pop() ?? '';
  const name = file === 'README.md' ? 'index.md' : file.toLowerCase();
  return [...parts.map((p) => p.toLowerCase()), name].join('/');
}

/** docs/ 下的源文件 → 站内路由，如 /design/adr/adr-015-x、/design/adr/。 */
export function designRoute(repoRelPath: string): string {
  const page = designPagePath(repoRelPath).replace(/\.md$/, '');
  if (page === 'index') return '/design/';
  return page.endsWith('/index') ? `/design/${page.slice(0, -'index'.length)}` : `/design/${page}`;
}

/**
 * 源文件里的相对链接 → 站内路由（目标在 docs/ 内且有对应页面）或 GitHub 地址（其余仓库文件）。
 * 目标不存在时报错：docs/ 的链接已由 check_docs.py 把关，到这里还断说明两边规则不一致。
 */
export function makeDocsResolver(sourceRepoRel: string, repoRoot: string = REPO_ROOT): LinkResolver {
  return (href) => {
    if (/^[a-z][a-z0-9+.-]*:/i.test(href) || href.startsWith('//') || href.startsWith('/')) return null;
    if (href.startsWith('#')) return null;
    const hashAt = href.indexOf('#');
    const rawPath = hashAt === -1 ? href : href.slice(0, hashAt);
    const anchor = hashAt === -1 ? '' : href.slice(hashAt);
    const abs = path.resolve(repoRoot, path.dirname(sourceRepoRel), decodeURI(rawPath));
    const rel = toPosix(path.relative(repoRoot, abs));
    if (rel.startsWith('../') || path.isAbsolute(rel)) {
      throw new Error(`${sourceRepoRel}：链接指向仓库之外 ${href}`);
    }
    if (!fs.existsSync(abs)) {
      throw new Error(`${sourceRepoRel}：链接目标不存在 ${href}`);
    }
    const isDir = fs.statSync(abs).isDirectory();
    if (rel.startsWith('docs/') || rel === 'docs') {
      if (!isDir && rel.endsWith('.md')) return designRoute(rel) + anchor;
      const readme = path.join(abs, 'README.md');
      if (isDir && fs.existsSync(readme)) return designRoute(toPosix(path.relative(repoRoot, readme))) + anchor;
    }
    return githubUrl(rel, isDir) + anchor;
  };
}

// ---------------------------------------------------------------- design/

export function listDocsSources(repoRoot: string = REPO_ROOT): string[] {
  const docsDir = path.join(repoRoot, 'docs');
  const out: string[] = [];
  const walk = (dir: string) => {
    for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
      const full = path.join(dir, entry.name);
      if (entry.isDirectory()) walk(full);
      else if (entry.name.endsWith('.md')) out.push(toPosix(path.relative(repoRoot, full)));
    }
  };
  walk(docsDir);
  return out.sort();
}

function sourceNote(repoRelPath: string): string {
  return `> 本页由构建脚本镜像自仓库 [\`${repoRelPath}\`](${githubUrl(repoRelPath, false)})，请修改源文件，不要改本页。`;
}

/** 在第一个一级标题之后插入一段说明；没有一级标题时放在正文最前。 */
function insertAfterTitle(body: string, block: string): string {
  const lines = body.split('\n');
  const h1 = lines.findIndex((l) => /^#\s/.test(l));
  if (h1 === -1) return `${block}\n\n${body}`;
  lines.splice(h1 + 1, 0, '', block);
  return lines.join('\n');
}

export function mirrorDoc(repoRelPath: string, source: string, repoRoot: string = REPO_ROOT): string {
  const { frontmatter, body } = splitFrontmatter(source);
  const rewritten = rewriteLinks(body, makeDocsResolver(repoRelPath, repoRoot));
  const withNote = insertAfterTitle(rewritten, sourceNote(repoRelPath));
  return frontmatter ? `---\n${frontmatter}\n---\n${withNote}` : withNote;
}

/** 侧栏文字：ADR 用「编号 + 冒号前的短标题」，其余用一级标题。 */
export function sidebarLabel(repoRelPath: string, source: string): string {
  const { frontmatter, body } = splitFrontmatter(source);
  const adr = /ADR-(\d{3})/.exec(path.basename(repoRelPath));
  if (adr) {
    const title = /^title:\s*(.+)$/m.exec(frontmatter)?.[1]?.trim() ?? '';
    const short = title.split(/[：:]/)[0]?.trim();
    return short ? `ADR-${adr[1]} ${short}` : `ADR-${adr[1]}`;
  }
  const h1 = /^#\s+(.+)$/m.exec(body)?.[1]?.trim();
  return h1 ?? path.basename(repoRelPath, '.md');
}

type MetaItem = string | { type: 'file' | 'dir'; name: string; label: string; collapsible?: boolean; collapsed?: boolean };

export function syncDesign(repoRoot: string = REPO_ROOT): GeneratedFile[] {
  const sources = listDocsSources(repoRoot);
  const files: GeneratedFile[] = [];
  const labels = new Map<string, string>();
  for (const rel of sources) {
    const source = fs.readFileSync(path.join(repoRoot, rel), 'utf8');
    files.push({ path: designPagePath(rel), content: mirrorDoc(rel, source, repoRoot) });
    labels.set(rel, sidebarLabel(rel, source));
  }

  // 侧栏：每个目录一份 _meta.json，目录首页（README → index）排第一。
  const byDir = new Map<string, string[]>();
  const allDirs = new Set<string>(['docs']);
  for (const rel of sources) {
    const dir = path.posix.dirname(rel);
    byDir.set(dir, [...(byDir.get(dir) ?? []), rel]);
    for (let d = dir; d !== 'docs' && d !== '.'; d = path.posix.dirname(d)) allDirs.add(d);
  }
  const subDirsOf = (dir: string) => {
    const names = new Set<string>();
    for (const s of sources) {
      if (!s.startsWith(`${dir}/`)) continue;
      const rest = s.slice(dir.length + 1).split('/');
      if (rest.length > 1 && rest[0]) names.add(rest[0]);
    }
    return [...names].sort();
  };

  for (const dir of allDirs) {
    const items: MetaItem[] = [];
    const filesHere = byDir.get(dir) ?? [];
    const fileItem = (rel: string, label?: string): MetaItem => ({
      type: 'file',
      name: path.posix.basename(designPagePath(rel), '.md'),
      label: label ?? labels.get(rel) ?? rel,
    });
    if (dir === 'docs') {
      for (const { file, label } of DESIGN_ROOT_FILES) {
        if (filesHere.includes(`docs/${file}`)) items.push(fileItem(`docs/${file}`, label));
      }
      for (const rel of filesHere) {
        if (!DESIGN_ROOT_FILES.some((f) => `docs/${f.file}` === rel)) items.push(fileItem(rel));
      }
    } else {
      const readme = `${dir}/README.md`;
      if (filesHere.includes(readme)) items.push(fileItem(readme, '概览'));
      for (const rel of filesHere) if (rel !== readme) items.push(fileItem(rel));
    }
    const subDirs = subDirsOf(dir);
    const ordered = dir === 'docs'
      ? [...DESIGN_DIRS.map((d) => d.name).filter((n) => subDirs.includes(n)), ...subDirs.filter((n) => !DESIGN_DIRS.some((d) => d.name === n))]
      : subDirs;
    for (const name of ordered) {
      const label = DESIGN_DIRS.find((d) => d.name === name && dir === 'docs')?.label ?? (name === 'specs' ? '专题规格' : name);
      items.push({ type: 'dir', name: name.toLowerCase(), label, collapsible: true, collapsed: dir === 'docs' });
    }
    const metaPath = dir === 'docs' ? '_meta.json' : `${dir.slice('docs/'.length).toLowerCase()}/_meta.json`;
    files.push({ path: metaPath, content: `${JSON.stringify(items, null, 2)}\n` });
  }
  return files;
}

// ---------------------------------------------------------------- cli/

export interface SpecSection {
  number: number;
  heading: string;
  body: string;
}

/** 按二级标题切分（跳过代码块内的 ##），返回带编号的章节；编号取标题开头的「N.」。 */
export function splitSections(markdown: string): SpecSection[] {
  const lines = markdown.split('\n');
  const sections: SpecSection[] = [];
  let current: { number: number; heading: string; lines: string[] } | null = null;
  let fence: string | null = null;
  for (const line of lines) {
    const marker = /^\s*(`{3,}|~{3,})/.exec(line);
    if (marker) {
      const token = marker[1] ?? '';
      if (fence === null) fence = token;
      else if (token[0] === fence[0] && token.length >= fence.length) fence = null;
    }
    const h2 = fence === null && !marker ? /^##\s+(.+)$/.exec(line) : null;
    if (h2) {
      if (current) sections.push({ number: current.number, heading: current.heading, body: current.lines.join('\n') });
      const heading = (h2[1] ?? '').trim();
      const num = /^(\d+)\./.exec(heading);
      current = { number: num ? Number(num[1]) : -1, heading, lines: [] };
      continue;
    }
    current?.lines.push(line);
  }
  if (current) sections.push({ number: current.number, heading: current.heading, body: current.lines.join('\n') });
  return sections;
}

/** 标题整体升一级（### → ##），代码块内不动。 */
export function promoteHeadings(markdown: string): string {
  let fence: string | null = null;
  return markdown
    .split('\n')
    .map((line) => {
      const marker = /^\s*(`{3,}|~{3,})/.exec(line);
      if (marker) {
        const token = marker[1] ?? '';
        if (fence === null) fence = token;
        else if (token[0] === fence[0] && token.length >= fence.length) fence = null;
        return line;
      }
      if (fence !== null) return line;
      return /^#{3,6}\s/.test(line) ? line.slice(1) : line;
    })
    .join('\n');
}

export function syncCli(repoRoot: string = REPO_ROOT): GeneratedFile[] {
  const specPath = path.join(repoRoot, COMPONENT_MODEL_SPEC);
  const { body } = splitFrontmatter(fs.readFileSync(specPath, 'utf8'));
  const sections = splitSections(body);
  const specRoute = designRoute(COMPONENT_MODEL_SPEC);
  const docsResolver = makeDocsResolver(COMPONENT_MODEL_SPEC, repoRoot);
  // 章节内的「#锚点」指回设计镜像里的完整规格。
  const resolver: LinkResolver = (href) => (href.startsWith('#') ? `${specRoute}${href}` : docsResolver(href));
  const files: GeneratedFile[] = [];
  for (const page of CLI_PAGES) {
    const section = sections.find((s) => s.number === page.section);
    if (!section) {
      throw new Error(`${COMPONENT_MODEL_SPEC} 里找不到第 ${page.section} 节，/cli/${page.slug} 无法生成；改了章节编号请同步 CLI_PAGES。`);
    }
    const content = rewriteLinks(promoteHeadings(section.body), resolver).replace(/\n+---\s*$/, '').trim();
    files.push({
      path: `${page.slug}.md`,
      content: [
        '---',
        `title: ${yamlString(page.title)}`,
        `description: ${yamlString(page.description)}`,
        'part: cli',
        '---',
        '',
        `# ${page.title}`,
        '',
        `> 本页由构建脚本同步自 [组件模型规格 §${page.section}](${specRoute})（仓库 \`${COMPONENT_MODEL_SPEC}\`），不另写；请修改规格源文件。`,
        '',
        content,
        '',
      ].join('\n'),
    });
  }
  const list = CLI_PAGES.map((p) => `| [${p.title}](/cli/${p.slug}) | §${p.section} | ${p.description} |`).join('\n');
  files.push({
    path: 'index.md',
    content: [
      '---',
      `title: ${yamlString('CLI 与规格')}`,
      `description: ${yamlString('qian 命令与 plugin.json、marketplace.json、profile / patch 的字段参考，从组件模型规格同步。')}`,
      'part: cli',
      '---',
      '',
      '# CLI 与规格',
      '',
      `本部分从[组件模型规格](${specRoute})同步生成，规格是唯一真相源；运行时模型、seam 规则与安全约束见规格全文。`,
      '',
      '| 页面 | 规格章节 | 内容 |',
      '|---|---|---|',
      list,
      '',
    ].join('\n'),
  });
  files.push({
    path: '_meta.json',
    content: `${JSON.stringify(['index', ...CLI_PAGES.map((p) => p.slug)], null, 2)}\n`,
  });
  return files;
}
