// Kun API 参考（docs-site.md §2b.2，S1-DOCS-005）：DocFX 从 KunVersion 锁定版本的 Kun.* 包（nuget.org）的 XML 注释生成，挂 /sdk/api/kun/。
// 流程：缓存目录里生成一个引用全部 Kun.* 包的空工程 → dotnet build 取 DLL 与 XML → docfx metadata 输出 Markdown → 转成 Rspress 页面。
// 结果按（版本与提交、包、DocFX 版本、转换脚本内容）缓存，dev 重启不重复跑 .NET。/sdk/api/ 是一个生成目录，S1-DOCS-002 往这里加 Qian.* 与前端。
import { execFileSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { WEBSITE_ROOT, yamlString, type GeneratedFile } from './repo.ts';
import { CACHE_DIR, KUN_OWNER_REPO, kunGithubUrl, kunSource, listKunFiles, type KunCheckout } from './kun.ts';
import { firstParagraph, plainText, truncate } from './sync-kun-docs.ts';

export const KUN_API_ROUTE = '/sdk/api/kun/';
const TOOL_MANIFEST = path.join(WEBSITE_ROOT, '.config', 'dotnet-tools.json');
/** 转换逻辑所在的脚本：内容变了缓存就失效。 */
const CONVERTER_FILES = ['build-kun-api.ts', 'sync-kun-docs.ts', 'repo.ts', 'kun.ts'].map((f) =>
  path.join(path.dirname(fileURLToPath(import.meta.url)), f),
);

/** tag 上 src/<名>/<名>.csproj 的项目：每个都以同名包发布到 nuget.org，也就是 API 参考的范围。 */
export function kunPackages(files: readonly string[]): string[] {
  return files
    .map((f) => /^src\/([^/]+)\/\1\.csproj$/.exec(f)?.[1])
    .filter((name): name is string => name !== undefined)
    .sort();
}

export function docfxVersion(manifest: string = TOOL_MANIFEST): string {
  const json = JSON.parse(fs.readFileSync(manifest, 'utf8')) as { tools?: Record<string, { version?: string }> };
  const version = json.tools?.docfx?.version;
  if (!version) throw new Error(`${manifest} 里没有 docfx`);
  return version;
}

function run(command: string, args: string[], cwd: string): string {
  try {
    return execFileSync(command, args, { cwd, encoding: 'utf8', maxBuffer: 256 * 1024 * 1024, stdio: ['ignore', 'pipe', 'pipe'] });
  } catch (error) {
    const e = error as { stdout?: string; stderr?: string; message: string; code?: string };
    if (e.code === 'ENOENT') throw new Error(`找不到 ${command}：生成 Kun API 参考需要 .NET SDK（版本见仓库根 global.json）`);
    const output = `${e.stdout ?? ''}${e.stderr ?? ''}`.trim() || e.message;
    throw new Error(`${command} ${args.join(' ')} 失败：\n${output.slice(-4000)}`);
  }
}

// ---------------------------------------------------------------- DocFX

export interface DocfxOutput {
  /** 文件名（不含 .md）→ 内容 */
  pages: Record<string, string>;
  /** 包名 → nuspec 的 description */
  descriptions: Record<string, string>;
  warnings: number;
}

/** 生成标签对应的 API 前，确认实际包解析结果，拒绝版本回退或本地项目替代。 */
export function verifyKunPackageVersions(version: string, packages: readonly string[], libraries: unknown): void {
  if (!libraries || typeof libraries !== 'object' || Array.isArray(libraries)) throw new Error('Kun API 还原结果缺少包目录');
  const entries = Object.entries(libraries);
  for (const name of packages) {
    const matches = entries.filter(([key]) => key.slice(0, key.lastIndexOf('/')).toLowerCase() === name.toLowerCase());
    if (matches.length !== 1 || matches[0]![0].slice(matches[0]![0].lastIndexOf('/') + 1) !== version ||
      !matches[0]![1] || typeof matches[0]![1] !== 'object' || (matches[0]![1] as { type?: string }).type !== 'package') {
      throw new Error(`Kun API 的 ${name} 未按锁定版本 ${version} 还原，拒绝生成错误版本的参考文档`);
    }
  }
}

/**
 * 在 workDir 里还原全部 Kun.* 包并跑 docfx metadata。workDir 放了空的 Directory.Build.* 与关掉中央包管理的
 * Directory.Packages.props，隔开仓库根的 .NET 构建基线；NuGet.config 与 global.json 仍用仓库根的。
 */
export function runDocfx(version: string, packages: readonly string[], workDir: string): DocfxOutput {
  fs.rmSync(workDir, { recursive: true, force: true });
  fs.mkdirSync(workDir, { recursive: true });
  const write = (name: string, content: string) => fs.writeFileSync(path.join(workDir, name), content);
  write('Directory.Build.props', '<Project />\n');
  write('Directory.Build.targets', '<Project />\n');
  write(
    'Directory.Packages.props',
    '<Project>\n  <PropertyGroup>\n    <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>\n  </PropertyGroup>\n</Project>\n',
  );
  const refs = packages.map((p) => `    <PackageReference Include="${p}" Version="[${version}]" />`).join('\n');
  write(
    'KunApi.csproj',
    `<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <CopyDocumentationFilesFromPackages>true</CopyDocumentationFilesFromPackages>
  </PropertyGroup>
  <ItemGroup>
${refs}
  </ItemGroup>
</Project>
`,
  );
  run('dotnet', ['build', 'KunApi.csproj', '-c', 'Release', '-o', 'out', '--nologo', '-v', 'q'], workDir);
  const restored = JSON.parse(fs.readFileSync(path.join(workDir, 'obj', 'project.assets.json'), 'utf8')) as { libraries?: unknown };
  verifyKunPackageVersions(version, packages, restored.libraries);
  run('dotnet', ['tool', 'restore'], WEBSITE_ROOT);
  write(
    'docfx.json',
    `${JSON.stringify(
      { metadata: [{ src: [{ src: 'out', files: packages.map((p) => `${p}.dll`) }], dest: 'api', outputFormat: 'markdown' }] },
      null,
      2,
    )}\n`,
  );
  const log = run('dotnet', ['tool', 'run', 'docfx', 'metadata', 'docfx.json'], workDir);
  const warnings = Number(/(\d+) warning\(s\)/.exec(log)?.[1] ?? 0);

  const apiDir = path.join(workDir, 'api');
  const pages: Record<string, string> = {};
  for (const name of fs.readdirSync(apiDir)) {
    if (name.endsWith('.md')) pages[name.slice(0, -3)] = fs.readFileSync(path.join(apiDir, name), 'utf8');
  }
  return { pages, descriptions: nuspecDescriptions(workDir, packages, version), warnings };
}

/** 从 obj/project.assets.json 找到还原出的包目录，读 nuspec 的 description。 */
function nuspecDescriptions(workDir: string, packages: readonly string[], version: string): Record<string, string> {
  const assets = JSON.parse(fs.readFileSync(path.join(workDir, 'obj', 'project.assets.json'), 'utf8')) as {
    packageFolders?: Record<string, unknown>;
    libraries?: Record<string, { path?: string }>;
  };
  const folders = Object.keys(assets.packageFolders ?? {});
  const out: Record<string, string> = {};
  for (const pkg of packages) {
    const rel = assets.libraries?.[`${pkg}/${version}`]?.path;
    if (!rel) continue;
    for (const folder of folders) {
      const nuspec = path.join(folder, rel, `${pkg.toLowerCase()}.nuspec`);
      if (!fs.existsSync(nuspec)) continue;
      const text = /<description>([\s\S]*?)<\/description>/.exec(fs.readFileSync(nuspec, 'utf8'))?.[1];
      if (text) out[pkg] = decodeEntities(text).replace(/\s+/g, ' ').trim();
      break;
    }
  }
  return out;
}

// ---------------------------------------------------------------- DocFX Markdown → Rspress 页面

function decodeEntities(text: string): string {
  return text
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;|&apos;/g, "'")
    .replace(/&amp;/g, '&');
}

function codeSpan(text: string): string {
  const content = decodeEntities(text).replace(/\s+/g, ' ').trim();
  if (content === '') return '';
  const longest = Math.max(0, ...[...content.matchAll(/`+/g)].map((m) => m[0].length));
  const ticks = '`'.repeat(longest + 1);
  return longest > 0 ? `${ticks} ${content} ${ticks}` : `${ticks}${content}${ticks}`;
}

/** 链接文字里的尖括号与方括号转义，避免被当成 HTML 或打断链接。 */
function escapeLabel(text: string): string {
  return text.replace(/([<>[\]])/g, '\\$1');
}

export interface ApiPage {
  stem: string;
  uid: string;
  kind: string;
  /** 标题里的类型名，Markdown 已转义（如 Result<T\>）。 */
  display: string;
  namespace: string;
  assembly: string;
  /** 相对 sdk/api/kun/ 的页面路径。 */
  path: string;
  route: string;
  body: string;
}

const H1_RE = /^# (?:<a id="[^"]*"><\/a> )?(Namespace|Class|Struct|Interface|Enum|Delegate) (.+)$/m;

function slugNamespace(ns: string): string {
  return ns.toLowerCase().replace(/\./g, '-');
}

/** 解析 DocFX 页面并分配站内路径：命名空间页 <ns>/index.md，类型页 <ns>/<type>.md（嵌套类型的 . 与泛型的 ` 换成 -）。 */
export function parseDocfxPages(pages: Record<string, string>): ApiPage[] {
  const parsed: ApiPage[] = [];
  for (const [stem, text] of Object.entries(pages)) {
    const h1 = H1_RE.exec(text);
    if (!h1) continue;
    const kind = h1[1] ?? '';
    const display = (h1[2] ?? '').trim();
    const namespace = kind === 'Namespace' ? display : (/^Namespace: \[([^\]]+)\]\(/m.exec(text)?.[1] ?? '');
    if (namespace === '') continue; // 全局命名空间（如 Kun.Gateway 顶级语句生成的 Program），不进站
    const assembly = /^Assembly: (.+?)\.dll\s*$/m.exec(text)?.[1] ?? '';
    const nsSlug = slugNamespace(namespace);
    const file = kind === 'Namespace' ? `${nsSlug}/index.md` : `${nsSlug}/${stem.slice(namespace.length + 1).toLowerCase().replace(/[.`]/g, '-')}.md`;
    const route = KUN_API_ROUTE + (kind === 'Namespace' ? `${nsSlug}/` : file.slice(0, -3));
    parsed.push({ stem, uid: stem.replace(/-/g, '`'), kind, display, namespace, assembly, path: file, route, body: text });
  }
  const seen = new Map<string, string>();
  for (const page of parsed) {
    const key = page.path.toLowerCase();
    const other = seen.get(key);
    if (other) throw new Error(`API 页面路径冲突：${other} 与 ${page.stem} 都映射到 ${page.path}`);
    seen.set(key, page.stem);
  }
  return parsed;
}

/** DocFX 的成员锚点：uid 里非字母数字的字符换成 _。 */
export function docfxAnchor(uid: string): string {
  return uid.replace(/[^A-Za-z0-9]/g, '_');
}

function shortTypeName(uid: string): string {
  const name = uid.split('.').pop() ?? uid;
  return name.replace(/`+\d+$/, '');
}

/** <xref href="uid"> → 站内页面（类型或其成员的锚点）；外部类型链到 .NET 文档，其余写成行内代码。 */
export function makeXrefResolver(pages: readonly ApiPage[]): (href: string) => string {
  const byUid = new Map(pages.map((p) => [p.uid, p]));
  return (href) => {
    const uid = decodeURIComponent(href);
    const bare = uid.replace(/\(.*$/, '');
    let page = byUid.get(bare);
    let member = '';
    if (!page) {
      const parts = bare.split('.');
      for (let i = parts.length - 1; i > 0 && !page; i--) {
        page = byUid.get(parts.slice(0, i).join('.'));
        if (page) member = parts.slice(i).join('.');
      }
    }
    if (page) {
      const label = member ? `${page.display}.${escapeLabel(member.replace(/`+\d+$/, ''))}` : page.display;
      return `[${label}](${page.route}${member ? `#${docfxAnchor(uid)}` : ''})`;
    }
    const name = codeSpan(shortTypeName(bare));
    return /^(System|Microsoft)\./.test(bare) ? `[${name}](https://learn.microsoft.com/dotnet/api/${bare.toLowerCase().replace(/`/g, '-')})` : name;
  };
}

/** XML 注释带进来的 HTML（段落、列表、强调、代码、xref、see、a）转成 Markdown，链接才会被渲染并参与断链检查。 */
export function htmlToMarkdown(text: string, xref: (href: string) => string): string {
  const blocks: string[] = [];
  let out = text.replace(/<pre><code(?: class="lang-([\w-]+)")?>([\s\S]*?)<\/code><\/pre>/g, (_, lang: string | undefined, code: string) => {
    blocks.push(`\n\n\`\`\`${lang ?? ''}\n${decodeEntities(code).replace(/\n+$/, '')}\n\`\`\`\n\n`);
    return `\u0000${blocks.length - 1}\u0000`;
  });
  const inline = (s: string) =>
    s
      .replace(/<xref href="([^"]+)"[^>]*>(?:<\/xref>)?/g, (_, href: string) => xref(href))
      .replace(/<see cref="(?:[A-Z]:)?([^"]+)"\s*\/?>(?:<\/see>)?/g, (_, cref: string) => codeSpan(cref))
      .replace(/<code(?: [^>]*)?>([\s\S]*?)<\/code>/g, (_, code: string) => codeSpan(code))
      .replace(/<(b|strong)>([\s\S]*?)<\/\1>/g, '**$2**')
      .replace(/<(i|em)>([\s\S]*?)<\/\1>/g, '*$2*')
      .replace(/<a href="([^"]+)"[^>]*>([\s\S]*?)<\/a>/g, (_, url: string, label: string) => `[${label.trim()}](${url})`);
  out = inline(out);
  out = out.replace(/<(ul|ol)>([\s\S]*?)<\/\1>/g, (_, kind: string, items: string) => {
    const list = [...items.matchAll(/<li>([\s\S]*?)<\/li>/g)].map(
      (m, i) => `${kind === 'ol' ? `${i + 1}.` : '-'} ${(m[1] ?? '').replace(/\s+/g, ' ').trim()}`,
    );
    return `\n\n${list.join('\n')}\n\n`;
  });
  out = out.replace(/<\/?p>/g, '\n\n');
  out = out.replace(/\u0000(\d+)\u0000/g, (_, i: string) => blocks[Number(i)] ?? '');
  return out.replace(/[ \t]+\n/g, (m) => (m.startsWith('  ') ? '  \n' : '\n')).replace(/\n{3,}/g, '\n\n');
}

interface ConvertContext {
  kun: KunCheckout;
  pages: readonly ApiPage[];
  byStem: ReadonlyMap<string, ApiPage>;
  xref: (href: string) => string;
}

function convertContext(kun: KunCheckout, pages: readonly ApiPage[]): ConvertContext {
  return { kun, pages, byStem: new Map(pages.map((p) => [p.stem, p])), xref: makeXrefResolver(pages) };
}

function apiBanner(kun: KunCheckout, assembly: string): string {
  const source = `src/${assembly}`;
  return [
    ':::info 生成页',
    `本页由 DocFX 从 nuget.org 上 \`${assembly}\` ${kun.version} 的 XML 注释生成，源码在 kun 仓库 [\`${source}\`](${kunGithubUrl(kun, source, true)})（\`${kun.tag}\`）。` +
      'Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship)）；' +
      'Qian 对 Kun 的选用与约束见[与 Qian 的关系](/sdk/kun/with-qian)。',
    ':::',
  ].join('\n');
}

/**
 * sidebar 为 false 时页面不渲染侧栏（R54）：命名空间页与类型页本来就不在侧栏里，再把整棵 SDK 侧栏渲染进
 * 这四百多页只增体积、不增导航（每页约 20 KB）；两个概览页保留侧栏，作为从 SDK 侧栏进来的落点。
 */
function apiFrontmatter(kun: KunCheckout, title: string, description: string, sourcePath: string, sidebar = true): string {
  return [
    '---',
    `title: ${yamlString(title)}`,
    `description: ${yamlString(truncate(description || title))}`,
    'part: sdk',
    'project: kun',
    `version: ${yamlString(kun.version)}`,
    `source: ${yamlString(kunSource(kun, sourcePath))}`,
    `updated: ${kun.date}`,
    'audience: [developer]',
    ...(sidebar ? [] : ['sidebar: false']),
    '---',
    '',
  ].join('\n');
}

/** 把一页 DocFX Markdown 转成站点页面。 */
export function convertPage(page: ApiPage, ctx: ConvertContext): string {
  const { byStem, xref } = ctx;
  const fence = /^```/;
  const lines = page.body.split('\n');
  const out: string[] = [];
  let inFence = false;
  let prose: string[] = [];
  const flush = () => {
    if (prose.length > 0) out.push(htmlToMarkdown(prose.join('\n'), xref));
    prose = [];
  };
  for (const line of lines) {
    if (fence.test(line)) {
      if (!inFence) flush();
      inFence = !inFence;
      out.push(line);
      continue;
    }
    if (inFence) out.push(line);
    else prose.push(line);
  }
  flush();
  let body = out.join('\n');

  body = body.replace(/^# <a id="[^"]*"><\/a> /m, '# ');
  body = body.replace(/^(#{2,6}) <a id="([^"]+)"><\/a> (.*)$/gm, (_, hashes: string, id: string, text: string) => `${hashes} ${text.trim()} {#${id}}`);
  // DocFX 页面之间的相对链接（目标里的 \- \# \_ 是 Markdown 转义）→ 站内路由
  body = body.replace(/\[((?:[^\]\\]|\\.)*)\]\(([^)\s]+)\)/g, (whole, label: string, dest: string) => {
    if (/^[a-z][a-z0-9+.-]*:/i.test(dest)) return whole;
    const target = dest.replace(/\\(.)/g, '$1');
    const m = /^([^#]+)\.md(#.*)?$/.exec(target);
    if (!m) return whole;
    const linked = byStem.get(m[1] ?? '');
    return linked ? `[${label}](${linked.route}${m[2] ?? ''})` : label;
  });

  const title = plainText(page.display);
  let description: string;
  if (page.kind === 'Namespace') {
    const count = ctx.pages.filter((p) => p.kind !== 'Namespace' && p.namespace === page.namespace).length;
    description = `命名空间 ${page.namespace}：${count} 个公开类型（${page.assembly || dominantAssembly(page.namespace, ctx.pages)}）`;
  } else {
    const afterMeta = body.split(/^Assembly: .*$/m)[1] ?? '';
    description = plainText(firstParagraph(afterMeta.split(/^```/m)[0] ?? ''));
  }
  const assembly = page.kind === 'Namespace' ? dominantAssembly(page.namespace, ctx.pages) : page.assembly;
  const h1End = body.indexOf('\n', body.search(/^# /m));
  const withBanner = h1End === -1 ? `${body}\n\n${apiBanner(ctx.kun, assembly)}\n` : `${body.slice(0, h1End)}\n\n${apiBanner(ctx.kun, assembly)}\n${body.slice(h1End)}`;
  const content = `${apiFrontmatter(ctx.kun, page.kind === 'Namespace' ? title : `${title}（${page.kind}）`, description, `src/${assembly}`, false)}\n${withBanner.trim()}\n`;
  return content;
}

function dominantAssembly(namespace: string, pages: readonly ApiPage[]): string {
  const counts = new Map<string, number>();
  for (const p of pages) {
    if (p.kind !== 'Namespace' && p.namespace === namespace && p.assembly) counts.set(p.assembly, (counts.get(p.assembly) ?? 0) + 1);
  }
  return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]))[0]?.[0] ?? namespace;
}

export interface KunApiResult {
  /** 相对 sdk/api/kun/ 的文件。 */
  files: GeneratedFile[];
  packages: string[];
  types: number;
  namespaces: number;
  warnings: number;
}

/** DocFX 输出 → sdk/api/kun/ 下的页面与概览（不生成侧栏）。纯函数，单测直接喂 DocFX 样例。 */
export function convertDocfx(kun: KunCheckout, output: DocfxOutput, packages: readonly string[]): KunApiResult {
  const pages = parseDocfxPages(output.pages);
  const ctx = convertContext(kun, pages);
  const files: GeneratedFile[] = pages.map((p) => ({ path: p.path, content: convertPage(p, ctx) }));

  // API 页不进侧栏：Rspress 每页都渲染整棵侧栏，四百多个 API 页面进侧栏会让每页多出约 250 KB（产物从 57 MB 涨到 170 MB）。
  // 导航靠本节概览（包 → 命名空间）、命名空间页（→ 类型）、类型页顶部的命名空间链接与全文搜索。
  const namespaces = pages.filter((p) => p.kind === 'Namespace').sort((a, b) => a.namespace.localeCompare(b.namespace));
  const byPackage = new Map<string, ApiPage[]>();
  for (const ns of namespaces) {
    const pkg = dominantAssembly(ns.namespace, pages);
    byPackage.set(pkg, [...(byPackage.get(pkg) ?? []), ns]);
  }

  const rows = packages.map((pkg) => {
    const nsLinks = (byPackage.get(pkg) ?? []).map((ns) => `[${ns.namespace}](${ns.route})`).join('、') || '—';
    const description = (output.descriptions[pkg] ?? '').replace(/\|/g, '\\|');
    return `| [\`${pkg}\`](https://www.nuget.org/packages/${pkg}/${kun.version}) | ${description || '—'} | ${nsLinks} |`;
  });
  const typeCount = pages.filter((p) => p.kind !== 'Namespace').length;
  files.push({
    path: 'index.md',
    content: [
      apiFrontmatter(kun, 'Kun API 参考', `Kun ${kun.version} 的 ${packages.length} 个 Kun.* 包、${namespaces.length} 个命名空间、${typeCount} 个公开类型的 API 参考，DocFX 生成`, 'src'),
      '# Kun API 参考',
      '',
      ':::info 生成页',
      `本节由 DocFX 从 nuget.org 上 \`Kun.*\` ${kun.version} 包的 XML 注释生成，版本取仓库 \`Directory.Packages.props\` 的 \`KunVersion\`，` +
        `与 Qian 实际依赖的 Kun 一致；源码在 kun 仓库 [\`src/\`](${kunGithubUrl(kun, 'src', true)})（\`${kun.tag}\`）。` +
        'Kun 文档与代码不一致处以代码为准（[ADR-001 D1.8](/design/adr/adr-001-kun-relationship)）；Kun 的文档见 [Kun 框架](/sdk/kun/)。',
      ':::',
      '',
      `共 ${packages.length} 个包、${namespaces.length} 个命名空间、${typeCount} 个公开类型。包与 kun 仓库 \`src/\` 下的项目一一对应，说明取自包的 nuspec。`,
      '',
      '| 包 | 说明 | 命名空间 |',
      '|---|---|---|',
      ...rows,
      '',
    ].join('\n'),
  });
  return { files, packages: [...packages], types: typeCount, namespaces: namespaces.length, warnings: output.warnings };
}

// ---------------------------------------------------------------- 入口

/** 生成 sdk/api/kun/ 的文件，结果缓存在 website/.cache/kun-api/<key>.json。 */
export function buildKunApi(kun: KunCheckout, cacheDir: string = CACHE_DIR): KunApiResult {
  const packages = kunPackages(listKunFiles(kun));
  if (packages.length === 0) throw new Error(`kun 仓库 ${kun.tag} 的 src/ 下没有项目`);
  const docfx = docfxVersion();
  const key = createHash('sha256')
    .update(JSON.stringify({ version: kun.version, commit: kun.commit, packages, docfx, scripts: CONVERTER_FILES.map((f) => fs.readFileSync(f, 'utf8')) }))
    .digest('hex')
    .slice(0, 16);
  const cacheFile = path.join(cacheDir, 'kun-api', `${key}.json`);
  if (fs.existsSync(cacheFile)) return JSON.parse(fs.readFileSync(cacheFile, 'utf8')) as KunApiResult;
  const output = runDocfx(kun.version, packages, path.join(cacheDir, 'kun-api', 'build'));
  const result = convertDocfx(kun, output, packages);
  fs.mkdirSync(path.dirname(cacheFile), { recursive: true });
  fs.writeFileSync(cacheFile, JSON.stringify(result));
  return result;
}

/**
 * sdk/api/ 整个生成目录：落地页与 Kun 的 API 参考（不进侧栏，入口是 SDK 侧栏的「API 参考」链接，R54）。
 * 全部 API 参考都在 /sdk/api/ 下（R56）：Qian.*、前端包与平台 HTTP API 随 S1-DOCS-002 加入。
 */
export function apiReferenceFiles(kun: KunCheckout, kunApi: KunApiResult): GeneratedFile[] {
  return [
    ...kunApi.files.map((f) => ({ path: `kun/${f.path}`, content: f.content })),
    {
      path: 'index.md',
      content: [
        '---',
        'title: API 参考',
        `description: ${yamlString('由工具生成的 API 参考：Kun.* 已上线，Qian.*、前端包与平台 HTTP API 随 S1-DOCS-002 加入。')}`,
        'part: sdk',
        'project: qian',
        'audience: [developer, agent]',
        `updated: ${kun.date}`,
        '---',
        '',
        '# API 参考',
        '',
        'API 参考由工具生成，不手写（[文档站规范](/design/prd/specs/docs-site) §6）。编程模型与示例见 SDK 各节，这里只有类型与成员。',
        '',
        '| 范围 | 位置 | 状态 |',
        '|---|---|---|',
        `| Kun（\`Kun.*\`，按 \`KunVersion\` 锁定） | [Kun API 参考](${KUN_API_ROUTE}) | 已生成：${kunApi.packages.length} 个包、${kunApi.types} 个类型，${kun.tag}（${KUN_OWNER_REPO}） |`,
        '| Qian 后端（`Qian.*`） | `/sdk/api/dotnet/` | 随 S1-DOCS-002 |',
        '| 前端（`@qian/*`、`@yue/*`） | `/sdk/api/frontend/` | 随 S1-DOCS-002 |',
        '| 平台 HTTP API（OpenAPI） | `/sdk/api/http/` | 随 S1-DOCS-002 |',
        '',
      ].join('\n'),
    },
  ];
}
