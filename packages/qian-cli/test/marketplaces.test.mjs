import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,readFileSync,writeFileSync,rmSync,realpathSync,existsSync,symlinkSync,readdirSync,renameSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {generateKeyPairSync,createHash,sign,randomBytes} from 'node:crypto';
import {stringify,parse as parseYaml} from 'yaml';
import {registerMarketplace,resolveMarketplacePlugin,parseMarketplaceReference} from '../dist/marketplaces.js';
import {readDirectory,unpack} from '../dist/marketplace-files.js';
import {zipSync,strToU8} from 'fflate';
import {pack} from 'tar-stream';
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
function write(root,name,value){const target=path.join(root,name);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,value);}
function run(root,...args){return spawnSync(process.execPath,[path.join(repo,'packages/qian-cli/dist/main.js'),'--cwd',root,'--json',...args],{encoding:'utf8',timeout:60000});}
function component(root,id,tier='private',extra={}){
 const manifest={id,version:'1.0.0',description:'可独立校验的市场验收组件与只读使用说明。',author:{name:'测试发布者'},license:'Apache-2.0',interface:{displayName:'验收指引',category:'operations'},inject:[],provides:[],policy:{tier,sandbox:'none',authentication:'ON_INSTALL'},scopes:['global'],...extra};
 write(root,'.qian-plugin/plugin.json',JSON.stringify(manifest,null,2)+'\n');write(root,'skills/guide/SKILL.md','---\nname: guide\ndescription: 当用户需要只读检查配置并获取核对说明时使用，不用于任何写入操作。\n---\n核对配置说明。\n');write(root,'README.md','只读组件说明。\n');return manifest;
}
function market(root,name,id,{signed=true,tier='private',authentication='ON_INSTALL',installation='auto',extra={}}={}){
 component(path.join(root,'plugins',id),id,tier,extra);
 const keys=generateKeyPairSync('ed25519'),publicPem=keys.publicKey.export({type:'spki',format:'pem'});write(root,'keys/owner.pub',publicPem);
 const data={name,interface:{displayName:name},owner:{name:'验收发布者',publicKeys:['./keys/owner.pub']},categories:['operations'],plugins:[{name:'guide',source:{type:'local',path:'./plugins/'+id},category:'operations',policy:{installation,authentication,profiles:['minimal']}}]};
 const persist=()=>{const bytes=Buffer.from(JSON.stringify(data,null,2)+'\n');write(root,'.qian/marketplace.json',bytes);if(signed)write(root,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(bytes).digest(),keys.privateKey));};persist();return{data,publicPem,persist,keys};
}
function fixture(){const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-markets-')),project=path.join(root,'project'),home=path.join(root,'home');mkdirSync(project);mkdirSync(home);write(project,'profiles/minimal/qian.profile.yml','name: minimal\nbundles: []\npatch: ./qian.patch.yml\n');write(project,'profiles/minimal/qian.patch.yml','[]\n');return{root,project,home,dispose:()=>rmSync(root,{recursive:true,force:true})};}
function configure(f,markets,keys){write(f.project,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:markets,trust:{keys}}));}

test('真实 CLI 合并两处目录、按优先级安装并锁定来源，重跑保持幂等',async()=>{
 const f=fixture();try{
  const official=market(path.join(f.root,'official'),'official','test.public'),privateMarket=market(path.join(f.root,'private'),'tenant-private','test.private');
  configure(f,[{name:'official',source:path.join(f.root,'official'),priority:20}],[official.publicPem,privateMarket.publicPem]);
  registerMarketplace({workingDirectory:f.project,homeDirectory:f.home},{name:'tenant-private',source:path.join(f.root,'private'),priority:10});
  const listing=run(f.project,'--home',f.home,'list','--remote');assert.equal(listing.status,0,listing.stderr);const entries=JSON.parse(listing.stdout).entries;
  assert.equal(entries.length,2);assert.equal(entries[0].market,'tenant-private');assert.equal(entries[0].selected,true);assert.equal(entries[1].selected,false);
  const install=run(f.project,'--home',f.home,'add','guide@1.0.0','--no-code','--yes');assert.equal(install.status,0,install.stderr);assert.equal(JSON.parse(install.stdout).status,'installed');
  assert.ok(existsSync(path.join(f.project,'.qian/plugins/test.private/skills/guide/SKILL.md')));
  const lock=JSON.parse(readFileSync(path.join(f.project,'.qian/lock.json'),'utf8'));assert.equal(lock.plugins[0].market,'tenant-private');assert.equal(lock.plugins[0].source.path,'./plugins/test.private');assert.match(lock.plugins[0].digest,/^sha256:[a-f0-9]{64}$/);
  const repeat=run(f.project,'--home',f.home,'add','guide@1.0.0','--no-code','--yes');assert.equal(repeat.status,0,repeat.stderr);assert.equal(JSON.parse(repeat.stdout).status,'unchanged');
  write(f.project,'.qian/plugins/test.private/README.md','人工改动\n');assert.equal(run(f.project,'--home',f.home,'add','guide','--no-code','--yes').status,1);
 }finally{f.dispose();}
});
test('签名、内容、组件校验及未验证市场权限均不可绕过',async()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  write(location,'plugins/test.guide/skills/guide/SKILL.md','不是合法技能\n');await assert.rejects(resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false}),error=>error.diagnostics.some(d=>d.error.errors[0].code==='SURFACE_SYNTAX_INVALID'));
  component(path.join(location,'plugins/test.guide'),'test.guide');created.data.plugins[0].source.digest='sha256:'+'0'.repeat(64);created.persist();await assert.rejects(resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false}));
  delete created.data.plugins[0].source.digest;created.persist();write(location,'.qian/marketplace.json.sig',Buffer.alloc(64));await assert.rejects(resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false}));
  await assert.rejects(resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false,allowUnverified:true}));
  component(path.join(location,'plugins/test.guide'),'test.guide','community');const resolved=await resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false,allowUnverified:true});assert.equal(resolved.trust,'unverified');
 }finally{f.dispose();}
});
test('未通过校验器或验签的组件经真实 CLI add 失败，--json 输出结构化错误且不留下锁、patch 或文件',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const patchFile=path.join(f.project,'profiles/minimal/qian.patch.yml'),patch=readFileSync(patchFile,'utf8');
  // 失败只写 stderr：一行 {diagnostics:[…]}，每条是带 trace_id 的问题详情，逐项错误给出机器可读的 code 与 JSON Pointer。
  const rejected=(file,code,pointer,message)=>{
   const output=run(f.project,'--no-home','add','guide','--no-code','--yes');
   assert.equal(output.status,1);assert.equal(output.stdout,'');
   const[diagnostic,...rest]=JSON.parse(output.stderr).diagnostics;assert.deepEqual(rest,[]);
   assert.equal(diagnostic.file,file);assert.equal(diagnostic.severity,'error');
   assert.equal(diagnostic.error.code,'INPUT_INVALID');assert.equal(diagnostic.error.status,400);assert.match(diagnostic.error.trace_id,/^[0-9a-f]{32}$/);
   assert.deepEqual(diagnostic.error.errors.map(item=>[item.code,item.pointer]),[[code,pointer]]);assert.match(diagnostic.error.errors[0].message,message);
   assert.ok(!existsSync(path.join(f.project,'.qian/lock.json')));assert.ok(!existsSync(path.join(f.project,'.qian/plugins/test.guide')));assert.equal(readFileSync(patchFile,'utf8'),patch);
  };
  // 校验器：技能文件不是合法表面。
  write(location,'plugins/test.guide/skills/guide/SKILL.md','不是合法技能\n');rejected('skills/guide/SKILL.md','SURFACE_SYNTAX_INVALID','skills/guide/SKILL.md#',/语法/);
  // 验签：市场签名不是受信任公钥签出的。
  component(path.join(location,'plugins/test.guide'),'test.guide');write(location,'.qian/marketplace.json.sig',Buffer.alloc(64));
  rejected('.qian/marketplace.json','SURFACE_POLICY_DENIED','.qian/marketplace.json#',/验签/);
  // 签名有效，但条目 digest 与组件目录内容不符。
  created.data.plugins[0].source.digest='sha256:'+'0'.repeat(64);created.persist();rejected('.qian/marketplace.json','SURFACE_POLICY_DENIED','.qian/marketplace.json#/plugins',/digest 不匹配/);
  // 修好后同一命令成功，证明失败来自上述校验而不是夹具。
  delete created.data.plugins[0].source.digest;created.persist();
  const installed=run(f.project,'--no-home','add','guide','--no-code','--yes');assert.equal(installed.status,0,installed.stderr);assert.equal(JSON.parse(installed.stdout).status,'installed');
 }finally{f.dispose();}
});
test('ON_USE 只提示登记，confirm 需要明确确认，失败不留下锁或文件',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide',{authentication:'ON_USE'});configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  let output=run(f.project,'--no-home','add','guide','--no-code','--yes');assert.equal(output.status,0,output.stderr);assert.equal(JSON.parse(output.stdout).status,'on-use');assert.ok(!existsSync(path.join(f.project,'.qian/lock.json')));
  created.data.plugins[0].policy={authentication:'ON_INSTALL',installation:'confirm'};created.persist();output=run(f.project,'--no-home','add','guide','--no-code');assert.equal(output.status,1);assert.ok(!existsSync(path.join(f.project,'.qian/plugins/test.guide')));
  output=run(f.project,'--no-home','add','guide','--no-code','--dry-run');assert.equal(output.status,0,output.stderr);assert.equal(JSON.parse(output.stdout).status,'planned');assert.ok(!existsSync(path.join(f.project,'.qian/lock.json')));
 }finally{f.dispose();}
});
test('归档拒绝越界和链接，目录 digest 与枚举顺序无关',async()=>{
 const f=fixture();try{
  write(f.root,'one/a.txt','a');write(f.root,'one/b.txt','b');write(f.root,'two/b.txt','b');write(f.root,'two/a.txt','a');assert.equal(readDirectory(path.join(f.root,'one')).digest,readDirectory(path.join(f.root,'two')).digest);write(f.root,'two/a.txt','changed');assert.notEqual(readDirectory(path.join(f.root,'one')).digest,readDirectory(path.join(f.root,'two')).digest);
  await assert.rejects(unpack(Buffer.from(zipSync({'../escape':strToU8('bad')})),'zip'));
  const archive=pack(),chunks=[];archive.on('data',chunk=>chunks.push(chunk));const done=new Promise(resolve=>archive.on('end',resolve));archive.entry({name:'escape',type:'symlink',linkname:'/etc/passwd'});archive.finalize();await done;await assert.rejects(unpack(Buffer.concat(chunks),'tar'));
  assert.deepEqual(parseMarketplaceReference('private/guide@1.2.3'),{name:'guide',market:'private',version:'1.2.3',gitTag:'refs/tags/v1.2.3'});
 }finally{f.dispose();}
});

function git(root,...args){const result=spawnSync('git',['-c','user.name=cody','-c','user.email=idadawn@outlook.com','-c','core.hooksPath=/dev/null','-C',root,...args],{encoding:'utf8',timeout:30000});assert.equal(result.status,0,result.stderr);return result.stdout.trim();}
test('实际 Git 标签选择精确版本，代码包版本来自所选清单，分支必须有 digest',async()=>{
 const f=fixture();try{
  const checkout=path.join(f.root,'git'),location=path.join(f.root,'catalog');mkdirSync(checkout);
  git(checkout,'init','--template=','-b','main');
  component(path.join(checkout,'plugins/guide'),'test.guide','official',{version:'1.2.3',engines:{kun:'>=2026.10.9'},runtime:{form:'module',entry:'Test.Provider',nuget:[{id:'Test.Abstractions',version:'1.2.3'}],npm:[{name:'@test/guide',version:'1.2.3'}]}});
  git(checkout,'add','.');assert.match(git(checkout,'var','GIT_AUTHOR_IDENT'),/^cody <idadawn@outlook.com>/);git(checkout,'commit','-m','fixture 1.2.3');git(checkout,'tag','v1.2.3');const tagged=git(checkout,'rev-parse','HEAD');
  component(path.join(checkout,'plugins/guide'),'test.guide','official',{version:'2.0.0',engines:{kun:'>=2026.10.9'},runtime:{form:'module',entry:'Test.Provider',nuget:[{id:'Test.Abstractions',version:'2.0.0'}]}});git(checkout,'add','.');git(checkout,'var','GIT_AUTHOR_IDENT');git(checkout,'commit','-m','fixture 2.0.0');
  const created=market(location,'catalog','test.unused');created.data.plugins[0].source={type:'git-subdir',repo:'https://git.example.test/plugins.git',ref:'main',path:'plugins/guide'};created.persist();configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const options={workingDirectory:f.project,profile:'platform',includeHome:false,gitMirrors:{'https://git.example.test/plugins.git':checkout}};
  await assert.rejects(resolveMarketplacePlugin('guide',options),error=>error.diagnostics.some(d=>d.error.errors[0].message.includes('分支')));
  const selected=await resolveMarketplacePlugin('guide@1.2.3',options).catch(error=>assert.fail(JSON.stringify(error.diagnostics)));assert.equal(selected.commit,tagged);assert.equal(selected.resolvedRef,'refs/tags/v1.2.3');assert.deepEqual(selected.packages,{nuget:['Test.Abstractions@1.2.3'],npm:['@test/guide@1.2.3']});
 }finally{f.dispose();}
});
test('URL 归档先验原始 digest，再验证解包后的真实组件',async()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');const content=readDirectory(path.join(location,'plugins/test.guide'));
  const bytes=Buffer.from(zipSync(Object.fromEntries(content.files))),url='https://plugins.example.test/guide.zip';
  created.data.plugins[0].source={type:'url',url,digest:'sha256:'+createHash('sha256').update(bytes).digest('hex')};created.persist();configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const selected=await resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false,fetcher:async()=>new Response(bytes)});assert.equal(selected.component.id,'test.guide');assert.equal(selected.digest,content.digest);
  await assert.rejects(resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false,fetcher:async()=>new Response(Buffer.from('changed'))}));
 }finally{f.dispose();}
});
test('装配后发现依赖缺失会回滚文件、patch 和 lock',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide',{extra:{inject:['missing.service']}});configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const before=readFileSync(path.join(f.project,'profiles/minimal/qian.patch.yml'),'utf8');
  const result=run(f.project,'--no-home','add','guide','--no-code','--yes');assert.equal(result.status,1);
  assert.equal(readFileSync(path.join(f.project,'profiles/minimal/qian.patch.yml'),'utf8'),before);assert.ok(!existsSync(path.join(f.project,'.qian/plugins/test.guide')));assert.ok(!existsSync(path.join(f.project,'.qian/lock.json')));
 }finally{f.dispose();}
});

test('撤销公钥、悬空链接和凭据 URL 均不能绕过边界',async()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  created.data.owner.publicKeys=[];created.persist();await assert.rejects(resolveMarketplacePlugin('guide',{workingDirectory:f.project,includeHome:false}));
  created.data.owner.publicKeys=['./keys/owner.pub'];created.persist();symlinkSync(path.join(f.root,'missing'),path.join(f.project,'.qian/lock.json'));
  const linked=run(f.project,'--no-home','add','guide','--no-code','--yes');assert.equal(linked.status,1);assert.ok(!existsSync(path.join(f.project,'.qian/plugins/test.guide')));
  configure(f,[{name:'catalog',source:'git+https://user:DO-NOT-LEAK@example.test/market.git#main',priority:10}],[]);
  const exposed=run(f.project,'--no-home','list','--markets');assert.equal(exposed.status,1);assert.ok(!exposed.stdout.includes('DO-NOT-LEAK'));assert.ok(!exposed.stderr.includes('DO-NOT-LEAK'));
 }finally{f.dispose();}
});
test('ZIP 的链接标记被拒绝，安装锁阻止并发写入',async()=>{
 const zip=Buffer.from(zipSync({link:strToU8('/etc/passwd')}));
 const central=zip.indexOf(Buffer.from([0x50,0x4b,0x01,0x02]));assert.ok(central>=0);zip.writeUInt32LE((0xa1ff*65536)>>>0,central+38);await assert.rejects(unpack(zip,'zip'));
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  write(f.project,'.qian/.install.lock','other-operation');const locked=run(f.project,'--no-home','add','guide','--no-code','--yes');assert.equal(locked.status,1);assert.equal(readFileSync(path.join(f.project,'.qian/.install.lock'),'utf8'),'other-operation');assert.ok(!existsSync(path.join(f.project,'.qian/plugins/test.guide')));
 }finally{f.dispose();}
});

test('真实 Config Provider 制成临时 NuGet 包后经签名市场安装，重复安装不改写宿主，末检失败整批恢复',()=>{
 const f=fixture();
 try{
  const version='0.1.0-cli-'+generateKeyPairSync('ed25519').publicKey.export({type:'spki',format:'der'}).subarray(-6).toString('hex');
  const feed=path.join(f.root,'feed');mkdirSync(feed);
  const xml=value=>value.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');
  const extra={version,engines:{kun:'>=2026.10.12'},provides:['Config'],runtime:{form:'module',entry:'Qian.Config.Apollo.ConfigurationPlugin, Qian.Config.Apollo',nuget:[{id:'Qian.Config.Apollo',version}]}};
  const packageComponent=path.join(f.root,'package-component');component(packageComponent,'qian.config','official',extra);
  const packTargets=path.join(f.root,'pack-components.targets');
  writeFileSync(packTargets,`<Project><ItemGroup Condition="'$(MSBuildProjectName)' == 'Qian.Config.Apollo'"><None Include="${xml(packageComponent)}/**/*" Pack="true" PackagePath="qian/%(RecursiveDir)%(Filename)%(Extension)" /></ItemGroup></Project>`);
  // 按项目引用求 Qian.Config.Apollo 的 Qian 依赖闭包（依赖在前）全部打进临时源；只在编译期使用的生成器引用不成为包依赖。
  const closure=(name,order=[])=>{
   if(order.includes(name))return order;
   for(const match of readFileSync(path.join(repo,'packages',name,name+'.csproj'),'utf8').matchAll(/<ProjectReference Include="([^"]+)"([^>]*)>/g))
    if(!match[2].includes('ReferenceOutputAssembly="false"'))closure(path.basename(match[1],'.csproj'),order);
   order.push(name);return order;
  };
  for(const name of closure('Qian.Config.Apollo')){
   const packed=spawnSync('dotnet',['pack',path.join(repo,'packages',name,name+'.csproj'),'-c','Release','-p:Version='+version,'-p:CustomAfterMicrosoftCommonTargets='+packTargets,'-o',feed,'--artifacts-path',path.join(f.root,'build'),'--disable-build-servers','-m:1'],{cwd:repo,encoding:'utf8',timeout:120000});
   assert.equal(packed.status,0,packed.stdout+packed.stderr);
  }
  write(f.project,'NuGet.Config','<configuration><packageSources><clear/><add key="fixture" value="'+xml(feed)+'"/><add key="nuget.org" value="https://api.nuget.org/v3/index.json"/></packageSources><config><add key="globalPackagesFolder" value="'+xml(path.join(f.root,'consumer-cache'))+'"/></config><fallbackPackageFolders><add key="shared" value="'+xml(process.env.NUGET_PACKAGES??path.join(os.homedir(),'.nuget/packages'))+'"/></fallbackPackageFolders></configuration>');
  write(f.project,'Host.csproj','<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup></Project>');
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','qian.config',{tier:'official',extra});
  component(path.join(location,'plugins/test.consumer'),'test.consumer','private',{inject:['Config']});
  created.data.plugins.push({...created.data.plugins[0],name:'consumer',source:{type:'local',path:'./plugins/test.consumer'}});created.persist();
  configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const installed=run(f.project,'--no-home','add','consumer','guide','--yes');assert.equal(installed.status,0,installed.stderr);
  const result=JSON.parse(installed.stdout).plugins.find(item=>item.id==='qian.config');assert.equal(result.status,'installed');assert.deepEqual(result.packages.nuget,['Qian.Config.Apollo@'+version]);
  const lockFile=path.join(f.project,'.qian/lock.json'),projectFile=path.join(f.project,'Host.csproj');
  const locked=readFileSync(lockFile,'utf8'),project=readFileSync(projectFile,'utf8'),packageLock=readFileSync(path.join(f.project,'packages.lock.json'),'utf8');
  assert.equal(JSON.parse(locked).packageState[0].managed,true);
  const repeated=run(f.project,'--no-home','add','guide','--yes');assert.equal(repeated.status,0,repeated.stderr);assert.equal(JSON.parse(repeated.stdout).status,'unchanged');
  assert.equal(readFileSync(lockFile,'utf8'),locked);assert.equal(readFileSync(projectFile,'utf8'),project);assert.equal(readFileSync(path.join(f.project,'packages.lock.json'),'utf8'),packageLock);
  write(f.project,'Program.cs',`using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Qian.Config;
using Qian.Config.Apollo;
using Qian.Runtime;
using var configuration = (ConfigurationRoot)new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["Marker"]="installed" }).Build();
using var services = new ServiceCollection().AddQianConfiguration(configuration).BuildServiceProvider();
await using var runtime = new PluginLoader(services);
await runtime.MountAsync([new PluginDefinition(new PluginEntry("config","qian.config","${version}",ScopeKey.Global,0,JsonSerializer.SerializeToElement(new {})),new ConfigurationPlugin(),[ConfigDefinition.Key])]);
if(runtime.GetContext("config",ScopeKey.Global).Config().GetRequiredValue("Marker")!="installed")throw new Exception();
Console.WriteLine("real-provider-ready");
`);
  const started=spawnSync('dotnet',['run','--project','Host.csproj','--no-restore','-p:UseSharedCompilation=false'],{cwd:f.project,encoding:'utf8',timeout:120000});
  assert.equal(started.status,0,started.stdout+started.stderr);assert.ok(started.stdout.includes('real-provider-ready'));
  component(path.join(location,'plugins/qian.failed'),'qian.failed','official',{...extra,provides:[],inject:['Missing']});
  created.data.plugins.push({...created.data.plugins[0],name:'failed',source:{type:'local',path:'./plugins/qian.failed'}});created.persist();
  const failed=run(f.project,'--no-home','add','failed','--yes');assert.equal(failed.status,1);
  assert.equal(readFileSync(lockFile,'utf8'),locked);assert.equal(readFileSync(projectFile,'utf8'),project);
  assert.equal(existsSync(path.join(f.project,'.qian/plugins/qian.failed')),false);
  assert.equal(existsSync(path.join(f.project,'.qian/.install.lock')),false);
  const listed=run(f.project,'--no-home','list');assert.equal(listed.status,0,listed.stderr);assert.ok(JSON.parse(listed.stdout).plugins.some(item=>item.id==='qian.config'));
  const beforeRejectedRemoval=readFileSync(lockFile,'utf8');
  const rejectedRemoval=run(f.project,'--no-home','remove','guide');assert.equal(rejectedRemoval.status,1);
  assert.equal(readFileSync(lockFile,'utf8'),beforeRejectedRemoval);assert.equal(readFileSync(projectFile,'utf8'),project);
  assert.ok(existsSync(path.join(f.project,'.qian/plugins/qian.config')));
  const consumerRemoval=run(f.project,'--no-home','remove','consumer','--purge');assert.equal(consumerRemoval.status,0,consumerRemoval.stderr);
  const removed=run(f.project,'--no-home','remove','guide');assert.equal(removed.status,0,removed.stderr);
  assert.equal(existsSync(path.join(f.project,'.qian/plugins/qian.config')),false);
  assert.ok(!readFileSync(projectFile,'utf8').includes('Qian.Config.Apollo'));
  assert.deepEqual(JSON.parse(run(f.project,'--no-home','list').stdout).plugins,[]);
  const reinstalled=run(f.project,'--no-home','add','guide','--yes');assert.equal(reinstalled.status,0,reinstalled.stderr);
  const purged=run(f.project,'--no-home','remove','catalog/guide','--purge');assert.equal(purged.status,0,purged.stderr);
  assert.equal(readFileSync(path.join(f.project,'profiles/minimal/qian.patch.yml'),'utf8'),'[]\n');
  const bundle=path.join(location,'plugins/qian.bundle.fixture');
  const providerArchive=readFileSync(path.join(feed,readdirSync(feed).find(name=>name.toLowerCase()===('Qian.Config.Apollo.'+version+'.nupkg').toLowerCase())));
  const providerDigest='sha256:'+createHash('sha256').update(providerArchive).digest('hex');
  const bundleExtra={engines:{kun:'>=2026.10.12'},runtime:{nuget:[{id:'Qian.Config.Apollo',version,digest:providerDigest}]},surfaces:{patch:'./config/qian.patch.yml'}};
  component(bundle,'qian.bundle.fixture','official',bundleExtra);
  component(path.join(bundle,'components/qian.config'),'qian.config','official',extra);
  component(path.join(bundle,'components/test.consumer'),'test.consumer','private',{inject:['Config']});
  write(bundle,'components/test.consumer/schemas/config.schema.json',JSON.stringify({type:'object',properties:{value:{type:'integer',default:1}},additionalProperties:false}));
  write(bundle,'config/qian.patch.yml',stringify([{id:'config',plugin:'qian.config@'+version,config:{}},{id:'consumer',plugin:'test.consumer@1.0.0',config:{value:2}}]));
  created.data.plugins.push({...created.data.plugins[0],name:'bundle',source:{type:'local',path:'./plugins/qian.bundle.fixture'}});created.persist();
  const bundleInstall=run(f.project,'--no-home','add','bundle','--yes');assert.equal(bundleInstall.status,0,bundleInstall.stderr);
  const definitionFile=path.join(f.project,'profiles/minimal/qian.profile.yml'),patchFile=path.join(f.project,'profiles/minimal/qian.patch.yml');
  assert.deepEqual(parseYaml(readFileSync(definitionFile,'utf8')).bundles,['qian.bundle.fixture@1.0.0']);
  assert.ok(existsSync(path.join(f.project,'.qian/plugins/qian.bundle.fixture/components/qian.config/.qian-plugin/plugin.json')));
  const tree=run(f.project,'--no-home','dump-config');assert.equal(tree.status,0,tree.stderr);assert.equal(JSON.parse(tree.stdout).entries.length,3);
  writeFileSync(patchFile,stringify([...parseYaml(readFileSync(patchFile,'utf8')),{id:'consumer',config:{value:7},note:'keep'}]));
  component(bundle,'qian.bundle.fixture','official',{...bundleExtra,version:'2.0.0'});
  const bundleUpgrade=run(f.project,'--no-home','add','bundle','--yes');assert.equal(bundleUpgrade.status,0,bundleUpgrade.stderr);
  assert.equal(parseYaml(readFileSync(definitionFile,'utf8')).bundles[0],'qian.bundle.fixture@2.0.0');
  assert.equal(JSON.parse(run(f.project,'--no-home','dump-config').stdout).entries.find(entry=>entry.id==='consumer').config.value,7);
  const bundleRemoval=run(f.project,'--no-home','remove','bundle');assert.equal(bundleRemoval.status,0,bundleRemoval.stderr);
  assert.deepEqual(parseYaml(readFileSync(definitionFile,'utf8')).bundles,[]);
  assert.equal(parseYaml(readFileSync(patchFile,'utf8')).find(entry=>entry.id==='consumer').config.value,7);
  assert.equal(JSON.parse(run(f.project,'--no-home','dump-config').stdout).entries.length,0);
  const bundleReinstall=run(f.project,'--no-home','add','bundle','--yes');assert.equal(bundleReinstall.status,0,bundleReinstall.stderr);
  assert.equal(JSON.parse(run(f.project,'--no-home','dump-config').stdout).entries.find(entry=>entry.id==='consumer').config.value,7);
  assert.equal(run(f.project,'--no-home','remove','bundle').status,0);
  const bundlePurge=run(f.project,'--no-home','remove','bundle','--purge');assert.equal(bundlePurge.status,0,bundlePurge.stderr);assert.equal(readFileSync(patchFile,'utf8'),'[]\n');
  const packageBundle=path.join(location,'plugins/qian.bundle.package');component(packageBundle,'qian.bundle.package','official',bundleExtra);
  write(packageBundle,'config/qian.patch.yml',stringify([{id:'config',plugin:'qian.config@'+version,config:{}}]));
  created.data.plugins.push({...created.data.plugins[0],name:'from-package',source:{type:'local',path:'./plugins/qian.bundle.package'}});created.persist();
  const beforePlan=readFileSync(projectFile,'utf8'),beforePlanLock=readFileSync(lockFile,'utf8');
  component(packageBundle,'qian.bundle.package','official',{...bundleExtra,runtime:{nuget:[{id:'Qian.Config.Apollo',version,digest:'sha256:'+'0'.repeat(64)}]}});
  const wrongDigest=run(f.project,'--no-home','add','from-package','--force','--yes');assert.equal(wrongDigest.status,1);assert.match(wrongDigest.stderr,/digest 不一致/);
  assert.equal(readFileSync(projectFile,'utf8'),beforePlan);assert.equal(readFileSync(lockFile,'utf8'),beforePlanLock);
  assert.equal(existsSync(path.join(f.project,'.qian/plugins/qian.bundle.package')),false);
  component(packageBundle,'qian.bundle.package','official',bundleExtra);
  renameSync(projectFile,projectFile+'.disabled');
  const codeFree=run(f.project,'--no-home','add','from-package','--surfaces','skills','--no-code','--yes');
  renameSync(projectFile+'.disabled',projectFile);
  assert.equal(codeFree.status,0,codeFree.stderr);assert.equal(readFileSync(projectFile,'utf8'),beforePlan);
  const codeFreeRecord=JSON.parse(readFileSync(lockFile,'utf8')).plugins.find(item=>item.id==='qian.bundle.package');
  assert.deepEqual(codeFreeRecord.packages,{nuget:[],npm:[]});assert.equal(codeFreeRecord.packageArtifacts.length,0);assert.equal(codeFreeRecord.sourcePackageArtifacts[0].digest,providerDigest);
  assert.equal(JSON.parse(readFileSync(path.join(f.project,'.qian/plugins/qian.bundle.package/components/qian.config/.qian-plugin/plugin.json'),'utf8')).runtime,undefined);
  const codeFreeTree=JSON.parse(run(f.project,'--no-home','dump-config').stdout);assert.ok(codeFreeTree.entries.every(entry=>entry.provides.length===0));assert.equal(codeFreeTree.entries.length,2);
  const removedCodeFree=run(f.project,'--no-home','remove','from-package','--purge');assert.equal(removedCodeFree.status,0,removedCodeFree.stderr);
  assert.equal(readFileSync(projectFile,'utf8'),beforePlan);assert.equal(readFileSync(lockFile,'utf8'),beforePlanLock);
  const planned=run(f.project,'--no-home','add','from-package','--dry-run','--yes');assert.equal(planned.status,0,planned.stderr);
  assert.equal(readFileSync(projectFile,'utf8'),beforePlan);assert.equal(readFileSync(lockFile,'utf8'),beforePlanLock);
  assert.equal(existsSync(path.join(f.project,'.qian/plugins/qian.bundle.package')),false);
  const packageInstalled=run(f.project,'--no-home','add','from-package','--yes');assert.equal(packageInstalled.status,0,packageInstalled.stderr);
  assert.ok(existsSync(path.join(f.project,'.qian/plugins/qian.bundle.package/components/qian.config/.qian-plugin/plugin.json')));
  const packageRecord=JSON.parse(readFileSync(lockFile,'utf8')).plugins.find(item=>item.id==='qian.bundle.package');
  assert.ok(packageRecord.packageArtifacts.some(item=>item.name==='Qian.Config.Apollo'&&item.componentId==='qian.config'&&/^sha256:/.test(item.digest)));
  assert.equal(packageRecord.packageArtifacts.find(item=>item.name==='Qian.Config.Apollo').digest,providerDigest);
  assert.notEqual(packageRecord.deliveryDigest,packageRecord.digest);
  const packageTree=run(f.project,'--no-home','dump-config');assert.equal(packageTree.status,0,packageTree.stderr);assert.equal(JSON.parse(packageTree.stdout).entries[0].plugin,'qian.config@'+version);
  const lockedPackage=readFileSync(lockFile,'utf8'),installedProject=readFileSync(projectFile,'utf8');
  component(path.join(packageBundle,'components/qian.config'),'qian.config','official',{...extra,description:'这个同名声明故意与真实 NuGet 包内容不同，用于验证不可覆盖。'});
  const conflict=run(f.project,'--no-home','add','from-package','--force','--yes');assert.equal(conflict.status,1);
  assert.equal(readFileSync(lockFile,'utf8'),lockedPackage);assert.equal(readFileSync(projectFile,'utf8'),installedProject);
 }finally{f.dispose();}
});

test('required 平台组件不能被 remove 或 purge 删除',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','qian.required',{tier:'system',extra:{policy:{tier:'system',sandbox:'none',authentication:'ON_INSTALL',required:true}}});
  configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const installed=run(f.project,'--no-home','add','guide','--yes');assert.equal(installed.status,0,installed.stderr);
  const file=path.join(f.project,'.qian/lock.json'),before=readFileSync(file,'utf8');
  const removed=run(f.project,'--no-home','remove','guide','--purge');assert.equal(removed.status,1);assert.equal(readFileSync(file,'utf8'),before);
  assert.ok(existsSync(path.join(f.project,'.qian/plugins/qian.required')));assert.equal(existsSync(path.join(f.project,'.qian/.install.lock')),false);
  write(f.project,'profiles/secondary/qian.profile.yml','name: secondary\nbundles: []\npatch: ./qian.patch.yml\n');write(f.project,'profiles/secondary/qian.patch.yml','[]\n');
  const unrelated=run(f.project,'--no-home','--profile','secondary','remove','guide','--purge');assert.equal(unrelated.status,0,unrelated.stderr);assert.equal(JSON.parse(unrelated.stdout).status,'unchanged');
  assert.equal(readFileSync(file,'utf8'),before);
 }finally{f.dispose();}
});

test('本地列表不回显 lock 扩展配置或 URL 凭据',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  assert.equal(run(f.project,'--no-home','add','guide','--yes').status,0);
  const file=path.join(f.project,'.qian/lock.json'),lock=JSON.parse(readFileSync(file,'utf8'));
  lock.plugins[0].extra={credential:'DO-NOT-LEAK'};lock.plugins[0].entries[0].config={secret:'DO-NOT-LEAK'};
  lock.plugins[0].source={type:'url',url:'https://user:DO-NOT-LEAK@example.test/archive?token=DO-NOT-LEAK'};
  writeFileSync(file,JSON.stringify(lock));const output=run(f.project,'--no-home','list');
  assert.equal(output.status,0,output.stderr);assert.ok(!output.stdout.includes('DO-NOT-LEAK'));
  assert.equal(JSON.parse(output.stdout).plugins[0].source.url,'https://example.test/archive');
 }finally{f.dispose();}
});

test('批量实际包安装失败时全部组件与宿主恢复，不留下半批记录',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');
  component(path.join(location,'plugins/test.missing'),'test.missing','official',{runtime:{form:'module',entry:'NeverActivated',npm:[{name:'semver',version:'7.7.4'}]}});
  created.data.plugins.push({...created.data.plugins[0],name:'missing',source:{type:'local',path:'./plugins/test.missing'}});created.persist();
  configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const initialPackage='{"private":true,"overrides":{"semver":"7.7.3"}}\n';
  write(f.project,'package.json',initialPackage);write(f.project,'.npmrc','fetch-retries=0\nfetch-timeout=5000\n');
  const before=readFileSync(path.join(f.project,'profiles/minimal/qian.patch.yml'),'utf8');
  const failed=run(f.project,'--no-home','add','guide','missing','--yes');assert.equal(failed.status,1);
  assert.equal(readFileSync(path.join(f.project,'package.json'),'utf8'),initialPackage);
  assert.equal(readFileSync(path.join(f.project,'profiles/minimal/qian.patch.yml'),'utf8'),before);
  assert.equal(existsSync(path.join(f.project,'.qian/plugins/test.guide')),false);assert.equal(existsSync(path.join(f.project,'.qian/plugins/test.missing')),false);
  assert.equal(existsSync(path.join(f.project,'.qian/lock.json')),false);assert.equal(existsSync(path.join(f.project,'node_modules')),false);
  const journal=path.join(f.project,'.qian/journal'),records=readdirSync(journal).filter(name=>name.endsWith('.json')).map(name=>JSON.parse(readFileSync(path.join(journal,name),'utf8')));
  assert.equal(records.length,1);assert.equal(records[0].state,'rolled-back');assert.equal(existsSync(path.join(f.project,'.qian/.install.lock')),false);
 }finally{f.dispose();}
});

test('升级保留配置，不兼容时无改动，reset-config 使用新默认值，新增权限重新确认',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide'),directory=path.join(location,'plugins/test.guide');
  const schema=(type,defaultValue)=>({type:'object',additionalProperties:false,properties:{value:{type,default:defaultValue}}});
  write(directory,'schemas/config.schema.json',JSON.stringify(schema('integer',10)));configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  assert.equal(run(f.project,'--no-home','add','guide','--yes').status,0);
  const patchFile=path.join(f.project,'profiles/minimal/qian.patch.yml'),lockFile=path.join(f.project,'.qian/lock.json');
  let patch=parseYaml(readFileSync(patchFile,'utf8'));assert.equal(patch[0].config.value,10);patch[0].config.value=17;writeFileSync(patchFile,stringify(patch));
  component(directory,'test.guide','private',{version:'2.0.0'});write(directory,'schemas/config.schema.json',JSON.stringify(schema('integer',25)));
  let upgraded=run(f.project,'--no-home','add','guide');assert.equal(upgraded.status,0,upgraded.stderr);assert.equal(parseYaml(readFileSync(patchFile,'utf8'))[0].config.value,17);
  component(directory,'test.guide','private',{version:'3.0.0'});write(directory,'schemas/config.schema.json',JSON.stringify(schema('string','new-default')));
  const before=readFileSync(lockFile,'utf8'),beforePatch=readFileSync(patchFile,'utf8');
  upgraded=run(f.project,'--no-home','add','guide');assert.equal(upgraded.status,1);assert.equal(readFileSync(lockFile,'utf8'),before);assert.equal(readFileSync(patchFile,'utf8'),beforePatch);
  upgraded=run(f.project,'--no-home','add','guide','--reset-config');assert.equal(upgraded.status,0,upgraded.stderr);assert.equal(parseYaml(readFileSync(patchFile,'utf8'))[0].config.value,'new-default');
  component(directory,'test.guide','private',{version:'4.0.0',policy:{tier:'private',sandbox:'none',authentication:'ON_INSTALL',permissions:{platform:['files:read']}}});
  const locked=readFileSync(lockFile,'utf8');
  const unapproved=run(f.project,'--no-home','add','guide');assert.equal(unapproved.status,1);assert.equal(readFileSync(lockFile,'utf8'),locked);
  const approved=run(f.project,'--no-home','add','guide','--yes');assert.equal(approved.status,0,approved.stderr);
 }finally{f.dispose();}
});

test('同组件可挂到多个 profile，升级更新全部归属但分别保留配置',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide'),directory=path.join(location,'plugins/test.guide');
  write(directory,'schemas/config.schema.json',JSON.stringify({type:'object',properties:{value:{type:'integer',default:1}},additionalProperties:false}));
  configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  write(f.project,'profiles/secondary/qian.profile.yml','name: secondary\nbundles: []\npatch: ./qian.patch.yml\n');write(f.project,'profiles/secondary/qian.patch.yml','[]\n');
  assert.equal(run(f.project,'--no-home','add','guide','--yes').status,0);
  const second=run(f.project,'--no-home','--profile','secondary','add','guide','--yes','--force-profile');assert.equal(second.status,0,second.stderr);
  for(const[name,value]of [['minimal',11],['secondary',22]]){const file=path.join(f.project,'profiles',name,'qian.patch.yml'),rows=parseYaml(readFileSync(file,'utf8'));rows[0].config.value=value;writeFileSync(file,stringify(rows));}
  component(directory,'test.guide','private',{version:'2.0.0'});
  const upgraded=run(f.project,'--no-home','add','guide','--force-profile');assert.equal(upgraded.status,0,upgraded.stderr);
  for(const[name,value]of [['minimal',11],['secondary',22]]){const rows=parseYaml(readFileSync(path.join(f.project,'profiles',name,'qian.patch.yml'),'utf8'));assert.equal(rows[0].plugin,'test.guide@2.0.0');assert.equal(rows[0].config.value,value);}
  const firstRemoval=run(f.project,'--no-home','remove','guide','--purge');assert.equal(firstRemoval.status,0,firstRemoval.stderr);assert.ok(existsSync(path.join(f.project,'.qian/plugins/test.guide')));
  const finalRemoval=run(f.project,'--no-home','--profile','secondary','remove','guide','--purge');assert.equal(finalRemoval.status,0,finalRemoval.stderr);assert.equal(existsSync(path.join(f.project,'.qian/plugins/test.guide')),false);
 }finally{f.dispose();}
});

test('force 恢复本地文件，不自动批准被改写清单的权限',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.guide');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  assert.equal(run(f.project,'--no-home','add','guide').status,0);
  const directory=path.join(f.project,'.qian/plugins/test.guide'),readme=path.join(directory,'README.md'),manifest=path.join(directory,'.qian-plugin/plugin.json');
  writeFileSync(readme,'local edit');assert.equal(run(f.project,'--no-home','add','guide').status,1);
  const repaired=run(f.project,'--no-home','add','guide','--force');assert.equal(repaired.status,0,repaired.stderr);assert.equal(readFileSync(readme,'utf8'),'只读组件说明。\n');
  const changed=JSON.parse(readFileSync(manifest,'utf8'));changed.policy.permissions={platform:['unapproved:all']};writeFileSync(manifest,JSON.stringify(changed));
  assert.equal(run(f.project,'--no-home','add','guide','--force').status,1);
  const confirmed=run(f.project,'--no-home','add','guide','--force','--yes');assert.equal(confirmed.status,0,confirmed.stderr);
  assert.equal(JSON.parse(readFileSync(manifest,'utf8')).policy.permissions,undefined);
 }finally{f.dispose();}
});

test('默认卸载后可按原市场安装名 purge，不能清除已被复用的配置',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.different');configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  assert.equal(run(f.project,'--no-home','add','guide').status,0);assert.equal(run(f.project,'--no-home','remove','guide').status,0);
  const patchFile=path.join(f.project,'profiles/minimal/qian.patch.yml'),lockFile=path.join(f.project,'.qian/lock.json');
  assert.equal(parseYaml(readFileSync(patchFile,'utf8'))[0].disabled,true);
  assert.deepEqual(JSON.parse(run(f.project,'--no-home','list').stdout).plugins,[]);
  const purged=run(f.project,'--no-home','remove','catalog/guide','--purge');assert.equal(purged.status,0,purged.stderr);assert.equal(readFileSync(patchFile,'utf8'),'[]\n');
  assert.deepEqual(JSON.parse(readFileSync(lockFile,'utf8')).removedEntries,[]);
  assert.equal(run(f.project,'--no-home','add','guide').status,0);assert.equal(run(f.project,'--no-home','remove','guide').status,0);
  const patch=parseYaml(readFileSync(patchFile,'utf8'));patch[0].disabled=false;patch[0].plugin='test.other@1.0.0';writeFileSync(patchFile,stringify(patch));
  const before=readFileSync(patchFile,'utf8');assert.equal(run(f.project,'--no-home','remove','guide','--purge').status,1);assert.equal(readFileSync(patchFile,'utf8'),before);
 }finally{f.dispose();}
});

test('未验证组合包不能通过内联子组件带入高信任代码',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.bundle',{signed:false,tier:'community',extra:{surfaces:{patch:'./config/qian.patch.yml'}}});
  const directory=path.join(location,'plugins/test.bundle');
  component(path.join(directory,'components/qian.hidden'),'qian.hidden','official',{engines:{kun:'>=2026.10.12'},runtime:{form:'module',entry:'NeverActivated',nuget:[{id:'Kun.Core',version:'2026.10.12'}]}});
  write(directory,'config/qian.patch.yml','- id: hidden\n  plugin: qian.hidden\n  config: {}\n');
  configure(f,[{name:'catalog',source:location,priority:10,trust:'unverified'}],[]);
  const result=run(f.project,'--no-home','add','guide','--allow-unverified','--yes');assert.equal(result.status,1);
  assert.ok(result.stderr.includes('SURFACE_POLICY_DENIED'));assert.equal(existsSync(path.join(f.project,'.qian/lock.json')),false);
 }finally{f.dispose();}
});

test('组合层不能绕过活动子组件的 required 保护',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.bundle',{tier:'official',extra:{surfaces:{patch:'./config/qian.patch.yml'}}}),directory=path.join(location,'plugins/test.bundle');
  component(path.join(directory,'components/qian.required'),'qian.required','system',{policy:{tier:'system',sandbox:'none',authentication:'ON_INSTALL',required:true}});
  write(directory,'config/qian.patch.yml','- id: required\n  plugin: qian.required\n  config: {}\n');
  configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const added=run(f.project,'--no-home','add','guide');assert.equal(added.status,0,added.stderr);
  const file=path.join(f.project,'.qian/lock.json'),before=readFileSync(file,'utf8');
  const removed=run(f.project,'--no-home','remove','guide','--purge');assert.equal(removed.status,1);assert.equal(readFileSync(file,'utf8'),before);
 }finally{f.dispose();}
});

test('批量卸载多个组合层时共同处理其配置覆盖，全部保留可重装数据',()=>{
 const f=fixture();try{
  const location=path.join(f.root,'catalog'),created=market(location,'catalog','test.bundleone',{tier:'official',extra:{surfaces:{patch:'./config/qian.patch.yml'}}});
  for(const[name,value]of [['bundleone',1],['bundletwo',2]]){
   const directory=path.join(location,'plugins/test.'+name);
   if(name==='bundletwo')component(directory,'test.'+name,'official',{surfaces:{patch:'./config/qian.patch.yml'}});
   component(path.join(directory,'components/test.'+name+'child'),'test.'+name+'child','private');
   write(directory,'components/test.'+name+'child/schemas/config.schema.json',JSON.stringify({type:'object',properties:{value:{type:'integer'}},additionalProperties:false}));
   write(directory,'config/qian.patch.yml',stringify([{id:name,plugin:'test.'+name+'child',config:{value}}]));
  }
  created.data.plugins.push({...created.data.plugins[0],name:'second',source:{type:'local',path:'./plugins/test.bundletwo'}});created.persist();
  configure(f,[{name:'catalog',source:location,priority:10}],[created.publicPem]);
  const added=run(f.project,'--no-home','add','guide','second');assert.equal(added.status,0,added.stderr);
  const patchFile=path.join(f.project,'profiles/minimal/qian.patch.yml');writeFileSync(patchFile,stringify([...parseYaml(readFileSync(patchFile,'utf8')),{id:'bundleone',config:{value:11}},{id:'bundletwo',config:{value:22}}]));
  const removed=run(f.project,'--no-home','remove','guide','second');assert.equal(removed.status,0,removed.stderr);
  const rows=parseYaml(readFileSync(patchFile,'utf8'));assert.deepEqual(rows.filter(row=>['bundleone','bundletwo'].includes(row.id)).map(row=>row.config.value),[11,22]);assert.ok(rows.every(row=>row.disabled===true));
  assert.equal(JSON.parse(run(f.project,'--no-home','dump-config').stdout).entries.length,0);
 }finally{f.dispose();}
});

test('注册表与 CLI 使用同一份路径、长度与原始字节摘要清单',async()=>{
 const {snapshot}=await import('../dist/marketplace-files.js');
 const fixture=JSON.parse(readFileSync(new URL('../../../tck/registry-artifacts/content.json',import.meta.url),'utf8'));
 const content=snapshot(new Map(Object.entries(fixture.files).reverse().map(([name,value])=>[name,Buffer.from(value)])));
 assert.equal(content.digest,fixture.digest);assert.deepEqual(content.records,fixture.records);
});
