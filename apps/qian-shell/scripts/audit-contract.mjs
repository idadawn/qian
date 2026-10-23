import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
const state = JSON.parse(readFileSync(process.env.QIAN_MF_AUDIT_STATE, 'utf8'));
const url = new URL(state.url);
assert.equal(url.protocol, 'http:'); assert.ok(['127.0.0.1', '[::1]'].includes(url.hostname));
const read = async () => {
  const response = await fetch(new URL('/__acceptance/audit', url), { headers: { Authorization: `Bearer ${state.readerToken}` }, redirect: 'error' });
  assert.equal(response.status, 200); return response.json();
};
const report = { name: 'shell.remote.rejected', remoteId: 'qian.tenancy', scope: `tenant:${state.tenantA}`, reason: 'engines.yue', code: 'INPUT_INVALID', hostVersion: state.yueVersion, requiredVersion: '^99.0.0' };
const before = await read();
for (const [token, body, expected] of [
  [null, report, 401], [state.readerToken, report, 401], [state.writerTokenB, report, 403],
  [state.writerToken, { ...report, actor: 'forged-actor' }, 400], [state.writerToken, { ...report, remoteId: 'qian.unknown' }, 400],
  [state.writerToken, { ...report, scope: `tenant:${state.tenantB}` }, 403], [state.writerToken, { ...report, requiredVersion: '^0.1.0' }, 400],
]) {
  const response = await fetch(new URL('/api/shell/audit/remote-rejected', url), { method: 'POST', redirect: 'error', headers: { 'Content-Type': 'application/json', ...(token ? { Authorization: `Bearer ${token}` } : {}) }, body: JSON.stringify(body) });
  assert.equal(response.status, expected);
  const detail = await response.json(); assert.equal(detail.status, expected); assert.match(detail.trace_id, /^[0-9a-f]{32}$/);
}
const after = await read(); assert.equal(after.checkedEntries, before.checkedEntries); assert.equal(after.valid, true);
console.log('审计端口 7 个认证、租户与固定事件负例通过，真实链未新增条目。');
