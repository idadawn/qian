import { createHash } from 'node:crypto';
import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { gzipSync } from 'node:zlib';

// 外壳构建产物的静态断言（pnpm --filter qian-shell build 之后自动运行）：
// 1. index.html 有且只有一条 meta CSP，紧跟 charset；script-src 只有 'self'、登记过的 'unsafe-eval'（yue-form 运行期编译表单 Schema，见 vite.config.ts）
//    与内联脚本的 sha256，且每段内联脚本的哈希都在其中（反之亦然）；
// 2. 不再有旧的 /app-conf.js、/app-config.js 脚本注入，运行时配置只走 /app-config.json；入口引用的脚本与样式全部同源；referrer 策略为 no-referrer；
// 3. 正式产物不含验收控制面（__qianAcceptance / installAcceptance）；
// 4. 首屏 JS（入口模块 + modulepreload）gzip 合计 ≤ 250 KiB（运行期同口径由 e2e/federation.spec.ts 在浏览器里再量一次）。
const dist = resolve(process.argv[2] ?? 'dist/host');
const html = readFileSync(join(dist, 'index.html'), 'utf8');
const fail = message => { console.error('外壳产物校验失败：' + message); process.exit(1); };

const policies = [...html.matchAll(/<meta http-equiv="Content-Security-Policy" content="([^"]+)"\s*\/?>/g)].map(match => match[1]);
if (policies.length !== 1) fail(`meta CSP 应恰好一条，实际 ${policies.length} 条`);
if (!/<meta charset="UTF-8" \/>\s*<meta http-equiv="Content-Security-Policy"/.test(html)) fail('meta CSP 必须紧跟 charset、先于任何脚本');
const directives = new Map(policies[0].split(';').map(part => part.trim().split(/\s+/)).map(([name, ...values]) => [name, values]));
for (const [name, expected] of [['default-src', ["'self'"]], ['connect-src', ["'self'"]], ['object-src', ["'none'"]], ['base-uri', ["'none'"]], ['frame-src', ["'none'"]], ['form-action', ["'self'"]]])
  if (JSON.stringify(directives.get(name)) !== JSON.stringify(expected)) fail(`${name} 应为 ${expected.join(' ')}，实际 ${directives.get(name)?.join(' ') ?? '缺失'}`);
const scriptSources = directives.get('script-src') ?? [];
if (scriptSources[0] !== "'self'" || scriptSources[1] !== "'unsafe-eval'" || scriptSources.slice(2).some(value => !/^'sha256-[A-Za-z0-9+/]+=*'$/.test(value))) fail(`script-src 只许 'self'、'unsafe-eval' 与 sha256 哈希：${scriptSources.join(' ')}`);
if (scriptSources.some(value => /unsafe-inline|strict-dynamic|https?:|data:|blob:|\*/.test(value))) fail('script-src 不得放行 unsafe-inline / strict-dynamic / 外部源');
const inline = [...html.matchAll(/<script(?![^>]*\bsrc=)[^>]*>([\s\S]*?)<\/script>/g)].map(match => `'sha256-${createHash('sha256').update(match[1]).digest('base64')}'`);
if (inline.length === 0) fail('缺少首帧主题脚本');
if (JSON.stringify([...inline].sort()) !== JSON.stringify(scriptSources.slice(2).sort())) fail('内联脚本哈希与 script-src 不一致');
if (/app-conf\.js|app-config\.js|__APP_CONF__/.test(html)) fail('index.html 仍注入脚本式运行时配置');
const references = [...html.matchAll(/\b(?:src|href)="([^"]+)"/g)].map(match => match[1]);
if (references.some(reference => !reference.startsWith('/') || reference.startsWith('//'))) fail(`入口引用了非同源资源：${references.join(' ')}`);
if ([...html.matchAll(/<meta name="referrer" content="no-referrer"\s*\/?>/g)].length !== 1) fail('index.html 应恰好有一条 referrer no-referrer');

function files(directory) { return readdirSync(directory, { withFileTypes: true }).flatMap(entry => entry.isDirectory() ? files(join(directory, entry.name)) : [join(directory, entry.name)]); }
const scripts = files(dist).filter(file => file.endsWith('.js'));
if (!process.argv.includes('--acceptance')) for (const file of scripts) if (/__qianAcceptance|installAcceptance/.test(readFileSync(file, 'utf8'))) fail('正式外壳含验收控制面：' + file);

const initial = [...html.matchAll(/<script type="module"[^>]*\bsrc="([^"]+)"/g), ...html.matchAll(/<link rel="modulepreload"[^>]*\bhref="([^"]+)"/g)].map(match => match[1]);
const total = initial.reduce((sum, path) => sum + gzipSync(readFileSync(join(dist, path))).byteLength, 0);
if (total > 250 * 1024) fail(`首屏 JS gzip ${(total / 1024).toFixed(1)} KiB 超出 250 KiB 预算`);
console.log(`外壳产物校验通过：meta CSP（${inline.length} 段内联脚本按哈希放行）、无脚本式运行时配置、无验收控制面；首屏 JS gzip ${(total / 1024).toFixed(1)} KiB / 250 KiB（${initial.length} 个文件）。`);
