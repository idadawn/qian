import { readFileSync, writeFileSync } from 'node:fs';
const schema = JSON.parse(readFileSync(new URL('../../../schemas/v2/ui.schema.json', import.meta.url), 'utf8'));
const target = new URL('../src/ui-schema.generated.ts', import.meta.url);
const content = '// 由 schemas/v2/ui.schema.json 派生；禁止手改。运行 scripts/ui-schema.mjs。\nexport const uiSchema = ' + JSON.stringify(schema, null, 2) + ' as const;\n';
if (process.argv.includes('--check')) {
  if (readFileSync(target, 'utf8') !== content) throw new Error('SDK ui/v2 Schema 副本发生漂移');
} else writeFileSync(target, content);
