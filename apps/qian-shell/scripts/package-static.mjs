import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve, relative } from 'node:path';
const root = resolve(import.meta.dirname, '../../..');
const host = resolve(root, 'apps/qian-shell/dist/host');
const remote = resolve(root, 'packages/qian-tenancy-ui/dist/remote');
const output = resolve(root, 'apps/qian-shell/dist/artifact');
function files(directory) { return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => entry.isDirectory() ? files(join(directory, entry.name)) : [join(directory, entry.name)]); }
for (const file of [join(host, 'index.html'), join(remote, 'mf-manifest.json'), join(remote, '@mf-types.zip')]) if (!existsSync(file)) throw new Error('请先构建外壳及官方远程：' + relative(root, file));
for (const file of files(host).filter((file) => file.endsWith('.js'))) if (/__qianAcceptance|installAcceptance/.test(readFileSync(file, 'utf8'))) throw new Error('正式外壳含验收控制面');
const ui = JSON.parse(readFileSync(resolve(root, 'plugins/qian.tenancy/ui/manifest.json'), 'utf8'));
const mf = JSON.parse(readFileSync(join(remote, 'mf-manifest.json'), 'utf8'));
if (JSON.stringify([...ui.federation.exposes].sort()) !== JSON.stringify(mf.exposes.map((value) => value.name).sort())) throw new Error('UI expose 与 MF 制品不一致');
rmSync(output, { recursive: true, force: true }); mkdirSync(output, { recursive: true });
cpSync(host, output, { recursive: true }); cpSync(remote, join(output, 'remotes/qian.tenancy'), { recursive: true });
writeFileSync(join(output, 'remotes/qian.tenancy/ui-manifest.json'), JSON.stringify(ui, null, 2) + '\n');
const entries = files(output).sort().map((file) => ({ path: relative(output, file).replaceAll('\\', '/'), bytes: readFileSync(file).byteLength, sha256: createHash('sha256').update(readFileSync(file)).digest('hex') }));
writeFileSync(join(output, 'static-manifest.json'), JSON.stringify({ schemaVersion: 1, component: 'qian.shell', files: entries }, null, 2) + '\n');
console.log('已生成外壳静态目录与 ' + entries.length + ' 项文件摘要。');
