import {constants,cpSync,existsSync,lstatSync,mkdirSync,openSync,closeSync,readFileSync,writeFileSync,renameSync,rmSync,unlinkSync,chmodSync,readdirSync,realpathSync} from 'node:fs';
import path from 'node:path';
import {randomBytes} from 'node:crypto';
import {assertNoLinks,within,sha256} from './marketplace-files.js';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import type {PackageMutationTarget} from './package-manager.js';
import {PackageCommandFailure,packageProcessGroupExited} from './package-process.js';

type State='preparing'|'prepared'|'committed'|'rolled-back'|'rollback-failed';
interface Backup {path:string;kind:'file'|'directory';existed:boolean;mode?:number;backup:string;ready:boolean;sha256?:string}
interface Journal {type:'qian-workspace-transaction';version:1;id:string;operation:string;state:State;entries:Backup[];pendingProcess?:{pid:number|undefined;tool:string}}
function failure(message:string):never {throw new MarketplaceFailure([marketDiagnostic('.qian/journal','',message)]);}
function atomic(file:string,bytes:Buffer):void {
 assertNoLinks(file);const temporary=file+'.'+randomBytes(6).toString('hex')+'.tmp';
 try{writeFileSync(temporary,bytes,{flag:'wx',mode:0o600});renameSync(temporary,file);}finally{if(existsSync(temporary))unlinkSync(temporary);}
}
function remove(file:string):void {
 if(!existsSync(file)){try{if(lstatSync(file).isSymbolicLink())unlinkSync(file);}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}return;}
 const info=lstatSync(file);if(info.isSymbolicLink()||info.isFile())unlinkSync(file);else if(info.isDirectory())rmSync(file,{recursive:true});else failure('事务目标出现特殊文件，拒绝覆盖。');
}

/** 持有工作区安装锁，保存文件及生成目录的原貌，失败时逆序还原。 */
export class WorkspaceTransaction {
 private constructor(readonly root:string,readonly journalFile:string,private readonly record:Journal,private readonly lease:string,private readonly leaseOwner:string){}
 get hasPendingProcess():boolean {return this.record.pendingProcess!==undefined;}

 static begin(directory:string,targets:readonly PackageMutationTarget[],operation:string,existingLeaseOwner?:string):WorkspaceTransaction {
  const root=realpathSync(directory),id=Date.now()+'-'+randomBytes(8).toString('hex'),entries:Backup[]=[],paths:string[]=[];
  let bytes=0;
  for(const target of targets){
   const relative=path.relative(root,path.resolve(target.path)).split(path.sep).join('/'),file=within(root,relative);assertNoLinks(file);
   if(relative==='.qian/.install.lock'||relative.startsWith('.qian/journal/')||relative==='.qian/journal')failure('安装状态目录不能作为事务目标。');
   if(paths.includes(file))continue;
   if(paths.some(existing=>file.startsWith(existing+path.sep)||existing.startsWith(file+path.sep)))failure('事务目标不能相互包含。');
   paths.push(file);const info=existsSync(file)?lstatSync(file):undefined;
   if(info&&(target.kind==='file'?!info.isFile():!info.isDirectory()))failure('事务目标类型与变更计划不一致。');
   if(target.kind==='file'&&info){bytes+=info.size;if(bytes>64*1024*1024)failure('事务文件备份超过 64 MiB。');}
   entries.push({path:relative,kind:target.kind,existed:!!info,mode:info?.mode,backup:'.qian/journal/'+id+'.backup/'+entries.length,ready:false});
  }
  const lease=path.join(root,'.qian/.install.lock'),journalFile=path.join(root,'.qian/journal',id+'.json');
  assertNoLinks(lease);assertNoLinks(journalFile);mkdirSync(path.dirname(lease),{recursive:true});
  const leaseOwner=existingLeaseOwner??id;
  try{
   if(existingLeaseOwner){const owner=JSON.parse(readFileSync(lease,'utf8'));if(owner.pid!==process.pid||owner.id!==existingLeaseOwner)failure('不能接管不属于当前操作的安装锁。');
    atomic(lease,Buffer.from(JSON.stringify({...owner,journal:path.relative(root,journalFile)})));}
   else{const handle=openSync(lease,'wx',0o600);try{writeFileSync(handle,JSON.stringify({pid:process.pid,id,journal:path.relative(root,journalFile)}));}finally{closeSync(handle);}}
  }
  catch{failure('已有安装事务或残留安装锁；检查日志后再继续。');}
  const transaction=new WorkspaceTransaction(root,journalFile,{type:'qian-workspace-transaction',version:1,id,operation,state:'preparing',entries},lease,leaseOwner);
  try{
   mkdirSync(path.dirname(journalFile),{recursive:true,mode:0o700});
   const ignore=path.join(path.dirname(journalFile),'.gitignore');assertNoLinks(ignore);
   if(!existsSync(ignore))writeFileSync(ignore,'*\n',{flag:'wx',mode:0o600});
   mkdirSync(path.join(root,'.qian/journal',id+'.backup'),{mode:0o700});transaction.save('preparing');
   for(const entry of entries){
    if(entry.existed){
     const source=within(root,entry.path),backup=within(root,entry.backup);assertNoLinks(source);assertNoLinks(backup);
     if(entry.kind==='file'){
      const content=readFileSync(source);entry.sha256=sha256(content);writeFileSync(backup,content,{flag:'wx',mode:0o600});
     }else cpSync(source,backup,{recursive:true,dereference:false,verbatimSymlinks:true,preserveTimestamps:true,mode:constants.COPYFILE_FICLONE});
    }
    entry.ready=true;transaction.save('preparing');
   }
   transaction.save('prepared');return transaction;
  }catch(error){transaction.discardPreparation();throw error;}
 }

 private save(state:State):void {
  const next={...this.record,state};atomic(this.journalFile,Buffer.from(JSON.stringify(next,null,2)+'\n'));this.record.state=state;
 }
 private release():void {
  assertNoLinks(this.lease);
  if(existsSync(this.lease)){
   let data:{id?:string};try{data=JSON.parse(readFileSync(this.lease,'utf8'));}catch{return;}
   if(data.id===this.leaseOwner)unlinkSync(this.lease);
  }
 }
 private discardPreparation():void {
  // 准备阶段只复制，没有修改任何目标；部分备份不能用于覆盖原文件。
  try{if(existsSync(this.journalFile))this.save('rolled-back');}
  finally{this.release();this.cleanup();}
 }
 private cleanup():void {
  const backup=path.join(this.root,'.qian/journal',this.record.id+'.backup');
  try{assertNoLinks(backup);rmSync(backup,{recursive:true,force:true});}catch{ /* 已完成事务的备份可在下次整理时清理。 */ }
 }

 commit():void {
  if(this.record.state!=='prepared')failure('只有已准备的事务可以提交。');
  this.save('committed');this.release();this.cleanup();this.prune();
 }
 /** 只有写入与最终校验都成功才提交；调用方不必在每个步骤重复拼接回滚。 */
 async run<T>(operation:()=>Promise<T>):Promise<T> {
  if(this.record.state!=='prepared')failure('事务未准备或已结束。');
  try{const value=await operation();this.commit();return value;}
  catch(error){
   if(error instanceof PackageCommandFailure&&!error.terminationConfirmed){
    this.record.pendingProcess={pid:error.pid,tool:error.tool};this.save('rollback-failed');throw error;
   }
   if(this.record.state==='prepared'||this.record.state==='rollback-failed')await this.rollback();throw error;
  }
 }
 async rollback():Promise<void> {
  if(this.record.state==='rolled-back')return;
  if(this.record.state!=='prepared'&&this.record.state!=='rollback-failed')failure('该事务不处于可回滚状态。');
  if(this.record.pendingProcess){
   const pid=this.record.pendingProcess.pid;
   if(pid===undefined||!await packageProcessGroupExited(pid))failure('尚未确认包管理器进程组退出，保留锁和备份，不执行回滚。');
   delete this.record.pendingProcess;
  }
  let failed=false;
  for(const entry of [...this.record.entries].reverse()){
   try{
    if(!entry.ready)failure('备份未完成，拒绝覆盖原目标。');
    const file=within(this.root,entry.path),backup=within(this.root,entry.backup);assertNoLinks(path.dirname(file));assertNoLinks(backup);
    if(entry.existed){
     if(!existsSync(backup))failure('事务备份缺失。');
     if(entry.kind==='file'){
      const content=readFileSync(backup);if(sha256(content)!==entry.sha256)failure('事务备份校验失败。');
      remove(file);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,content,{flag:'wx',mode:entry.mode});
      if(entry.mode!==undefined)chmodSync(file,entry.mode);
     }else{
      remove(file);mkdirSync(path.dirname(file),{recursive:true});cpSync(backup,file,{recursive:true,dereference:false,verbatimSymlinks:true,preserveTimestamps:true,mode:constants.COPYFILE_FICLONE});
     }
    }else remove(file);
   }catch{failed=true;}
  }
  this.save(failed?'rollback-failed':'rolled-back');
  if(failed)failure('回滚未完成；安装锁、日志和备份已保留，停止后续写入。');
  this.release();this.cleanup();this.prune();
 }

 private prune():void {
  const folder=path.dirname(this.journalFile),completed:string[]=[];
  for(const name of readdirSync(folder).filter(value=>/^\d+-[0-9a-f]+\.json$/.test(value)).sort()){
   const file=path.join(folder,name);
   try{assertNoLinks(file);const value:Journal=JSON.parse(readFileSync(file,'utf8'));
    if(value.type==='qian-workspace-transaction'&&(value.state==='committed'||value.state==='rolled-back'))completed.push(file);
   }catch{ /* 未知或损坏日志保留给恢复流程，不猜测后删除。 */ }
  }
  for(const file of completed.slice(0,-20)){
   const backup=file.slice(0,-5)+'.backup';
   try{assertNoLinks(backup);rmSync(backup,{recursive:true,force:true});unlinkSync(file);}catch{ /* 历史清理失败不回滚已提交的变更。 */ }
  }
 }
}
