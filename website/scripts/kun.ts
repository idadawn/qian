// Kun 小节的公共部分（docs-site.md §2b，ADR-021 D21.3 / D21.4）：读 KunVersion、打开 kun 仓库、在 tag 上读文件。
// kun 仓库是私有的：CI 用只读 deploy key 检出后经 KUN_DOCS_PATH 传进来；本地可把 KUN_DOCS_PATH 指向自己的克隆，
// 都没有时用 SSH 克隆到 website/.cache/kun-repo。只读 tag 上的对象，不读工作区，所以克隆停在哪个分支都不影响结果。
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { REPO_ROOT, WEBSITE_ROOT } from './repo.ts';

export const KUN_OWNER_REPO = 'idadawn/kun';
export const KUN_REPO_URL = `https://github.com/${KUN_OWNER_REPO}`;
export const KUN_CLONE_URL = `git@github.com:${KUN_OWNER_REPO}.git`;
/** 生成过程的缓存（kun 克隆、API 参考），不进 Git。 */
export const CACHE_DIR = path.join(WEBSITE_ROOT, '.cache');

const KUN_VERSION_RE = /<KunVersion>\s*([^<\s]*)\s*<\/KunVersion>/;

/** 读仓库根 Directory.Packages.props 的 <KunVersion>；缺失即失败，不回退 main（docs-site.md §2b.1）。 */
export function readKunVersion(repoRoot: string = REPO_ROOT): string {
  const file = path.join(repoRoot, 'Directory.Packages.props');
  const text = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
  const version = KUN_VERSION_RE.exec(text)?.[1] ?? '';
  if (version === '') {
    throw new Error(`${file} 里没有 <KunVersion>：Kun 文档按它锁定的版本镜像，缺失时构建失败，不回退 main（docs-site.md §2b.1）`);
  }
  if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?$/.test(version)) {
    throw new Error(`KunVersion 写成了「${version}」，应为 kun 的 CalVer 版本号，如 2026.9.13`);
  }
  return version;
}

export interface KunCheckout {
  /** 本地 git 仓库目录。 */
  dir: string;
  version: string;
  /** v<KunVersion> */
  tag: string;
  /** tag 指向的提交。 */
  commit: string;
  /** 该提交的日期 YYYY-MM-DD。 */
  date: string;
}

function git(dir: string, args: string[]): string {
  return execFileSync('git', ['-C', dir, ...args], {
    encoding: 'utf8',
    maxBuffer: 256 * 1024 * 1024,
    stdio: ['ignore', 'pipe', 'pipe'],
  });
}

function tagCommit(dir: string, tag: string): string | null {
  try {
    return git(dir, ['rev-parse', '--verify', '--quiet', `refs/tags/${tag}^{commit}`]).trim() || null;
  } catch {
    return null;
  }
}

function isGitRepo(dir: string): boolean {
  try {
    git(dir, ['rev-parse', '--git-dir']);
    return true;
  } catch {
    return false;
  }
}

/** 没给 KUN_DOCS_PATH 时的兜底：无 blob 克隆到缓存目录，缺 tag 就 fetch。需要本机对 kun 仓库有 SSH 读权限。 */
function cachedClone(tag: string, cacheDir: string): string {
  const dir = path.join(cacheDir, 'kun-repo');
  if (!isGitRepo(dir)) {
    fs.rmSync(dir, { recursive: true, force: true });
    fs.mkdirSync(cacheDir, { recursive: true });
    try {
      execFileSync('git', ['clone', '--quiet', '--filter=blob:none', '--no-checkout', KUN_CLONE_URL, dir], {
        stdio: ['ignore', 'pipe', 'pipe'],
      });
    } catch (error) {
      throw new Error(
        `没有设置 KUN_DOCS_PATH，自动克隆 ${KUN_CLONE_URL} 也失败了。把 KUN_DOCS_PATH 指向本机的 kun 克隆，` +
          `或确认对 ${KUN_OWNER_REPO} 有读权限（website/README.md §6）：${(error as Error).message}`,
      );
    }
  }
  if (tagCommit(dir, tag) === null) git(dir, ['fetch', '--quiet', '--tags', 'origin']);
  return dir;
}

/** 打开 kun 仓库并确认 tag v<version> 存在；不存在即失败。 */
export function openKun(version: string, env: NodeJS.ProcessEnv = process.env, cacheDir: string = CACHE_DIR): KunCheckout {
  const tag = `v${version}`;
  const fromEnv = env.KUN_DOCS_PATH;
  const dir = fromEnv ? path.resolve(fromEnv) : cachedClone(tag, cacheDir);
  if (!isGitRepo(dir)) throw new Error(`KUN_DOCS_PATH=${dir} 不是 git 仓库`);
  const commit = tagCommit(dir, tag);
  if (commit === null) {
    const hint = fromEnv ? `；本机克隆可先 git -C ${dir} fetch --tags` : '';
    throw new Error(`kun 仓库里没有 tag ${tag}（Directory.Packages.props 的 KunVersion = ${version}）${hint}`);
  }
  const date = git(dir, ['log', '-1', '--format=%cs', commit]).trim();
  return { dir, version, tag, commit, date };
}

/** tag 上的全部文件路径（/ 分隔）。 */
export function listKunFiles(kun: KunCheckout): string[] {
  return git(kun.dir, ['ls-tree', '-r', '--name-only', kun.commit]).split('\n').filter(Boolean);
}

export function readKunFile(kun: KunCheckout, file: string): string {
  return git(kun.dir, ['show', `${kun.commit}:${file}`]);
}

/** 文件在该 tag 上的最后提交日期（frontmatter 的 updated，docs-site.md §2b.3）。 */
export function kunFileDate(kun: KunCheckout, file: string): string {
  return git(kun.dir, ['log', '-1', '--format=%cs', kun.commit, '--', file]).trim() || kun.date;
}

/** kun 仓库该 tag 上的文件或目录地址。 */
export function kunGithubUrl(kun: KunCheckout, file: string, isDir = false): string {
  const rel = file.replace(/\/$/, '');
  return `${KUN_REPO_URL}/${isDir ? 'tree' : 'blob'}/${kun.tag}${rel === '' || rel === '.' ? '' : `/${rel}`}`;
}

/** frontmatter 的 source：<owner>/<repo>@<tag>:<path>（docs-site.md §4）。 */
export function kunSource(kun: KunCheckout, file: string): string {
  return `${KUN_OWNER_REPO}@${kun.tag}:${file}`;
}
