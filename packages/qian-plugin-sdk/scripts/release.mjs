import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { assertCohort, assertWriteContext, BOOTSTRAP_TAG, bootstrapPrivateCohort, cleanupPrivateBootstrap, COHORT, publishPrivateCohort, withBootstrapArchive } from './private-publication.mjs';
import { packageApi } from './private-package-api.mjs';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const [mode, output] = process.argv.slice(2);
assert(['pack', 'publish', 'bootstrap-private', 'cleanup'].includes(mode), '需要 pack/publish/bootstrap-private/cleanup 模式');
if (mode !== 'bootstrap-private') assert(output, '需要制品目录');
const directory = output ? path.resolve(output) : undefined;
const cohort = COHORT.map(pkg => pkg.source);
const runtime = 'packages/qian-runtime';
const packageAt = (source) => JSON.parse(readFileSync(path.join(root, source, 'package.json'), 'utf8'));
const sdk = packageAt(cohort.at(-1));
const packages = COHORT.map(expected => { const pkg = packageAt(expected.source); assert.equal(pkg.name, expected.name); return { ...expected, version: pkg.version }; });
assertCohort(packages);
const records = [];
if (mode === 'pack') {
  mkdirSync(directory, { recursive: true });
  for (const source of [runtime, ...cohort]) {
    const pkg = packageAt(source);
    execFileSync('pnpm', ['--filter', './' + source, 'build'], { cwd: root, stdio: 'inherit' });
    execFileSync('pnpm', ['--filter', './' + source, 'pack', '--pack-destination', directory], { cwd: root, stdio: 'inherit' });
    const archive = path.join(directory, `${pkg.name.slice(1).replace('/', '-')}-${pkg.version}.tgz`);
    const manifest = JSON.parse(execFileSync('tar', ['-xOf', archive, 'package/package.json'], { encoding: 'utf8' }));
    const files = execFileSync('tar', ['-tzf', archive], { encoding: 'utf8' }).trim().split('\n');
    assert(!files.some((file) => /(?:^|\/)test(?:s)?\/|\.(?:test|spec)\.[cm]?[jt]sx?$/.test(file.replace(/\.d\.ts$/, '.ts'))), '测试源码或声明不得进入发布制品');
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
  assertWriteContext(process.env, sdk.version);
  const api = packageApi(process.env.NODE_AUTH_TOKEN);
  if (mode === 'bootstrap-private') {
    const runId = process.env.GITHUB_RUN_ID;
    const report = await bootstrapPrivateCohort(packages, sdk.version, runId, api, pkg => withBootstrapArchive(pkg, sdk.version, runId, async artifact => {
      execFileSync('npm', ['publish', artifact.archive, '--registry=https://npm.pkg.github.com', `--tag=${BOOTSTRAP_TAG}`, '--access=restricted', '--ignore-scripts'], { cwd: root, stdio: 'inherit' });
      return artifact;
    }));
    console.log(JSON.stringify(report));
    if (report.status === 'needs-admin') {
      console.error('这里只完成空包可见性初始化；仍有名称非 private，须管理员先在 GitHub 包设置中配置并回读。没有上传任何正式 SDK 实现。');
      process.exitCode = 2;
    }
    // 初始化模式不读取 packages.json、dist 或任何正式归档。
  } else {
    const artifacts = JSON.parse(readFileSync(path.join(directory, 'packages.json'), 'utf8')).filter((record) => record.publish);
    assert.deepEqual(artifacts.map((record) => record.source), cohort);
    for (const artifact of artifacts) {
      const current = packageAt(artifact.source);
      assert.equal(artifact.name, current.name); assert.equal(artifact.version, current.version);
      assert.equal(path.dirname(path.resolve(artifact.archive)), directory);
      assert.equal(artifact.integrity, 'sha512-' + createHash('sha512').update(readFileSync(artifact.archive)).digest('base64'));
    }
    if (mode === 'publish') {
      await publishPrivateCohort(artifacts, api, artifact => execFileSync('npm', ['publish', artifact.archive, '--registry=https://npm.pkg.github.com', '--access=restricted', '--ignore-scripts'], { cwd: root, stdio: 'inherit' }));
      for (const artifact of artifacts) console.log(JSON.stringify({ package: artifact.name, version: artifact.version, integrity: artifact.integrity, visibility: 'private', revision: process.env.GITHUB_SHA }));
    } else {
      const report = await cleanupPrivateBootstrap(artifacts, sdk.version, process.env.BOOTSTRAP_RUN_ID, process.env.QIAN_REGISTRY_CONSUMER_VERIFIED === 'true', api);
      console.log(JSON.stringify(report));
    }
  }
}
