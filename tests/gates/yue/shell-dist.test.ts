import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { afterAll, describe, expect, it } from 'vitest';

// 外壳产物校验（apps/qian-shell/scripts/verify-dist.mjs）自测：真跑一遍脚本。CSP 的已登记例外是「至多一个」而不是必需项；
// 名单外的放宽指令、重复声明（浏览器只认第一条）、未登记的例外与外部源一律拒绝。
const ROOT = path.resolve(__dirname, '../../..');
const SCRIPT = path.join(ROOT, 'apps/qian-shell/scripts/verify-dist.mjs');
const THEME = "document.documentElement.dataset.theme='light'";
const HASH = `'sha256-${createHash('sha256').update(THEME).digest('base64')}'`;
const OTHERS = ["style-src 'self' 'unsafe-inline'", "img-src 'self' data:", "font-src 'self' data:", "connect-src 'self'", "object-src 'none'", "base-uri 'none'",
  "form-action 'self'", "frame-src 'none'", "worker-src 'self'", "manifest-src 'self'"];
const dists: string[] = [];

function verify(script: string, extra: readonly string[] = []) {
  const dist = mkdtempSync(path.join(tmpdir(), 'qian-shell-dist-')); dists.push(dist);
  mkdirSync(path.join(dist, 'assets'));
  writeFileSync(path.join(dist, 'assets', 'index.js'), 'export const shell = 1;\n');
  const policy = ["default-src 'self'", script, ...OTHERS, ...extra].join('; ');
  writeFileSync(path.join(dist, 'index.html'), `<!doctype html>\n<html lang="zh-CN"><head>\n    <meta charset="UTF-8" />\n    <meta http-equiv="Content-Security-Policy" content="${policy}" />\n`
    + `    <meta name="referrer" content="no-referrer" />\n    <script>${THEME}</script>\n    <script type="module" crossorigin src="/assets/index.js"></script>\n</head><body><div id="root"></div></body></html>\n`);
  const result = spawnSync(process.execPath, [SCRIPT, dist], { encoding: 'utf8' });
  return { status: result.status, out: `${result.stdout}\n${result.stderr}` };
}
afterAll(() => { for (const dist of dists) rmSync(dist, { recursive: true, force: true }); });

const rejected: [string, string, string[]][] = [
  ['两个例外', `script-src 'self' 'unsafe-eval' 'unsafe-inline' ${HASH}`, []],
  ['未登记的例外', `script-src 'self' 'unsafe-hashes' ${HASH}`, []],
  ['外部源', `script-src 'self' https://cdn.example ${HASH}`, []],
  ['script-src-elem 放宽', `script-src 'self' ${HASH}`, ["script-src-elem 'self' https://evil.example"]],
  ['script-src-attr 放宽', `script-src 'self' ${HASH}`, ["script-src-attr 'unsafe-inline'"]],
  ['重复声明：前一条放宽、后一条合规', `script-src 'self' 'unsafe-inline' ${HASH}`, [`script-src 'self' ${HASH}`]],
  ['内联脚本哈希不符', "script-src 'self' 'sha256-AAAA'", []],
];

describe('外壳产物校验（verify-dist.mjs）', () => {
  it('没有例外的 CSP 通过：已登记例外不是必需项，撤销后照样通过', () => {
    const result = verify(`script-src 'self' ${HASH}`);
    expect(result.out).toContain('外壳产物校验通过');
    expect(result.status).toBe(0);
  });
  it('至多一个已登记例外（unsafe-eval）通过', () => {
    expect(verify(`script-src 'self' 'unsafe-eval' ${HASH}`).status).toBe(0);
  });
  it.each(rejected)('拒绝：%s', (_name, script, extra) => {
    const result = verify(script, extra);
    expect(result.out).toContain('外壳产物校验失败');
    expect(result.status).toBe(1);
  });
});
