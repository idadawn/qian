import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,writeFileSync,readFileSync,rmSync,realpathSync,existsSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash,generateKeyPairSync,sign} from 'node:crypto';
import {createServer} from 'node:http';
import {once} from 'node:events';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {stringify} from 'yaml';
import {npmExecutable,inspectPackageWorkspace} from '../dist/package-manager.js';
import {installMarketplacePlugins} from '../dist/marketplace-install.js';
import {removeMarketplacePlugins} from '../dist/marketplace-remove.js';
import {planEngineVerification,readHostEngineVersions,verifyComponentEngines} from '../dist/engine-compatibility.js';
import {parsePackageReference} from '../dist/package-references.js';
const execute=promisify(execFile);
const write=(root,name,bytes)=>{const file=path.join(root,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,bytes);};
const ref=(kind,name)=>parsePackageReference(kind,name);
const incompatible=error=>error.diagnostics?.some(item=>item.error.errors.some(detail=>detail.code==='SURFACE_ENGINE_INCOMPATIBLE'));

function fixture(){
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-engine-plan-')),host=path.join(root,'host'),catalog=path.join(root,'catalog');
 const keys=generateKeyPairSync('ed25519'),pem=keys.publicKey.export({type:'spki',format:'pem'});
 write(host,'profiles/minimal/qian.profile.yml','name: minimal\nbundles: []\npatch: ./qian.patch.yml\n');write(host,'profiles/minimal/qian.patch.yml','[]\n');
 write(catalog,'key.pub',pem);
 const market={name:'catalog',interface:{displayName:'版本计划验收'},owner:{name:'Fixture',publicKeys:['./key.pub']},plugins:[{name:'engine',source:{type:'local',path:'./plugins/test.engine'},category:'operations',policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}}]};
 const persist=()=>{const bytes=Buffer.from(JSON.stringify(market));write(catalog,'.qian/marketplace.json',bytes);write(catalog,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(bytes).digest(),keys.privateKey));};persist();
 write(host,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:[{name:'catalog',source:catalog,priority:1}],trust:{keys:[pem]}}));
 const component=(version,packages,engines)=>write(catalog,'plugins/test.engine/.qian-plugin/plugin.json',JSON.stringify({id:'test.engine',version,description:'使用真实包还原图验证运行时版本预检与升级的组件。',author:{name:'Fixture'},license:'Apache-2.0',interface:{displayName:'引擎版本',category:'operations'},inject:[],provides:[],policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global'],engines,runtime:{form:'module',entry:'Fixture.Entry',...packages}}));
 const surface=(name,engines,version='1.0.0')=>{
  write(catalog,'plugins/test.'+name+'/.qian-plugin/plugin.json',JSON.stringify({id:'test.'+name,version,description:'依赖宿主运行时范围的无代码指引，用于验证受影响组件的末检范围。',author:{name:'Fixture'},license:'Apache-2.0',interface:{displayName:'无代码指引',category:'operations'},inject:[],provides:[],policy:{tier:'private',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global'],engines}));
  write(catalog,'plugins/test.'+name+'/skills/check/SKILL.md','---\nname: check\ndescription: 当用户需要查看宿主配置及其版本信息时使用，不执行修改。\n---\n只读检查。\n');
  if(!market.plugins.some(item=>item.name===name))market.plugins.push({name,source:{type:'local',path:'./plugins/test.'+name},category:'operations',policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}});persist();
 };
 return{root,host,catalog,component,surface,install:(dryRun=false)=>installMarketplacePlugins(['engine'],{workingDirectory:host,profile:'minimal',includeHome:false,yes:true,dryRun}),dispose:()=>rmSync(root,{recursive:true,force:true})};
}
async function npmFeed(f){
 const name='@idadawn/qian-runtime',packages=new Map(),archives=new Map();
 for(const[pkg,version,dependencies]of [[name,'0.1.0',{}],[name,'0.2.0',{}],['qian-engine-helper','1.0.0',{[name]:'0.1.0'}],['qian-engine-helper','2.0.0',{[name]:'0.2.0'}]]){
  const folder=path.join(f.root,pkg.replaceAll('/','-')+'-'+version);write(folder,'package.json',JSON.stringify({name:pkg,version,dependencies,main:'index.js'}));write(folder,'index.js','module.exports='+JSON.stringify(version)+';');
  const output=await execute(process.execPath,[npmExecutable(),'pack','--ignore-scripts','--json','--pack-destination',folder],{cwd:folder,encoding:'utf8'});
  const bytes=readFileSync(path.join(folder,JSON.parse(output.stdout)[0].filename)),archive='/'+pkg.replaceAll('/','-')+'-'+version+'.tgz';archives.set(archive,bytes);
  if(!packages.has(pkg))packages.set(pkg,{});packages.get(pkg)[version]={name:pkg,version,dependencies,dist:{path:archive,integrity:'sha512-'+createHash('sha512').update(bytes).digest('base64')}};
 }
 const server=createServer((request,response)=>{
  const url=decodeURIComponent(request.url),bytes=archives.get(url);if(bytes){response.end(bytes);return;}
  const versions=packages.get(url.slice(1));if(!versions){response.writeHead(404).end();return;}
  const origin='http://127.0.0.1:'+server.address().port;
  response.setHeader('content-type','application/json');response.end(JSON.stringify({name:url.slice(1),'dist-tags':{latest:Object.keys(versions).at(-1)},versions:Object.fromEntries(Object.entries(versions).map(([version,value])=>[version,{...value,dist:{integrity:value.dist.integrity,tarball:origin+value.dist.path}}]))}));
 });server.listen(0,'127.0.0.1');await once(server,'listening');
 write(f.host,'package.json','{"private":true}\n');write(f.host,'.npmrc','registry=http://127.0.0.1:'+server.address().port+'/\nfetch-retries=0\ncache='+path.join(f.root,'npm-cache')+'\n');
 return async()=>{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));};
}

test('真实 npm 托管 Runtime 从 0.1 升到 0.2，预检采用计划精确版本且仍拒绝不兼容的计划',async()=>{
 const f=fixture();let close;try{
  close=await npmFeed(f);const name='@idadawn/qian-runtime';f.component('1.0.0',{npm:[{name,version:'0.1.0'}]},{qian:'^0.1.0'});
  assert.equal((await f.install()).status,'installed');assert.equal(JSON.parse(readFileSync(path.join(f.host,'node_modules',name,'package.json'),'utf8')).version,'0.1.0');
  f.component('2.0.0',{npm:[{name,version:'0.2.0'}]},{qian:'^0.2.0'});const plan=await f.install(true);
  assert.equal(plan.enginePlan.evidence.find(item=>item.package===name).version,'0.2.0');assert.equal(plan.enginePlan.evidence.find(item=>item.package===name).source,'package-plan/npm');
  const upgraded=await f.install();assert.equal(upgraded.status,'installed');assert.equal(upgraded.verification[0].engines.find(item=>item.package===name).version,'0.2.0');
  assert.equal(JSON.parse(readFileSync(path.join(f.host,'node_modules',name,'package.json'),'utf8')).version,'0.2.0');
  const lock=readFileSync(path.join(f.host,'.qian/lock.json'),'utf8');f.component('3.0.0',{npm:[{name,version:'0.2.0'}]},{qian:'^0.3.0'});
  await assert.rejects(f.install(),incompatible);assert.equal(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8'),lock);
 }finally{await close?.();f.dispose();}
});

test('传递 Runtime 随真实 npm 升级重算，计划标 pending；末检不匹配时恢复旧依赖和锁',async()=>{
 const f=fixture();let close;try{
  close=await npmFeed(f);const name='qian-engine-helper';f.component('1.0.0',{npm:[{name,version:'1.0.0'}]},{qian:'^0.1.0'});
  assert.equal((await f.install()).status,'installed');
  f.component('2.0.0',{npm:[{name,version:'2.0.0'}]},{qian:'^0.2.0'});const plan=await f.install(true);assert.ok(plan.enginePlan.pending.includes('qian'));assert.ok(!plan.enginePlan.evidence.some(item=>item.engine==='qian'));
  const upgraded=await f.install();assert.equal(upgraded.status,'installed');assert.equal(upgraded.verification[0].engines.find(item=>item.engine==='qian').version,'0.2.0');
  const lock=readFileSync(path.join(f.host,'.qian/lock.json'),'utf8'),npmLock=readFileSync(path.join(f.host,'package-lock.json'),'utf8');
  f.component('3.0.0',{npm:[{name,version:'1.0.0'}]},{qian:'^0.3.0'});const invalidPlan=await f.install(true);assert.ok(invalidPlan.enginePlan.pending.includes('qian'));
  await assert.rejects(f.install(),incompatible);assert.equal(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8'),lock);assert.equal(readFileSync(path.join(f.host,'package-lock.json'),'utf8'),npmLock);assert.equal(readHostEngineVersions(f.host).find(item=>item.engine==='qian').version,'0.2.0');
 }finally{await close?.();f.dispose();}
});

test('Runtime 升级重新核验同宿主已安装的相关组件，不兼容时回滚且不重复检查无关组件',async()=>{
 const f=fixture();let close;try{
  close=await npmFeed(f);const name='@idadawn/qian-runtime',options={workingDirectory:f.host,profile:'minimal',includeHome:false,yes:true};
  f.component('1.0.0',{npm:[{name,version:'0.1.0'}]},{qian:'^0.1.0'});await f.install();
  f.surface('consumer',{qian:'^0.1.0'});f.surface('unrelated',{});await installMarketplacePlugins(['consumer','unrelated'],options);
  const lock=readFileSync(path.join(f.host,'.qian/lock.json'),'utf8'),patch=readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8'),consumer=readFileSync(path.join(f.host,'.qian/plugins/test.consumer/.qian-plugin/plugin.json'),'utf8');
  f.component('2.0.0',{npm:[{name,version:'0.2.0'}]},{qian:'^0.2.0'});
  await assert.rejects(f.install(),error=>incompatible(error)&&JSON.stringify(error.diagnostics).includes('test.consumer'));
  assert.equal(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8'),lock);assert.equal(readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8'),patch);assert.equal(readFileSync(path.join(f.host,'.qian/plugins/test.consumer/.qian-plugin/plugin.json'),'utf8'),consumer);assert.equal(readHostEngineVersions(f.host).find(item=>item.engine==='qian').version,'0.1.0');
  f.surface('consumer',{qian:'>=0.1.0 <0.3.0'},'2.0.0');await installMarketplacePlugins(['consumer'],options);
  const installed=await f.install();assert.equal(installed.status,'installed');assert.deepEqual(installed.verification[0].components.sort(),['test.consumer','test.engine']);assert.ok(!installed.verification.some(result=>result.components.includes('test.unrelated')));
 }finally{await close?.();f.dispose();}
});

test('真实 NuGet 图中候选传递 Runtime 1.0 不覆盖宿主直引 1.2，借用后卸载仍保留宿主版本',async()=>{
 const f=fixture();try{
  const feed=path.join(f.root,'feed');mkdirSync(feed);const xml=value=>value.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');
  const config='<configuration><packageSources><clear/><add key="fixture" value="'+xml(feed)+'"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><config><add key="globalPackagesFolder" value="'+xml(path.join(f.root,'nuget-cache'))+'"/></config></configuration>';
  write(f.root,'NuGet.Config',config);
  const dotnet=async(directory,args)=>execute('dotnet',args,{cwd:directory,encoding:'utf8',timeout:120000,maxBuffer:8*1024*1024,env:{...process.env,DOTNET_NOLOGO:'1',MSBUILDDISABLENODEREUSE:'1',DOTNET_CLI_USE_MSBUILD_SERVER:'0'}});
  for(const version of ['1.0.0','1.2.0']){
   const folder=path.join(f.root,'runtime-'+version);write(folder,'Runtime.csproj','<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><PackageId>Qian.Runtime</PackageId><AssemblyName>Qian.Runtime</AssemblyName><Version>'+version+'</Version><NuGetAudit>false</NuGetAudit></PropertyGroup></Project>');write(folder,'Code.cs','namespace Qian.Runtime; public static class Marker { public const string Version = "'+version+'"; }');
   await dotnet(folder,['pack','Runtime.csproj','-c','Release','-o',feed,'--disable-build-servers','-m:1']);
  }
  const provider='Qian.EngineFixture.Provider',folder=path.join(f.root,'provider');write(folder,'Provider.csproj','<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><PackageId>'+provider+'</PackageId><Version>1.0.0</Version><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><PackageReference Include="Qian.Runtime" Version="[1.0.0,)"/><PackageReference Include="Kun.Core" Version="2026.10.12"/></ItemGroup></Project>');write(folder,'Code.cs','public static class Provider { public static string RuntimeVersion => Qian.Runtime.Marker.Version; }');
  await dotnet(folder,['pack','Provider.csproj','-c','Release','-o',feed,'--disable-build-servers','-m:1']);
  assert.ok(JSON.parse(readFileSync(path.join(folder,'obj/project.assets.json'),'utf8')).libraries['Qian.Runtime/1.0.0']);
  write(f.host,'Host.csproj','<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><NuGetAudit>false</NuGetAudit></PropertyGroup><ItemGroup><PackageReference Include="Qian.Runtime" Version="[1.2.0]"/><PackageReference Include="Kun.Core" Version="[2026.10.12]"/></ItemGroup></Project>');
  await dotnet(f.host,['restore','Host.csproj']);assert.equal(readHostEngineVersions(f.host).find(item=>item.package==='qian.runtime').version,'1.2.0');
  f.component('1.0.0',{nuget:[{id:provider,version:'1.0.0'}]},{qian:'^1.2.0',kun:'>=2026.10.12'});
  const planned=await f.install(true);assert.equal(planned.enginePlan.evidence.find(item=>item.package==='qian.runtime').version,'1.2.0');assert.match(planned.enginePlan.evidence.find(item=>item.package==='qian.runtime').source,/project.assets.json/);
  const installed=await f.install();assert.equal(installed.status,'installed');assert.equal(installed.verification[0].engines.find(item=>item.package==='qian.runtime').version,'1.2.0');
  const lock=readFileSync(path.join(f.host,'.qian/lock.json'),'utf8');f.component('2.0.0',{nuget:[{id:provider,version:'1.0.0'}]},{qian:'^2.0.0',kun:'>=2026.10.12'});await assert.rejects(f.install(),incompatible);assert.equal(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8'),lock);
  assert.equal((await removeMarketplacePlugins(['engine'],{workingDirectory:f.host,profile:'minimal',purge:true})).status,'removed');
  const workspace=await inspectPackageWorkspace(f.host,['nuget']);assert.equal(workspace.nuget.dependencies.find(item=>item.name==='Qian.Runtime').version,'1.2.0');assert.equal(readHostEngineVersions(f.host,workspace).find(item=>item.package==='qian.runtime').version,'1.2.0');
 }finally{f.dispose();}
});

test('只读或没有相关还原的 engines 保持严格，pending 不能豁免范围语法或已钉定版本',()=>{
 const old={engine:'qian',version:'0.1.0',package:'@idadawn/qian-runtime',source:'node_modules/@idadawn/qian-runtime/package.json'};
 assert.throws(()=>verifyComponentEngines({engines:{qian:'^0.2.0'}},planEngineVerification([old]).evidence),incompatible);
 const workspace={root:'/fixture',npm:{manifest:'/fixture/package.json',dependencies:[{name:old.package,version:'0.1.0',section:'dependencies'}]}};
 const plan=planEngineVerification([old],workspace,{add:[ref('npm','qian-engine-helper@1.0.0')],remove:[]});assert.equal(plan.evidence[0].version,'0.1.0');assert.ok(!plan.pending.includes('qian'));
 assert.throws(()=>verifyComponentEngines({engines:{qian:'^0.2.0'}},plan.evidence,undefined,plan.pending),incompatible);
 assert.throws(()=>verifyComponentEngines({engines:{qian:'not-a-range'}},[],undefined,['qian']),incompatible);
});
