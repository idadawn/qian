import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdtempSync,cpSync,rmSync,mkdirSync,existsSync,readdirSync} from 'node:fs';
import {spawnSync} from 'node:child_process';
import path from 'node:path';
import {tmpdir} from 'node:os';
import {fileURLToPath} from 'node:url';
import {PluginLoader} from '../../qian-runtime/dist/index.js';
import {HotReloadSurfacePlugin} from '../../qian-surfaces/dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const spec=JSON.parse(readFileSync(path.join(root,'tck/runtime/skill-only.json'),'utf8'));
test('共享无代码技能组件经真实 minimal 和 CLI dump 装载，租户隔离与卸载一致',async()=>{
 const directory=mkdtempSync(path.join(tmpdir(),'qian-skill-only-ts-'));const runtime=new PluginLoader();
 try{
  for(const relative of ['profiles/minimal','plugins/qian.bundle.minimal'])cpSync(path.join(root,relative),path.join(directory,relative),{recursive:true});
  const component=path.join(directory,spec.directory);
  for(const[file,content]of Object.entries(spec.files)){const target=path.join(component,file);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,content);}
  assert.equal(readdirSync(component,{recursive:true,withFileTypes:true}).filter(item=>item.isFile()).length,2);assert.ok(!existsSync(path.join(component,'src')));
  assert.ok(!Object.hasOwn(JSON.parse(readFileSync(path.join(component,'.qian-plugin/plugin.json'),'utf8')),'runtime'));
  writeFileSync(path.join(directory,'tenant.patch.yml'),spec.patch);
  const command=spawnSync(process.execPath,[path.join(root,'packages/qian-cli/dist/main.js'),'dump-config','--cwd',directory,'--profile',spec.profile,'--patch','tenant.patch.yml','--no-home','--json'],{encoding:'utf8',timeout:10000});
  assert.equal(command.status,0,command.stderr);const snapshot=JSON.parse(command.stdout);assert.deepEqual(snapshot.diagnostics,[]);assert.equal(snapshot.layers.find(item=>item.kind==='bundle').ref,spec.bundle);assert.equal(snapshot.entries.length,1);
  const entry=snapshot.entries[0],plugin=new HotReloadSurfacePlugin(component,{profile:spec.profile,executionPlane:'server'});
  assert.equal(entry.scope,spec.scope);assert.equal(entry.plugin,spec.plugin+'@'+spec.version);
  await runtime.mount([{entry:{id:entry.id,plugin:spec.plugin,version:spec.version,scope:entry.scope,layer:entry.layer,config:entry.config},plugin,provides:plugin.surfaceProvides}]);
  assert.equal(runtime.getState(entry.id,entry.scope),'Ready');const context=runtime.getContext(entry.id,entry.scope),other=context.scoped(spec.otherScope),name=spec.plugin+'/'+spec.skill;
  assert.deepEqual(context.list('Skills').map(item=>item.name),[name]);assert.deepEqual(context.list('Tools').map(item=>item.name),[name]);assert.deepEqual(other.list('Skills'),[]);assert.deepEqual(other.list('Tools'),[]);
  const dump=runtime.dumpConfig();assert.equal(dump.length,1);const dumped=dump[0];assert.equal(dumped.id,entry.id);assert.equal(dumped.scope,entry.scope);
  await runtime.unmount(entry.id,entry.scope);assert.deepEqual(runtime.dumpConfig(),[]);
  if(process.env.QIAN_SKILL_ONLY_REPORT){mkdirSync(path.dirname(path.resolve(process.env.QIAN_SKILL_ONLY_REPORT)),{recursive:true});writeFileSync(process.env.QIAN_SKILL_ONLY_REPORT,JSON.stringify({id:spec.id,noCode:true,profile:snapshot.profile,entryId:dumped.id,plugin:dumped.plugin+'@'+dumped.version,scope:dumped.scope,skill:name,otherVisible:false,clean:runtime.dumpConfig().length===0}));}
 }finally{await runtime.dispose();rmSync(directory,{recursive:true,force:true});}
});
