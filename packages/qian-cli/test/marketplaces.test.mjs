import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,mkdirSync,readFileSync,writeFileSync,rmSync,realpathSync,existsSync,symlinkSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {generateKeyPairSync,createHash,sign} from 'node:crypto';
import {stringify} from 'yaml';
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
