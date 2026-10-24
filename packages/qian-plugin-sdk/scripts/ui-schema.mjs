import { readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';

// 两份派生物，都由 schemas/v2/ui.schema.json 生成、禁止手改，--check 只比对不写：
// 1. src/ui-schema.generated.ts：Schema 的 TS 副本（slotContracts 与工具索引用）；
// 2. src/ui-validators.generated.ts：Ajv standalone 预编译的清单校验器与贡献校验器（S1-YUE-012）。浏览器运行时不再携带 Ajv 编译器、
//    不在运行期生成代码（外壳首屏预算与 CSP）；Ajv 的两个运行时辅助函数内联，不依赖 ajv 的内部路径。
//    contribution.parameters 的元 Schema 引用不进预编译：客户端动作参数改由 src/parameters.ts 的可解释子集校验（比元 Schema 更严），
//    否则 2020-12 元 Schema 的整套校验器会进首屏。
const require = createRequire(import.meta.url);
const { Ajv2020 } = require('ajv/dist/2020.js');
const standaloneCode = require('ajv/dist/standalone').default;
const schema = JSON.parse(readFileSync(new URL('../../../schemas/v2/ui.schema.json', import.meta.url), 'utf8'));

const schemaTarget = new URL('../src/ui-schema.generated.ts', import.meta.url);
const schemaContent = '// 由 schemas/v2/ui.schema.json 派生；禁止手改。运行 scripts/ui-schema.mjs。\nexport const uiSchema = ' + JSON.stringify(schema, null, 2) + ' as const;\n';

const compiled = structuredClone(schema);
if (compiled.$defs.contribution.properties.parameters.$ref !== 'https://json-schema.org/draft/2020-12/schema') throw new Error('contribution.parameters 不再引用 2020-12 元 Schema，检查预编译的剥离规则');
delete compiled.$defs.contribution.properties.parameters.$ref;
const ajv = new Ajv2020({ strict: true, allErrors: true, code: { source: true, esm: true, lines: true } });
ajv.addSchema(compiled);
let code = standaloneCode(ajv, { validateUiManifest: schema.$id, validateContribution: `${schema.$id}#/$defs/contribution` });
const helpers = {
  'require("ajv/dist/runtime/ucs2length").default': 'ucs2length',
  'require("ajv/dist/runtime/equal").default': 'jsonEqual',
};
for (const [from, to] of Object.entries(helpers)) code = code.split(from).join(to);
if (/\brequire\(|\bnew Function\b|\beval\(/.test(code)) throw new Error('预编译校验器含未内联的 require 或运行期代码生成');
code = code.replace(/^"use strict";\s*/, '');
const validatorsTarget = new URL('../src/ui-validators.generated.ts', import.meta.url);
const validatorsContent = `// 由 schemas/v2/ui.schema.json 经 Ajv standalone 预编译（contribution.parameters 不含元 Schema 引用，见 src/parameters.ts）；禁止手改。运行 scripts/ui-schema.mjs。
/* eslint-disable */
// @ts-nocheck
// Ajv 运行时辅助（与 ajv/dist/runtime/ucs2length、equal 同义）：按 Unicode 码点计长；JSON 值的深相等。
function ucs2length(str) { let length = 0; for (let pos = 0; pos < str.length; length++) { const value = str.charCodeAt(pos++); if (value >= 0xd800 && value <= 0xdbff && pos < str.length && (str.charCodeAt(pos) & 0xfc00) === 0xdc00) pos++; } return length; }
function jsonEqual(a, b) {
  if (a === b) return true;
  if (!a || !b || typeof a !== 'object' || typeof b !== 'object' || Array.isArray(a) !== Array.isArray(b)) return a !== a && b !== b;
  const keys = Object.keys(a);
  if (keys.length !== Object.keys(b).length) return false;
  return keys.every((key) => Object.prototype.hasOwnProperty.call(b, key) && jsonEqual(a[key], b[key]));
}
${code}
`;

if (process.argv.includes('--check')) {
  if (readFileSync(schemaTarget, 'utf8') !== schemaContent) throw new Error('SDK ui/v2 Schema 副本发生漂移');
  if (readFileSync(validatorsTarget, 'utf8') !== validatorsContent) throw new Error('SDK ui/v2 预编译校验器发生漂移');
} else { writeFileSync(schemaTarget, schemaContent); writeFileSync(validatorsTarget, validatorsContent); }
