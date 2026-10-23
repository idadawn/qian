import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync,writeFileSync,mkdtempSync,cpSync,rmSync,mkdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {loadProfile} from '../dist/profiles.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const cases=JSON.parse(readFileSync(path.join(root,'tck/profiles/cases.json'),'utf8'));
test('共用分层 TCK 并输出逐字段一致性报告',()=>{
 const report={};
 for(const item of cases){
  const directory=mkdtempSync(path.join(os.tmpdir(),'qian-profile-'));
  try{
   cpSync(path.join(root,'tck/profiles/base'),directory,{recursive:true});
   for(const[file,content]of Object.entries(item.files)){mkdirSync(path.dirname(path.join(directory,file)),{recursive:true});writeFileSync(path.join(directory,file),typeof content==='string'?content:Buffer.from(content.base64,'base64'));}
   if(item.missingHome)rmSync(path.join(directory,'home/qian.patch.yml'));
   const result=loadProfile({workingDirectory:directory,profile:'test',patches:item.patches,homeDirectory:'home',includeHomePatch:item.home,scopePatches:item.scopePatches});
   assert.equal(result.isValid,item.expect,item.name+': '+JSON.stringify(result));
   if(item.pointer)assert.ok(result.diagnostics.some(d=>d.error.pointer===item.pointer),item.name+': '+JSON.stringify(result));
   if(item.reason)assert.ok(result.diagnostics.some(d=>d.error.errors.some(e=>e.pointer===item.pointer&&e.message===item.reason)),item.name+': '+JSON.stringify(result));
   if(result.isValid){
    const entries=result.snapshot.entries,settings=entries.find(e=>e.id==='settings'&&e.scope==='global');
    if(item.count!==undefined)assert.equal(entries.length,item.count,item.name);
    if(item.port!==undefined)assert.equal(settings.config.port,item.port,item.name);
    if(item.marker!==undefined)assert.equal(settings.config.marker,item.marker,item.name);
    if(item.noObsolete)assert.ok(!Object.hasOwn(settings.config,'obsolete'));
    if(item.version)assert.equal(settings.plugin,'test.settings@'+item.version);
    if(entries.some(e=>e.id==='consumer'))assert.ok(result.snapshot.mountOrder.indexOf('global/settings')<result.snapshot.mountOrder.indexOf('global/consumer'));
    // optionalInject 的 Provider 在树里时先于消费方装载；缺席时消费方照常装载。
    if(entries.some(e=>e.id==='observer'))assert.ok(result.snapshot.mountOrder.indexOf('global/settings')<result.snapshot.mountOrder.indexOf('global/observer'),item.name);
   }
   if(item.warnings!==undefined)assert.equal(result.diagnostics.filter(d=>d.severity==='warning').length,item.warnings);
   assert.ok(!JSON.stringify(result).includes('DO-NOT-LEAK'));
   for(const diagnostic of result.diagnostics)diagnostic.error.trace_id='TRACE';
   if(item.disabledCount!==undefined)assert.equal(result.snapshot.disabledEntries.length,item.disabledCount,item.name);
   report[item.name]=item.scopePatches?{...result,disabledEntries:result.snapshot?.disabledEntries??null}:result;
  }finally{rmSync(directory,{recursive:true,force:true});}
 }
 if(process.env.QIAN_PROFILE_REPORT){mkdirSync(path.dirname(process.env.QIAN_PROFILE_REPORT),{recursive:true});writeFileSync(process.env.QIAN_PROFILE_REPORT,JSON.stringify(report,null,2));}
});
test('交付的五个 profile 具有正确业务边界及骨架警告',()=>{
 // [条目数, 骨架警告数]：qian.data / qian.events / qian.config / qian.files 经真实安装后不再是骨架。
 for(const[profile,[count,skeletons]]of Object.entries({platform:[21,18],gateway:[6,6],identity:[8,6],standalone:[3,1],minimal:[0,0]})){
  const result=loadProfile({workingDirectory:root,profile,includeHomePatch:false});
  assert.ok(result.isValid,JSON.stringify(result));assert.equal(result.snapshot.entries.length,count);assert.equal(result.diagnostics.filter(d=>d.severity==='warning').length,skeletons);
  if(profile!=='platform')assert.ok(!result.snapshot.entries.some(e=>/^qian\.(registry|lifecycle|capabilities)@/.test(e.plugin)));
 }
});
test('真实 CLI dump-config、overlay 来源及非法配置退出',()=>{
 const directory=mkdtempSync(path.join(os.tmpdir(),'qian-cli-'));
 const run=(...args)=>spawnSync(process.execPath,[path.join(root,'packages/qian-cli/dist/main.js'),'--cwd',root,'--no-home','--profile','platform','--dump-config','--json',...args],{encoding:'utf8'});
 try{
  const minimal=run('--profile','minimal');assert.equal(minimal.status,0,minimal.stderr);
  const baseline=JSON.parse(minimal.stdout);assert.equal(baseline.profile,'minimal');assert.deepEqual(baseline.entries,[]);assert.deepEqual(baseline.mountOrder,[]);assert.deepEqual(baseline.diagnostics,[]);assert.equal(baseline.layers.find(layer=>layer.kind==='bundle').ref,'qian.bundle.minimal@1.0.0');
  const initial=run();assert.equal(initial.status,0,initial.stderr);const snapshot=JSON.parse(initial.stdout);assert.equal(snapshot.entries.length,21);
  const patch=path.join(directory,'override.yml');writeFileSync(patch,'- id: files\n  config:\n    storage: {kind: local, root: /tmp/profile-proof}\n');
  const updated=run('--patch',patch);assert.equal(updated.status,0,updated.stderr);const final=JSON.parse(updated.stdout),files=final.entries.find(e=>e.id==='files');assert.equal(files.config.storage.root,'/tmp/profile-proof');assert.equal(final.layers[files.layer].kind,'overlay');
  writeFileSync(patch,'- id: files\n  config:\n    storage: {kind: impossible}\n');
  const rejected=run('--patch',patch);assert.equal(rejected.status,1);assert.equal(rejected.stdout,'');const diagnostic=JSON.parse(rejected.stderr).diagnostics.find(d=>d.severity==='error');assert.equal(diagnostic.error.code,'INPUT_INVALID');assert.ok(diagnostic.error.pointer.endsWith('#/0/config/storage/kind'));
  const badArg=run('--unknown');assert.equal(badArg.status,1);assert.equal(JSON.parse(badArg.stderr).diagnostics[0].error.code,'INPUT_INVALID');
 }finally{rmSync(directory,{recursive:true,force:true});}
});
