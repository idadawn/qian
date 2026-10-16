import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { compileFromFile } from 'json-schema-to-typescript';
const root = new URL('../src/', import.meta.url);
async function output(target, text) {
  if (process.argv.includes('--check')) {
    if (await readFile(target, 'utf8') !== text) throw new Error(`生成契约已过期：${fileURLToPath(target)}`);
  } else await writeFile(target, text);
}
// ErrorDetail 只从冻结契约复制，随包发布的副本不能手改。
await output(new URL('error-detail.schema.json', root), await readFile(new URL('../../../../contracts/v0/error-detail.schema.json', root), 'utf8'));
for (const name of ['component-manifest', 'error-detail', 'sse-event', 'form', 'list', 'widget']) {
  const generated = await compileFromFile(fileURLToPath(new URL(`${name}.schema.json`, root)), {
    bannerComment: `// 由 ${name}.schema.json 生成；运行 contracts:generate。`, unknownAny: true,
  });
  await output(new URL(`${name}.generated.ts`, root), generated);
}
