import { readFile, writeFile } from 'node:fs/promises';
const catalog = JSON.parse(await readFile(new URL('../src/manifest-catalog.json', import.meta.url), 'utf8'));
const { name, version } = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
await writeFile(new URL('../dist/yue-manifest.json', import.meta.url), JSON.stringify(Object.values(catalog).map(item => ({ ...item, package: name, version })), null, 2) + '\n');
