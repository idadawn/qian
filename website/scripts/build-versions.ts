// 版本矩阵（docs-site.md §2b.4，ADR-021 D21.2 第 3 条）：从仓库里的钉版文件产出 docs/start/versions.md，不手改。
// 来源：Directory.Packages.props（KunVersion）、packages/*/*.csproj（Qian.* 包清单与版本）、global.json（.NET SDK）、
// package.json（Node、pnpm）、pnpm-workspace.yaml 与工作区里的 yue 包（@idadawn/yue-*，文档简称 @yue/*）、deploy/baseline.json（K3s）。
import fs from 'node:fs';
import path from 'node:path';
import { readKunVersion } from './kun.ts';
import { REPO_ROOT, repoFileDate, yamlString, type GeneratedFile } from './repo.ts';

export const VERSIONS_PAGE = 'start/versions.md';

export interface PackageVersion {
  name: string;
  /** 没有版本号（还没发布）时为 null。 */
  version: string | null;
}

export interface VersionMatrix {
  /** 文档版本：多版本开启前只有 main（开发版）。 */
  docVersion: string;
  kun: string;
  qian: PackageVersion[];
  yue: PackageVersion[];
  dotnetSdk: string;
  dotnetRollForward: string;
  node: string;
  pnpm: string;
  /** 部署基线里的 K3s 版本；没有基线文件时为 null。 */
  k3s: string | null;
  /** 每一项的钉版文件（相对仓库根）。 */
  sources: Record<string, string>;
}

const QIAN_VERSION_RE = /<(?:Version|VersionPrefix)>\s*([^<\s]+)\s*<\/(?:Version|VersionPrefix)>/;

/** Qian 的可打包项目：packages/<名>/<名>.csproj 里写了 <IsPackable>true</IsPackable> 的；版本取 csproj 或 Directory.Build.props 的 Version / VersionPrefix。 */
export function readQianPackages(repoRoot: string = REPO_ROOT): PackageVersion[] {
  const dir = path.join(repoRoot, 'packages');
  if (!fs.existsSync(dir)) return [];
  const shared = fs.existsSync(path.join(repoRoot, 'Directory.Build.props')) ? fs.readFileSync(path.join(repoRoot, 'Directory.Build.props'), 'utf8') : '';
  const sharedVersion = QIAN_VERSION_RE.exec(shared)?.[1] ?? null;
  const out: PackageVersion[] = [];
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    if (!entry.isDirectory()) continue;
    const csproj = path.join(dir, entry.name, `${entry.name}.csproj`);
    if (!fs.existsSync(csproj)) continue;
    const text = fs.readFileSync(csproj, 'utf8');
    if (!/<IsPackable>\s*true\s*<\/IsPackable>/i.test(text)) continue;
    out.push({ name: entry.name, version: QIAN_VERSION_RE.exec(text)?.[1] ?? sharedVersion });
  }
  return out.sort((a, b) => a.name.localeCompare(b.name));
}

/** 工作区里的 yue 包：按 pnpm-workspace.yaml 的 packages 通配找 package.json，名字以 @idadawn/yue- 开头的（S1-YUE-003 拍板的 GitHub Packages scope；兼容文档简称 @yue/）。 */
export const YUE_NAME_PREFIXES = ['@idadawn/yue-', '@yue/'] as const;
export const isYuePackageName = (name: string | undefined): name is string => !!name && YUE_NAME_PREFIXES.some((p) => name.startsWith(p));
export function readYuePackages(repoRoot: string = REPO_ROOT): PackageVersion[] {
  const workspace = path.join(repoRoot, 'pnpm-workspace.yaml');
  if (!fs.existsSync(workspace)) return [];
  const globs = [...fs.readFileSync(workspace, 'utf8').matchAll(/^\s*-\s*['"]?([^'"\s#]+)['"]?\s*$/gm)].map((m) => m[1] ?? '');
  const out: PackageVersion[] = [];
  for (const glob of globs) {
    const [prefix] = glob.split('*');
    const base = path.join(repoRoot, prefix ?? '');
    const candidates = glob.includes('*')
      ? fs.existsSync(base)
        ? fs.readdirSync(base, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => path.join(base, d.name))
        : []
      : [base];
    for (const dir of candidates) {
      const pkg = path.join(dir, 'package.json');
      if (!fs.existsSync(pkg)) continue;
      const json = JSON.parse(fs.readFileSync(pkg, 'utf8')) as { name?: string; version?: string };
      if (isYuePackageName(json.name)) out.push({ name: json.name, version: json.version ?? null });
    }
  }
  return out.sort((a, b) => a.name.localeCompare(b.name));
}

export function readVersionMatrix(repoRoot: string = REPO_ROOT): VersionMatrix {
  const globalJson = JSON.parse(fs.readFileSync(path.join(repoRoot, 'global.json'), 'utf8')) as { sdk?: { version?: string; rollForward?: string } };
  const rootPkg = JSON.parse(fs.readFileSync(path.join(repoRoot, 'package.json'), 'utf8')) as { packageManager?: string; engines?: { node?: string } };
  const baselineFile = path.join(repoRoot, 'deploy', 'baseline.json');
  const baseline = fs.existsSync(baselineFile) ? (JSON.parse(fs.readFileSync(baselineFile, 'utf8')) as { k3s?: string }) : {};
  const sdk = globalJson.sdk?.version ?? '';
  if (!sdk) throw new Error('global.json 里没有 sdk.version，版本矩阵缺 .NET SDK');
  return {
    docVersion: 'main',
    kun: readKunVersion(repoRoot),
    qian: readQianPackages(repoRoot),
    yue: readYuePackages(repoRoot),
    dotnetSdk: sdk,
    dotnetRollForward: globalJson.sdk?.rollForward ?? '',
    node: rootPkg.engines?.node ?? '',
    pnpm: (rootPkg.packageManager ?? '').replace(/^pnpm@/, ''),
    k3s: baseline.k3s ?? null,
    sources: {
      kun: 'Directory.Packages.props',
      qian: 'packages/*/*.csproj',
      yue: 'pnpm-workspace.yaml',
      dotnet: 'global.json',
      node: 'package.json',
      k3s: 'deploy/baseline.json',
    },
  };
}

const cell = (text: string) => text.replace(/\|/g, '\\|');

function summarize(packages: PackageVersion[], unreleased: string, none: string): string {
  if (packages.length === 0) return none;
  const versions = [...new Set(packages.map((p) => p.version).filter((v): v is string => v !== null))];
  if (versions.length === 0) return `${unreleased}（${packages.length} 个包）`;
  return versions.length === 1 ? `${versions[0]}（${packages.length} 个包）` : packages.map((p) => `${p.name} ${p.version ?? '未发布'}`).join('、');
}

/** 版本矩阵页：一张矩阵表 + 来源表 + 包清单。multi-version 开启后每个文档版本一行。 */
export function renderVersionsPage(m: VersionMatrix, updated: string): string {
  const qianSummary = summarize(m.qian, '未发布，开发版 main', '尚无可打包的 Qian.* 项目');
  const yueSummary = summarize(m.yue, '未发布，开发版 main', '未建立（S1-YUE-003 起）');
  const packageList = (packages: PackageVersion[]) => packages.map((p) => `- \`${p.name}\`${p.version ? ` ${p.version}` : '（未发布）'}`).join('\n');
  return [
    '---',
    `title: ${yamlString('版本矩阵')}`,
    `description: ${yamlString('当前文档版本对应的 Kun、Qian.*、@yue/*、.NET SDK、Node 与 K3s 基线，由仓库钉版文件生成。')}`,
    'part: start',
    'project: qian',
    'audience: [developer, operator]',
    `updated: ${updated}`,
    '---',
    '',
    '# 版本矩阵',
    '',
    '> 本页由 `website/scripts/build-versions.ts` 从仓库钉版文件生成（[ADR-021](/design/adr/adr-021-product-family-structure) D21.2 第 3 条），不要手改；改版本去改下表列出的源文件。',
    '',
    '「哪个 Qian 配哪个 Kun」只看这一张表：Kun 的版本在 `Directory.Packages.props` 的 `KunVersion` 写一次，代码引用的 `Kun.*` 包、[Kun 框架](/sdk/kun/)小节与 [Kun API 参考](/sdk/api/kun/)都按它锁定。',
    '',
    'Kun 的同日修复版 `-hotfix.N` 是推送 nuget.org 的正式修复，但 NuGet 按预发布排序；`Kun.*` 一律以 `[$(KunVersion)]` 精确钉定，不依赖最新稳定版排序（ADR-021 D21.3 补注）。',
    '',
    '| 文档版本 | Kun | Qian.* | @yue/* | .NET SDK | Node / pnpm | K3s |',
    '|---|---|---|---|---|---|---|',
    `| ${m.docVersion}（开发版） | ${cell(m.kun)} | ${cell(qianSummary)} | ${cell(yueSummary)} | ${cell(m.dotnetSdk)}${m.dotnetRollForward ? `（rollForward ${cell(m.dotnetRollForward)}）` : ''} | ${cell(m.node || '未钉版')} / ${cell(m.pnpm || '未钉版')} | ${m.k3s ? cell(m.k3s) : '未钉版'} |`,
    '',
    '多版本（docs-site.md §5）开启后每个文档版本一行；现在只有开发版 `main`。',
    '',
    '## 来源',
    '',
    '| 项 | 钉版文件 | 说明 |',
    '|---|---|---|',
    `| Kun | \`${m.sources.kun}\` | \`<KunVersion>\`，CalVer；升级 Kun 只改这一处（ADR-021 D21.3） |`,
    `| Qian.* | \`${m.sources.qian}\` | 写了 \`<IsPackable>true</IsPackable>\` 的项目；版本随首个包族发布钉在 \`Directory.Build.props\` |`,
    `| @yue/* | \`${m.sources.yue}\` | 工作区里名字以 \`@idadawn/yue-\` 开头的包（npm 包名；文档里 \`@yue/<包>\` 是简称，S1-YUE-003 建立） |`,
    `| .NET SDK | \`${m.sources.dotnet}\` | \`sdk.version\` 与 \`rollForward\` |`,
    `| Node / pnpm | \`${m.sources.node}\` | \`engines.node\` 与 \`packageManager\` |`,
    `| K3s | \`${m.sources.k3s}\` | 部署基线（S1-DEP-001）；集群本身在部署仓库 \`idadawn/iclaw-deploy\` |`,
    '',
    '## Qian 包清单',
    '',
    m.qian.length > 0 ? packageList(m.qian) : '- （尚无可打包的项目）',
    '',
    '## yue 包清单',
    '',
    m.yue.length > 0 ? packageList(m.yue) : '- （未建立，S1-YUE-003 起）',
    '',
  ].join('\n');
}

/** 生成 docs/start/versions.md；updated 取各钉版文件中最新的提交日期。 */
export function buildVersions(repoRoot: string = REPO_ROOT): GeneratedFile {
  const matrix = readVersionMatrix(repoRoot);
  const updated = ['Directory.Packages.props', 'Directory.Build.props', 'global.json', 'package.json', 'pnpm-workspace.yaml', 'deploy/baseline.json']
    .filter((rel) => fs.existsSync(path.join(repoRoot, rel)))
    .map((rel) => repoFileDate(rel, repoRoot))
    .sort()
    .at(-1) ?? repoFileDate('global.json', repoRoot);
  return { path: VERSIONS_PAGE, content: renderVersionsPage(matrix, updated) };
}
