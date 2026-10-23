import { readFileSync, writeFileSync } from 'node:fs';
const codes = JSON.parse(readFileSync(new URL('../../../contracts/v0/error-codes.json', import.meta.url), 'utf8')).codes;
const selected = Object.fromEntries(codes.filter(value => ['INPUT_INVALID', 'INSUFFICIENT_SCOPE', 'NOT_FOUND', 'CONFLICT'].includes(value.code)).map(value => [value.code, value]));
const path = new URL('../src/error-catalog.ts', import.meta.url);
const content = '// 由 contracts/v0/error-codes.json 派生；禁止手改。\nexport const errorCatalog = ' + JSON.stringify(selected, null, 2) + ' as const;\n';
if (process.argv.includes('--check')) { if (readFileSync(path, 'utf8') !== content) throw new Error('SDK 错误目录发生漂移'); }
else writeFileSync(path, content);
