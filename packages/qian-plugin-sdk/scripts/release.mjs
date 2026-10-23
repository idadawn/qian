import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const [mode, output] = process.argv.slice(2);
assert(['pack', 'publish'].includes(mode) && output, '需要 pack/publish 与制品目录');
const directory = path.resolve(output);
const cohort = ['packages/yue/tokens', 'packages/yue/contracts', 'packages/yue/sdk', 'packages/qian-plugin-sdk'];
const runtime = 'packages/qian-runtime';
const packageAt = (source) => JSON.parse(readFileSync(path.join(root, source, 'package.json'), 'utf8'));
const sdk = packageAt(cohort.at(-1));
const records = [];
mkdirSync(directory, { recursive: true });
if (mode === 'pack') {
  for (const source of [runtime, ...cohort]) {
    const pkg = packageAt(source);
    execFileSync('pnpm', ['--filter', './' + source, 'build'], { cwd: root, stdio: 'inherit' });
    execFileSync('pnpm', ['--filter', './' + source, 'pack', '--pack-destination', directory], { cwd: root, stdio: 'inherit' });
    const archive = path.join(directory, `${pkg.name.slice(1).replace('/', '-')}-${pkg.version}.tgz`);
    const manifest = JSON.parse(execFileSync('tar', ['-xOf', archive, 'package/package.json'], { encoding: 'utf8' }));
    assert.equal(manifest.name, pkg.name); assert.equal(manifest.version, pkg.version);
    assert.equal(manifest.repository.directory, source);
    assert.match(manifest.repository.url, /github\.com[:/]idadawn\/qian(?:\.git)?$/);
    for (const version of Object.values(manifest.dependencies ?? {})) assert(!/^(workspace|file|link):/.test(version), '发布制品不得引用工作区或本地文件');
    records.push({ name: pkg.name, version: pkg.version, source, archive,
      integrity: 'sha512-' + createHash('sha512').update(readFileSync(archive)).digest('base64'), publish: source !== runtime });
  }
  writeFileSync(path.join(directory, 'packages.json'), JSON.stringify(records, null, 2));
  console.log(JSON.stringify({ phase: 'pack', packages: records.map(({ name, version, integrity, publish }) => ({ name, version, integrity, publish })) }));
} else {
  assert.equal(process.env.GITHUB_REF, 'refs/heads/main'); assert.equal(process.env.GITHUB_REPOSITORY, 'idadawn/qian');
  assert.equal(process.env.RELEASE_VERSION, sdk.version); assert(/^\d+\.\d+\.\d+$/.test(sdk.version));
  assert(process.env.NODE_AUTH_TOKEN, '缺少 Actions 包发布凭据');
  const artifacts = JSON.parse(readFileSync(path.join(directory, 'packages.json'), 'utf8')).filter((record) => record.publish);
  assert.deepEqual(artifacts.map((record) => record.source), cohort);
  async function metadata(name, version) {
    const response = await fetch('https://npm.pkg.github.com/' + encodeURIComponent(name), {
      headers: { Authorization: `Bearer ${process.env.NODE_AUTH_TOKEN}` }, cache: 'no-store',
    });
    if (response.status === 404) return null;
    assert.equal(response.status, 200, `无法核对 ${name} 的私有源版本`);
    return (await response.json()).versions?.[version] ?? null;
  }
  // 先核对整个固定依赖组，避免发现同版本不同内容后才停止半途发布。
  for (const artifact of artifacts) {
    const current = packageAt(artifact.source);
    assert.equal(artifact.name, current.name); assert.equal(artifact.version, current.version);
    assert.equal(path.dirname(path.resolve(artifact.archive)), directory);
    assert.equal(artifact.integrity, 'sha512-' + createHash('sha512').update(readFileSync(artifact.archive)).digest('base64'));
    artifact.existing = await metadata(artifact.name, artifact.version);
    if (artifact.existing) assert.equal(artifact.existing.dist.integrity, artifact.integrity, `${artifact.name} 同版本制品不同，禁止覆盖`);
  }
  for (const artifact of artifacts) {
    if (!artifact.existing) execFileSync('npm', ['publish', artifact.archive, '--registry=https://npm.pkg.github.com', '--access=restricted', '--ignore-scripts'], { cwd: root, stdio: 'inherit' });
    const published = await metadata(artifact.name, artifact.version);
    assert(published, '发布后未查到固定版本'); assert.equal(published.dist.integrity, artifact.integrity);
    const response = await fetch('https://api.github.com/users/idadawn/packages/npm/' + encodeURIComponent(artifact.name.split('/')[1]), {
      headers: { Authorization: `Bearer ${process.env.NODE_AUTH_TOKEN}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' },
    });
    assert.equal(response.status, 200, '无法核对包可见性'); const record = await response.json();
    assert.equal(record.visibility, 'private');
    console.log(JSON.stringify({ package: artifact.name, version: artifact.version, integrity: artifact.integrity, visibility: record.visibility, revision: process.env.GITHUB_SHA }));
  }
}
