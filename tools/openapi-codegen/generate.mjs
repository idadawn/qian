import { readFile, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';
import openapiTS, { astToString } from 'openapi-typescript';
const { values } = parseArgs({ args: process.argv.slice(2).filter((value) => value !== '--'), options: { source: { type: 'string' }, output: { type: 'string' }, 'schema-output': { type: 'string' }, check: { type: 'boolean', default: false } } });
if (!values.source || !values.output) throw new Error('必须指定 --source 与 --output');
const output = '// 由 OpenAPI 契约生成；运行对应包的 contracts:generate。\n' + astToString(await openapiTS(pathToFileURL(resolve(values.source))));
if (values.check) {
  if (await readFile(values.output, 'utf8') !== output) throw new Error(`契约类型已过期：${values.output}`);
} else await writeFile(values.output, output);
if (values['schema-output']) {
  const schema = await readFile(values.source, 'utf8');
  if (values.check) {
    if (await readFile(values['schema-output'], 'utf8') !== schema) throw new Error(`运行时契约副本已过期：${values['schema-output']}`);
  } else await writeFile(values['schema-output'], schema);
}
