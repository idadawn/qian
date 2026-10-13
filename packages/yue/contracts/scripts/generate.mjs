import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import { compile } from 'json-schema-to-typescript';
const root = new URL('../src/', import.meta.url);
const schema = JSON.parse(await readFile(new URL('component-manifest.schema.json', root), 'utf8'));
const generated = await compile(schema, 'ComponentManifest', { bannerComment: '// 由 component-manifest.schema.json 生成；运行 contracts:generate。', unknownAny: true });
const target = new URL('component-manifest.generated.ts', root);
if (process.argv.includes('--check')) {
  if (await readFile(target, 'utf8') !== generated) throw new Error(`生成类型已过期：${fileURLToPath(target)}`);
} else await writeFile(target, generated);
