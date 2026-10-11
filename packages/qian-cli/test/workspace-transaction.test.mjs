import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,writeFileSync,readFileSync,rmSync,existsSync,mkdirSync,symlinkSync,unlinkSync,readdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {WorkspaceTransaction} from '../dist/workspace-transaction.js';
import {inspectPackageWorkspace,packageMutationTargets,applyPackageChanges} from '../dist/package-manager.js';
import {parsePackageReference} from '../dist/package-references.js';

function root(){return mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-transaction-'));}

test('失败恢复原始文件、目录和缺失状态，备份期间既有依赖仍可读取',()=>{
 const directory=root(),file=path.join(directory,'package.json'),modules=path.join(directory,'node_modules'),created=path.join(directory,'package-lock.json');
 try{
  writeFileSync(file,'original\n');mkdirSync(modules);writeFileSync(path.join(modules,'keep'),'existing dependency');
  const transaction=WorkspaceTransaction.begin(directory,[{path:file,kind:'file'},{path:modules,kind:'directory'},{path:created,kind:'file'}],'add');
  assert.equal(readFileSync(path.join(modules,'keep'),'utf8'),'existing dependency');
  writeFileSync(file,'changed');rmSync(modules,{recursive:true});mkdirSync(modules);writeFileSync(path.join(modules,'new'),'new dependency');writeFileSync(created,'new lock');
  transaction.rollback();
  assert.equal(readFileSync(file,'utf8'),'original\n');assert.equal(readFileSync(path.join(modules,'keep'),'utf8'),'existing dependency');
  assert.equal(existsSync(path.join(modules,'new')),false);assert.equal(existsSync(created),false);
  assert.equal(existsSync(path.join(directory,'.qian/.install.lock')),false);
  assert.equal(JSON.parse(readFileSync(transaction.journalFile,'utf8')).state,'rolled-back');
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('实际 NuGet 安装后的验证失败能够还原宿主与还原产物',async()=>{
 const directory=root(),project=path.join(directory,'Host.csproj');
 try{
  const before='<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>';
  writeFileSync(project,before);
  const changes={add:[parsePackageReference('nuget','Kun.Core@2026.10.12')],remove:[]};
  const workspace=await inspectPackageWorkspace(directory,['nuget']);
  const transaction=WorkspaceTransaction.begin(directory,packageMutationTargets(workspace,changes),'add');
  await assert.rejects(transaction.run(async()=>{
   await applyPackageChanges(workspace,changes);
   assert.ok(existsSync(path.join(directory,'obj/project.assets.json')));
   assert.ok(readFileSync(project,'utf8').includes('Kun.Core'));
   throw new Error('安装末校验失败');
  }),/安装末校验失败/);
  assert.equal(readFileSync(project,'utf8'),before);
  assert.equal(existsSync(path.join(directory,'obj')),false);assert.equal(existsSync(path.join(directory,'packages.lock.json')),false);
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('目标路径被换成链接时不会写根外文件，失败保留备份和安装锁',()=>{
 const directory=root(),outside=root(),file=path.join(directory,'package.json'),victim=path.join(outside,'keep');
 try{
  writeFileSync(file,'original');writeFileSync(victim,'outside');
  const transaction=WorkspaceTransaction.begin(directory,[{path:file,kind:'file'}],'add');
  unlinkSync(file);symlinkSync(victim,file);
  assert.throws(()=>transaction.rollback());
  assert.equal(readFileSync(victim,'utf8'),'outside');assert.ok(existsSync(path.join(directory,'.qian/.install.lock')));
  assert.equal(JSON.parse(readFileSync(transaction.journalFile,'utf8')).state,'rollback-failed');
  unlinkSync(file);transaction.rollback();assert.equal(readFileSync(file,'utf8'),'original');
 }finally{rmSync(directory,{recursive:true,force:true});rmSync(outside,{recursive:true,force:true});}
});

test('提交保留新状态、安装锁防并发，完成日志仅保留最近二十次',()=>{
 const directory=root(),file=path.join(directory,'package.json');
 try{
  writeFileSync(file,'old');
  const transaction=WorkspaceTransaction.begin(directory,[{path:file,kind:'file'}],'add');
  assert.throws(()=>WorkspaceTransaction.begin(directory,[],'concurrent'));
  writeFileSync(file,'new');transaction.commit();assert.equal(readFileSync(file,'utf8'),'new');
  for(let index=0;index<22;index++)WorkspaceTransaction.begin(directory,[],'noop').commit();
  const files=readdirSync(path.join(directory,'.qian/journal')).filter(name=>name.endsWith('.json'));
  assert.equal(files.length,20);assert.equal(readFileSync(path.join(directory,'.qian/journal/.gitignore'),'utf8'),'*\n');
 }finally{rmSync(directory,{recursive:true,force:true});}
});
