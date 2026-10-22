import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,writeFileSync,readFileSync,rmSync,existsSync,mkdirSync,symlinkSync,unlinkSync,readdirSync,chmodSync,statSync,readlinkSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {setTimeout as delay} from 'node:timers/promises';
import {WorkspaceTransaction,recoverWorkspaceTransaction} from '../dist/workspace-transaction.js';
import {acquireInstallationLease,readInstallationLease} from '../dist/installation-lease.js';
import {packageProcessGroupExited} from '../dist/package-process.js';
import {readProcessIdentity,processIdentityState} from '../dist/process-identity.js';

const supported=process.platform==='darwin'||process.platform==='linux';
const transactionUrl=new URL('../dist/workspace-transaction.js',import.meta.url).href;
const packageUrl=new URL('../dist/package-process.js',import.meta.url).href;
const leaseUrl=new URL('../dist/installation-lease.js',import.meta.url).href;
const launcherFile=new URL('../dist/package-launcher.js',import.meta.url).pathname;
function root(){return mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-recovery-'));}
const lockFile=directory=>path.join(directory,'.qian/.install.lock');
const readLock=directory=>JSON.parse(readFileSync(lockFile(directory),'utf8'));
function journal(directory){return path.join(directory,readLock(directory).journal);}
const fieldCode=code=>error=>error.diagnostics?.some(item=>item.error.errors?.some(field=>field.code===code));
async function waitFor(check,message='等待真实子进程状态超时'){
 for(let i=0;i<250;i++){if(await check())return;await delay(20);}assert.fail(message);
}
async function kill(child){if(child.exitCode!==null||child.signalCode!==null)return;const exited=once(child,'exit');child.kill('SIGKILL');await exited;}
function start(directory,body){
 return spawn(process.execPath,['--input-type=module','-e',`
  import {WorkspaceTransaction} from ${JSON.stringify(transactionUrl)};
  import {runPackageCommand} from ${JSON.stringify(packageUrl)};
  import {acquireInstallationLease} from ${JSON.stringify(leaseUrl)};
  import {writeFileSync,mkdirSync} from 'node:fs';import path from 'node:path';
  const root=${JSON.stringify(directory)};
  ${body}
 `],{cwd:directory,stdio:['ignore','ignore','pipe','ipc']});
}
function populate(directory){
 writeFileSync(path.join(directory,'package.json'),'original manifest\n');chmodSync(path.join(directory,'package.json'),0o640);
 mkdirSync(path.join(directory,'node_modules'));writeFileSync(path.join(directory,'node_modules/keep'),'original dependency');
 symlinkSync('keep',path.join(directory,'node_modules/alias'));
}
const createTransaction=`const transaction=WorkspaceTransaction.begin(root,[{path:path.join(root,'package.json'),kind:'file'},
 {path:path.join(root,'node_modules'),kind:'directory'},{path:path.join(root,'package-lock.json'),kind:'file'}],'add fixture');`;
const changeFiles=`writeFileSync(path.join(root,'package.json'),'changed');writeFileSync(path.join(root,'node_modules/keep'),'changed dependency');
 writeFileSync(path.join(root,'node_modules/new'),'new dependency');writeFileSync(path.join(root,'package-lock.json'),'new lock');`;
async function interrupted(directory,extra=''){
 populate(directory);const child=start(directory,`${createTransaction}${changeFiles}${extra}process.send({type:'prepared'});setInterval(()=>{},1000);`);
 const[message]=await once(child,'message');assert.equal(message.type,'prepared');await kill(child);return child;
}
function assertOriginal(directory){
 assert.equal(readFileSync(path.join(directory,'package.json'),'utf8'),'original manifest\n');assert.equal(statSync(path.join(directory,'package.json')).mode&0o777,0o640);
 assert.equal(readFileSync(path.join(directory,'node_modules/keep'),'utf8'),'original dependency');assert.equal(readlinkSync(path.join(directory,'node_modules/alias')),'keep');
 assert.equal(existsSync(path.join(directory,'node_modules/new')),false);assert.equal(existsSync(path.join(directory,'package-lock.json')),false);
}

test('真实事务子进程在 prepared 后被杀，恢复完整文件集合与模式，清锁后可继续变更',{skip:!supported},async()=>{
 const directory=root();
 try{
  await interrupted(directory);const file=journal(directory),before=JSON.parse(readFileSync(file,'utf8'));
  assert.equal(before.state,'prepared');assert.equal(before.version,2);
  assert.deepEqual(await recoverWorkspaceTransaction(directory),{recovered:true,state:'rolled-back',operation:'add fixture'});
  assertOriginal(directory);assert.equal(existsSync(lockFile(directory)),false);assert.equal(JSON.parse(readFileSync(file,'utf8')).state,'rolled-back');
  assert.equal(readdirSync(path.dirname(file)).some(name=>name.endsWith('.backup')),false);
  assert.deepEqual(await recoverWorkspaceTransaction(directory),{recovered:false});
  const installed=path.join(directory,'installed');
  await WorkspaceTransaction.begin(directory,[{path:installed,kind:'file'}],'add again').run(async()=>writeFileSync(installed,'installed'));
  await WorkspaceTransaction.begin(directory,[{path:installed,kind:'file'}],'remove again').run(async()=>unlinkSync(installed));
  assert.equal(existsSync(lockFile(directory)),false);assert.equal(existsSync(installed),false);
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('包命令实际写文件前归属已持久化，父进程被杀后有活跃孙进程时拒绝，整组退出后恢复',{skip:!supported},async()=>{
 const directory=root(),finish=path.join(directory,'finish'),marker=path.join(directory,'writer-started');let child,group;
 try{
  populate(directory);
  const descendant=`const fs=require('node:fs');const lock=JSON.parse(fs.readFileSync(${JSON.stringify(lockFile(directory))},'utf8'));
   if(lock.processes.length!==1)process.exit(7);fs.writeFileSync(${JSON.stringify(marker)},String(lock.processes[0].group));
   const timer=setInterval(()=>{if(fs.existsSync(${JSON.stringify(finish)})){clearInterval(timer);return;}
    fs.writeFileSync(${JSON.stringify(path.join(directory,'package.json'))},'active writer');},10);`;
  const command=`require('node:child_process').spawn(process.execPath,['-e',${JSON.stringify(descendant)}],{stdio:'ignore'});setInterval(()=>{},1000);`;
  // 包命令的父进程先退出；独立的孙进程继续写文件，恢复必须检查整个组。
  const commandWithExit=command.replace('setInterval(()=>{},1000);','setTimeout(()=>process.exit(0),1500);');
  child=start(directory,`${createTransaction}await transaction.run(async()=>{${changeFiles}await runPackageCommand(process.execPath,['-e',${JSON.stringify(commandWithExit)}],root,'fixture',60000);});`);
  await waitFor(()=>existsSync(marker));group=Number(readFileSync(marker,'utf8'));assert.equal(readLock(directory).processes[0].identity.pid,group);
  await kill(child);await delay(1600);
  const originalLock=readFileSync(lockFile(directory),'utf8'),originalJournal=readFileSync(journal(directory),'utf8');
  await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));
  assert.equal(readFileSync(lockFile(directory),'utf8'),originalLock);assert.equal(readFileSync(journal(directory),'utf8'),originalJournal);
  assert.equal(await packageProcessGroupExited(group),false);writeFileSync(finish,'finish');await waitFor(()=>packageProcessGroupExited(group));
  await recoverWorkspaceTransaction(directory);assertOriginal(directory);assert.equal(existsSync(lockFile(directory)),false);
 }finally{if(child)await kill(child);if(group&&!await packageProcessGroupExited(group)){try{process.kill(-group,'SIGKILL');}catch{}}rmSync(directory,{recursive:true,force:true});}
});

test('预检临时根的包进程同样登记在安装锁，崩溃后整组退出才清理无 journal 的锁',{skip:!supported},async()=>{
 const directory=root(),marker=path.join(directory,'preflight-started'),finish=path.join(directory,'finish');let child,group;
 try{
  const command=`const fs=require('node:fs');fs.writeFileSync(${JSON.stringify(marker)},'ready');const timer=setInterval(()=>{if(fs.existsSync(${JSON.stringify(finish)}))clearInterval(timer);},20);`;
  child=start(directory,`const lease=acquireInstallationLease(root);const temporary=path.join(root,'.qian','.staging','preflight');mkdirSync(temporary,{recursive:true});
   await lease.run(()=>runPackageCommand(process.execPath,['-e',${JSON.stringify(command)}],temporary,'fixture',60000));lease.release();`);
  await waitFor(()=>existsSync(marker));group=readLock(directory).processes[0].group;assert.equal(readLock(directory).journal,undefined);await kill(child);
  await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));
  writeFileSync(finish,'finish');await waitFor(()=>packageProcessGroupExited(group));assert.equal((await recoverWorkspaceTransaction(directory)).recovered,true);
  assert.equal(existsSync(lockFile(directory)),false);const lease=acquireInstallationLease(directory);lease.release();
 }finally{if(child)await kill(child);if(group&&!await packageProcessGroupExited(group)){try{process.kill(-group,'SIGKILL');}catch{}}rmSync(directory,{recursive:true,force:true});}
});

test('登记屏障放行前杀父进程，未登记的 launcher 自动退出且不启动包命令',{skip:!supported},async()=>{
 const directory=root();let child,group;
 try{
  child=start(directory,`const {spawn}=await import('node:child_process');const launcher=spawn(process.execPath,[${JSON.stringify(launcherFile)},'barrier-fixture'],
   {detached:true,stdio:['ignore','ignore','ignore','ipc']});launcher.on('message',()=>process.send({type:'barrier',pid:launcher.pid}));`);
  const[message]=await once(child,'message');group=message.pid;assert.equal(message.type,'barrier');await kill(child);await waitFor(()=>packageProcessGroupExited(group));
  assert.equal(existsSync(lockFile(directory)),false);assert.deepEqual(readdirSync(directory),[]);
 }finally{if(child)await kill(child);if(group&&!await packageProcessGroupExited(group)){try{process.kill(-group,'SIGKILL');}catch{}}rmSync(directory,{recursive:true,force:true});}
});

test('活动安装和 PID 复用身份均拒绝恢复；旧版锁、损坏锁或 journal 不被猜测删除',{skip:!supported},async()=>{
 const directory=root();
 try{
  writeFileSync(path.join(directory,'package.json'),'before');const transaction=WorkspaceTransaction.begin(directory,[{path:path.join(directory,'package.json'),kind:'file'}],'active');
  writeFileSync(path.join(directory,'package.json'),'working');await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_INSTALLATION_BUSY'));
  await transaction.rollback();await interrupted(directory);
  const original=readFileSync(lockFile(directory),'utf8'),file=journal(directory),journalBytes=readFileSync(file,'utf8');
  const reused=readLock(directory);reused.owner=readProcessIdentity(process.pid);reused.owner.started+='-different-process';reused.pid=process.pid;
  writeFileSync(lockFile(directory),JSON.stringify(reused));assert.equal(processIdentityState(reused.owner),'reused');
  await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));assert.equal(readFileSync(file,'utf8'),journalBytes);
  for(const invalid of ['{',JSON.stringify({pid:2147483647,id:'old',journal:'.qian/journal/old.json'})]){
   writeFileSync(lockFile(directory),invalid);await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));assert.equal(readFileSync(lockFile(directory),'utf8'),invalid);
  }
  writeFileSync(lockFile(directory),original);writeFileSync(file,'{');await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));
  assert.equal(readFileSync(file,'utf8'),'{');assert.equal(readFileSync(path.join(directory,'package.json'),'utf8'),'changed');
  writeFileSync(file,journalBytes);await recoverWorkspaceTransaction(directory);assertOriginal(directory);
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('路径越界、受保护目标、备份摘要损坏与符号链接攻击均保留锁和备份，不进行部分回滚',{skip:!supported},async()=>{
 const directory=root(),outside=root();
 try{
  await interrupted(directory);const file=journal(directory),bytes=readFileSync(file,'utf8'),record=JSON.parse(bytes),victim=path.join(outside,'victim');writeFileSync(victim,'outside');
  for(const unsafe of ['../victim','.git/config','.qian','.qian/.install.lock','.qian/journal/item']){
   const forged=JSON.parse(bytes);forged.entries[0].path=unsafe;writeFileSync(file,JSON.stringify(forged));
   await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));
   assert.equal(readFileSync(path.join(directory,'package.json'),'utf8'),'changed');assert.ok(existsSync(lockFile(directory)));
  }
  writeFileSync(file,bytes);const backup=path.join(directory,record.entries[0].backup),backupBytes=readFileSync(backup);
  writeFileSync(backup,'corrupt');await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));assert.equal(readFileSync(path.join(directory,'node_modules/keep'),'utf8'),'changed dependency');
  writeFileSync(backup,backupBytes);unlinkSync(backup);symlinkSync(victim,backup);await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));
  assert.equal(readFileSync(victim,'utf8'),'outside');unlinkSync(backup);writeFileSync(backup,backupBytes);
  const manifest=path.join(directory,'package.json');unlinkSync(manifest);symlinkSync(victim,manifest);await assert.rejects(recoverWorkspaceTransaction(directory),fieldCode('SURFACE_RECOVERY_REQUIRED'));
  assert.equal(readFileSync(victim,'utf8'),'outside');assert.ok(existsSync(lockFile(directory)));unlinkSync(manifest);writeFileSync(manifest,'changed');
  await recoverWorkspaceTransaction(directory);assertOriginal(directory);
 }finally{rmSync(directory,{recursive:true,force:true});rmSync(outside,{recursive:true,force:true});}
});

test('并发恢复只允许一个写入者，其他恢复和安装拒绝，未完成的旧日志不被清理',{skip:!supported},async()=>{
 const directory=root();
 try{
  await interrupted(directory);const unfinished=path.join(directory,'.qian/journal','1000000000000-0000000000000000.json');writeFileSync(unfinished,'{"state":"prepared"}');
  const first=recoverWorkspaceTransaction(directory);
  assert.throws(()=>acquireInstallationLease(directory),fieldCode('SURFACE_INSTALLATION_BUSY'));
  const second=recoverWorkspaceTransaction(directory);await assert.rejects(second,fieldCode('SURFACE_INSTALLATION_BUSY'));await first;
  assert.equal(readFileSync(unfinished,'utf8'),'{"state":"prepared"}');assertOriginal(directory);
  assert.deepEqual(await recoverWorkspaceTransaction(directory),{recovered:false});
 }finally{rmSync(directory,{recursive:true,force:true});}
});

test('恢复进程在回滚中被真实杀掉，下一次恢复可清理死票据并完整重做回滚',{skip:!supported},async()=>{
 const directory=root(),marker=path.join(directory,'recovery-paused');let child;
 try{
  await interrupted(directory);
  child=start(directory,`const fs=await import('node:fs');const {syncBuiltinESMExports}=await import('node:module');
   fs.default.cpSync=()=>{fs.writeFileSync(${JSON.stringify(marker)},'paused');Atomics.wait(new Int32Array(new SharedArrayBuffer(4)),0,0,60000);};syncBuiltinESMExports();
   const {recoverWorkspaceTransaction}=await import(${JSON.stringify(transactionUrl)});await recoverWorkspaceTransaction(root);`);
  await waitFor(()=>existsSync(marker));assert.ok(readdirSync(path.join(directory,'.qian/.recovery.lock')).some(name=>name.endsWith('.json')));
  await kill(child);await recoverWorkspaceTransaction(directory);assertOriginal(directory);assert.equal(existsSync(lockFile(directory)),false);
  assert.equal(existsSync(path.join(directory,'.qian/.recovery.lock')),false);
 }finally{if(child)await kill(child);rmSync(directory,{recursive:true,force:true});}
});

test('preparing 只丢弃部分备份，committed 只清残留锁，两者不覆盖当前文件',{skip:!supported},async()=>{
 for(const state of ['preparing','committed']){
  const directory=root();try{
   await interrupted(directory);const file=journal(directory),record=JSON.parse(readFileSync(file,'utf8'));record.state=state;
   if(state==='preparing'){record.entries[0].ready=false;delete record.entries[0].sha256;unlinkSync(path.join(directory,record.entries[0].backup));}
   writeFileSync(file,JSON.stringify(record));await recoverWorkspaceTransaction(directory);assert.equal(readFileSync(path.join(directory,'package.json'),'utf8'),'changed');assert.equal(existsSync(lockFile(directory)),false);
  }finally{rmSync(directory,{recursive:true,force:true});}
 }
});

test('不支持身份核验的平台保留普通声明式事务，恢复明确拒绝',{skip:!supported},async()=>{
 const directory=root();let child;
 try{
  child=start(directory,`Object.defineProperty(process,'platform',{value:'unsupported-fixture'});
   const file=path.join(root,'declarative');await WorkspaceTransaction.begin(root,[{path:file,kind:'file'}],'ordinary').run(async()=>writeFileSync(file,'ok'));
   const lease=acquireInstallationLease(root);process.send({type:'ready'});setInterval(()=>{},1000);`);
  await once(child,'message');assert.equal(readFileSync(path.join(directory,'declarative'),'utf8'),'ok');await kill(child);
  // 在相同不受支持的平台进程内恢复；不能凭 PID 消失推断归属可信。
  const recovered=start(directory,`Object.defineProperty(process,'platform',{value:'unsupported-fixture'});const {recoverWorkspaceTransaction}=await import(${JSON.stringify(transactionUrl)});
   try{await recoverWorkspaceTransaction(root);process.send({code:'unexpected'});}catch(error){process.send({code:error.diagnostics[0].error.errors[0].code});}`);
  const[message]=await once(recovered,'message');assert.equal(message.code,'SURFACE_RECOVERY_REQUIRED');await once(recovered,'exit');assert.ok(existsSync(lockFile(directory)));
 }finally{if(child)await kill(child);rmSync(directory,{recursive:true,force:true});}
});
