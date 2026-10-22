import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,readFileSync,writeFileSync,rmSync,realpathSync,existsSync,cpSync,readdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {generateKeyPairSync,createHash,sign} from 'node:crypto';
import {stringify,parse as parseYaml} from 'yaml';

// 正式私有 NuGet 源（GitHub Packages）上已发布的组件经真实 CLI 安装进空白 ASP.NET Core 项目。
// 市场是 CLI 已支持的签名本地目录，条目指向仓库里的真实组件文件夹；代码包只从私有源还原，不制作临时包。
// 需要 QIAN_FEED_TOKEN（读包权限）；缺失即失败，不跳过。
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const cli=path.join(repo,'packages/qian-cli/dist/main.js');
const feed=process.env.QIAN_FEED??'https://nuget.pkg.github.com/idadawn/index.json';
const market='qian-published';
const components=[{name:'data',id:'qian.data'},{name:'events',id:'qian.events'},{name:'config',id:'qian.config'}];
const xml=value=>value.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');
function write(root,name,value){const target=path.join(root,name);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,value);}
function qian(root,...args){return spawnSync(process.execPath,[cli,'--cwd',root,'--no-home','--json',...args],{encoding:'utf8',timeout:900000});}
function dotnet(cwd,...args){return spawnSync('dotnet',args,{cwd,encoding:'utf8',timeout:900000,env:{...process.env,DOTNET_NOLOGO:'1',DOTNET_CLI_TELEMETRY_OPTOUT:'1'}});}
function files(root,base=root){return readdirSync(root,{withFileTypes:true}).flatMap(item=>item.isDirectory()?files(path.join(root,item.name),base):[path.relative(base,path.join(root,item.name))]).sort();}
function manifest(id){return JSON.parse(readFileSync(path.join(repo,'plugins',id,'.qian-plugin/plugin.json'),'utf8'));}

const host=`using System.Text.Json;
using Kun.EventBus.DependencyInjection;
using Qian.Events;
using Qian.Runtime;

// 按 CLI 解析出的 profile 挂载已安装组件：代码入口取自 .qian/plugins 里的组件清单，配置取自 profile 条目。
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddKunEventBus();
builder.Services.AddQianEvents();
var app = builder.Build();
using var resolved = JsonDocument.Parse(File.ReadAllText(args[0]));
var definitions = new List<PluginDefinition>();
foreach (var item in resolved.RootElement.GetProperty("entries").EnumerateArray())
{
    var plugin = item.GetProperty("plugin").GetString()!;
    var id = plugin[..plugin.IndexOf('@')];
    using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(".qian", "plugins", id, ".qian-plugin", "plugin.json")));
    // 入口类型只从私有源还原的程序集解析；缺入口或类型不存在即失败。
    var type = Type.GetType(document.RootElement.GetProperty("runtime").GetProperty("entry").GetString()!, throwOnError: true)!;
    var provides = item.GetProperty("provides").EnumerateArray().Select(key => new ServiceKey(key.GetString()!)).ToArray();
    definitions.Add(new PluginDefinition(new PluginEntry(item.GetProperty("id").GetString()!, id, plugin[(plugin.IndexOf('@') + 1)..], ScopeKey.Global, 0, item.GetProperty("config").Clone()),
        (IQianPlugin)Activator.CreateInstance(type)!, provides));
}
await using var loader = new PluginLoader(app.Services);
await loader.MountAsync(definitions);
foreach (var definition in definitions)
    if (loader.GetState(definition.Entry.Id, ScopeKey.Global) != PluginState.Ready)
        throw new InvalidOperationException(definition.Entry.Plugin + " 未就绪", loader.GetFailure(definition.Entry.Id, ScopeKey.Global));
Console.WriteLine("installed-providers-ready:" + string.Join(',', definitions.Select(definition => definition.Entry.Plugin + "=" + definition.Plugin.GetType().Assembly.GetName().Version)));
`;

test('空白 ASP.NET Core 项目经签名市场安装私有源已发布的 data / events / config：表面进 .qian、NuGet 按清单钉版并自私有源还原、patch 与 lock 记录来源和 digest，真实 Provider 运行，remove 逆序撤销',()=>{
 assert.ok(process.env.QIAN_FEED_TOKEN,'需要 QIAN_FEED_TOKEN：私有 NuGet 源的读包令牌（CI 使用带 packages: read 的 GITHUB_TOKEN）。');
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-feed-e2e-')),project=path.join(root,'shop'),catalog=path.join(root,'catalog'),cache=path.join(root,'nuget-packages');
 try{
  const created=dotnet(root,'new','web','--name','Shop','--output',project,'--framework','net10.0','--no-restore');
  assert.equal(created.status,0,created.stdout+created.stderr);
  // 只用私有源与 nuget.org；Qian.* 映射到私有源，凭据以环境变量引用，配置文件里不出现令牌。独立包目录、不设回退目录，证明包确实取自源。
  write(project,'NuGet.Config',`<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources><clear /><add key="qian" value="${xml(feed)}" /><add key="nuget.org" value="https://api.nuget.org/v3/index.json" /></packageSources>
  <packageSourceMapping><packageSource key="qian"><package pattern="Qian.*" /></packageSource><packageSource key="nuget.org"><package pattern="*" /></packageSource></packageSourceMapping>
  <packageSourceCredentials><qian><add key="Username" value="${xml(process.env.GITHUB_ACTOR??'qian-e2e')}" /><add key="ClearTextPassword" value="%QIAN_FEED_TOKEN%" /></qian></packageSourceCredentials>
  <config><add key="globalPackagesFolder" value="${xml(cache)}" /></config>
</configuration>
`);
  // 安装器不猜测宿主组装：先放入仓库交付的 minimal profile 与其空组合包（init 模板归 S1-RT-012）。
  cpSync(path.join(repo,'profiles/minimal'),path.join(project,'profiles/minimal'),{recursive:true});
  cpSync(path.join(repo,'plugins/qian.bundle.minimal'),path.join(project,'plugins/qian.bundle.minimal'),{recursive:true});
  for(const item of components)cpSync(path.join(repo,'plugins',item.id),path.join(catalog,'plugins',item.id),{recursive:true});
  const keys=generateKeyPairSync('ed25519'),publicPem=keys.publicKey.export({type:'spki',format:'pem'});
  write(catalog,'keys/publisher.pub',publicPem);
  const marketplace={name:market,interface:{displayName:'私有源已发布组件'},owner:{name:'Qian',publicKeys:['./keys/publisher.pub']},categories:['platform'],
   plugins:components.map(item=>({name:item.name,source:{type:'local',path:'./plugins/'+item.id},category:'platform',policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}}))};
  const document=Buffer.from(JSON.stringify(marketplace,null,2)+'\n');
  write(catalog,'.qian/marketplace.json',document);write(catalog,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(document).digest(),keys.privateKey));
  write(project,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:[{name:market,source:catalog,priority:10}],trust:{keys:[publicPem]}}));
  const projectFile=path.join(project,'Shop.csproj'),patchFile=path.join(project,'profiles/minimal/qian.patch.yml');
  const before={project:readFileSync(projectFile,'utf8'),patch:readFileSync(patchFile,'utf8'),files:files(project)};

  const installed=qian(project,'add','data','events','config','--yes');
  assert.equal(installed.status,0,installed.stderr);
  assert.doesNotMatch(installed.stdout+installed.stderr,new RegExp(process.env.QIAN_FEED_TOKEN.replace(/[.*+?^${}()|[\]\\]/g,'\\$&')));
  const plugins=JSON.parse(installed.stdout).plugins,lock=JSON.parse(readFileSync(path.join(project,'.qian/lock.json'),'utf8'));
  const projectXml=readFileSync(projectFile,'utf8');
  for(const item of components){
   const declared=manifest(item.id).runtime.nuget[0],reported=plugins.find(plugin=>plugin.id===item.id);
   assert.equal(reported.status,'installed');assert.deepEqual(reported.packages.nuget,[declared.id+'@'+declared.version]);
   // 表面原样进入 .qian：逐字节等于仓库组件文件夹。
   const source=path.join(repo,'plugins',item.id),target=path.join(project,'.qian/plugins',item.id);
   assert.deepEqual(files(target),files(source));
   for(const name of files(source))assert.ok(readFileSync(path.join(target,name)).equals(readFileSync(path.join(source,name))),item.id+'/'+name);
   // NuGet 依赖按组件清单钉到确切版本（精确区间 [x]），归档摘要与清单声明一致，且由私有源提供。
   assert.ok(projectXml.includes('<PackageReference Include="'+declared.id+'" Version="['+declared.version+']" />'),projectXml);
   const restored=path.join(cache,declared.id.toLowerCase(),declared.version);
   assert.equal(JSON.parse(readFileSync(path.join(restored,'.nupkg.metadata'),'utf8')).source,feed);
   assert.equal('sha256:'+createHash('sha256').update(readFileSync(path.join(restored,declared.id.toLowerCase()+'.'+declared.version+'.nupkg'))).digest('hex'),declared.digest);
   const record=lock.plugins.find(plugin=>plugin.id===item.id);
   assert.equal(record.market,market);assert.match(record.digest,/^sha256:[0-9a-f]{64}$/);
   assert.equal(record.packageArtifacts.find(artifact=>artifact.name===declared.id).digest,declared.digest);
  }
  assert.deepEqual(parseYaml(readFileSync(patchFile,'utf8')).map(entry=>[entry.id,entry.plugin]).sort(),components.map(item=>[item.name,item.id+'@'+manifest(item.id).version]).sort());

  // 宿主用 CLI 解析出的 profile 挂载已安装组件：三个 Provider 都由私有源还原的程序集真实运行。
  const resolved=qian(project,'dump-config');assert.equal(resolved.status,0,resolved.stderr);
  const programFile=path.join(project,'Program.cs'),program=readFileSync(programFile,'utf8');
  write(root,'resolved.json',resolved.stdout);writeFileSync(programFile,host);
  const started=dotnet(project,'run','--no-restore','-p:UseSharedCompilation=false','--',path.join(root,'resolved.json'));
  assert.equal(started.status,0,started.stdout+started.stderr);
  const ready=started.stdout.split('\n').find(line=>line.startsWith('installed-providers-ready:'));
  assert.ok(ready,started.stdout);assert.deepEqual(ready.slice('installed-providers-ready:'.length).split(',').sort(),['qian.config=0.1.0.0','qian.data=0.1.0.0','qian.events=0.1.0.0']);
  writeFileSync(programFile,program);

  const listed=qian(project,'list');assert.equal(listed.status,0,listed.stderr);
  assert.deepEqual(JSON.parse(listed.stdout).plugins.map(plugin=>[plugin.id,plugin.market,plugin.packages.nuget.join(',')]).sort(),
   components.map(item=>[item.id,market,manifest(item.id).runtime.nuget.map(reference=>reference.id+'@'+reference.version).join(',')]).sort());

  // 逆序移除并清除配置：项目文件、profile patch 与文件集合回到安装前，列表为空。
  const removed=qian(project,'remove','config','events','data','--purge');assert.equal(removed.status,0,removed.stderr);
  assert.equal(readFileSync(projectFile,'utf8'),before.project);assert.equal(readFileSync(patchFile,'utf8'),before.patch);
  for(const item of components)assert.equal(existsSync(path.join(project,'.qian/plugins',item.id)),false);
  assert.deepEqual(JSON.parse(qian(project,'list').stdout).plugins,[]);
  const remaining=files(project).filter(name=>!name.startsWith('obj/')&&!name.startsWith('bin/')&&!name.startsWith('.qian/'));
  // 工作区状态不随组件撤销：首次安装写入的 .qian 忽略规则保留（.qian/ 本身仍在）；CLI 不创建 NuGet 锁文件。
  assert.deepEqual(remaining,[...before.files.filter(name=>!name.startsWith('.qian/')),'.gitignore'].sort());
  assert.equal(readFileSync(path.join(project,'.gitignore'),'utf8'),'.qian/journal/\n.qian/.staging/\n');
  assert.equal(existsSync(path.join(project,'packages.lock.json')),false);
 }finally{rmSync(root,{recursive:true,force:true});}
});
