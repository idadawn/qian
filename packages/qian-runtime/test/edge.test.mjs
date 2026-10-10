import {test} from 'node:test';
import assert from 'node:assert/strict';
import {PluginLoader,defineEvent} from '../dist/index.js';
const entry=(id,apply,provides=[],inject=[],scope='global')=>({entry:{id,plugin:id,version:'1.0.0',scope,layer:0,config:{}},plugin:{name:id,apply,inject},provides});
test('异步 Apply 的半成品不可见，失败时逆序等待撤销',async()=>{
 const runtime=new PluginLoader(),effects=[];let enter,release;
 const started=new Promise(r=>enter=r),blocked=new Promise(r=>release=r);
 await runtime.mount([entry('observer',()=>{})]);
 const mounting=runtime.mount([entry('provider',async ctx=>{ctx.provide('x','half');ctx.effect(async()=>{await Promise.resolve();effects.push('first');});ctx.effect(()=>{effects.push('last');});enter();await blocked;throw new Error('失败');},['x'])]);
 await started;assert.equal(runtime.getContext('observer').tryInject('x'),undefined);release();await mounting;await runtime.whenIdle();assert.equal(runtime.getState('provider'),'Failed');assert.deepEqual(effects,['last','first']);assert.equal(runtime.getContext('observer').tryInject('x'),undefined);await runtime.dispose();
});
test('bail 的 false 是首个有效结果，上游默认语义不能泄漏',async()=>{
 const runtime=new PluginLoader(),event=defineEvent('test/bail','bail');let called=false;
 await runtime.mount([entry('consumer',ctx=>{ctx.on(event,'bail',()=>false);ctx.on(event,'bail',()=>{called=true;return true;});})]);
 assert.equal(await runtime.getContext('consumer').dispatch(event,null),false);assert.equal(called,false);await runtime.dispose();
});
test('动态撤销即刻隐藏依赖者；whenIdle 等待级联释放和恢复',async()=>{
 const runtime=new PluginLoader();let remove;let runs=0;
 await runtime.mount([entry('provider',ctx=>{remove=ctx.provide('x','a');},['x']),entry('consumer',ctx=>{runs++;ctx.provide('y',ctx.x);},['y'],['x']),entry('observer',()=>{})]);
 remove();assert.equal(runtime.getContext('observer').tryInject('y'),undefined);await runtime.whenIdle();assert.equal(runtime.getState('consumer'),'Pending');await runtime.unmount('provider');await runtime.mount([entry('replacement',ctx=>ctx.provide('x','b'),['x'])]);assert.equal(runs,2);assert.equal(runtime.getContext('observer').y,'b');await runtime.dispose();assert.deepEqual(runtime.dumpConfig(),[]);
});
test('whenReady 可取消，已关闭的上下文不可继续注册',async()=>{
 const runtime=new PluginLoader();await runtime.mount([entry('observer',()=>{})]);const ctx=runtime.getContext('observer'),cancel=new AbortController();const waiting=ctx.whenReady('absent',cancel.signal);cancel.abort();await assert.rejects(waiting);await runtime.dispose();assert.throws(()=>ctx.provide('x','late'));
});
test('整树逆序 teardown 不产生已关闭依赖者的异常',async()=>{
 const errors=[],disposed=[];const runtime=new PluginLoader({onError:error=>errors.push(error)});
 await runtime.mount([entry('a',ctx=>{ctx.provide('x',{});ctx.effect(()=>{disposed.push('a');});},['x']),entry('b',ctx=>{ctx.provide('y',{});ctx.effect(()=>{disposed.push('b');});},['y'],['x']),entry('c',ctx=>ctx.effect(()=>{disposed.push('c');}),[],['y'])]);
 await runtime.dispose();await runtime.whenIdle();assert.deepEqual(disposed,['c','b','a']);assert.deepEqual(errors,[]);
});
test('观察器与日志回调失败均不阻断其余观察器',async()=>{
 const runtime=new PluginLoader({onError:()=>{throw new Error('日志失败');}}),event=defineEvent('test/observe','emit');let done=false;
 await runtime.mount([entry('a',ctx=>{ctx.on(event,'emit',()=>{throw new Error('观察失败');});ctx.on(event,'emit',()=>{done=true;});})]);
 await runtime.getContext('a').dispatch(event,null);assert.equal(done,true);await runtime.dispose();
});
test('条目身份使用 scope/id 二元组，分隔符不能造成伪冲突',async()=>{
 const runtime=new PluginLoader();await runtime.mount([entry('c',()=>{},[],[],'tenant:a/b'),entry('b/c',()=>{},[],[],'tenant:a')]);assert.equal(runtime.dumpConfig().length,2);await runtime.dispose();
});
