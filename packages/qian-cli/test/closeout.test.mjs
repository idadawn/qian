import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,readFileSync,writeFileSync,rmSync,realpathSync,existsSync,readdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath} from 'node:url';
import {spawnSync,spawn} from 'node:child_process';
import {once} from 'node:events';
import {setTimeout as delay} from 'node:timers/promises';
import {createHash,generateKeyPairSync,sign} from 'node:crypto';
import {stringify} from 'yaml';
import {zipSync} from 'fflate';
import {readDirectory} from '../dist/marketplace-files.js';
import {registerMarketplace,diagnosticFile} from '../dist/marketplaces.js';
import {readHostEngineVersions,verifyComponentEngines} from '../dist/engine-compatibility.js';
const repo=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..'),cli=path.join(repo,'packages/qian-cli/dist/main.js');
const write=(root,name,bytes)=>{const file=path.join(root,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,bytes);};
function fixture(){
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-closeout-')),host=path.join(root,'host'),catalog=path.join(root,'catalog');
 write(host,'profiles/minimal/qian.profile.yml','name: minimal\nbundles: []\npatch: ./qian.patch.yml\n');write(host,'profiles/minimal/qian.patch.yml','[]\n');
 const keys=generateKeyPairSync('ed25519'),pem=keys.publicKey.export({type:'spki',format:'pem'});
 const market={name:'catalog',interface:{displayName:'验收市场'},owner:{name:'Qian',publicKeys:['./key.pub']},plugins:[]};
 write(catalog,'key.pub',pem);write(host,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:[{name:'catalog',source:catalog,priority:1}],trust:{keys:[pem]}}));
 const persist=()=>{const data=Buffer.from(JSON.stringify(market,null,2)+'\n');write(catalog,'.qian/marketplace.json',data);write(catalog,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(data).digest(),keys.privateKey));};
 const component=(name,extra={})=>{
  const directory=path.join(catalog,'plugins',name),manifest={id:'test.'+name,version:'1.0.0',description:'用于验证 CLI 收尾和来源安全的独立声明式组件。',author:{name:'Qian'},license:'Apache-2.0',interface:{displayName:'指引',category:'operations'},inject:[],provides:[],policy:{tier:'private',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global'],...extra};
  write(directory,'.qian-plugin/plugin.json',JSON.stringify(manifest,null,2)+'\n');write(directory,'skills/check/SKILL.md','---\nname: check\ndescription: 当用户需要核对安装信息和来源时使用，不用于写入操作。\n---\n只读核对安装状态。\n');
  if(!market.plugins.some(item=>item.name===name))market.plugins.push({name,source:{type:'local',path:'./plugins/'+name},category:'operations',policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}});
  persist();return directory;
 };
 const run=(args,env={},imports=[])=>spawnSync(process.execPath,[...imports.flatMap(file=>['--import',file]),cli,'--cwd',host,'--no-home','--json',...args],{encoding:'utf8',timeout:60000,env:{...process.env,...env}});
 return{root,host,catalog,market,pem,persist,component,run,dispose:()=>rmSync(root,{recursive:true,force:true})};
}
function rejected(result,code){assert.equal(result.status,1,result.stdout);assert.equal(result.stdout,'');assert.equal(result.stderr.trim().split('\n').length,1);const data=JSON.parse(result.stderr);assert.ok(data.diagnostics.some(item=>item.error.errors.some(detail=>detail.code===code)),result.stderr);assert.ok(data.diagnostics.every(item=>!path.isAbsolute(item.file)&&item.error.errors.every(detail=>!['INPUT_INVALID','UNAVAILABLE'].includes(detail.code))));return data;}

test('来源与登记共用 SAS 凭据守卫，拒绝前不写 lock 或泄漏查询值',()=>{
 const f=fixture();try{
  f.component('guide');
  for(const key of ['sig','SIG','%73ig','se','sp','sv','sktid','skoid','X-Amz-Signature','X-Goog-Credential','AWSAccessKeyId']){
   const secret='DO-NOT-LEAK';
   for(const field of ['url','repo']){
    f.market.plugins[0].source=field==='url'?{type:'url',url:'https://example.test/guide.zip?'+key+'='+secret,digest:'sha256:'+'0'.repeat(64)}:{type:'git-subdir',repo:'https://example.test/guide.git?'+key+'='+secret,ref:'main',path:'guide'};f.persist();
    const result=f.run(['add','guide','--yes']);rejected(result,'SURFACE_POLICY_DENIED');assert.ok(!(result.stdout+result.stderr).includes(secret));assert.equal(existsSync(path.join(f.host,'.qian/lock.json')),false);
    const list=f.run(['list','--remote']);rejected(list,'SURFACE_POLICY_DENIED');assert.ok(!list.stderr.includes(secret));
   }
   const home=path.join(f.root,'home');assert.throws(()=>registerMarketplace({workingDirectory:f.host,homeDirectory:home},{name:'unsafe',source:'git+https://example.test/market.git?'+key+'='+secret+'#main',priority:1}));assert.equal(existsSync(path.join(home,'marketplaces.json')),false);
  }
 }finally{f.dispose();}
});

test('安装错误分类与单行 JSON、相对路径诊断保持一致',()=>{
 const f=fixture();try{
  const directory=f.component('guide');
  write(f.catalog,'.qian/marketplace.json.sig',Buffer.alloc(64));rejected(f.run(['add','guide','--yes']),'SURFACE_SIGNATURE_INVALID');f.persist();
  f.market.plugins[0].source.digest='sha256:'+'0'.repeat(64);f.persist();rejected(f.run(['add','guide','--yes']),'SURFACE_DIGEST_MISMATCH');delete f.market.plugins[0].source.digest;f.persist();
  f.market.plugins[0].policy.profiles=['platform'];f.persist();rejected(f.run(['add','guide','--yes']),'SURFACE_PROFILE_DENIED');f.market.plugins[0].policy.profiles=['minimal'];f.persist();
  write(f.host,'.qian/.install.lock','stale');rejected(f.run(['add','guide','--yes']),'SURFACE_INSTALLATION_BUSY');rmSync(path.join(f.host,'.qian/.install.lock'));
  const installed=f.run(['add','guide','--yes']);assert.equal(installed.status,0,installed.stderr);
  write(f.host,'.qian/plugins/test.guide/skills/check/SKILL.md','manually modified');rejected(f.run(['remove','guide','--purge']),'SURFACE_LOCAL_MODIFIED');
  for(const args of [['dump-config','--profile','missing'],['docs','generate','--profile','missing']])rejected(f.run(args),'PROFILE_INVALID');
  assert.equal(diagnosticFile(path.join(f.host,'profiles/minimal/qian.patch.yml')),'profiles/minimal/qian.patch.yml');
  assert.ok(existsSync(directory));
 }finally{f.dispose();}
});

test('安装末检只覆盖受影响组件，返回默认提示词、密钥和 profile；批量 warning 只列一次',()=>{
 const f=fixture();try{
  f.component('guide',{'x-skeleton':true,interface:{displayName:'验收',category:'operations',defaultPrompt:['核对当前配置']},'x-config':{key:'secretRef://service/example'}});
  f.component('other');const installed=f.run(['add','guide','other','--yes']);assert.equal(installed.status,0,installed.stderr);const batch=JSON.parse(installed.stdout);
  assert.equal(batch.diagnostics.filter(item=>item.severity==='warning').length,1);assert.ok(batch.plugins.every(item=>!item.diagnostics.some(d=>d.error.errors.some(e=>e.message.includes('骨架')))));
  assert.deepEqual(batch.verification[0].components,['test.guide','test.other']);assert.equal(batch.verification[0].healthy,true);assert.equal(batch.verification[0].runtimeVerified,false);
  const guide=batch.plugins.find(item=>item.id==='test.guide');assert.deepEqual(guide.defaultPrompt,['核对当前配置']);assert.ok(guide.nextSteps.some(item=>item.kind==='secretRef'&&item.reference==='secretRef://service/example'));assert.ok(guide.nextSteps.some(item=>item.kind==='profile'&&item.profile==='minimal'));
  f.component('third');const next=f.run(['add','third','--yes']);assert.equal(next.status,0,next.stderr);assert.deepEqual(JSON.parse(next.stdout).verification[0].components,['test.third']);
 }finally{f.dispose();}
});

test('engines 使用真实宿主 Runtime 包版本，缺证据、不兼容与未许可预发布均拒绝',()=>{
 const f=fixture();try{
  f.component('guide',{engines:{qian:'>=0.1.1 <0.2.0'}});
  rejected(f.run(['add','guide','--yes']),'SURFACE_ENGINE_INCOMPATIBLE');
  const runtime=JSON.parse(readFileSync(path.join(repo,'packages/qian-runtime/package.json'),'utf8'));
  write(f.host,'node_modules/@idadawn/qian-runtime/package.json',JSON.stringify({name:runtime.name,version:runtime.version}));
  const installed=f.run(['add','guide','--yes']);assert.equal(installed.status,0,installed.stderr);assert.equal(JSON.parse(installed.stdout).verification[0].engines[0].version,runtime.version);
  write(f.host,'node_modules/@idadawn/qian-runtime/package.json',JSON.stringify({name:runtime.name,version:'0.1.2-beta.1'}));
  assert.throws(()=>verifyComponentEngines({engines:{qian:'^0.1.0'}},readHostEngineVersions(f.host)),error=>error.diagnostics[0].error.errors[0].code==='SURFACE_ENGINE_INCOMPATIBLE');
  verifyComponentEngines({engines:{qian:'>=0.1.2-beta.1 <0.2.0'}},readHostEngineVersions(f.host));
  rejected(f.run(['add','guide','--yes']),'SURFACE_ENGINE_INCOMPATIBLE');
  assert.equal(JSON.parse(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8')).plugins[0].version,'1.0.0');
 }finally{f.dispose();}
});

test('list --outdated 检验市场最新组件，--surfaces 支持独立显示和按表面过滤',()=>{
 const f=fixture();try{
  f.component('guide');const installed=f.run(['add','guide','--yes']);assert.equal(installed.status,0,installed.stderr);
  f.component('guide',{version:'2.0.0'});
  const outdated=f.run(['list','--outdated']);assert.equal(outdated.status,0,outdated.stderr);const result=JSON.parse(outdated.stdout);assert.equal(result.plugins[0].latestVersion,'2.0.0');assert.equal(result.plugins[0].version,'1.0.0');
  for(const args of [['list','--surfaces'],['list','--surfaces','skills']]){const shown=f.run(args);assert.equal(shown.status,0,shown.stderr);assert.deepEqual(JSON.parse(shown.stdout).plugins[0].surfaceFiles,[{surface:'skills',path:'skills/check/SKILL.md'}]);}
  assert.deepEqual(JSON.parse(f.run(['list','--surfaces','commands']).stdout).plugins,[]);
  write(f.catalog,'.qian/marketplace.json.sig',Buffer.alloc(64));rejected(f.run(['list','--outdated']),'SURFACE_SIGNATURE_INVALID');
 }finally{f.dispose();}
});

test('组件名 list 不覆盖 add 动词，前置 surfaces flag 仍按最终动词解释',()=>{
 const f=fixture();try{
  f.component('list');const added=f.run(['add','list','--surfaces','skills','--yes']);assert.equal(added.status,0,added.stderr);assert.equal(JSON.parse(added.stdout).id,'test.list');
  const listed=f.run(['--surfaces','skills','list']);assert.equal(listed.status,0,listed.stderr);assert.equal(JSON.parse(listed.stdout).plugins[0].surfaceFiles[0].surface,'skills');
 }finally{f.dispose();}
});

test('list 表面明细按锁定清单的自定义 metadata 指针定位',()=>{
 const f=fixture();try{
  const directory=f.component('metadata',{surfaces:{metadata:'./declarations'}});write(directory,'declarations/catalog.json','{}');
  const installed=f.run(['add','metadata','--yes']);assert.equal(installed.status,0,installed.stderr);
  const listed=f.run(['list','--surfaces','metadata']);assert.equal(listed.status,0,listed.stderr);assert.deepEqual(JSON.parse(listed.stdout).plugins[0].surfaceFiles,[{surface:'metadata',path:'declarations/catalog.json'}]);
 }finally{f.dispose();}
});

test('URL 来源经完整 CLI 下载、验摘要、安装、锁定和移除',()=>{
 const f=fixture();try{
  const directory=f.component('guide'),content=readDirectory(directory),archive=Buffer.from(zipSync(Object.fromEntries(content.files))),url='https://plugins.example.test/guide.zip?version=1';
  const archivePath=path.join(f.root,'guide.zip');writeFileSync(archivePath,archive);const preload=path.join(f.root,'fetch.mjs');
  writeFileSync(preload,`import {readFileSync} from 'node:fs'; globalThis.fetch=async (url,options)=>{if(String(url)!==${JSON.stringify(url)}||options.redirect!=='error')throw new Error('unexpected download');return new Response(readFileSync(${JSON.stringify(archivePath)}));};`);
  f.market.plugins[0].source={type:'url',url,digest:'sha256:'+createHash('sha256').update(archive).digest('hex')};f.persist();
  const result=f.run(['add','guide','--yes'],{},[preload]);assert.equal(result.status,0,result.stderr);const record=JSON.parse(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8')).plugins[0];
  assert.deepEqual(record.source,f.market.plugins[0].source);assert.equal(record.digest,content.digest);assert.equal(JSON.parse(f.run(['list']).stdout).plugins[0].source.url,'https://plugins.example.test/guide.zip');
  assert.equal(f.run(['remove','guide','--purge']).status,0);
 }finally{f.dispose();}
});

test('git-subdir 来源经完整 CLI 获取真实提交、锁定并按摘要验证分支',()=>{
 const f=fixture();try{
  const directory=f.component('guide'),checkout=path.join(f.root,'checkout');mkdirSync(checkout);const files=readDirectory(directory);
  for(const[name,bytes]of files.files)write(checkout,'plugins/guide/'+name,bytes);
  const git=(...args)=>{const result=spawnSync('git',['-c','user.name=cody','-c','user.email=idadawn@outlook.com','-c','core.hooksPath=/dev/null','-C',checkout,...args],{encoding:'utf8'});assert.equal(result.status,0,result.stderr);return result.stdout.trim();};
  git('init','--template=','-b','main');git('add','.');git('commit','-m','fixture');const commit=git('rev-parse','HEAD');
  const remote='https://git.example.test/plugins.git';f.market.plugins[0].source={type:'git-subdir',repo:remote,ref:'main',path:'plugins/guide',digest:files.digest};f.persist();
  const env={GIT_CONFIG_COUNT:'2',GIT_CONFIG_KEY_0:'url.'+checkout+'.insteadOf',GIT_CONFIG_VALUE_0:remote,GIT_CONFIG_KEY_1:'protocol.file.allow',GIT_CONFIG_VALUE_1:'always'};
  const result=f.run(['add','guide','--yes'],env);assert.equal(result.status,0,result.stderr);const record=JSON.parse(readFileSync(path.join(f.host,'.qian/lock.json'),'utf8')).plugins[0];
  assert.equal(record.commit,commit);assert.equal(record.source.ref,'main');assert.equal(record.source.repo,remote);assert.equal(record.digest,files.digest);assert.equal(f.run(['remove','guide','--purge']).status,0);
 }finally{f.dispose();}
});

test('restricted 与带代码组件的坏签名拒绝整批安装，不产生宿主或半批文件',()=>{
 const f=fixture();try{
  f.component('guide');f.component('code',{policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL'},engines:{kun:'>=2026.10.12'},runtime:{form:'module',entry:'Example.Code',nuget:[{id:'Kun.Core',version:'2026.10.12'}]}});
  write(f.host,'Host.csproj','<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>');
  const before=readFileSync(path.join(f.host,'Host.csproj'),'utf8'),patch=readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8');
  f.market.plugins[1].policy.installation='restricted';f.persist();assert.match(JSON.stringify(rejected(f.run(['add','guide','code','--yes']),'SURFACE_POLICY_DENIED')),/restricted/);
  const second=path.join(f.root,'code-catalog'),keys=generateKeyPairSync('ed25519'),pem=keys.publicKey.export({type:'spki',format:'pem'});
  for(const[name,bytes]of readDirectory(path.join(f.catalog,'plugins/code')).files)write(second,'plugins/code/'+name,bytes);
  const market={...f.market,name:'code-catalog',owner:{name:'代码发布者',publicKeys:['./key.pub']},plugins:[{...f.market.plugins[1],policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}}]};
  write(second,'key.pub',pem);const bytes=Buffer.from(JSON.stringify(market));write(second,'.qian/marketplace.json',bytes);write(second,'.qian/marketplace.json.sig',Buffer.alloc(64));
  write(f.host,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:[{name:'catalog',source:f.catalog,priority:1},{name:'code-catalog',source:second,priority:2}],trust:{keys:[f.pem,pem]}}));
  // 首候选显式来自合法市场；解析第二个代码候选才触发验签错误。
  assert.equal(f.run(['add','catalog/guide','--dry-run','--yes']).status,0);
  rejected(f.run(['add','catalog/guide','code-catalog/code','--yes']),'SURFACE_SIGNATURE_INVALID');
  assert.equal(readFileSync(path.join(f.host,'Host.csproj'),'utf8'),before);assert.equal(readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8'),patch);assert.equal(existsSync(path.join(f.host,'.qian/lock.json')),false);assert.equal(existsSync(path.join(f.host,'.qian/plugins')),false);assert.equal(existsSync(path.join(f.host,'obj')),false);
  write(second,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(bytes).digest(),keys.privateKey));
  const plan=f.run(['add','catalog/guide','code-catalog/code','--dry-run','--yes']);assert.equal(plan.status,0,plan.stderr);assert.deepEqual(JSON.parse(plan.stdout).plugins[1].packages.nuget,['Kun.Core@2026.10.12']);assert.equal(existsSync(path.join(f.host,'.qian/lock.json')),false);
 }finally{f.dispose();}
});

test('真实 CLI add 写入后 SIGKILL，doctor --fix 恢复后可再次 add/remove',async()=>{
 const f=fixture();let child;try{
  f.component('guide');const patch=readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8'),preload=path.join(f.root,'pause.mjs'),ready=path.join(f.root,'written');
  // 仅测试进程替换提交边界；已完成真实 CLI 写入，journal 仍是 prepared。
  writeFileSync(preload,`import {writeFileSync} from 'node:fs';import {WorkspaceTransaction} from ${JSON.stringify(path.join(repo,'packages/qian-cli/dist/workspace-transaction.js'))};WorkspaceTransaction.prototype.commit=function(){writeFileSync(${JSON.stringify(ready)},'ready');Atomics.wait(new Int32Array(new SharedArrayBuffer(4)),0,0);};`);
  child=spawn(process.execPath,['--import',preload,cli,'--cwd',f.host,'--no-home','--json','add','guide','--yes'],{stdio:['ignore','pipe','pipe']});let stderr='';child.stderr.on('data',data=>stderr+=data);
  const deadline=Date.now()+15000;while(!existsSync(ready)){assert.ok(Date.now()<deadline,stderr);assert.equal(child.exitCode,null,stderr);await delay(25);}
  assert.equal(existsSync(path.join(f.host,'.qian/plugins/test.guide')),true);const stopped=once(child,'close');child.kill('SIGKILL');await stopped;
  const fixed=f.run(['doctor','--fix']);assert.equal(fixed.status,0,fixed.stderr);assert.equal(JSON.parse(fixed.stdout).recovery.recovered,true);
  assert.equal(readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8'),patch);assert.equal(existsSync(path.join(f.host,'.qian/plugins/test.guide')),false);assert.equal(existsSync(path.join(f.host,'.qian/lock.json')),false);assert.equal(existsSync(path.join(f.host,'.qian/.install.lock')),false);
  const repeated=f.run(['doctor','--fix']);assert.equal(repeated.status,0,repeated.stderr);assert.equal(JSON.parse(repeated.stdout).recovery.recovered,false);
  const added=f.run(['add','guide','--yes']);assert.equal(added.status,0,added.stderr);const removed=f.run(['remove','guide','--purge']);assert.equal(removed.status,0,removed.stderr);
 }finally{if(child?.exitCode===null)child.kill('SIGKILL');f.dispose();}
});
