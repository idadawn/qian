import { createHash } from 'node:crypto';
import { cpSync, existsSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { join, resolve, relative, sep } from 'node:path';
const root = resolve(import.meta.dirname, '../../..');
const host = resolve(root, 'apps/qian-shell/dist/host');
const remote = resolve(root, 'packages/qian-tenancy-ui/dist/remote');
const output = resolve(root, 'apps/qian-shell/dist/artifact');
const distribution = resolve(root, 'apps/qian-shell/dist/distribution');
const component = join(distribution, 'plugins/qian.shell');
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
// 组件分发保持组件文件夹形状；运行时入口必须真的落在本组件内。
const source = resolve(root, 'plugins/qian.shell');
const manifest = JSON.parse(readFileSync(join(source, '.qian-plugin/plugin.json'), 'utf8'));
if (manifest.runtime?.form !== 'ui' || manifest.runtime.entry !== './ui/dist/index.html' || manifest.runtime.npm?.length || manifest['x-skeleton'] !== true) throw new Error('外壳静态制品入口或安装边界与交付约定不符');
rmSync(distribution, { recursive: true, force: true });
mkdirSync(component, { recursive: true });
cpSync(source, component, { recursive: true });
cpSync(output, join(component, 'ui/dist'), { recursive: true });
const entry = resolve(component, manifest.runtime.entry);
if (!entry.startsWith(component + sep) || !existsSync(entry)) throw new Error('组件 runtime.entry 没有对应真实 HTML');
const hash = (file) => createHash('sha256').update(readFileSync(file)).digest('hex');
const webrootMapping = files(output).sort().map((file) => {
  const webPath = relative(output, file).replaceAll('\\', '/');
  const target = join(component, 'ui/dist', webPath);
  if (!existsSync(target) || hash(file) !== hash(target)) throw new Error('Web 根与组件制品内容不一致：' + webPath);
  return { webPath: '/' + webPath, componentPath: 'ui/dist/' + webPath, sha256: hash(target) };
});
const artifactFiles = files(component).sort().map((file) => ({ path: relative(component, file).replaceAll('\\', '/'), bytes: readFileSync(file).byteLength, sha256: hash(file) }));
writeFileSync(join(distribution, 'component-artifact.json'), JSON.stringify({ schemaVersion: 1, component: 'qian.shell', runtimeEntry: manifest.runtime.entry,
  componentRoot: 'plugins/qian.shell', webroot: 'plugins/qian.shell/ui/dist', installationVerified: false, files: artifactFiles, webrootMapping }, null, 2) + '\n');
console.log('已生成外壳 Web 根与 plugins/qian.shell/ui/dist 组件制品；' + artifactFiles.length + ' 项组件摘要、' + webrootMapping.length + ' 项同字节映射。');
