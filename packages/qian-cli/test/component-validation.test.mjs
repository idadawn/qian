import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,mkdirSync,writeFileSync,readFileSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath} from 'node:url';
import {validateComponent} from '@qian/surfaces';
import {validateOfflineComponent} from '../dist/component-validation.js';

const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const write=(root,name,value)=>{const file=path.join(root,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,value);};

// 带代码组件的工具由 [Capability] 派生，command 绑定指向本组件命名空间：CLI 离线校验视为已登记；指向别的组件仍是未登记引用。
test('离线校验把组件自身命名空间的 command 工具视为已登记能力，跨组件的 command 引用仍报错',()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-offline-validation-'));
 try{
  const tool=JSON.parse(JSON.parse(readFileSync(path.join(repo,'tck/surfaces/cases.json'),'utf8')).find(item=>item.name==='all-surfaces').files['tools/read.json']);
  write(root,'.qian-plugin/plugin.json',JSON.stringify({id:'test.commands',version:'1.0.0',description:'带命令工具的离线校验验收组件。',author:{name:'Fixture'},license:'Apache-2.0',
   interface:{displayName:'命令工具',category:'operations'},inject:[],provides:[],policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global']}));
  const unknown=diagnostic=>(diagnostic.error.errors??[]).some(item=>item.code==='SURFACE_REFERENCE_UNKNOWN'&&item.pointer==='tools/read.json#/binding/id');
  write(root,'tools/read.json',JSON.stringify({...tool,binding:{kind:'command',id:'test.commands/read'}}));
  assert.ok(validateComponent(root,{profile:'minimal'}).diagnostics.some(unknown));
  const own=validateOfflineComponent(root,{profile:'minimal'});
  assert.equal(own.isValid,true,JSON.stringify(own.diagnostics));
  write(root,'tools/read.json',JSON.stringify({...tool,binding:{kind:'command',id:'other.component/read'}}));
  const foreign=validateOfflineComponent(root,{profile:'minimal'});
  assert.equal(foreign.isValid,false);assert.ok(foreign.diagnostics.some(unknown));
 }finally{rmSync(root,{recursive:true,force:true});}
});
