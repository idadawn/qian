import {mkdirSync,copyFileSync} from 'node:fs';
const source=new URL('../src/vendor/',import.meta.url),target=new URL('../dist/vendor/',import.meta.url);mkdirSync(target,{recursive:true});
for(const file of ['cordis.js','cordis.d.ts','LICENSE.cordis.txt','provenance.json'])copyFileSync(new URL(file,source),new URL(file,target));
