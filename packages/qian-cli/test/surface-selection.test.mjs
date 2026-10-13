import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,mkdirSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash,generateKeyPairSync,sign} from 'node:crypto';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';
import {stringify} from 'yaml';
import {readDirectory} from '../dist/marketplace-files.js';
import {installMarketplacePlugins} from '../dist/marketplace-install.js';
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const write=(root,name,value)=>{const file=path.join(root,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,value);};
const run=(root,...args)=>spawnSync(process.execPath,[path.join(repo,'packages/qian-cli/dist/main.js'),'--cwd',root,'--no-home','--json',...args],{encoding:'utf8',timeout:60000});
function fixture(){
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-surface-selection-')),host=path.join(root,'host'),catalog=path.join(root,'catalog'),component=path.join(catalog,'component');
 const keys=generateKeyPairSync('ed25519'),publicPem=keys.publicKey.export({type:'spki',format:'pem'});
 const manifest={id:'test.mixed',version:'1.0.0',description:'同时包含代码与声明式表面的安装选择验收组件。',author:{name:'Fixture'},license:'Apache-2.0',interface:{displayName:'选择安装',category:'operations'},inject:['CodeDependency'],provides:['OriginalProvider'],policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL',egress:['api.example.test']},scopes:['global'],runtime:{form:'module',entry:'NeverLoaded',npm:[{name:'qian-never-install-surface-fixture',version:'1.0.0'}]}};
 write(component,'.qian-plugin/plugin.json',JSON.stringify(manifest));
 for(const name of ['guide','second'])write(component,'skills/'+name+'/SKILL.md','---\nname: '+name+'\ndescription: 当用户需要只读检查所选组件并核对配置时使用，不执行任何业务写入。\n---\n阅读说明。\n');
 write(component,'skills/guide/reference.txt','关联说明\n');write(component,'README.md','组件说明\n');
 write(component,'commands/check.md','---\ndescription: 当用户需要检查组件配置时使用，不执行写入。\n---\n核对配置。\n');
 write(catalog,'keys/owner.pub',publicPem);
 const market=Buffer.from(JSON.stringify({name:'catalog',interface:{displayName:'验收目录'},owner:{name:'Fixture',publicKeys:['./keys/owner.pub']},categories:['operations'],plugins:[{name:'mixed',source:{type:'local',path:'./component'},category:'operations',policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}}]}));
 write(catalog,'.qian/marketplace.json',market);write(catalog,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(market).digest(),keys.privateKey));
 write(host,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:[{name:'catalog',source:catalog,priority:10}],trust:{keys:[publicPem]}}));
 write(host,'profiles/minimal/qian.profile.yml','name: minimal\nbundles: []\npatch: ./qian.patch.yml\n');write(host,'profiles/minimal/qian.patch.yml','[]\n');
 write(host,'.npmrc','registry=http://127.0.0.1:1/\nfetch-retries=0\n');
 return{root,host,catalog,component,manifest,dispose:()=>rmSync(root,{recursive:true,force:true})};
}

test('真实 CLI 从代码组件仅装技能，不取代码依赖；投影可被真实 .NET 表面运行时装载',()=>{
 const f=fixture();try{
  const original=readDirectory(f.component).digest;
  const result=run(f.host,'add','mixed','--surfaces','skills','--no-code','--yes');assert.equal(result.status,0,result.stderr);
  const installed=path.join(f.host,'.qian/plugins/test.mixed'),manifest=JSON.parse(readFileSync(path.join(installed,'.qian-plugin/plugin.json'),'utf8'));
  assert.equal(manifest.runtime,undefined);assert.deepEqual(manifest.inject,[]);assert.deepEqual(manifest.provides,[]);
  assert.equal(existsSync(path.join(installed,'commands')),false);assert.equal(readFileSync(path.join(installed,'skills/guide/reference.txt'),'utf8'),'关联说明\n');
  for(const file of ['package.json','package-lock.json','node_modules','obj'])assert.equal(existsSync(path.join(f.host,file)),false,file);
  const lockFile=path.join(f.host,'.qian/lock.json'),lock=readFileSync(lockFile,'utf8'),record=JSON.parse(lock).plugins[0];
  assert.equal(record.digest,original);assert.notEqual(record.deliveryDigest,original);assert.deepEqual(record.packages,{nuget:[],npm:[]});
  assert.deepEqual(record.surfaces,['skills']);assert.equal(JSON.parse(run(f.host,'add','mixed','--surfaces','skills','--no-code').stdout).status,'unchanged');assert.equal(readFileSync(lockFile,'utf8'),lock);
  const probe=path.join(f.root,'dotnet-probe'),xml=value=>value.replaceAll('&','&amp;').replaceAll('"','&quot;');
  write(probe,'Probe.csproj','<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings></PropertyGroup><ItemGroup><ProjectReference Include="'+xml(path.join(repo,'packages/Qian.Surfaces/Qian.Surfaces.csproj'))+'" /></ItemGroup></Project>');
  write(probe,'Program.cs',`using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Qian.Runtime;
using Qian.Surfaces;
var checkedComponent = new SurfaceValidator().ValidateComponent(args[0]);
if (!checkedComponent.IsValid) throw new Exception(JsonSerializer.Serialize(checkedComponent.Diagnostics));
var plugin = new DeclarativeSurfacePlugin(checkedComponent);
using var services = new ServiceCollection().BuildServiceProvider();
await using var runtime = new PluginLoader(services);
await runtime.MountAsync([new PluginDefinition(new PluginEntry("slice", "test.mixed", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new {})), plugin, plugin.Provides)]);
var context = runtime.GetContext("slice", ScopeKey.Global);
if (context.List(SurfaceKeys.Skills).Count != 2 || context.List(SurfaceKeys.Commands).Count != 0 || context.TryInject<object>(new ServiceKey("OriginalProvider")) is not null) throw new Exception("Unexpected registrations");
await runtime.UnmountAsync("slice", ScopeKey.Global);
Console.WriteLine("actual-dotnet-slice-ready");
`);
  const verified=spawnSync('dotnet',['run','--project',path.join(probe,'Probe.csproj'),'-c','Release','--artifacts-path',path.join(probe,'artifacts'),'--disable-build-servers','-p:UseSharedCompilation=false','--',installed],{cwd:probe,encoding:'utf8',timeout:120000});
  assert.equal(verified.status,0,verified.stdout+verified.stderr);assert.match(verified.stdout,/actual-dotnet-slice-ready/);
  assert.equal(run(f.host,'remove','mixed','--purge').status,0);assert.equal(existsSync(installed),false);assert.equal(existsSync(path.join(f.host,'package.json')),false);
 }finally{f.dispose();}
});

test('按名称选择保留技能目录附件，丢失的工具引用在提交前被拒绝',async()=>{
 const f=fixture();try{
  const selected=await installMarketplacePlugins([{reference:'mixed',selection:{noCode:true,surfaces:['skills'],members:{skills:['guide']}}}],{workingDirectory:f.host,includeHome:false,profile:'minimal',yes:true});
  assert.equal(selected.status,'installed');const installed=path.join(f.host,'.qian/plugins/test.mixed');assert.equal(existsSync(path.join(installed,'skills/second')),false);assert.equal(existsSync(path.join(installed,'skills/guide/reference.txt')),true);
  assert.equal(run(f.host,'remove','mixed','--purge').status,0);
  const cases=JSON.parse(readFileSync(path.join(repo,'tck/surfaces/cases.json'),'utf8')),tool=cases.find(item=>item.name==='all-surfaces').files['tools/read.json'];
  write(f.component,'tools/read.json',tool);write(f.component,'skills/guide/SKILL.md','---\nname: guide\ndescription: 当用户需要通过只读工具核对当前组件配置时使用，不执行写入操作。\nallowed-tools: [test.mixed/read]\n---\n调用只读工具。\n');
  const failure=run(f.host,'add','mixed','--surfaces','skills','--no-code','--yes');assert.equal(failure.status,1);assert.match(failure.stderr,/SURFACE_REFERENCE_UNKNOWN/);assert.equal(existsSync(installed),false);
  const withTool=run(f.host,'add','mixed','--surfaces','skills,tools','--no-code','--yes');assert.equal(withTool.status,0,withTool.stderr);assert.equal(existsSync(path.join(installed,'tools/read.json')),true);
 }finally{f.dispose();}
});

test('表面选择不绕过原来源信任、无效成员或代码形态约束',async()=>{
 const f=fixture();try{
  await assert.rejects(installMarketplacePlugins([{reference:'mixed',selection:{noCode:true,surfaces:['skills'],members:{skills:['missing']}}}],{workingDirectory:f.host,includeHome:false,profile:'minimal'}));
  const ui=run(f.host,'add','mixed','--surfaces','ui','--no-code');assert.equal(ui.status,1);assert.match(ui.stderr,/不能与 --no-code/);
  rmSync(path.join(f.catalog,'.qian/marketplace.json.sig'));
  const unverified=run(f.host,'add','mixed','--surfaces','skills','--no-code','--allow-unverified');assert.equal(unverified.status,1);assert.equal(existsSync(path.join(f.host,'.qian/lock.json')),false);
  const invalid=run(f.host,'add','mixed','--surfaces','skills,unknown','--no-code');assert.equal(invalid.status,1);assert.match(invalid.stderr,/未知名称/);
 }finally{f.dispose();}
});

test('自定义 metadata 路径可以单独选择，清单不会残留其它表面指针',()=>{
 const f=fixture();try{
  writeFileSync(path.join(f.component,'.qian-plugin/plugin.json'),JSON.stringify({...f.manifest,surfaces:{metadata:'./custom/catalog',skills:'./skills'}}));write(f.component,'custom/catalog/permissions.json','[]\n');
  const result=run(f.host,'add','mixed','--surfaces','metadata','--no-code');assert.equal(result.status,0,result.stderr);
  const installed=path.join(f.host,'.qian/plugins/test.mixed');assert.equal(existsSync(path.join(installed,'custom/catalog/permissions.json')),true);assert.equal(existsSync(path.join(installed,'skills')),false);
  assert.deepEqual(JSON.parse(readFileSync(path.join(installed,'.qian-plugin/plugin.json'),'utf8')).surfaces,{metadata:'./custom/catalog'});
 }finally{f.dispose();}
});

test('force 不能通过表面切片移除已安装组合包中的 required 代码',()=>{
 const f=fixture();try{
  const root={...f.manifest,inject:[],provides:[],surfaces:{patch:'./config/qian.patch.yml'}};delete root.runtime;
  writeFileSync(path.join(f.component,'.qian-plugin/plugin.json'),JSON.stringify(root));
  const child={...f.manifest,id:'test.required',inject:[],provides:[],runtime:{form:'module',entry:'PolicyFixture'},policy:{tier:'system',sandbox:'none',authentication:'ON_INSTALL',required:true}};
  write(f.component,'components/test.required/.qian-plugin/plugin.json',JSON.stringify(child));write(f.component,'components/test.required/skills/guide/SKILL.md',readFileSync(path.join(f.component,'skills/guide/SKILL.md')));
  write(f.component,'config/qian.patch.yml','- id: required\n  plugin: test.required\n  config: {}\n');
  const installed=run(f.host,'add','mixed');assert.equal(installed.status,0,installed.stderr);
  const lockFile=path.join(f.host,'.qian/lock.json'),before=readFileSync(lockFile,'utf8');
  const failed=run(f.host,'add','mixed','--no-code','--surfaces','skills','--force','--yes');assert.equal(failed.status,1);assert.match(failed.stderr,/required/);assert.equal(readFileSync(lockFile,'utf8'),before);
 }finally{f.dispose();}
});
