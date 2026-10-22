import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdirSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {validateMarketplace,surfaceErrorCodes} from '../dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
test('市场文档共用 TCK：结构、重复名称、分类、路径及私钥保护',()=>{
 const report={};
 const fields=Object.values(surfaceErrorCodes).sort();assert.deepEqual(fields,JSON.parse(readFileSync(path.join(root,'tck/marketplaces/field-codes.json'),'utf8')));assert.equal(new Set(fields).size,fields.length);report['field-codes']=fields;
 for(const entry of JSON.parse(readFileSync(path.join(root,'tck/marketplaces/cases.json'),'utf8'))){
  const result=validateMarketplace('.qian/marketplace.json',entry.content,'0'.repeat(32));
  const normalized={valid:result.isValid,diagnostics:result.diagnostics.map(d=>({file:d.file,line:d.line,severity:d.severity,pointer:d.error.pointer,code:d.error.errors[0].code}))};
  assert.equal(result.isValid,entry.valid,entry.name+': '+JSON.stringify(normalized));
  assert.ok(!JSON.stringify(result).includes('DO-NOT-LEAK'));
  report[entry.name]=normalized;
 }
 if(process.env.QIAN_MARKET_REPORT){mkdirSync(path.dirname(process.env.QIAN_MARKET_REPORT),{recursive:true});writeFileSync(process.env.QIAN_MARKET_REPORT,JSON.stringify(report,null,2));}
});
