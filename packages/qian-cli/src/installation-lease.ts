import {AsyncLocalStorage} from 'node:async_hooks';
import {constants,closeSync,existsSync,fsyncSync,fstatSync,lstatSync,mkdirSync,openSync,readFileSync,renameSync,unlinkSync,writeFileSync,realpathSync,readdirSync,rmdirSync} from 'node:fs';
import path from 'node:path';
import {randomBytes} from 'node:crypto';
import {assertNoLinks} from './marketplace-files.js';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import {readProcessIdentity,validProcessIdentity,processGroupExited,processIdentityState,sameProcess,type ProcessIdentity} from './process-identity.js';
import {setTimeout as delay} from 'node:timers/promises';
import type {PackageCommandFailure} from './package-process.js';

export interface RegisteredPackageProcess {identity:ProcessIdentity;group:number;tool:string}
export interface InstallationLeaseRecord {
 type:'qian-installation-lease';version:2;id:string;pid:number;owner:ProcessIdentity;
 workspace:{device:number;inode:number};journal?:string;processes:RegisteredPackageProcess[];unconfirmedProcess?:boolean;
}
export interface InstallationLease {
 owner:string;readonly hasPendingProcess:boolean;run<T>(operation:()=>Promise<T>):Promise<T>;release():void;preserveProcess(error:PackageCommandFailure):void;
}
interface LeaseContext {root:string;owner:string}
const context=new AsyncLocalStorage<LeaseContext>();
export const installationLeasePath=(root:string):string=>path.join(root,'.qian/.install.lock');
export const recoveryGuardPath=(root:string):string=>path.join(root,'.qian/.recovery.lock');
interface RecoveryTicket {type:'qian-recovery-ticket';version:1;id:string;owner:ProcessIdentity;number:number;choosing:boolean}
function recoveryTickets(root:string):{file:string;ticket:RecoveryTicket}[] {
 const directory=recoveryGuardPath(root);assertNoLinks(directory);if(!existsSync(directory))return[];
 if(!lstatSync(directory).isDirectory())recoveryFailure('恢复锁格式无效，保留原状态。');
 return readdirSync(directory).filter(name=>!(/^[a-f0-9]{32}\.json\.[a-f0-9]{16}\.tmp$/).test(name)).flatMap(name=>{
  if(!/^[a-f0-9]{32}\.json$/.test(name))recoveryFailure('恢复票据名称无效，保留原状态。');
  const file=path.join(directory,name);let ticket:RecoveryTicket;
  try{ticket=readRecoveryJson(file) as RecoveryTicket;}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT')return[];throw error;}
  if(!ticket||ticket.type!=='qian-recovery-ticket'||ticket.version!==1||ticket.id+'.json'!==name||!validProcessIdentity(ticket.owner)||
   !Number.isSafeInteger(ticket.number)||ticket.number<0||typeof ticket.choosing!=='boolean'||!ticket.choosing&&ticket.number===0)recoveryFailure('恢复票据格式无效，保留原状态。');
  return[{file,ticket}];
 });
}
export function recoveryInProgress(root:string):boolean {return recoveryTickets(root).length>0;}
/** 唯一票据避免“读旧锁后 unlink 新锁”的竞争；死亡进程的票据不会被其它恢复者复用。 */
export async function claimRecoveryLease(root:string):Promise<()=>void> {
 const directory=recoveryGuardPath(root),id=randomBytes(16).toString('hex'),file=path.join(directory,id+'.json');
 const ticket:RecoveryTicket={type:'qian-recovery-ticket',version:1,id,owner:readProcessIdentity(process.pid),number:0,choosing:true};
 // 初始票据也以 rename 完整发布，其他恢复者不会读到半份 JSON。
 for(let attempt=0;;attempt++){
  try{assertNoLinks(directory);mkdirSync(directory,{recursive:true,mode:0o700});durableJson(file,ticket);break;}
  catch(error){if(attempt>=3||(error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
 }
 const release=():void=>{
  assertNoLinks(file);if(existsSync(file)){const current=readRecoveryJson(file) as RecoveryTicket;
   if(current.id!==id||!sameProcess(current.owner,ticket.owner))recoveryFailure('恢复票据归属发生变化，保留原状态。');unlinkSync(file);
  }
  try{rmdirSync(directory);}catch(error){if(!['ENOENT','ENOTEMPTY','EEXIST'].includes(String((error as NodeJS.ErrnoException).code)))throw error;}
 };
 const others=():RecoveryTicket[]=>{
  const active:RecoveryTicket[]=[];
  for(const item of recoveryTickets(root)){
   if(item.ticket.id===id)continue;
   const state=processIdentityState(item.ticket.owner);
   if(state==='exited'){
    // 名称是不可复用的随机 ID；核验身份仍相同，再删除已经不能写文件的拥有者票据。
    try{const latest=readRecoveryJson(item.file) as RecoveryTicket;
     if(latest.id!==item.ticket.id||!sameProcess(latest.owner,item.ticket.owner))recoveryFailure('恢复票据在检查期间改变。');unlinkSync(item.file);
    }catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
   }else if(state==='active')active.push(item.ticket);else recoveryFailure('恢复票据的进程身份无法确认，保留原状态。');
  }
  return active;
 };
 try{
  ticket.number=Math.max(0,...others().map(item=>item.number))+1;ticket.choosing=false;durableJson(file,ticket);
  for(let attempt=0;attempt<40;attempt++){
   const competitors=others();
   if(competitors.some(item=>item.choosing)){await delay(25);continue;}
   if(competitors.some(item=>item.number<ticket.number||item.number===ticket.number&&item.id<id))installationBusy();
   return release;
  }
  installationBusy();
 }catch(error){release();throw error;}
}
export function recoveryFailure(message:string,file='.qian/journal'):never {
 throw new MarketplaceFailure([marketDiagnostic(file,'',message,'SURFACE_RECOVERY_REQUIRED')]);
}
export function installationBusy():never {
 throw new MarketplaceFailure([marketDiagnostic('.qian/.install.lock','','已有安装事务或残留安装锁；确认安装结束后运行 qian doctor --fix。','SURFACE_INSTALLATION_BUSY')]);
}
export function readRecoveryJson(file:string):unknown {
 assertNoLinks(file);const fd=openSync(file,constants.O_RDONLY|constants.O_NOFOLLOW);
 try{const info=fstatSync(fd);if(!info.isFile()||info.nlink!==1||info.size>4*1024*1024)recoveryFailure('恢复元数据不是受支持的普通文件。');return JSON.parse(readFileSync(fd,'utf8'));}
 finally{closeSync(fd);}
}
export function durableJson(file:string,value:unknown,exclusive=false):void {
 assertNoLinks(file);const temporary=exclusive?file:file+'.'+randomBytes(8).toString('hex')+'.tmp';
 let fd:number|undefined;
 try{fd=openSync(temporary,constants.O_CREAT|constants.O_EXCL|constants.O_WRONLY|constants.O_NOFOLLOW,0o600);writeFileSync(fd,JSON.stringify(value,null,2)+'\n');fsyncSync(fd);closeSync(fd);fd=undefined;
  if(!exclusive){assertNoLinks(file);renameSync(temporary,file);}
  if(process.platform!=='win32'){const directory=openSync(path.dirname(file),constants.O_RDONLY);try{fsyncSync(directory);}finally{closeSync(directory);}}
 }finally{if(fd!==undefined)closeSync(fd);if(!exclusive&&existsSync(temporary))unlinkSync(temporary);}
}
export function readInstallationLease(root:string):InstallationLeaseRecord {
 try{
  const value=readRecoveryJson(installationLeasePath(root)) as InstallationLeaseRecord,workspace=lstatSync(root);
  if(value.type!=='qian-installation-lease'||value.version!==2||typeof value.id!=='string'||!/^[a-z0-9-]{16,80}$/.test(value.id)||
   !validProcessIdentity(value.owner)||value.pid!==value.owner.pid||!value.workspace||value.workspace.device!==workspace.dev||value.workspace.inode!==workspace.ino||
   !Array.isArray(value.processes)||value.processes.length>128||value.processes.some(item=>!item||!validProcessIdentity(item.identity)||item.group!==item.identity.pid||
    typeof item.tool!=='string'||!/^[a-z][a-z0-9-]{0,31}$/.test(item.tool))||
   value.journal!==undefined&&(typeof value.journal!=='string'||!/^\.qian\/journal\/\d+-[a-f0-9]{16}\.json$/.test(value.journal))||
   value.unconfirmedProcess!==undefined&&value.unconfirmedProcess!==true)recoveryFailure('安装锁缺少可信的版本、进程身份或工作区归属，保留原状态。');
  return value;
 }catch(error){if(error instanceof MarketplaceFailure)throw error;return recoveryFailure('安装锁无效或路径不安全，保留原状态。');}
}
export function ownedInstallationLease(root:string,owner:string):InstallationLeaseRecord {
 const record=readInstallationLease(root);
 if(record.id!==owner||record.pid!==process.pid)recoveryFailure('安装锁不属于当前操作。');
 return record;
}
export function attachInstallationJournal(root:string,owner:string,journal:string):void {
 const record=ownedInstallationLease(root,owner);
 if(record.processes.length||record.unconfirmedProcess)recoveryFailure('预检进程尚未结束，不能开始工作区变更。');
 if(record.journal)recoveryFailure('安装锁已关联另一份事务日志。');
 durableJson(installationLeasePath(root),{...record,journal});
}
export function runWithInstallationLease<T>(lease:InstallationLease|undefined,operation:()=>Promise<T>):Promise<T> {return lease?lease.run(operation):operation();}
export function runInInstallationContext<T>(root:string,owner:string,operation:()=>Promise<T>):Promise<T> {return context.run({root,owner},operation);}
export function registerPackageProcess(identity:ProcessIdentity,tool:string):(terminationConfirmed?:boolean)=>void {
 const current=context.getStore();if(!current)return()=>{};
 const record=ownedInstallationLease(current.root,current.owner);
 if(record.processes.length>=128)recoveryFailure('包管理器进程登记超过限制。');
 record.processes.push({identity,group:identity.pid,tool:/^[a-z][a-z0-9-]{0,31}$/.test(tool)?tool:'process'});
 durableJson(installationLeasePath(current.root),record);
 return(terminationConfirmed=false)=>{
  if(!(process.platform==='win32'&&terminationConfirmed)&&!processGroupExited(identity.pid))recoveryFailure('包管理器进程组尚未退出，保留登记。');
  const latest=ownedInstallationLease(current.root,current.owner);
  latest.processes=latest.processes.filter(item=>item.identity.pid!==identity.pid||item.identity.started!==identity.started||item.identity.commandHash!==identity.commandHash);
  durableJson(installationLeasePath(current.root),latest);
 };
}
export function releaseInstallationLease(root:string,owner:string):void {
 const file=installationLeasePath(root);if(!existsSync(file))return;
 const record=ownedInstallationLease(root,owner);if(record.processes.length||record.unconfirmedProcess)return;
 if(record.journal){
  try{const journal=readRecoveryJson(path.join(root,record.journal)) as {state?:string;leaseOwner?:string};
   if(journal.leaseOwner!==owner||!['committed','rolled-back'].includes(String(journal.state)))return;
  }catch{return;}
 }
 assertNoLinks(file);unlinkSync(file);
}
export function acquireInstallationLease(directory:string):InstallationLease {
 const root=realpathSync(directory),file=installationLeasePath(root),owner=randomBytes(16).toString('hex');
 try{assertNoLinks(file);if(recoveryInProgress(root))installationBusy();mkdirSync(path.dirname(file),{recursive:true});
  const workspace=lstatSync(root),identity=readProcessIdentity(process.pid);
  const record:InstallationLeaseRecord={type:'qian-installation-lease',version:2,id:owner,pid:process.pid,owner:identity,workspace:{device:workspace.dev,inode:workspace.ino},processes:[]};
  durableJson(file,record,true);
 }catch(error){if((error as NodeJS.ErrnoException).code==='EEXIST')installationBusy();if(error instanceof MarketplaceFailure)throw error;recoveryFailure('无法安全创建安装锁。','.qian/.install.lock');}
 return{owner,get hasPendingProcess(){try{const record=ownedInstallationLease(root,owner);return record.processes.length>0||!!record.unconfirmedProcess;}catch{return existsSync(file);}},
  run(operation){return runInInstallationContext(root,owner,operation);},release(){releaseInstallationLease(root,owner);},
  preserveProcess(error){if(error.terminationConfirmed)return;const record=ownedInstallationLease(root,owner);
   // 正常入口在启动前已登记；无法证明归属的旧调用拒绝自动恢复。
   if(!record.processes.some(item=>item.group===error.pid))record.unconfirmedProcess=true;
   durableJson(file,record);
  }};
}
