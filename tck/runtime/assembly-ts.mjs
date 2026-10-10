// T15–T17 复用真实 profile 装配器和 Runtime；Fixture Provider 只负责暴露配置值。
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdtempSync,cpSync,rmSync,mkdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';

export function assemblyScenarios(PluginLoader,loadProfile,root){
 for(const item of JSON.parse(readFileSync(path.join(root,'tck/runtime/assembly.json'),'utf8')))test(item.id,async()=>{
  const directory=mkdtempSync(path.join(os.tmpdir(),'qian-runtime-assembly-'));
  const runtime=new PluginLoader(),applied=[];
  try{
   cpSync(path.join(root,'tck/profiles/base'),directory,{recursive:true});
   for(const[file,content]of Object.entries(item.files)){const target=path.join(directory,file);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,content);}
   const result=loadProfile({workingDirectory:directory,profile:'test',includeHomePatch:false,patches:item.patches});
   assert.equal(result.isValid,item.valid,JSON.stringify(result));
   if(result.isValid){
    await runtime.mount(result.snapshot.entries.map(entry=>({entry:{id:entry.id,plugin:entry.plugin.split('@')[0],version:entry.plugin.split('@')[1],scope:entry.scope,layer:entry.layer,config:entry.config},provides:entry.provides,plugin:{name:entry.id,inject:entry.inject,apply(ctx,config){applied.push(entry.id);if(entry.provides.includes('Settings'))ctx.provide('Settings',config.marker);else assert.ok(ctx.inject('Settings'));}}})));
    const settings=runtime.dumpConfig().find(e=>e.id==='settings');
    assert.equal(settings.plugin+'@'+settings.version,item.plugin);assert.deepEqual(settings.config,item.config);assert.equal(result.snapshot.layers[settings.layer].kind,item.layer);
    assert.deepEqual(applied,['settings','consumer']);assert.equal(runtime.getContext('consumer').inject('Settings'),item.consumer);
    await runtime.dispose();assert.deepEqual(runtime.dumpConfig(),[]);
   }else{assert.ok(result.diagnostics.some(d=>d.error.pointer===item.pointer));assert.deepEqual(applied,[]);assert.deepEqual(runtime.dumpConfig(),[]);}
  }finally{await runtime.dispose();rmSync(directory,{recursive:true,force:true});}
 });
}
