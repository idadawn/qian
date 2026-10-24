import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { afterAll, describe, expect, it } from 'vitest';

// 外壳产物校验（apps/qian-shell/scripts/verify-dist.mjs）自测：真跑一遍脚本。script-src 只许 'self' 与内联脚本的 sha256（S1-YUE-014 撤销了
// 'unsafe-eval' 例外）；名单外的放宽指令、重复声明（浏览器只认第一条）、任何例外与外部源一律拒绝。
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
  ['unsafe-eval（已撤销的例外）', `script-src 'self' 'unsafe-eval' ${HASH}`, []],
  ['wasm-unsafe-eval', `script-src 'self' 'wasm-unsafe-eval' ${HASH}`, []],
  ['unsafe-eval 与 unsafe-inline 并列', `script-src 'self' 'unsafe-eval' 'unsafe-inline' ${HASH}`, []],
  ['unsafe-hashes', `script-src 'self' 'unsafe-hashes' ${HASH}`, []],
  ['self 不在开头', `script-src ${HASH} 'self'`, []],
  // 首位换成放宽源、其余全是哈希：只有「首位必须是 'self'」这条检查拦得住。
  ['首位放宽源', `script-src 'unsafe-eval' ${HASH}`, []],
  ['外部源', `script-src 'self' https://cdn.example ${HASH}`, []],
  ['script-src-elem 放宽', `script-src 'self' ${HASH}`, ["script-src-elem 'self' https://evil.example"]],
  ['script-src-attr 放宽', `script-src 'self' ${HASH}`, ["script-src-attr 'unsafe-inline'"]],
  ['重复声明：前一条放宽、后一条合规', `script-src 'self' 'unsafe-inline' ${HASH}`, [`script-src 'self' ${HASH}`]],
  ['内联脚本哈希不符', "script-src 'self' 'sha256-AAAA'", []],
];

describe('外壳产物校验（verify-dist.mjs）', () => {
  it('合规的 CSP 通过：script-src 只有 self 与内联脚本的 sha256', () => {
    const result = verify(`script-src 'self' ${HASH}`);
    expect(result.out).toContain('外壳产物校验通过');
    expect(result.status).toBe(0);
  });
  it.each(rejected)('拒绝：%s', (_name, script, extra) => {
    const result = verify(script, extra);
    expect(result.out).toContain('外壳产物校验失败');
    expect(result.status).toBe(1);
  });
});
