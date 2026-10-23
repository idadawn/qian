import assert from 'node:assert/strict';
import { createHash } from 'node:crypto';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';

export const REPOSITORY = 'idadawn/qian';
export const BOOTSTRAP_VERSION = '0.0.0-private.0';
export const BOOTSTRAP_TAG = 'private-bootstrap';
export const COHORT = Object.freeze([
  { name: '@idadawn/yue-tokens', source: 'packages/yue/tokens' },
  { name: '@idadawn/yue-contracts', source: 'packages/yue/contracts' },
  { name: '@idadawn/yue-sdk', source: 'packages/yue/sdk' },
  { name: '@idadawn/qian-plugin-sdk', source: 'packages/qian-plugin-sdk' },
].map(Object.freeze));
const MARKER = 'x-qian-private-bootstrap';
const MARKER_SCHEMA = 'qian.private-bootstrap/v1';
const versionPattern = /^\d+\.\d+\.\d+$/;
export const integrity = bytes => 'sha512-' + createHash('sha512').update(bytes).digest('base64');

export function assertCohort(packages) {
  assert.deepEqual(packages.map(({ name, source }) => ({ name, source })), COHORT, '只允许完整、固定顺序的四包依赖组');
  for (const pkg of packages) assert.match(pkg.version, versionPattern, '正式包版本必须固定且非预发布版本');
}
export function assertWriteContext(env, sdkVersion) {
  assert.equal(env.GITHUB_ACTIONS, 'true', '包写操作只允许 Actions');
  assert.equal(env.GITHUB_REF, 'refs/heads/main');
  assert.equal(env.GITHUB_REPOSITORY, REPOSITORY);
  assert.match(sdkVersion, versionPattern);
  assert.equal(env.RELEASE_VERSION, sdkVersion);
  assert.match(env.GITHUB_RUN_ID ?? '', /^[1-9]\d*$/, '需要可追溯的 Actions 运行 ID');
  assert(env.NODE_AUTH_TOKEN, '缺少 Actions 包发布凭据');
}

/** 完整预检先结束，才允许任何正式制品上传。404、public、internal 均失败。 */
export async function requirePrivateCohort(packages, api) {
  assertCohort(packages);
  const records = [];
  for (const pkg of packages) records.push({ name: pkg.name, record: await api.getPackage(pkg.name) });
  const refused = records.filter(({ record }) => record?.visibility !== 'private');
  assert.equal(refused.length, 0, '正式发布要求四包均已 private；缺失或非私有：' + refused.map(({ name, record }) => `${name}=${record?.visibility ?? 'missing'}`).join(', '));
  return records;
}

export async function publishPrivateCohort(packages, api, upload) {
  await requirePrivateCohort(packages, api);
  const existing = new Map();
  // 同版本冲突也必须在任何上传前整组检查完，保留原防覆盖门。
  for (const pkg of packages) {
    const value = await api.getNpmVersion(pkg.name, pkg.version);
    if (value) assert.equal(value.dist.integrity, pkg.integrity, `${pkg.name} 同版本制品不同，禁止覆盖`);
    existing.set(pkg.name, value);
  }
  for (const pkg of packages) {
    assert.equal((await api.getPackage(pkg.name))?.visibility, 'private', '上传前可见性发生变化：' + pkg.name);
    if (!existing.get(pkg.name)) await upload(pkg);
    const published = await api.getNpmVersion(pkg.name, pkg.version);
    assert(published, '发布后未查到固定版本：' + pkg.name);
    assert.equal(published.dist.integrity, pkg.integrity);
    assert.equal((await api.getPackage(pkg.name))?.visibility, 'private', '上传后可见性不符：' + pkg.name);
  }
}

export function bootstrapFiles(pkg, releaseVersion, runId) {
  assert(COHORT.some(entry => entry.name === pkg.name && entry.source === pkg.source), '不允许初始化未知包');
  assert.match(pkg.version, versionPattern); assert.match(releaseVersion, versionPattern); assert.match(runId, /^[1-9]\d*$/);
  const marker = { schema: MARKER_SCHEMA, repository: REPOSITORY, runId, releaseVersion, packageName: pkg.name, packageVersion: pkg.version };
  const manifest = {
    name: pkg.name, version: BOOTSTRAP_VERSION, description: 'Qian 私有包可见性初始化占位版本，不包含 SDK 实现。', license: 'Apache-2.0',
    repository: { type: 'git', url: `git+https://github.com/${REPOSITORY}.git`, directory: pkg.source },
    files: ['README.md'], publishConfig: { registry: 'https://npm.pkg.github.com', access: 'restricted' }, [MARKER]: marker,
  };
  const readme = `# 私有包可见性初始化\n\n此版本仅用于初始化 ${pkg.name} 的 GitHub Packages 可见性与 Actions 访问权限。\n\n只含 package.json 和本说明，不提供任何 SDK、运行时或应用实现，不可用作正式依赖。正式版本为 ${pkg.version}。初始化运行：${runId}。\n`;
  return { 'package/package.json': JSON.stringify(manifest, null, 2) + '\n', 'package/README.md': readme };
}
export function inspectBootstrapArchive(bytes) {
  assert(bytes.length <= 64 * 1024, '初始化归档异常过大');
  const names = execFileSync('tar', ['-tzf', '-'], { input: bytes, encoding: 'utf8' }).trim().split('\n').sort();
  assert.deepEqual(names, ['package/README.md', 'package/package.json'], '初始化归档只能包含 package.json 和 README.md');
  return Object.fromEntries(names.map(name => [name, execFileSync('tar', ['-xOzf', '-', name], { input: bytes, encoding: 'utf8' })]));
}
export function verifyBootstrapFiles(files, pkg, releaseVersion, runId) {
  const expected = bootstrapFiles(pkg, releaseVersion, runId);
  assert.deepEqual(Object.keys(files).sort(), Object.keys(expected).sort());
  assert.deepEqual(JSON.parse(files['package/package.json']), JSON.parse(expected['package/package.json']), '初始化标记、版本或包身份不匹配');
  assert.equal(files['package/README.md'], expected['package/README.md'], '初始化说明与本轮标记不匹配');
}

/** 新建隔离目录，不复制源包，不读取任何 dist/实现，不继承源包 scripts/files/dependencies。 */
export async function withBootstrapArchive(pkg, releaseVersion, runId, consume) {
  const temporary = mkdtempSync(path.join(tmpdir(), 'qian-private-bootstrap-'));
  try {
    const files = bootstrapFiles(pkg, releaseVersion, runId);
    for (const [name, content] of Object.entries(files)) writeFileSync(path.join(temporary, path.basename(name)), content);
    const packed = JSON.parse(execFileSync('npm', ['pack', '.', '--ignore-scripts', '--json'], { cwd: temporary, encoding: 'utf8' }));
    assert.equal(packed.length, 1); assert.equal(path.basename(packed[0].filename), packed[0].filename);
    const archive = path.join(temporary, packed[0].filename);
    const bytes = readFileSync(archive);
    verifyBootstrapFiles(inspectBootstrapArchive(bytes), pkg, releaseVersion, runId);
    return await consume({ archive, integrity: integrity(bytes) });
  } finally { rmSync(temporary, { recursive: true, force: true }); }
}

export async function bootstrapPrivateCohort(packages, releaseVersion, runId, api, uploadEmpty) {
  assertCohort(packages);
  const before = new Map();
  for (const pkg of packages) before.set(pkg.name, await api.getPackage(pkg.name));
  const results = [];
  for (const pkg of packages) {
    // 既有名称不发布、不改 tag 或版本。上传前再次查存在性，避免覆盖并发创建。
    const existing = before.get(pkg.name) ?? await api.getPackage(pkg.name);
    if (existing) { results.push({ name: pkg.name, created: false, visibility: existing.visibility }); continue; }
    const expected = await uploadEmpty(pkg, bootstrapFiles(pkg, releaseVersion, runId));
    const version = await api.getNpmVersion(pkg.name, BOOTSTRAP_VERSION);
    assert(version, '初始化上传后未查到空版本：' + pkg.name);
    assert.equal(version.dist.integrity, expected.integrity, '初始化制品摘要不符');
    const record = await api.getPackage(pkg.name);
    assert(record, '初始化上传后未查到包：' + pkg.name);
    results.push({ name: pkg.name, created: true, version: BOOTSTRAP_VERSION, visibility: record.visibility });
  }
  return { operation: 'bootstrap-private', bootstrapRunId: runId, implementationPublished: false,
    status: results.every(item => item.visibility === 'private') ? 'visibility-ready' : 'needs-admin', packages: results };
}

/** 只在正式 registry 消费成功后，按原 bootstrap runId 和归档标记删除精确空版本。 */
export async function cleanupPrivateBootstrap(packages, releaseVersion, runId, consumerVerified, api) {
  assert.equal(consumerVerified, true, 'registry 消费未成功，不允许清理');
  assert.match(runId ?? '', /^[1-9]\d*$/, '清理必须显式提供初始化运行 ID');
  await requirePrivateCohort(packages, api);
  // 所有正式版本须存在且摘要与本次已验收制品相同，不能删除某包仅有的空版本。
  for (const pkg of packages) {
    const formal = await api.getNpmVersion(pkg.name, pkg.version);
    assert(formal, '缺少正式版本，不允许清理：' + pkg.name);
    assert.equal(formal.dist.integrity, pkg.integrity, '正式制品摘要不符，不允许清理：' + pkg.name);
  }
  const result = [];
  for (const pkg of packages) {
    const candidates = (await api.listVersions(pkg.name)).filter(version => version.name === BOOTSTRAP_VERSION);
    assert(candidates.length <= 1, '初始化版本不唯一');
    if (!candidates.length) { result.push({ name: pkg.name, deleted: false, reason: 'absent' }); continue; }
    const candidate = candidates[0];
    assert(Number.isSafeInteger(candidate.id) && candidate.id > 0);
    const files = await api.readBootstrapFiles(pkg.name);
    let owned = false;
    try { verifyBootstrapFiles(files, pkg, releaseVersion, runId); owned = true; }
    catch { /* 不属于本轮、没有标记或带实现的版本一律保留。 */ }
    if (!owned) { result.push({ name: pkg.name, deleted: false, reason: 'unrecognized-bootstrap' }); continue; }
    assert.equal((await api.getPackage(pkg.name))?.visibility, 'private');
    const live = await api.getVersion(pkg.name, candidate.id);
    assert.equal(live?.name, BOOTSTRAP_VERSION); assert.equal(live?.id, candidate.id);
    await api.deleteVersion(pkg.name, candidate.id);
    assert.equal(await api.getVersion(pkg.name, candidate.id), null, '空版本删除未确认');
    result.push({ name: pkg.name, deleted: true, version: BOOTSTRAP_VERSION, id: candidate.id });
  }
  return { operation: 'cleanup', bootstrapRunId: runId, packages: result };
}
