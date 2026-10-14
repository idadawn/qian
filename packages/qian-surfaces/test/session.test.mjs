import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {PluginLoader} from '../../qian-runtime/dist/index.js';
import {SurfaceRegistrationSession,validateComponent} from '../dist/index.js';
const plugin='test.session',keep=plugin+'/keep',keys=['Skills','Tools'];
const first='当用户需要核对第一版租户资料时读取已授权内容并给出完整结果。',second='当用户需要核对第二版租户资料时按更新后的规则读取内容并给出完整结果。';
const snapshot=skills=>{
 const directory=mkdtempSync(path.join(tmpdir(),'qian-session-ts-'));
 try{
  const write=(name,content)=>{const file=path.join(directory,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,content);};
  write('.qian-plugin/plugin.json',JSON.stringify({id:plugin,version:'1.0.0',description:'声明式表面替换的并发读取验收组件。',author:{name:'Qian'},license:'Apache-2.0',interface:{displayName:'表面替换',category:'testing'},inject:[],provides:[],policy:{tier:'private',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global','tenant']}));
  for(const [name,description]of skills)write(`skills/${name}/SKILL.md`,`---\nname: ${name}\ndescription: ${description}\n---\n只读取当前 scope 的已授权内容。\n`);
  const result=validateComponent(directory,{profile:'minimal',executionPlane:'server'});assert.ok(result.isValid,JSON.stringify(result.diagnostics));return result;
 }finally{rmSync(directory,{recursive:true,force:true});}
};
const host=async()=>{const runtime=new PluginLoader();await runtime.mount([{entry:{id:'surfaces',plugin,version:'1.0.0',scope:'global',layer:0,config:{}},plugin:{name:plugin,inject:[],apply(){}},provides:[]}]);assert.equal(runtime.getState('surfaces'),'Ready');return runtime;};
// 转发到真实上下文；每次登记、替换与撤销完成后回调，并可让第 n 次替换失败。
const observed=ctx=>{
 const port={changed:()=>{},failReplace:0,replacements:0,entry:ctx.entry,scope:ctx.scope,effect:dispose=>ctx.effect(dispose)};
 port.provide=(...args)=>{const handle=ctx.provide(...args),label=args.length===3?args[0]+':'+args[1]:args[0];port.changed('登记 '+label);
  return Object.assign(()=>{const result=handle();port.changed('撤销 '+label);return result;},{replace(value){if(++port.replacements===port.failReplace)throw new Error('注入的替换失败。');handle.replace(value);port.changed('替换 '+label);}});};
 return port;
};
const description=(ctx,key,name)=>ctx.inject(key,plugin+'/'+name).data.description;
test('并发读取方在反复替换期间只看到旧文档或新文档，从不缺失',async()=>{
 const before=snapshot([['keep',first],['drop',first]]),after=snapshot([['keep',second],['add',second]]);
 const runtime=await host();
 try{
  const ctx=runtime.getContext('surfaces'),session=await SurfaceRegistrationSession.create(ctx,before),seen=new Set();let misses=0,reads=0,done=false;
  // 读取方在每次读取之间让出事件循环，与替换中的每个 await 交错执行；新旧快照都有 keep，任何一次读取都不能缺失。
  const reader=async()=>{while(!done){for(const key of keys){const document=ctx.tryInject(key,keep);if(document===undefined)misses++;else seen.add(document.data.description);if(ctx.list(key).filter(item=>item.name===keep).length!==1)misses++;reads++;}await null;}};
  const readers=[reader(),reader(),reader(),reader()];
  for(let i=1;i<=500;i++)assert.notEqual(await session.replace(i%2?after:before),0);
  done=true;await Promise.all(readers);
  assert.equal(misses,0,`500 次替换、${reads} 次读取中出现 ${misses} 次缺失。`);assert.deepEqual([...seen].sort(),[first,second]);
  await session.dispose();
 }finally{await runtime.dispose();}
});
test('新增项冲突时回滚，旧注册与旧内容保持不变',async()=>{
 const before=snapshot([['keep',first],['drop',first]]),after=snapshot([['keep',second],['add',second]]);
 const runtime=await host();
 try{
  const ctx=runtime.getContext('surfaces'),session=await SurfaceRegistrationSession.create(ctx,before),original=ctx.inject('Skills',keep);
  // 新快照要新增的名称已被同 scope 的其它注册占用；独占注册（R38）让本次替换整体失败。
  const occupied=ctx.provide('Tools',plugin+'/add',{});
  await assert.rejects(session.replace(after),error=>error.code==='REGISTRATION_CONFLICT');
  assert.equal(ctx.inject('Skills',keep),original);assert.equal(description(ctx,'Tools','keep'),first);assert.ok(ctx.tryInject('Skills',plugin+'/drop'));assert.equal(ctx.tryInject('Skills',plugin+'/add'),undefined);
  assert.deepEqual(ctx.list('Skills').map(item=>item.name).sort(),[plugin+'/drop',keep]);
  occupied();assert.notEqual(await session.replace(after),0);
  assert.equal(description(ctx,'Skills','keep'),second);assert.deepEqual(ctx.list('Tools').map(item=>item.name).sort(),[plugin+'/add',keep]);
  await session.dispose();
 }finally{await runtime.dispose();}
});
test('每一步注册变更之后，新旧快照都有的文档仍可解析',async()=>{
 const before=snapshot([['keep',first],['drop',first]]),after=snapshot([['keep',second],['add',second]]);
 const runtime=await host();
 try{
  const ctx=runtime.getContext('surfaces'),port=observed(ctx),session=await SurfaceRegistrationSession.create(port,before),gaps=[];let steps=0;
  // 不依赖调度：会话每完成一次登记、替换或撤销，立即检查 keep 在两个目录入口是否仍可解析。
  port.changed=operation=>{steps++;for(const key of keys)if(ctx.tryInject(key,keep)===undefined)gaps.push(`${operation} 之后 ${key} 缺失`);};
  await session.replace(after);await session.replace(before);
  assert.deepEqual(gaps,[]);assert.equal(steps,12);assert.equal(description(ctx,'Tools','keep'),first);
  await session.dispose();
 }finally{await runtime.dispose();}
});
test('原位替换中途失败时恢复已替换项，会话仍可继续替换',async()=>{
 const runtime=await host();
 try{
  const ctx=runtime.getContext('surfaces'),port=observed(ctx),session=await SurfaceRegistrationSession.create(port,snapshot([['alpha',first],['beta',first]])),next=snapshot([['alpha',second],['beta',second]]);
  port.failReplace=3; // alpha 的 Skills 与 Tools 已替换，beta 的第一项替换失败。
  await assert.rejects(session.replace(next),/注入的替换失败/);
  for(const name of ['alpha','beta'])for(const key of keys)assert.equal(description(ctx,key,name),first);
  port.failReplace=0;assert.equal(await session.replace(next),4);
  assert.equal(description(ctx,'Tools','beta'),second);assert.deepEqual(ctx.list('Skills').map(item=>item.name),[plugin+'/alpha',plugin+'/beta']);
  await session.dispose();
 }finally{await runtime.dispose();}
});
