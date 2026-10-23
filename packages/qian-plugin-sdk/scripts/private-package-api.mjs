import assert from 'node:assert/strict';
import { BOOTSTRAP_VERSION, COHORT, inspectBootstrapArchive, integrity } from './private-publication.mjs';

/** GitHub npm 的实际下载链：首跳带认证，CDN 跳转使用签名 URL，绝不转发认证头。 */
export async function downloadPackageArchive(address, token, fetcher = fetch) {
  let url;
  try { url = new URL(address); } catch { throw new Error('包归档地址无效'); }
  const validate = value => assert(value.protocol === 'https:' && !value.username && !value.password && !value.hash && !value.port
    && ['npm.pkg.github.com', 'pkg-npm.githubusercontent.com'].includes(value.hostname), '包归档地址不在固定 HTTPS 下载源内');
  validate(url); assert(url.hostname === 'npm.pkg.github.com', '包归档必须从 npm registry 首跳开始');
  for (let redirects = 0; redirects <= 3; redirects++) {
    let response;
    try {
      response = await fetcher(url, { headers: redirects === 0 ? { Authorization: `Bearer ${token}` } : {}, redirect: 'manual', cache: 'no-store', signal: AbortSignal.timeout(30_000) });
    } catch { throw new Error('包归档下载请求失败'); }
    if ([301, 302, 303, 307, 308].includes(response.status)) {
      await response.body?.cancel();
      assert(redirects < 3, '包归档跳转超过三次');
      const location = response.headers.get('location'); assert(location, '包归档跳转缺少地址');
      try { url = new URL(location, url); } catch { throw new Error('包归档跳转地址无效'); }
      validate(url); continue;
    }
    assert.equal(response.status, 200, '包归档不可读');
    const declaredLength = Number(response.headers.get('content-length'));
    assert(Number.isFinite(declaredLength) && declaredLength <= 64 * 1024, '包归档超过限制');
    assert(response.body, '包归档内容为空');
    const chunks = []; let length = 0;
    for await (const chunk of response.body) { length += chunk.length; assert(length <= 64 * 1024, '包归档超过限制'); chunks.push(chunk); }
    return Buffer.concat(chunks);
  }
  throw new Error('包归档下载未完成');
}

/** 只访问本任务固定包；不使用 gh/PAT 回退，也不记录认证头或响应中的临时下载地址。 */
export function packageApi(token, fetcher = fetch) {
  const endpoint = name => {
    assert(COHORT.some(pkg => pkg.name === name), '不允许访问未知包');
    return `https://api.github.com/users/idadawn/packages/npm/${encodeURIComponent(name.split('/')[1])}`;
  };
  const headers = { Authorization: `Bearer ${token}`, Accept: 'application/vnd.github+json', 'X-GitHub-Api-Version': '2022-11-28' };
  async function github(url, method = 'GET') {
    const response = await fetcher(url, { method, headers, redirect: 'error', cache: 'no-store', signal: AbortSignal.timeout(30_000) });
    if (response.status === 404 && method === 'GET') return null;
    assert.equal(response.status, method === 'DELETE' ? 204 : 200, `包 API ${method} 失败（HTTP ${response.status}）`);
    return method === 'DELETE' ? undefined : response.json();
  }
  async function getNpmVersion(name, version) {
    endpoint(name);
    const response = await fetcher('https://npm.pkg.github.com/' + encodeURIComponent(name), { headers: { Authorization: `Bearer ${token}` }, redirect: 'error', cache: 'no-store', signal: AbortSignal.timeout(30_000) });
    if (response.status === 404) return null;
    assert.equal(response.status, 200, `无法核对 ${name} 的私有源版本`);
    return (await response.json()).versions?.[version] ?? null;
  }
  return {
    getPackage: name => github(endpoint(name)),
    getNpmVersion,
    async listVersions(name) {
      const versions = [];
      for (let page = 1; ; page++) {
        const next = await github(`${endpoint(name)}/versions?per_page=100&page=${page}`);
        assert(Array.isArray(next), '包版本列表不可用'); versions.push(...next);
        if (next.length < 100) return versions;
      }
    },
    getVersion: (name, id) => { assert(Number.isSafeInteger(id) && id > 0); return github(`${endpoint(name)}/versions/${id}`); },
    deleteVersion: (name, id) => { assert(Number.isSafeInteger(id) && id > 0); return github(`${endpoint(name)}/versions/${id}`, 'DELETE'); },
    async readBootstrapFiles(name) {
      const version = await getNpmVersion(name, BOOTSTRAP_VERSION);
      assert(version, '空版本元数据不可用');
      const bytes = await downloadPackageArchive(version.dist.tarball, token, fetcher);
      assert.equal(integrity(bytes), version.dist.integrity, '空版本归档摘要不符');
      return inspectBootstrapArchive(bytes);
    },
  };
}
