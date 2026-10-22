import {spawn,execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {setTimeout as delay} from 'node:timers/promises';
import {fileURLToPath} from 'node:url';
import {randomBytes} from 'node:crypto';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import {processGroupExited,processIdentityState,readProcessIdentity,type ProcessIdentity} from './process-identity.js';
import {registerPackageProcess} from './installation-lease.js';

const execute=promisify(execFile);
/** 包管理器操作的缺省超时（秒）：冷缓存下完整还原依赖闭包，在慢网络上可能需要几分钟。 */
export const DEFAULT_PACKAGE_TIMEOUT_SECONDS=600;
const MAX_PACKAGE_TIMEOUT_SECONDS=86_400;

/** 读取 QIAN_PACKAGE_TIMEOUT（整数秒，1–86400）；未设置或为空取缺省值，其它写法以结构化错误拒绝。 */
export function packageCommandTimeout(environment:NodeJS.ProcessEnv=process.env):number {
 const value=environment.QIAN_PACKAGE_TIMEOUT;
 if(value===undefined||value==='')return DEFAULT_PACKAGE_TIMEOUT_SECONDS*1000;
 if(!/^[1-9][0-9]{0,4}$/.test(value)||Number(value)>MAX_PACKAGE_TIMEOUT_SECONDS)
  throw new MarketplaceFailure([marketDiagnostic('QIAN_PACKAGE_TIMEOUT','','包管理器超时必须是 1 到 86400 之间的整数秒。','SURFACE_SCHEMA_INVALID')]);
 return Number(value)*1000;
}

export class PackageCommandFailure extends MarketplaceFailure {
 constructor(readonly tool:string,readonly pid:number|undefined,readonly terminationConfirmed:boolean,readonly timeoutMilliseconds?:number){
  const label=/^[a-z][a-z0-9-]{0,31}$/.test(tool)?tool:'process';
  // 未确认停止优先：此时锁与备份必须保留，超时提示放在其后。
  const timeoutMessage=timeoutMilliseconds!==undefined?'包管理器未在 '+Math.ceil(timeoutMilliseconds/1000)+' 秒内完成；网络或包源较慢时，可用环境变量 QIAN_PACKAGE_TIMEOUT（整数秒）加长后重试。':'';
  const message=!terminationConfirmed?'包管理器进程尚未确认停止；事务锁、日志和备份必须保留。'+timeoutMessage:
   timeoutMessage||'包管理器未完成操作；检查工具、包源与宿主配置。';
  const diagnostic=marketDiagnostic('package-manager','',message,'SURFACE_PACKAGE_UNAVAILABLE');
  diagnostic.error={...diagnostic.error,type:'https://qian.dev/problems/protocol/UNAVAILABLE',code:'UNAVAILABLE',status:503,
   title:'服务暂不可用',message,detail:message,errors:[{code:'SURFACE_PACKAGE_UNAVAILABLE',message:label+'：'+message,pointer:'package-manager#'}]};
  super([diagnostic]);
 }
}

/** 只读核验本次创建的 POSIX 进程组；僵尸已不能写文件，查询失败不视为退出。 */
export async function packageProcessGroupExited(pid:number):Promise<boolean> {
 return processGroupExited(pid);
}

async function stopGroup(pid:number,identity?:ProcessIdentity):Promise<boolean> {
 for(let attempt=0;attempt<40;attempt++){
  if(await packageProcessGroupExited(pid))return true;
  const state=identity?processIdentityState(identity):'unknown';
  if(state==='reused'||state==='unknown')return false;
  // 首进程退出后只等本次发出的停止信号生效，不向可能重用的 PID 发信号。
  if(state==='active')try{process.kill(-pid,'SIGKILL');}catch(error){if((error as NodeJS.ErrnoException).code!=='ESRCH')return false;}
  await delay(50);
 }
 return await packageProcessGroupExited(pid);
}

/** 不经 shell，不回显原始输出；确认写文件的进程已结束后才允许上层回滚。 */
export async function runPackageCommand(executable:string,args:readonly string[],root:string,label:string,timeout=packageCommandTimeout()):Promise<string> {
 if(!Number.isFinite(timeout)||timeout<=0)throw new MarketplaceFailure([marketDiagnostic('package-manager','','包管理器超时必须为正数。','SURFACE_SCHEMA_INVALID')]);
 return await new Promise<string>((resolve,reject)=>{
  const child=spawn(process.execPath,[fileURLToPath(new URL('./package-launcher.js',import.meta.url)),randomBytes(16).toString('hex')],{cwd:root,shell:false,detached:process.platform!=='win32',windowsHide:true,
   stdio:['ignore','pipe','pipe','ipc'],env:{...process.env,CI:'true',DOTNET_NOLOGO:'1',DOTNET_CLI_TELEMETRY_OPTOUT:'1',
    MSBUILDDISABLENODEREUSE:'1',DOTNET_CLI_USE_MSBUILD_SERVER:'0',UseSharedCompilation:'false',NPM_CONFIG_UPDATE_NOTIFIER:'false'}});
  let stdout='',size=0,failed=false,settled=false,timedOut=false,stopping:Promise<boolean>|undefined,identity:ProcessIdentity|undefined,unregister:((terminationConfirmed?:boolean)=>void)|undefined;
  let registrationError:unknown;
  const stop=():void=>{
   if(stopping)return;failed=true;
   // 屏障还没放行时只需终止这个不会启动包命令的子进程。
   if(child.pid&&!identity)child.kill('SIGKILL');
   stopping=!child.pid?Promise.resolve(true):process.platform!=='win32'?identity?stopGroup(child.pid,identity):Promise.resolve(true):
    execute('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{timeout:5000,windowsHide:true}).then(()=>true,()=>false);
   void stopping.then(confirmed=>{if(!confirmed)void finish(null);});
  };
  const timer=setTimeout(()=>{timedOut=true;stop();},timeout);timer.unref();
  const finish=async(code:number|null):Promise<void>=>{
   if(settled)return;settled=true;clearTimeout(timer);
   let confirmed=true;
   if(stopping)confirmed=await stopping;
   else if(child.pid&&process.platform!=='win32')confirmed=await stopGroup(child.pid,identity);
   else if(child.pid)confirmed=await execute('taskkill.exe',['/PID',String(child.pid),'/T','/F'],{timeout:5000,windowsHide:true}).then(()=>true,()=>false);
   if(!confirmed){child.unref();child.stdout!.destroy();child.stderr!.destroy();if(child.connected)child.disconnect();}
   if(confirmed&&unregister)try{unregister(confirmed);}catch(error){registrationError=error;}
   if(registrationError&&confirmed)reject(registrationError);
   else if(failed||code!==0||!confirmed)reject(new PackageCommandFailure(label,child.pid,confirmed,timedOut?timeout:undefined));else resolve(stdout);
  };
  child.stdout!.setEncoding('utf8');
  child.stdout!.on('data',(text:string)=>{size+=Buffer.byteLength(text);if(size>16*1024*1024)stop();else if(!failed)stdout+=text;});
  child.stderr!.on('data',(bytes:Buffer)=>{size+=bytes.length;if(size>16*1024*1024)stop();});
  child.on('message',(message:unknown)=>{
   const event=message as {type?:string;code?:number|null};
   if(event.type==='ready'&&!identity&&!settled&&!failed){
    try{identity=readProcessIdentity(child.pid!);unregister=registerPackageProcess(identity,label);
     child.send({type:'run',executable,args},error=>{if(error){failed=true;stop();}});
    }catch(error){registrationError=error;stop();}
   }else if(event.type==='result')void finish(typeof event.code==='number'?event.code:null);
  });
  child.once('error',()=>{failed=true;if(child.pid)stop();else void finish(null);});
  child.once('close',code=>void finish(code));
 });
}
