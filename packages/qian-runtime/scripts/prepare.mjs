import {readFileSync,writeFileSync} from 'node:fs';
import {createHash} from 'node:crypto';
const vendor=new URL('../src/vendor/',import.meta.url),record=JSON.parse(readFileSync(new URL('provenance.json',vendor),'utf8'));
for(const[file,expected]of Object.entries(record.files)){const actual=createHash('sha256').update(readFileSync(new URL(file,vendor))).digest('hex');if(actual!==expected)throw new Error('Vendor 文件校验失败：'+file);}
const codes=JSON.parse(readFileSync(new URL('../../../contracts/v0/error-codes.json',import.meta.url),'utf8')).codes;
const selected=Object.fromEntries(codes.filter(c=>['DEPENDENCY_MISSING','DEPENDENCY_CYCLE','REGISTRATION_CONFLICT','NOT_FOUND','PLUGIN_DEPENDENCY_PENDING'].includes(c.code)).map(c=>[c.code,c]));
writeFileSync(new URL('../src/error-catalog.ts',import.meta.url),'// 由 contracts/v0/error-codes.json 派生；禁止手改。\nexport const errorCatalog = '+JSON.stringify(selected,null,2)+' as const;\n');
