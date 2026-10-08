// 单测用的样例 kun 仓库：临时目录里 git init，按给定日期逐次提交，最后打 tag。只用 Node 内置模块与本机 git。
import { execFileSync } from 'node:child_process';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

export interface FixtureCommit {
  /** 提交日期 YYYY-MM-DD，对应镜像页 frontmatter 的 updated。 */
  date: string;
  files: Record<string, string>;
}

export function makeKunRepo(commits: FixtureCommit[], tag: string | null = 'v2026.9.13'): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'qian-kun-fixture-'));
  const git = (args: string[], env: Record<string, string> = {}) =>
    execFileSync('git', ['-C', dir, ...args], { stdio: 'pipe', env: { ...process.env, ...env } });
  git(['init', '-q']);
  for (const commit of commits) {
    for (const [file, content] of Object.entries(commit.files)) {
      const target = path.join(dir, file);
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, content);
    }
    git(['add', '-A']);
    const stamp = `${commit.date}T12:00:00+00:00`;
    git(
      ['-c', 'user.name=fixture', '-c', 'user.email=fixture@example.com', '-c', 'commit.gpgsign=false', 'commit', '-q', '--no-verify', '-m', commit.date],
      { GIT_AUTHOR_DATE: stamp, GIT_COMMITTER_DATE: stamp },
    );
  }
  if (tag) git(['tag', tag]);
  return dir;
}

/** 写一个只含 <KunVersion> 的 Directory.Packages.props，返回其所在目录（当作仓库根）。 */
export function makeRepoRoot(propsBody: string): string {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'qian-root-fixture-'));
  fs.writeFileSync(path.join(dir, 'Directory.Packages.props'), `<Project>\n${propsBody}\n</Project>\n`);
  return dir;
}
