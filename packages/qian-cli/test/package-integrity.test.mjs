import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,mkdirSync,writeFileSync,readFileSync,existsSync,readdirSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash,generateKeyPairSync,sign,randomBytes} from 'node:crypto';
import {createServer} from 'node:http';
import {once} from 'node:events';
import {spawnSync} from 'node:child_process';
import {stringify} from 'yaml';
import {npmExecutable} from '../dist/package-manager.js';
import {installMarketplacePlugins} from '../dist/marketplace-install.js';
import {installedPackageArtifacts,verifyDeclaredPackageDigests} from '../dist/package-integrity.js';

function write(root,name,value){const file=path.join(root,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,value);}
const failureText=error=>JSON.stringify(error.diagnostics);
const rejectsWith=pattern=>error=>{assert.match(failureText(error),pattern);return true;};

async function fixture(){
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-integrity-')),host=path.join(root,'host'),catalog=path.join(root,'catalog');
 const name='qian-integrity-'+randomBytes(5).toString('hex'),version='1.0.0',archives=[];
 let current=0,switchAfterFetch=false;const fetched=[];
 try{
  for(const marker of ['original','replacement']){
   const source=path.join(root,marker);mkdirSync(source);
   write(source,'package.json',JSON.stringify({name,version,main:'index.js'}));write(source,'index.js','module.exports='+JSON.stringify(marker)+';\n');
   const packed=spawnSync(process.execPath,[npmExecutable(),'pack','--ignore-scripts','--json','--pack-destination',source],{cwd:source,encoding:'utf8',timeout:30000});
   assert.equal(packed.status,0,packed.stderr);const bytes=readFileSync(path.join(source,JSON.parse(packed.stdout)[0].filename));
   archives.push({bytes,digest:'sha256:'+createHash('sha256').update(bytes).digest('hex'),integrity:'sha512-'+createHash('sha512').update(bytes).digest('base64')});
  }
  const server=createServer((request,response)=>{
   response.setHeader('cache-control','no-store');
   if(request.url==='/'+name){
    response.setHeader('content-type','application/json');response.end(JSON.stringify({name,'dist-tags':{latest:version},versions:{[version]:{name,version,dist:{tarball:'http://127.0.0.1:'+server.address().port+'/artifact-'+current+'.tgz',integrity:archives[current].integrity}}}}));return;
   }
   const match=/^\/artifact-([01])\.tgz$/.exec(request.url);
   if(match){const index=Number(match[1]);fetched.push(index);response.end(archives[index].bytes);if(switchAfterFetch)current=1;return;}
   response.statusCode=404;response.end();
  });server.listen(0,'127.0.0.1');await once(server,'listening');
  write(host,'package.json','{"private":true}\n');
  write(host,'.npmrc','registry=http://127.0.0.1:'+server.address().port+'/\nfetch-retries=0\nprefer-online=true\ncache='+path.join(root,'cache')+'\n');
  write(host,'profiles/minimal/qian.profile.yml','name: minimal\nbundles: []\npatch: ./qian.patch.yml\n');write(host,'profiles/minimal/qian.patch.yml','[]\n');
  const keys=generateKeyPairSync('ed25519'),publicPem=keys.publicKey.export({type:'spki',format:'pem'});
  write(catalog,'keys/owner.pub',publicPem);
  const componentFile=path.join(catalog,'plugins/test.integrity/.qian-plugin/plugin.json');
  const component={id:'test.integrity',version,description:'用于真实包管理器摘要核验的测试组件，包中不包含 Qian 声明式表面。',author:{name:'Fixture'},license:'Apache-2.0',interface:{displayName:'完整性测试',category:'operations'},inject:[],provides:[],policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global'],runtime:{form:'module',entry:'index.js',npm:[{name,version,digest:archives[0].digest}]}};
  write(catalog,'plugins/test.integrity/.qian-plugin/plugin.json',JSON.stringify(component));
  const market=Buffer.from(JSON.stringify({name:'catalog',interface:{displayName:'验收目录'},owner:{name:'Fixture',publicKeys:['./keys/owner.pub']},categories:['operations'],plugins:[{name:'check',source:{type:'local',path:'./plugins/test.integrity'},category:'operations',policy:{installation:'auto',authentication:'ON_INSTALL',profiles:['minimal']}}]}));
  write(catalog,'.qian/marketplace.json',market);write(catalog,'.qian/marketplace.json.sig',sign(null,createHash('sha256').update(market).digest(),keys.privateKey));
  write(host,'.qian/config.yml',stringify({defaultProfile:'minimal',marketplaces:[{name:'catalog',source:catalog,priority:10}],trust:{keys:[publicPem]}}));
  return{root,host,name,archives,fetched,component,componentFile,
   install:()=>installMarketplacePlugins(['check'],{workingDirectory:host,profile:'minimal',includeHome:false,yes:true}),
   switchAfterFetch:()=>{switchAfterFetch=true;},
   dispose:async()=>{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));rmSync(root,{recursive:true,force:true});}};
 }catch(error){rmSync(root,{recursive:true,force:true});throw error;}
}

test('真实 npm 无 qian 内容的包也校验声明摘要并锁定归档，幂等检查拒绝安装记录漂移',async()=>{
 const f=await fixture();try{
  const manifest=structuredClone(f.component);manifest.runtime.npm[0].digest='sha256:'+'0'.repeat(64);writeFileSync(f.componentFile,JSON.stringify(manifest));
  await assert.rejects(f.install(),rejectsWith(/digest 不一致/));
  assert.equal(readFileSync(path.join(f.host,'package.json'),'utf8'),'{"private":true}\n');assert.equal(existsSync(path.join(f.host,'node_modules')),false);assert.equal(existsSync(path.join(f.host,'.qian/lock.json')),false);
  writeFileSync(f.componentFile,JSON.stringify(f.component));const installed=await f.install();assert.equal(installed.status,'installed');
  const lockFile=path.join(f.host,'.qian/lock.json'),lock=readFileSync(lockFile,'utf8'),record=JSON.parse(lock).plugins[0];
  assert.equal(record.packageArtifacts.length,1);assert.equal(record.packageArtifacts[0].componentId,undefined);assert.equal(record.packageArtifacts[0].digest,f.archives[0].digest);assert.equal(record.packageArtifacts[0].integrity,f.archives[0].integrity);
  assert.match(readFileSync(path.join(f.host,'node_modules',f.name,'index.js'),'utf8'),/original/);
  assert.equal((await f.install()).status,'unchanged');assert.equal(readFileSync(lockFile,'utf8'),lock);
  const hidden=path.join(f.host,'node_modules/.package-lock.json'),actual=JSON.parse(readFileSync(hidden,'utf8'));actual.packages['node_modules/'+f.name].integrity=f.archives[1].integrity;writeFileSync(hidden,JSON.stringify(actual));
  await assert.rejects(f.install(),error=>failureText(error).includes('实际 npm 安装记录'));
  assert.equal(readFileSync(lockFile,'utf8'),lock);
 }finally{await f.dispose();}
});

test('真实 npm 在预检和安装间替换同版本归档时整批回滚，不留下组件或依赖',async()=>{
 const f=await fixture();try{
  f.switchAfterFetch();await assert.rejects(f.install(),rejectsWith(/实际 npm 锁定归档/));
  assert.ok(f.fetched.includes(0)&&f.fetched.includes(1),'真实 npm 必须先取原包再取替换包');
  assert.equal(readFileSync(path.join(f.host,'package.json'),'utf8'),'{"private":true}\n');assert.equal(readFileSync(path.join(f.host,'profiles/minimal/qian.patch.yml'),'utf8'),'[]\n');
  for(const file of ['node_modules','package-lock.json','.qian/lock.json','.qian/plugins/test.integrity','.qian/.install.lock'])assert.equal(existsSync(path.join(f.host,file)),false,file);
  const journal=path.join(f.host,'.qian/journal');assert.ok(readdirSync(journal).filter(name=>name.endsWith('.json')).some(name=>JSON.parse(readFileSync(path.join(journal,name),'utf8')).state==='rolled-back'));
 }finally{await f.dispose();}
});

test('共享包内容冲突与递归发现后补入的错误摘要都被拒绝',()=>{
 const artifact={kind:'npm',name:'example',version:'1.0.0',digest:'sha256:'+'a'.repeat(64),integrity:'sha512-'+Buffer.alloc(64).toString('base64')};
 const plugin={id:'test.first',packages:{nuget:[],npm:['example@1.0.0']},packageArtifacts:[artifact]};
 assert.equal(installedPackageArtifacts([plugin,{...plugin,id:'test.second'}]).length,1);
 assert.throws(()=>installedPackageArtifacts([plugin,{...plugin,id:'test.second',packageArtifacts:[{...artifact,digest:'sha256:'+'b'.repeat(64)}]}]),error=>failureText(error).includes('共享代码包'));
 assert.throws(()=>installedPackageArtifacts([{...plugin,packageArtifacts:[{...artifact,name:'not-declared'}]}]));
 assert.throws(()=>verifyDeclaredPackageDigests([{kind:'npm',name:'example',version:'1.0.0',specification:'example@1.0.0',owners:['test.child'],digest:'sha256:'+'b'.repeat(64)}],[artifact]),error=>failureText(error).includes('digest 不一致'));
});
