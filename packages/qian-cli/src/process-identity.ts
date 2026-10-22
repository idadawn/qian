import {execFileSync} from 'node:child_process';
import {readFileSync} from 'node:fs';
import {createHash} from 'node:crypto';

export interface ProcessIdentity {pid:number;started:string;commandHash:string}
interface ProcessRow {pid:number;group:number;state:string;started:string}
const hash=(text:string):string=>createHash('sha256').update(text).digest('hex');
const localOpaqueIdentity=hash(String(process.pid)+':'+process.hrtime.bigint());

/** 只读进程表；查询失败不能当作进程已停止。命令仅散列，不将参数或凭据写入锁。 */
function processTable():ProcessRow[] {
 if(process.platform!=='linux'&&process.platform!=='darwin')throw new Error('当前平台不能核验进程身份。');
 const output=execFileSync('ps',['-ax','-o','pid=,pgid=,stat=,lstart='],
  {encoding:'utf8',timeout:5000,maxBuffer:8*1024*1024,env:{...process.env,LC_ALL:'C',TZ:'UTC'}});
 const rows:ProcessRow[]=[];
 for(const line of output.split('\n')){
  if(!line.trim())continue;
  const match=/^\s*(\d+)\s+(\d+)\s+(\S+)\s+(\S+\s+\S+\s+\d+\s+\d+:\d+:\d+\s+\d+)\s*$/.exec(line);
  if(!match)throw new Error('进程表格式无效。');
  rows.push({pid:Number(match[1]),group:Number(match[2]),state:match[3],started:match[4].replace(/\s+/g,' ')});
 }
 if(!rows.some(row=>row.pid===process.pid))throw new Error('进程表不完整。');
 return rows;
}
function identity(row:ProcessRow):ProcessIdentity {
 let started=row.started;
 if(process.platform==='linux'){
  // /proc 的启动 tick 与 boot_id 区分同一 PID 的多次使用及重启。
  const stat=readFileSync('/proc/'+row.pid+'/stat','utf8'),fields=stat.slice(stat.lastIndexOf(')')+2).split(' ');
  started=readFileSync('/proc/sys/kernel/random/boot_id','utf8').trim()+':'+fields[19];
 }else{
  const boot=execFileSync('sysctl',['-n','kern.boottime'],{encoding:'utf8',timeout:5000});
  started=hash(boot.trim())+':'+started;
 }
 // 全表不读取命令参数；只对被核验的 PID 计算指纹。
 const command=execFileSync('ps',['-ww','-p',String(row.pid),'-o','command='],{encoding:'utf8',timeout:5000,maxBuffer:1024*1024});
 return{pid:row.pid,started,commandHash:hash(command.trim())};
}
export function validProcessIdentity(value:unknown):value is ProcessIdentity {
 const candidate=value as ProcessIdentity|undefined;
 return!!candidate&&Number.isSafeInteger(candidate.pid)&&candidate.pid>0&&typeof candidate.started==='string'&&candidate.started.length>0&&candidate.started.length<200&&
  typeof candidate.commandHash==='string'&&/^[a-f0-9]{64}$/.test(candidate.commandHash);
}
export function readProcessIdentity(pid:number):ProcessIdentity {
 // 不支持精确只读核验的平台仍允许当前操作；崩溃后明确拒绝自动恢复。
 if(process.platform!=='linux'&&process.platform!=='darwin')return{pid,started:'unsupported:'+localOpaqueIdentity,commandHash:hash(String(pid))};
 const row=processTable().find(item=>item.pid===pid);
 if(!row||row.state.startsWith('Z'))throw new Error('进程已经退出。');
 return identity(row);
}
export function sameProcess(left:ProcessIdentity,right:ProcessIdentity):boolean {
 return left.pid===right.pid&&left.started===right.started&&left.commandHash===right.commandHash;
}
export function processIdentityState(expected:ProcessIdentity):'active'|'exited'|'reused'|'unknown' {
 try{
  const row=processTable().find(item=>item.pid===expected.pid);
  if(!row||row.state.startsWith('Z'))return'exited';
  return sameProcess(expected,identity(row))?'active':'reused';
 }catch{return'unknown';}
}
/** 即使 PID 被重用也不将活跃进程认作退出；恢复从不向任何进程发送信号。 */
export function processGroupExited(group:number):boolean {
 if(!Number.isSafeInteger(group)||group<=0)return false;
 try{return!processTable().some(row=>row.group===group&&!row.state.startsWith('Z'));}catch{return false;}
}
