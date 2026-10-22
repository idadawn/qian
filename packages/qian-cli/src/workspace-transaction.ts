import {constants,cpSync,existsSync,lstatSync,mkdirSync,openSync,closeSync,readFileSync,writeFileSync,rmSync,unlinkSync,chmodSync,readdirSync,realpathSync,readlinkSync,fsyncSync} from 'node:fs';
import path from 'node:path';
import {createHash,randomBytes} from 'node:crypto';
import {assertNoLinks,within,sha256,safePath} from './marketplace-files.js';
import type {PackageMutationTarget} from './package-manager.js';
import {PackageCommandFailure,packageProcessGroupExited} from './package-process.js';
import {acquireInstallationLease,attachInstallationJournal,claimRecoveryLease,durableJson,installationBusy,installationLeasePath,ownedInstallationLease,readInstallationLease,readRecoveryJson,recoveryFailure,recoveryInProgress,runInInstallationContext,type InstallationLeaseRecord} from './installation-lease.js';
import {processGroupExited,processIdentityState,sameProcess,type ProcessIdentity} from './process-identity.js';

type State='preparing'|'prepared'|'committed'|'rolled-back'|'rollback-failed';
interface Backup {path:string;kind:'file'|'directory';existed:boolean;mode?:number;backup:string;ready:boolean;sha256?:string}
interface Journal {
 type:'qian-workspace-transaction';version:2;id:string;operation:string;state:State;entries:Backup[];
 leaseOwner:string;owner:ProcessIdentity;workspace:{device:number;inode:number};pendingProcess?:{pid:number|undefined;tool:string};
}
export interface WorkspaceRecovery {recovered:boolean;state?:State;operation?:string}
const failure=recoveryFailure;
function remove(file:string):void {
 assertNoLinks(file);
 if(!existsSync(file))return;
 const info=lstatSync(file);if(info.isFile())unlinkSync(file);else if(info.isDirectory())rmSync(file,{recursive:true});else failure('事务目标出现特殊文件，拒绝覆盖。');
}
function target(root:string,relative:string):string {
 if(typeof relative!=='string'||safePath(relative)!==relative||relative==='.'||relative==='.qian'||relative==='.qian/.install.lock'||relative==='.qian/.recovery.lock'||
  relative.startsWith('.qian/.recovery.lock/')||relative==='.qian/journal'||relative.startsWith('.qian/journal/'))failure('事务目标涉及受保护的安装状态目录。');
 return within(root,relative);
}
/** 目录备份的摘要包含路径、类型、模式和内容。依赖目录中的链接仅记录链接文本，绝不跟随。 */
function directoryDigest(root:string,durable=false):string {
 assertNoLinks(root);const digest=createHash('sha256');let count=0;
 const visit=(file:string,relative:string,depth:number):void=>{
  if(depth>128||++count>1_000_000)failure('事务目录备份超过结构限制。');
  const info=lstatSync(file);digest.update(JSON.stringify([relative,info.mode&0o7777]));
  if(info.isSymbolicLink()){digest.update('link:'+readlinkSync(file));return;}
  if(info.isDirectory()){
   digest.update('directory');for(const name of readdirSync(file).sort())visit(path.join(file,name),relative?relative+'/'+name:name,depth+1);
   if(durable&&process.platform!=='win32'){const fd=openSync(file,constants.O_RDONLY);try{fsyncSync(fd);}finally{closeSync(fd);}}
  }else if(info.isFile()){
   const fd=openSync(file,constants.O_RDONLY|constants.O_NOFOLLOW);try{const bytes=readFileSync(fd);digest.update('file:'+sha256(bytes));if(durable)fsyncSync(fd);}finally{closeSync(fd);}
  }else failure('事务备份含有特殊文件。');
 };
 visit(root,'',0);return digest.digest('hex');
}
function validateJournal(root:string,value:unknown,lease:InstallationLeaseRecord):Journal {
 const record=value as Journal;
 if(!record||record.type!=='qian-workspace-transaction'||record.version!==2||typeof record.id!=='string'||!/^\d+-[a-f0-9]{16}$/.test(record.id)||
  lease.journal!=='.qian/journal/'+record.id+'.json'||record.leaseOwner!==lease.id||!record.owner||!sameProcess(record.owner,lease.owner)||
  !record.workspace||record.workspace.device!==lease.workspace.device||record.workspace.inode!==lease.workspace.inode||
  typeof record.operation!=='string'||record.operation.length>4096||!['preparing','prepared','committed','rolled-back','rollback-failed'].includes(record.state)||
  !Array.isArray(record.entries)||record.entries.length>10000)failure('事务日志无效或不属于安装锁；旧版缺少进程身份的日志不能自动恢复。');
 const paths:string[]=[];
 for(const[index,entry]of record.entries.entries()){
  if(!entry||typeof entry.path!=='string'||!['file','directory'].includes(entry.kind)||typeof entry.existed!=='boolean'||typeof entry.ready!=='boolean'||
   entry.backup!=='.qian/journal/'+record.id+'.backup/'+index||entry.mode!==undefined&&(!Number.isSafeInteger(entry.mode)||entry.mode<0||entry.mode>0xffff)||
   entry.existed&&entry.mode===undefined||entry.sha256!==undefined&&!/^[a-f0-9]{64}$/.test(entry.sha256)||
   entry.existed&&entry.ready&&entry.sha256===undefined||record.state!=='preparing'&&record.state!=='rolled-back'&&!entry.ready)failure('事务备份条目无效。');
  const file=target(root,entry.path);within(root,entry.backup);
  if(paths.some(previous=>previous===file||file.startsWith(previous+path.sep)||previous.startsWith(file+path.sep)))failure('事务目标重复或相互包含。');paths.push(file);
 }
 return record;
}
function assertPackageProcessesExited(lease:InstallationLeaseRecord):void {
 if(lease.unconfirmedProcess)failure('缺少可核验的包管理器进程归属；保留安装锁、日志和备份。');
 for(const item of lease.processes){
  if(processIdentityState(item.identity)!=='exited'||!processGroupExited(item.group))failure('包管理器或其后代仍在运行，或 PID 身份无法确认；保留安装锁、日志和备份。');
 }
}
function validateBackups(root:string,record:Journal):void {
 // 在删除任意目标前验证所有条目，损坏日志或后面的坏备份不能造成部分回滚。
 for(const entry of record.entries){
  const file=target(root,entry.path),backup=within(root,entry.backup);assertNoLinks(file);assertNoLinks(backup);
  if(!entry.ready)failure('备份未完成，拒绝覆盖原目标。');
  if(!entry.existed)continue;
  if(!existsSync(backup))failure('事务备份缺失。');const info=lstatSync(backup);
  if(entry.kind==='file'?!info.isFile()||info.nlink!==1:!info.isDirectory())failure('事务备份类型无效。');
  const actual=entry.kind==='file'?sha256(readFileSync(backup)):directoryDigest(backup);
  if(actual!==entry.sha256)failure('事务备份校验失败。');
 }
}

/** 持有工作区安装锁，保存文件及生成目录的原貌，失败时逆序还原。 */
export class WorkspaceTransaction {
 private constructor(readonly root:string,readonly journalFile:string,private readonly record:Journal,private readonly leaseOwner:string){}
 get hasPendingProcess():boolean {try{return this.record.pendingProcess!==undefined||readInstallationLease(this.root).processes.length>0;}catch{return existsSync(installationLeasePath(this.root));}}

 static begin(directory:string,targets:readonly PackageMutationTarget[],operation:string,existingLeaseOwner?:string):WorkspaceTransaction {
  const root=realpathSync(directory),id=Date.now()+'-'+randomBytes(8).toString('hex'),entries:Backup[]=[],paths:string[]=[];let bytes=0;
  for(const item of targets){
   const relative=path.relative(root,path.resolve(item.path)).split(path.sep).join('/'),file=target(root,relative);
   if(paths.includes(file))continue;
   if(paths.some(previous=>file.startsWith(previous+path.sep)||previous.startsWith(file+path.sep)))failure('事务目标不能相互包含。');
   paths.push(file);const info=existsSync(file)?lstatSync(file):undefined;
   if(info&&(item.kind==='file'?!info.isFile():!info.isDirectory()))failure('事务目标类型与变更计划不一致。');
   if(item.kind==='file'&&info){bytes+=info.size;if(bytes>64*1024*1024)failure('事务文件备份超过 64 MiB。');}
   entries.push({path:relative,kind:item.kind,existed:!!info,mode:info?.mode,backup:'.qian/journal/'+id+'.backup/'+entries.length,ready:false});
  }
  const leaseOwner=existingLeaseOwner??acquireInstallationLease(root).owner,lease=ownedInstallationLease(root,leaseOwner),journalFile=path.join(root,'.qian/journal',id+'.json');
  if(lease.processes.length||lease.unconfirmedProcess||lease.journal)failure('安装锁已有活动进程或事务，不能开始新的工作区变更。');
  const transaction=new WorkspaceTransaction(root,journalFile,{type:'qian-workspace-transaction',version:2,id,operation,state:'preparing',entries,
   leaseOwner,owner:lease.owner,workspace:lease.workspace},leaseOwner);
  try{
   assertNoLinks(journalFile);mkdirSync(path.dirname(journalFile),{recursive:true,mode:0o700});
   const ignore=path.join(path.dirname(journalFile),'.gitignore');assertNoLinks(ignore);if(!existsSync(ignore))writeFileSync(ignore,'*\n',{flag:'wx',mode:0o600});
   mkdirSync(path.join(root,'.qian/journal',id+'.backup'),{mode:0o700});transaction.save('preparing');
   attachInstallationJournal(root,leaseOwner,path.relative(root,journalFile).split(path.sep).join('/'));
   for(const entry of entries){
    if(entry.existed){
     const source=target(root,entry.path),backup=within(root,entry.backup);
     if(entry.kind==='file'){
      const content=readFileSync(source);entry.sha256=sha256(content);const fd=openSync(backup,'wx',0o600);
      try{writeFileSync(fd,content);fsyncSync(fd);}finally{closeSync(fd);}
     }else{cpSync(source,backup,{recursive:true,dereference:false,verbatimSymlinks:true,preserveTimestamps:true,mode:constants.COPYFILE_FICLONE});entry.sha256=directoryDigest(backup,true);}
    }
    entry.ready=true;transaction.save('preparing');
   }
   transaction.save('prepared');return transaction;
  }catch(error){transaction.discardPreparation();throw error;}
 }
 private save(state:State):void {const next={...this.record,state};durableJson(this.journalFile,next);this.record.state=state;}
 private release():void {
  const file=installationLeasePath(this.root);if(!existsSync(file))return;
  const lease=readInstallationLease(this.root);
  if(lease.id!==this.leaseOwner||lease.journal!==path.relative(this.root,this.journalFile).split(path.sep).join('/'))failure('安装锁已变更，拒绝清除。');
  assertPackageProcessesExited(lease);assertNoLinks(file);unlinkSync(file);
 }
 private discardPreparation():void {
  // preparing 期间只复制目标，未放行工作区写入；部分备份绝不能覆盖原文件。
  if(existsSync(this.journalFile))this.save('rolled-back');
  const lease=readInstallationLease(this.root);assertPackageProcessesExited(lease);
  if(lease.journal)this.release();else if(lease.id===this.leaseOwner)unlinkSync(installationLeasePath(this.root));
  this.cleanup();
 }
 private cleanup():void {
  const backup=path.join(this.root,'.qian/journal',this.record.id+'.backup');
  try{assertNoLinks(backup);rmSync(backup,{recursive:true,force:true});}catch{ /* 已完成事务的备份可在下次整理时清理。 */ }
 }
 commit():void {
  if(this.record.state!=='prepared')failure('只有已准备的事务可以提交。');
  const lease=ownedInstallationLease(this.root,this.leaseOwner);assertPackageProcessesExited(lease);
  this.save('committed');this.release();this.cleanup();this.prune();
 }
 /** 包命令继承当前事务上下文，启动屏障在归属落盘后才放行。 */
 async run<T>(operation:()=>Promise<T>):Promise<T> {
  if(this.record.state!=='prepared')failure('事务未准备或已结束。');
  try{const value=await runInInstallationContext(this.root,this.leaseOwner,operation);this.commit();return value;}
  catch(error){
   if(error instanceof PackageCommandFailure&&!error.terminationConfirmed){
    if(!readInstallationLease(this.root).processes.length)this.record.pendingProcess={pid:error.pid,tool:error.tool};
    this.save('rollback-failed');throw error;
   }
   if(this.record.state==='prepared'||this.record.state==='rollback-failed')await this.rollback();throw error;
  }
 }
 async rollback():Promise<void> {
  if(this.record.state==='rolled-back')return;
  if(this.record.state!=='prepared'&&this.record.state!=='rollback-failed')failure('该事务不处于可回滚状态。');
  const lease=readInstallationLease(this.root);if(lease.id!==this.leaseOwner)failure('安装锁归属已改变。');assertPackageProcessesExited(lease);
  if(this.record.pendingProcess){
   const pid=this.record.pendingProcess.pid;if(pid===undefined||!await packageProcessGroupExited(pid))failure('尚未确认包管理器进程组退出，保留锁和备份，不执行回滚。');
   delete this.record.pendingProcess;
  }
  try{validateJournal(this.root,this.record,lease);validateBackups(this.root,this.record);
   for(const entry of [...this.record.entries].reverse()){
    const file=target(this.root,entry.path),backup=within(this.root,entry.backup);
    if(entry.existed){
     remove(file);mkdirSync(path.dirname(file),{recursive:true});
     if(entry.kind==='file'){writeFileSync(file,readFileSync(backup),{flag:'wx',mode:entry.mode});if(entry.mode!==undefined)chmodSync(file,entry.mode);}
     else cpSync(backup,file,{recursive:true,dereference:false,verbatimSymlinks:true,preserveTimestamps:true,mode:constants.COPYFILE_FICLONE});
    }else remove(file);
   }
  }catch{this.save('rollback-failed');failure('回滚未完成或路径、备份不安全；安装锁、日志和备份已保留。');}
  this.save('rolled-back');this.release();this.cleanup();this.prune();
 }
 static async recover(directory:string):Promise<WorkspaceRecovery> {
  try{
   assertNoLinks(directory);const root=realpathSync(directory),leaseFile=installationLeasePath(root);assertNoLinks(leaseFile);
   if(!existsSync(leaseFile)&&!recoveryInProgress(root))return{recovered:false};
   const release=await claimRecoveryLease(root);
   try{
    if(!existsSync(leaseFile))return{recovered:false};
    const lease=readInstallationLease(root),ownerState=processIdentityState(lease.owner);
    if(ownerState==='active')installationBusy();if(ownerState!=='exited')failure('安装锁拥有者的 PID 已复用或身份无法核验，保留原状态。');
    assertPackageProcessesExited(lease);
    if(!lease.journal){assertNoLinks(leaseFile);unlinkSync(leaseFile);return{recovered:true,state:'rolled-back'};}
    const journalFile=within(root,lease.journal),record=validateJournal(root,readRecoveryJson(journalFile),lease);
    if(record.pendingProcess)failure('日志中的包进程缺少启动身份，无法安全自动恢复。');
    const transaction=new WorkspaceTransaction(root,journalFile,record,lease.id);
    if(record.state==='preparing')transaction.discardPreparation();
    else if(record.state==='committed'||record.state==='rolled-back'){transaction.release();transaction.cleanup();transaction.prune();}
    else await transaction.rollback();
    return{recovered:true,state:record.state,operation:record.operation};
   }finally{release();}
  }catch(error){if(error instanceof Error&&'diagnostics' in error)throw error;return failure('事务恢复元数据或路径无效，保留安装锁、日志和备份。');}
 }
 private prune():void {
  const folder=path.dirname(this.journalFile),completed:string[]=[];
  for(const name of readdirSync(folder).filter(value=>/^\d+-[0-9a-f]{16}\.json$/.test(value)).sort()){
   const file=path.join(folder,name);
   try{const value=readRecoveryJson(file) as Journal;
    if(value.type==='qian-workspace-transaction'&&value.version===2&&value.id+'.json'===name&&(value.state==='committed'||value.state==='rolled-back'))completed.push(file);
   }catch{ /* 未知或损坏日志保留，不猜测后删除。 */ }
  }
  for(const file of completed.slice(0,-20)){
   const backup=file.slice(0,-5)+'.backup';try{assertNoLinks(backup);rmSync(backup,{recursive:true,force:true});unlinkSync(file);}catch{ /* 不回滚已提交的变更。 */ }
  }
 }
}
export async function recoverWorkspaceTransaction(root:string):Promise<WorkspaceRecovery> {return WorkspaceTransaction.recover(root);}
