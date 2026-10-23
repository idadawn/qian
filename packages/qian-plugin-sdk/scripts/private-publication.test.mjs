import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { assertWriteContext, BOOTSTRAP_VERSION, bootstrapFiles, bootstrapPrivateCohort, cleanupPrivateBootstrap, COHORT, inspectBootstrapArchive, integrity, publishPrivateCohort, verifyBootstrapFiles, withBootstrapArchive } from './private-publication.mjs';
import { downloadPackageArchive, packageApi } from './private-package-api.mjs';

const packages = () => COHORT.map(pkg => ({ ...pkg, version: '0.1.0', integrity: integrity(Buffer.from(pkg.name)) }));
function fixture() {
  const pkgs = packages(), events = [], records = new Map(pkgs.map(pkg => [pkg.name, { visibility: 'private' }]));
  const versions = new Map(), emptyFiles = new Map(), rest = new Map();
  const key = (name, version) => `${name}@${version}`;
  const api = {
    async getPackage(name) { events.push(['get-package', name]); return records.get(name) ?? null; },
    async getNpmVersion(name, version) { events.push(['get-npm', name, version]); return versions.get(key(name, version)) ?? null; },
    async listVersions(name) { return [...rest.values()].filter(value => value.packageName === name); },
    async readBootstrapFiles(name) { return emptyFiles.get(name); },
    async getVersion(_name, id) { return rest.get(id) ?? null; },
    async deleteVersion(name, id) { events.push(['delete', name, id]); rest.delete(id); },
  };
  const upload = async pkg => { events.push(['upload', pkg.name]); versions.set(key(pkg.name, pkg.version), { dist: { integrity: pkg.integrity } }); };
  const formal = () => { for (const pkg of pkgs) versions.set(key(pkg.name, pkg.version), { dist: { integrity: pkg.integrity } }); };
  const bootstrap = (index, runId = '123') => {
    const pkg = pkgs[index], id = 100 + index;
    rest.set(id, { id, name: BOOTSTRAP_VERSION, packageName: pkg.name });
    rest.set(id + 1000, { id: id + 1000, name: pkg.version, packageName: pkg.name });
    emptyFiles.set(pkg.name, bootstrapFiles(pkg, '0.1.0', runId));
  };
  return { pkgs, api, upload, events, records, versions, rest, emptyFiles, formal, bootstrap, key };
}

for (const visibility of ['missing', 'public', 'internal']) test(`整组最后一包为 ${visibility} 时零正式上传`, async () => {
  const t = fixture(), last = t.pkgs.at(-1).name;
  if (visibility === 'missing') t.records.delete(last); else t.records.set(last, { visibility });
  await assert.rejects(publishPrivateCohort(t.pkgs, t.api, t.upload), /均已 private/);
  assert.deepEqual(t.events.filter(event => event[0] === 'get-package').map(event => event[1]), COHORT.map(pkg => pkg.name));
  assert.equal(t.events.filter(event => event[0] === 'upload').length, 0);
});
test('后部包同版本不同摘要也在全组上传前失败；私有且匹配才发布缺失版本', async () => {
  const t = fixture(), last = t.pkgs.at(-1);
  t.versions.set(t.key(last.name, last.version), { dist: { integrity: 'different' } });
  await assert.rejects(publishPrivateCohort(t.pkgs, t.api, t.upload), /禁止覆盖/);
  assert.equal(t.events.filter(event => event[0] === 'upload').length, 0);
  t.versions.set(t.key(last.name, last.version), { dist: { integrity: last.integrity } });
  await publishPrivateCohort(t.pkgs, t.api, t.upload);
  assert.deepEqual(t.events.filter(event => event[0] === 'upload').map(event => event[1]), t.pkgs.slice(0, 3).map(pkg => pkg.name));
});
test('未知名称或不完整依赖组在任何 API/上传之前拒绝', async () => {
  const t = fixture();
  for (const pkgs of [t.pkgs.slice(0, 3), [...t.pkgs, { name: '@idadawn/other', source: 'other', version: '0.1.0' }]]) {
    await assert.rejects(publishPrivateCohort(pkgs, t.api, t.upload), /固定顺序/);
    await assert.rejects(bootstrapPrivateCohort(pkgs, '0.1.0', '123', t.api, t.upload), /固定顺序/);
  }
  assert.deepEqual(t.events, []);
});
test('初始化只创建不存在的名称；既有 public/private 均不变，非private明确待管理员', async () => {
  const t = fixture(); t.records.set(t.pkgs[0].name, { visibility: 'public' });
  t.records.delete(t.pkgs[2].name); t.records.delete(t.pkgs[3].name);
  const uploaded = [];
  const report = await bootstrapPrivateCohort(t.pkgs, '0.1.0', '123', t.api, async (pkg, files) => {
    verifyBootstrapFiles(files, pkg, '0.1.0', '123'); uploaded.push(pkg.name);
    t.records.set(pkg.name, { visibility: 'public' });
    t.versions.set(t.key(pkg.name, BOOTSTRAP_VERSION), { dist: { integrity: 'empty-integrity' } });
    return { integrity: 'empty-integrity' };
  });
  assert.deepEqual(uploaded, t.pkgs.slice(2).map(pkg => pkg.name));
  assert.equal(report.status, 'needs-admin'); assert.equal(report.implementationPublished, false);
  assert.equal(report.packages[0].created, false); assert.equal(report.packages[1].created, false);
});
test('真实 npm pack 的空归档仅两文件；源包 files/scripts/dependencies/dist 不进入归档', async () => {
  const pkg = { ...packages()[0], files: ['dist'], main: 'dist/index.js', scripts: { prepack: 'must-not-run' }, dependencies: { implementation: '1.0.0' } };
  await withBootstrapArchive(pkg, '0.1.0', '123', async artifact => {
    const files = inspectBootstrapArchive(readFileSync(artifact.archive));
    verifyBootstrapFiles(files, pkg, '0.1.0', '123');
    const manifest = JSON.parse(files['package/package.json']);
    assert.equal(manifest.version, BOOTSTRAP_VERSION); assert.equal(manifest.repository.url, 'git+https://github.com/idadawn/qian.git');
    assert(!manifest.scripts && !manifest.dependencies && !manifest.main && !manifest.exports);
    assert.equal(manifest['x-qian-private-bootstrap'].runId, '123');
    assert(!Object.values(files).join('').includes('must-not-run'));
  });
});
test('清理在消费未成功、runId缺失、正式版本缺失或摘要不符时零删除', async () => {
  for (const reason of ['consumer', 'run', 'missing', 'integrity']) {
    const t = fixture(); t.formal(); t.bootstrap(0);
    if (reason === 'missing') t.versions.delete(t.key(t.pkgs[3].name, '0.1.0'));
    if (reason === 'integrity') t.versions.set(t.key(t.pkgs[3].name, '0.1.0'), { dist: { integrity: 'changed' } });
    await assert.rejects(cleanupPrivateBootstrap(t.pkgs, '0.1.0', reason === 'run' ? '' : '123', reason !== 'consumer', t.api));
    assert.equal(t.events.filter(event => event[0] === 'delete').length, 0);
  }
});
test('仅删本轮精确空版本；其他运行、未知内容及正式版本均保留', async () => {
  const t = fixture(); t.formal(); t.bootstrap(0); t.bootstrap(1, '999'); t.bootstrap(2); t.bootstrap(3);
  t.emptyFiles.get(t.pkgs[2].name)['package/dist/index.js'] = 'implementation';
  const unknown = JSON.parse(t.emptyFiles.get(t.pkgs[3].name)['package/package.json']); delete unknown['x-qian-private-bootstrap'];
  t.emptyFiles.get(t.pkgs[3].name)['package/package.json'] = JSON.stringify(unknown);
  const result = await cleanupPrivateBootstrap(t.pkgs, '0.1.0', '123', true, t.api);
  assert.deepEqual(t.events.filter(event => event[0] === 'delete'), [['delete', t.pkgs[0].name, 100]]);
  assert.equal(result.packages.filter(pkg => pkg.deleted).length, 1);
  assert(t.rest.has(101) && t.rest.has(102) && t.rest.has(103));
  assert(t.pkgs.every((_pkg, index) => t.rest.has(1100 + index)));
});
test('清理前版本 ID 若对应正式版本则拒绝删除', async () => {
  const t = fixture(); t.formal(); t.bootstrap(0);
  t.api.getVersion = async (_name, id) => ({ id, name: '0.1.0' });
  await assert.rejects(cleanupPrivateBootstrap(t.pkgs, '0.1.0', '123', true, t.api));
  assert.equal(t.events.filter(event => event[0] === 'delete').length, 0);
});
test('所有外部写守卫拒绝非Actions、非main、错误仓库及错误输入版本', () => {
  const env = { GITHUB_ACTIONS: 'true', GITHUB_REF: 'refs/heads/main', GITHUB_REPOSITORY: 'idadawn/qian', RELEASE_VERSION: '0.1.0', GITHUB_RUN_ID: '123', NODE_AUTH_TOKEN: 'test-only' };
  assert.doesNotThrow(() => assertWriteContext(env, '0.1.0'));
  for (const [key, value] of [['GITHUB_ACTIONS', 'false'], ['GITHUB_REF', 'refs/heads/other'], ['GITHUB_REPOSITORY', 'other/qian'], ['RELEASE_VERSION', '0.1.1'], ['GITHUB_RUN_ID', ''], ['NODE_AUTH_TOKEN', '']]) assert.throws(() => assertWriteContext({ ...env, [key]: value }, '0.1.0'));
});
test('API适配器只允许固定包与版本DELETE，不支持删除整个包；403不能当缺包', async () => {
  const calls = [], api = packageApi('test-only', async (url, options) => {
    calls.push([url, options.method]);
    return new Response(null, { status: options.method === 'DELETE' ? 204 : 403 });
  });
  await api.deleteVersion(COHORT[0].name, 123);
  assert.equal(calls[0][0], 'https://api.github.com/users/idadawn/packages/npm/yue-tokens/versions/123');
  await assert.rejects(api.getPackage(COHORT[0].name), /HTTP 403/);
  assert.throws(() => api.deleteVersion('@idadawn/unknown', 123));
  assert.throws(() => api.deleteVersion(COHORT[0].name, '123/../..'));
});
test('真实302下载形态只在registry首跳带认证，固定CDN跳转不泄露凭据', async () => {
  const calls = [];
  const bytes = await downloadPackageArchive('https://npm.pkg.github.com/download/example', 'test-only', async (url, options) => {
    calls.push({ host: url.hostname, auth: options.headers.Authorization, redirect: options.redirect });
    return calls.length === 1 ? new Response(null, { status: 302, headers: { Location: 'https://pkg-npm.githubusercontent.com/test.tgz?signature=example' } }) : new Response('empty-archive');
  });
  assert.equal(bytes.toString(), 'empty-archive');
  assert.deepEqual(calls, [{ host: 'npm.pkg.github.com', auth: 'Bearer test-only', redirect: 'manual' }, { host: 'pkg-npm.githubusercontent.com', auth: undefined, redirect: 'manual' }]);
});
test('拒绝恶意下载源、认证URL、片段、HTTP和第四次跳转', async () => {
  for (const target of ['https://evil.example/archive', 'https://other.githubusercontent.com/archive', 'http://pkg-npm.githubusercontent.com/archive', 'https://user:password@pkg-npm.githubusercontent.com/archive', 'https://pkg-npm.githubusercontent.com/archive#fragment', 'https://pkg-npm.githubusercontent.com:444/archive']) {
    let calls = 0;
    await assert.rejects(downloadPackageArchive('https://npm.pkg.github.com/download/example', 'test-only', async () => { calls++; return new Response(null, { status: 302, headers: { Location: target } }); }), /固定 HTTPS/);
    assert.equal(calls, 1);
  }
  let calls = 0;
  await assert.rejects(downloadPackageArchive('https://npm.pkg.github.com/download/example', 'test-only', async () => { calls++; return new Response(null, { status: 302, headers: { Location: 'https://pkg-npm.githubusercontent.com/loop' } }); }), /超过三次/);
  assert.equal(calls, 4);
});
test('下载同时限制声明长度和实际字节数', async () => {
  await assert.rejects(downloadPackageArchive('https://npm.pkg.github.com/archive', 'test-only', async () => new Response('small', { headers: { 'Content-Length': '65537' } })), /超过限制/);
  await assert.rejects(downloadPackageArchive('https://npm.pkg.github.com/archive', 'test-only', async () => new Response(Buffer.alloc(65537))), /超过限制/);
});
