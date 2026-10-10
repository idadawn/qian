import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,readdirSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {parse} from 'yaml';
import {PluginLoader,defineEvent,RuntimeError,ParallelDispatchError} from '../dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const fixtureRoot=path.join(root,'tck/runtime');
for(const file of readdirSync(fixtureRoot).filter(f=>f.endsWith('.scenario.yml')).sort())test(file,async()=>{
 const scenario=parse(readFileSync(path.join(fixtureRoot,file),'utf8'));assert.equal(String(scenario.version),'1');
 const loader=new PluginLoader(),trace=[],events=new Map((scenario.events??[]).map(e=>[e.name,defineEvent(e.name,e.mode)])),entries=new Map(scenario.entries.map(e=>[e.id,e]));
 const context=step=>{const ctx=loader.getContext(step.entry,step.scope??'global');return step.view?ctx.scoped(step.view):ctx;};
 const definition=id=>{const entry=entries.get(id);return{entry:{id,plugin:id,version:'1.0.0',scope:entry.scope??'global',layer:0,config:{}},provides:entry.provides??[],plugin:{name:id,inject:entry.inject??[],apply(ctx){
  trace.push('apply:'+id);for(const action of entry.apply){const target=action.scope?ctx.scoped(action.scope):ctx;
   switch(action.op){
    case'provide':if(action.name!==undefined)target.provide(action.key,action.name,action.value);else target.provide(action.key,action.value);break;
    case'effect':ctx.effect(async()=>{await Promise.resolve();trace.push('dispose:'+action.name);if(action.fail)throw new Error('撤销失败');});break;
    case'optional':assert.equal(ctx.tryInject(action.key),undefined);break;
    case'restrict':ctx.restrict(action.scope,r=>action.keep.includes(r.name));break;
    case'fail':throw new Error('Apply 失败');
    case'on':{const event=events.get(action.event);target.on(event,action.mode??event.mode,async(_,next)=>{trace.push('enter:'+action.name);if(event.mode!=='waterfall')await Promise.resolve();if(action.behavior==='fail')throw new Error(action.name);const result=event.mode==='waterfall'&&action.behavior==='next'?`${action.name}(${await next()})`:action.value;trace.push('exit:'+action.name);return result;},action.priority??0);break;}
    default:throw new Error('未知 Apply 操作 '+action.op);
   }
  }
 }}};};
 try{for(const step of scenario.steps)switch(step.op){
  case'mount':if(step.error)await assert.rejects(loader.mount(step.entries.map(definition)),error=>{assert.ok(error instanceof RuntimeError);assert.equal(error.code,step.error.code);assert.deepEqual(error.args,step.error.args);return true;});else await loader.mount(step.entries.map(definition));break;
  case'unmount':await loader.unmount(step.entry,step.scope??'global');break;
  case'teardown':await loader.dispose();break;
  case'state':assert.equal(loader.getState(step.entry,step.scope??'global'),step.value);break;
  case'trace':assert.deepEqual(trace,step.value);break;
  case'traceContains':for(const value of step.value)assert.ok(trace.includes(value),value+': '+trace);break;
  case'inject':assert.equal(context(step).inject(step.key,step.name),step.value);break;
  case'missing':assert.equal(context(step).tryInject(step.key),undefined);break;
  case'list':assert.deepEqual(context(step).list(step.key).map(r=>r.name),step.value);break;
  case'notFoundEquivalent':{const ctx=context(step),get=name=>{try{ctx.inject(step.key,name);assert.fail('应不可见');}catch(error){assert.ok(error instanceof RuntimeError);return error.toDetail('a'.repeat(32));}};assert.deepEqual(get(step.name),get('does-not-exist'));break;}
  case'dispatch':{const event=events.get(step.event);const call=()=>context(step).dispatch(event,'payload',event.mode==='waterfall'?()=> 'terminal':undefined);if(step.error==='parallel')await assert.rejects(call(),error=>{assert.ok(error instanceof ParallelDispatchError);assert.deepEqual(error.partial,step.partial);return true;});else if(step.error)await assert.rejects(call());else assert.deepEqual((await call())??null,step.value??null);break;}
  default:throw new Error('未知 TCK 操作 '+step.op);
 }}finally{await loader.dispose();}
});
