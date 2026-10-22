import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,writeFileSync,readFileSync,rmSync,existsSync,mkdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawnSync} from 'node:child_process';
import {inspectPackageWorkspace,packageMutationTargets,applyPackageChanges,workspaceDependency} from '../dist/package-manager.js';
import {parsePackageReference} from '../dist/package-references.js';
import {fetchPackageComponents} from '../dist/package-components.js';
import {verifyInstalledPackageArtifacts} from '../dist/package-integrity.js';
import {WorkspaceTransaction} from '../dist/workspace-transaction.js';

function root(){return mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-package-host-'));}
const reference=(kind,value)=>parsePackageReference(kind,value);

test('实际 NuGet 精确安装可供 C# 编译使用，移除后恢复无依赖宿主',async()=>{
 const directory=root();
 try{
  writeFileSync(path.join(directory,'Host.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>');
  const pkg=reference('nuget','Kun.Core@2026.10.12');
  const fetched=await fetchPackageComponents(directory,{nuget:[pkg.specification],npm:[]});
  const artifacts=fetched.artifacts;fetched.dispose();assert.equal(fetched.components.length,0);assert.equal(artifacts.length,1);
  const state=await inspectPackageWorkspace(directory,['nuget']);
  const targets=packageMutationTargets(state,{add:[pkg],remove:[]});
  assert.ok(targets.some(item=>item.path===path.join(directory,'Host.csproj')));
  assert.ok(targets.some(item=>item.path===path.join(directory,'obj')&&item.kind==='directory'));
  await applyPackageChanges(state,{add:[pkg],remove:[]});
  assert.equal(existsSync(path.join(directory,'packages.lock.json')),false);
  assert.ok(readFileSync(path.join(directory,'Host.csproj'),'utf8').includes('[2026.10.12]'));
  const installed=await inspectPackageWorkspace(directory,['nuget']);
  assert.equal(workspaceDependency(installed,pkg).version,'2026.10.12');
  verifyInstalledPackageArtifacts(installed,artifacts);
  assert.throws(()=>verifyInstalledPackageArtifacts(installed,[{...artifacts[0],digest:'sha256:'+'0'.repeat(64)}]),error=>JSON.stringify(error.diagnostics).includes('摘要不一致'));
  writeFileSync(path.join(directory,'Check.cs'),'public static class Check { public static bool Run() => Kun.Core.Results.Result.Ok().IsSuccess; }');
  const build=spawnSync('dotnet',['build','Host.csproj','-c','Release','--no-restore','--disable-build-servers','-m:1'],{cwd:directory,encoding:'utf8',timeout:120000});
  assert.equal(build.status,0,build.stdout+build.stderr);
  await applyPackageChanges(installed,{add:[],remove:[pkg]});
  assert.equal(existsSync(path.join(directory,'packages.lock.json')),false);
  assert.equal(workspaceDependency(await inspectPackageWorkspace(directory,['nuget']),pkg),undefined);
  assert.ok(!Object.keys(JSON.parse(readFileSync(path.join(directory,'obj/project.assets.json'),'utf8')).libraries).some(name=>name.startsWith('Kun.Core/')));
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('自定义锁路径不隐式开启锁模式；用户原有锁文件与失败前字节均保留',async()=>{
 for(const enabled of [false,true]){
  const directory=root();try{
   const project=path.join(directory,'Host.csproj'),lock=path.join(directory,'locks/dependencies.json');mkdirSync(path.dirname(lock));
   writeFileSync(project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetLockFilePath>locks/dependencies.json</NuGetLockFilePath>'+ (enabled?'<RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>':'')+'</PropertyGroup></Project>');
   const pkg=reference('nuget','Kun.Core@2026.10.12');let state=await inspectPackageWorkspace(directory,['nuget']);
   const add={add:[pkg],remove:[]};await WorkspaceTransaction.begin(directory,packageMutationTargets(state,add),'test add').run(()=>applyPackageChanges(state,add));
   assert.equal(existsSync(path.join(directory,'packages.lock.json')),false);assert.equal(existsSync(lock),enabled);
   if(enabled){
    const before=readFileSync(lock),beforeProject=readFileSync(project);state=await inspectPackageWorkspace(directory,['nuget']);const remove={add:[],remove:[pkg]};
    await assert.rejects(WorkspaceTransaction.begin(directory,packageMutationTargets(state,remove),'test interrupted remove').run(async()=>{await applyPackageChanges(state,remove);throw new Error('fail after restore');}));
    assert.deepEqual(readFileSync(lock),before);assert.deepEqual(readFileSync(project),beforeProject);
   }
   state=await inspectPackageWorkspace(directory,['nuget']);const remove={add:[],remove:[pkg]};await WorkspaceTransaction.begin(directory,packageMutationTargets(state,remove),'test remove').run(()=>applyPackageChanges(state,remove));
   assert.equal(existsSync(lock),enabled);assert.equal(existsSync(path.join(directory,'packages.lock.json')),false);
   if(enabled)assert.ok(Object.values(JSON.parse(readFileSync(lock,'utf8')).dependencies).every(group=>Object.keys(group).length===0));
  }finally{rmSync(directory,{recursive:true,force:true});}
 }
});

test('宿主已有默认 NuGet 锁文件时继续更新自己的锁，不由 CLI 删除',async()=>{
 const directory=root();try{
  writeFileSync(path.join(directory,'Host.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>');
  writeFileSync(path.join(directory,'packages.lock.json'),JSON.stringify({version:1,dependencies:{'net10.0':{}}}));
  const pkg=reference('nuget','Kun.Core@2026.10.12');await applyPackageChanges(await inspectPackageWorkspace(directory,['nuget']),{add:[pkg],remove:[]});
  assert.ok(JSON.parse(readFileSync(path.join(directory,'packages.lock.json'),'utf8')).dependencies['net10.0']['Kun.Core']);
  await applyPackageChanges(await inspectPackageWorkspace(directory,['nuget']),{add:[],remove:[pkg]});
  assert.equal(existsSync(path.join(directory,'packages.lock.json')),true);
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('实际 npm 精确安装后可导入，生命周期脚本未执行，移除后依赖消失',async()=>{
 const directory=root();
 try{
  writeFileSync(path.join(directory,'package.json'),JSON.stringify({private:true,type:'module',scripts:{postinstall:'node -e "require(\'fs\').writeFileSync(\'should-not-run\',\'bad\')"'}}));
  const pkg=reference('npm','semver@7.7.4'),state=await inspectPackageWorkspace(directory,['npm']);
  await applyPackageChanges(state,{add:[pkg],remove:[]});
  assert.equal(JSON.parse(readFileSync(path.join(directory,'package.json'),'utf8')).dependencies.semver,'7.7.4');
  assert.equal(existsSync(path.join(directory,'should-not-run')),false);
  const imported=spawnSync(process.execPath,['--input-type=module','-e','import semver from "semver"; if(semver.valid("1.2.3")!=="1.2.3")process.exit(1);'],{cwd:directory,encoding:'utf8',timeout:10000});
  assert.equal(imported.status,0,imported.stderr);
  await applyPackageChanges(await inspectPackageWorkspace(directory,['npm']),{add:[],remove:[pkg]});
  assert.equal(workspaceDependency(await inspectPackageWorkspace(directory,['npm']),pkg),undefined);
  assert.equal(existsSync(path.join(directory,'node_modules/semver')),false);
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('中央 NuGet 版本使用 MSBuild 实际结果，越出工作区的写入计划被拒绝',async()=>{
 const directory=root();
 try{
  writeFileSync(path.join(directory,'Host.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup><ItemGroup><PackageReference Include="Kun.Core" /></ItemGroup></Project>');
  writeFileSync(path.join(directory,'Directory.Packages.props'),'<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include="Kun.Core" Version="2026.10.12" /></ItemGroup></Project>');
  const pkg=reference('nuget','Kun.Core@2026.10.12'),state=await inspectPackageWorkspace(directory,['nuget']);
  assert.equal(workspaceDependency(state,pkg).version,'2026.10.12');
  assert.ok(packageMutationTargets(state,{add:[pkg],remove:[]}).some(item=>item.path===path.join(directory,'Directory.Packages.props')));
  state.nuget.centralFile=path.join(directory,'../outside.props');
  assert.throws(()=>packageMutationTargets(state,{add:[pkg],remove:[]}));
  assert.equal(existsSync(path.join(directory,'packages.lock.json')),false);
 }finally{rmSync(directory,{recursive:true,force:true});}
});
