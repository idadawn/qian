import {spawn,execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {setTimeout as delay} from 'node:timers/promises';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';

const execute=promisify(execFile);
export class PackageCommandFailure extends MarketplaceFailure {
 constructor(readonly tool:string,readonly pid:number|undefined,readonly terminationConfirmed:boolean){
  const label=/^[a-z][a-z0-9-]{0,31}$/.test(tool)?tool:'process';
  const message=terminationConfirmed?'包管理器未完成操作；检查工具、包源与宿主配置。':'包管理器进程尚未确认停止；事务锁、日志和备份必须保留。';
  const diagnostic=marketDiagnostic('package-manager','',message,'UNAVAILABLE');
  diagnostic.error={...diagnostic.error,type:'https://qian.dev/problems/protocol/UNAVAILABLE',code:'UNAVAILABLE',status:503,
   title:'服务暂不可用',message,detail:message,errors:[{code:'UNAVAILABLE',message:label+'：'+message,pointer:'package-manager#'}]};
  super([diagnostic]);
 }
}

/** 只读核验本次创建的 POSIX 进程组；僵尸已不能写文件，查询失败不视为退出。 */
export async function packageProcessGroupExited(pid:number):Promise<boolean> {
 if(process.platform==='win32'||!Number.isSafeInteger(pid)||pid<=0)return false;
 try{
  const {stdout}=await execute('ps',['-axo','pid=,pgid=,stat='],{encoding:'utf8',timeout:5000,maxBuffer:4*1024*1024});
  const records=stdout.split('\n').map(line=>line.trim().split(/\s+/))
   .filter(fields=>fields.length>=3&&/^\d+$/.test(fields[0])&&/^\d+$/.test(fields[1]));
  if(!records.some(fields=>Number(fields[0])===process.pid))return false;
  return !records.some(fields=>Number(fields[1])===pid&&!fields[2].startsWith('Z'));
 }catch{return false;}
}

async function stopGroup(pid:number):Promise<boolean> {
 for(let attempt=0;attempt<40;attempt++){
  if(await packageProcessGroupExited(pid))return true;
  try{process.kill(-pid,'SIGKILL');}catch(error){if((error as NodeJS.ErrnoException).code!=='ESRCH')return false;}
  await delay(50);
 }
 return await packageProcessGroupExited(pid);
}

/** 不经 shell，不回显原始输出；确认写文件的进程已结束后才允许上层回滚。 */
export async function runPackageCommand(executable:string,args:readonly string[],root:string,label:string,timeout=120_000):Promise<string> {
 if(!Number.isFinite(timeout)||timeout<=0)throw new PackageCommandFailure(label,undefined,true);
 return await new Promise<string>((resolve,reject)=>{
  const child=spawn(executable,[...args],{cwd:root,shell:false,detached:process.platform!=='win32',windowsHide:true,
   stdio:['ignore','pipe','pipe'],env:{...process.env,CI:'true',DOTNET_NOLOGO:'1',DOTNET_CLI_TELEMETRY_OPTOUT:'1',
    MSBUILDDISABLENODEREUSE:'1',DOTNET_CLI_USE_MSBUILD_SERVER:'0',UseSharedCompilation:'false',NPM_CONFIG_UPDATE_NOTIFIER:'false'}});
  let stdout='',size=0,failed=false,settled=false,stopping:Promise<boolean>|undefined;
  const stop=():void=>{
   if(stopping)return;failed=true;
   stopping=!child.pid?Promise.resolve(true):process.platform!=='win32'?stopGroup(child.pid):
    execute('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{timeout:5000,windowsHide:true}).then(()=>true,()=>false);
   void stopping.then(confirmed=>{if(!confirmed)void finish(null);});
  };
  const timer=setTimeout(stop,timeout);timer.unref();
  const finish=async(code:number|null):Promise<void>=>{
   if(settled)return;settled=true;clearTimeout(timer);
   let confirmed=true;
   if(stopping)confirmed=await stopping;
   else if(child.pid&&process.platform!=='win32')confirmed=await stopGroup(child.pid);
   else if(child.pid&&code!==0)confirmed=false;
   if(!confirmed){child.unref();child.stdout.destroy();child.stderr.destroy();}
   if(failed||code!==0||!confirmed)reject(new PackageCommandFailure(label,child.pid,confirmed));else resolve(stdout);
  };
  child.stdout.setEncoding('utf8');
  child.stdout.on('data',(text:string)=>{size+=Buffer.byteLength(text);if(size>16*1024*1024)stop();else if(!failed)stdout+=text;});
  child.stderr.on('data',(bytes:Buffer)=>{size+=bytes.length;if(size>16*1024*1024)stop();});
  child.once('error',()=>{failed=true;if(child.pid)stop();else void finish(null);});
  child.once('close',code=>void finish(code));
 });
}
