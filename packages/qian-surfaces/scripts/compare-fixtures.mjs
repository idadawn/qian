import { readFileSync } from 'node:fs';
import assert from 'node:assert/strict';
const [dotnet, typescript] = process.argv.slice(2);
assert.ok(dotnet && typescript, '需提供 .NET 与 TS 两份报告路径');
const left = JSON.parse(readFileSync(dotnet, 'utf8')), right = JSON.parse(readFileSync(typescript, 'utf8'));
assert.deepEqual(Object.keys(left).sort(), Object.keys(right).sort());
for (const name of Object.keys(left)) assert.deepEqual(left[name], right[name], name);
console.log(left.id === 'T19' ? '双侧 T19 实际文件监视、代码生命周期与清理结果一致。' : `双侧 ${Object.keys(left).length} 个样例的有效性、错误码、文件、指针、行号与级别一致。`);
