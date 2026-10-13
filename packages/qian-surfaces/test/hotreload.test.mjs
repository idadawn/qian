import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,readFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {PluginLoader} from '../../qian-runtime/dist/index.js';
import {HotReloadSurfacePlugin,validateComponent} from '../dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const spec=JSON.parse(readFileSync(path.join(root,'tck/runtime/hotreload.json'),'utf8'));
test('T19 共用真实文件监视：表面更新，代码对象和效果不变',async()=>{
 const directory=mkdtempSync(path.join(tmpdir(),'qian-hot-ts-'));let applies=0,disposals=0;
 const runtime=new PluginLoader();
 try{
  const write=(name,content)=>{const file=path.join(directory,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,content);};
  const skill=description=>write(`skills/${spec.skill}/SKILL.md`,`---\nname: ${spec.skill}\ndescription: ${description}\n---\n检查当前 scope 的内容。\n`);
  write('.qian-plugin/plugin.json',JSON.stringify({id:spec.plugin,version:'1.0.0',description:'真实表面热重载的共用运行时场景。',author:{name:'Qian'},license:'Apache-2.0',interface:{displayName:'热重载',category:'testing'},inject:[],provides:['Code'],policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global','tenant'],runtime:{form:'module',entry:'Code'}}));
  skill(spec.oldDescription);write(spec.codeFile,'code one');
  const validation=validateComponent(directory,{profile:'minimal',executionPlane:'server'});assert.ok(validation.isValid,JSON.stringify(validation.diagnostics));
  const code={name:'Code',inject:[],apply(ctx){applies++;ctx.provide('Code',{});ctx.effect(()=>{disposals++;});}};
  const plugin=new HotReloadSurfacePlugin(directory,{profile:'minimal',executionPlane:'server'},code);
  await runtime.mount([{entry:{id:spec.entry,plugin:spec.plugin,version:'1.0.0',scope:spec.scope,layer:0,config:{}},plugin,provides:['Code',...plugin.surfaceProvides]}]);
  assert.equal(runtime.getState(spec.entry,spec.scope),'Ready');
  const ctx=runtime.getContext(spec.entry,spec.scope);const original=ctx.inject('Code');
  assert.equal(ctx.list('Skills').length,1);assert.equal(ctx.list('Tools').length,1);assert.equal(ctx.scoped(spec.otherScope).list('Tools').length,0);
  const description=()=>ctx.list('Tools')[0]?.value.data.description;
  const wait=async predicate=>{const deadline=Date.now()+spec.deadlineMs;while(!predicate()){assert.ok(Date.now()<deadline,'热重载超时');await new Promise(resolve=>setTimeout(resolve,20));}};
  skill(spec.newDescription);await wait(()=>description()===spec.newDescription);
  assert.equal(ctx.inject('Code'),original);assert.equal(applies,spec.expectedCodeApplies);assert.equal(disposals,spec.expectedCodeDisposalsBeforeUnmount);
  write(spec.codeFile,'code changed');await wait(()=>plugin.status.restartRequired);assert.equal(ctx.inject('Code'),original);assert.equal(applies,1);
  skill('bad');await wait(()=>plugin.status.diagnostics.some(item=>item.severity==='error'));assert.equal(description(),spec.newDescription);
  skill(spec.oldDescription);await wait(()=>description()===spec.oldDescription);
  await runtime.unmount(spec.entry,spec.scope);assert.equal(disposals,spec.expectedCodeDisposalsAfterUnmount);assert.equal(runtime.dumpConfig().length,0);
  if(process.env.QIAN_HOT_RELOAD_REPORT){mkdirSync(path.dirname(path.resolve(process.env.QIAN_HOT_RELOAD_REPORT)),{recursive:true});writeFileSync(process.env.QIAN_HOT_RELOAD_REPORT,JSON.stringify({id:spec.id,codeApplies:applies,codeDisposals:disposals,skillUpdated:true,codeStable:true,scoped:true,invalidRetained:true,restartRequired:plugin.status.restartRequired,clean:runtime.dumpConfig().length===0}));}
 }finally{await runtime.dispose();rmSync(directory,{recursive:true,force:true});}
});
