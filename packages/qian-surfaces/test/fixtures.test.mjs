import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, mkdtempSync, mkdirSync, writeFileSync, symlinkSync, rmSync } from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import { fileURLToPath } from 'node:url';
import { createHash } from 'node:crypto';
import { validateComponent, computeToolHash, HotReloadSurfacePlugin } from '../dist/index.js';
const repo = fileURLToPath(new URL('../../../', import.meta.url));
const cases = JSON.parse(readFileSync(path.join(repo, 'tck/surfaces/cases.json'), 'utf8'));
const normalize = result => ({ valid: result.isValid, documents: result.component?.documents.map(d => ({kind:d.kind,name:d.name,file:d.file,data:d.data,body:d.body})) ?? [], diagnostics: result.diagnostics.map(d => ({ file: d.file, line: d.line, severity: d.severity, pointer: d.error.pointer, code: d.error.errors[0].code })) });
test('共享正反例与结构化错误', () => {
  assert.ok(cases.filter(c => !c.valid).length >= 20);
  const report = {};
  for (const entry of cases) {
    const temporary = mkdtempSync(path.join(os.tmpdir(), 'qian-surface-')), root = path.join(temporary, 'component');
    mkdirSync(root);
    try {
      for (const [file, content] of Object.entries(entry.files)) { const target = path.join(root, file); assert.ok(target.startsWith(root + path.sep)); mkdirSync(path.dirname(target), { recursive: true }); writeFileSync(target, content); }
      for (const [link] of Object.entries(entry.symlinks ?? {})) { const outside = path.join(temporary, 'outside'); mkdirSync(outside); symlinkSync(outside, path.join(root, link), 'dir'); }
      const result = validateComponent(root, { ...entry.options, traceId: '0'.repeat(32) });
      assert.equal(result.isValid, entry.valid, entry.name + ': ' + JSON.stringify(normalize(result)));
      if(['hooks-shell-command','command-server-shell','command-unapproved-shell'].includes(entry.name))assert.throws(()=>new HotReloadSurfacePlugin(root,entry.options),/INPUT_INVALID/);
      if(entry.name==='all-surfaces')assert.doesNotThrow(()=>new HotReloadSurfacePlugin(root,entry.options));
      for (const expected of entry.expected) assert.ok(result.diagnostics.some(d => d.file === expected.file && d.error.pointer === expected.pointer && d.error.errors[0].code === expected.code), entry.name + ': ' + JSON.stringify(normalize(result)));
      for (const d of result.diagnostics) { assert.ok(d.line >= 1); assert.match(d.error.pointer, /^[^#\s]*#(?:\/.*)?$/); assert.equal(d.error.code, 'INPUT_INVALID'); }
      report[entry.name] = normalize(result);
    } finally { rmSync(temporary, { recursive: true, force: true }); }
  }
  if (process.env.QIAN_SURFACE_REPORT) { mkdirSync(path.dirname(process.env.QIAN_SURFACE_REPORT), { recursive: true }); writeFileSync(process.env.QIAN_SURFACE_REPORT, JSON.stringify(report, null, 2)); }
});
test('独立 JCS 数值与 Unicode 向量', () => {
  for (const vector of JSON.parse(readFileSync(path.join(repo, 'tck/surfaces/jcs-vectors.json'), 'utf8'))) {
    assert.equal(createHash('sha256').update(vector.canonical).digest('hex'), vector.sha256);
    assert.equal(computeToolHash(JSON.parse(vector.source)), vector.sha256, vector.name);
  }
});
test('字面量的未成对代理项按值无效处理', async () => {
  // 只可能来自直接传入的字符串（文件按 UTF-8 严格解码）；与 .NET 的 LiteralUnpairedSurrogates_AreInvalidValues 同断言（S1-RT-022）。
  const {parseSurface} = await import('../dist/index.js');
  const lone = String.fromCharCode(0xd800);
  for (const [file, content, markdown] of [['values.json', '{"a": "x' + lone + '"}', false], ['values.yml', 'a: x' + lone + '\n', false], ['guide.md', '---\nname: guide\n---\n' + lone + '\n', true]]) {
    const parsed = parseSurface(file, content, markdown, '0'.repeat(32));
    assert.equal(parsed.syntax, null, file);
    assert.deepEqual(parsed.diagnostics.map(d => [d.line, d.error.pointer, d.error.errors[0].code]), [[1, file + '#', 'SURFACE_SYNTAX_INVALID']], file);
  }
});
test('YAML 核心标量解释一致', async () => {
  const {parseSurface} = await import('../dist/index.js');
  for (const vector of JSON.parse(readFileSync(path.join(repo, 'tck/surfaces/parse-vectors.json'), 'utf8'))) {
    const parsed = parseSurface('values.yml', vector.source);
    assert.deepEqual(parsed.diagnostics, []);
    assert.deepEqual(parsed.syntax.data, vector.data, vector.name);
  }
});
