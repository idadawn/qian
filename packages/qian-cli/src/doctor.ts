import {randomBytes} from 'node:crypto';
import path from 'node:path';
import {validateSurfaceSchema, type SurfaceDiagnostic, type JsonValue, type SurfaceRuntimeReport} from '@qian/surfaces';
import {validateOfflineComponent} from './component-validation.js';
import {loadProfile, type ProfileOptions} from './profiles.js';
import {readInstalledState,verifyInstalledComponent,installObject,readInstallFile} from './installed-components.js';
import {within} from './marketplace-files.js';
import {readHostEngineVersions,verifyComponentEngines} from './engine-compatibility.js';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import type {PackageWorkspace} from './package-manager.js';

export class DoctorFailure extends Error {
 constructor(readonly diagnostics:SurfaceDiagnostic[]){super('诊断读取失败。');}
}
const fail=(message:string):never=>{
 const general='输入不符合该能力的输入 Schema，逐项错误见 errors。';
 throw new DoctorFailure([{file:'runtime',line:1,severity:'error',error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message:general,detail:general,trace_id:randomBytes(16).toString('hex'),pointer:'runtime#',errors:[{code:'RUNTIME_DIAGNOSTICS_UNAVAILABLE',message,pointer:'runtime#'}]}}]);
};
/** 只发起读取请求，不跟随重定向，不输出令牌、远端错误正文或本地配置。 */
export async function readRuntimeDiagnostics(address:string):Promise<SurfaceRuntimeReport>{
 let url:URL;
 try{
  url=new URL(address);
  if(url.username||url.password||url.search||url.hash||!['https:','http:'].includes(url.protocol)||
   (url.protocol==='http:'&&!['127.0.0.1','[::1]','localhost'].includes(url.hostname)))throw new Error();
  url.pathname=url.pathname.replace(/\/$/,'')+'/api/lifecycle/diagnostics/surfaces';
 }catch{fail('运行态地址必须为无凭据和查询参数的 HTTPS 地址；HTTP 仅限本机回环。');}
 let body:JsonValue;
 try{
  const token=process.env.QIAN_ACCESS_TOKEN;
  const response=await fetch(url!,{headers:token?{Authorization:'Bearer '+token}:{},redirect:'error',signal:AbortSignal.timeout(5000),cache:'no-store'});
  if(!response.ok||!response.body)fail('运行态诊断不可用或调用者未获授权；检查连接及 QIAN_ACCESS_TOKEN。');
  const reader=response.body!.getReader();const chunks:Uint8Array[]=[];let size=0;
  try{while(true){const{done,value}=await reader.read();if(done)break;size+=value.length;if(size>1048576)fail('运行态诊断超出大小限制。');chunks.push(value);}}
  finally{await reader.cancel();}
  body=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(Buffer.concat(chunks))) as JsonValue;
 }catch(error){if(error instanceof DoctorFailure)throw error;fail('无法读取运行态诊断；检查地址、授权及宿主是否正在运行。');}
 const diagnostics=validateSurfaceSchema('surface-diagnostics','runtime',body!);
 if(diagnostics.some(item=>item.severity==='error'))fail('运行态响应不符合表面诊断协议，不能据此判断健康。');
 const result=body! as unknown as SurfaceRuntimeReport;
 if(result.entries.some(item=>item.scope!=='global'&&item.scope!==result.scope)||new Set(result.entries.map(item=>item.plugin)).size!==result.entries.length)
  fail('运行态报告存在 scope 或重复条目冲突。');
 return result;
}

/** 当前覆盖静态 profile 装配和可选的真实表面监视状态；不声称完整平台健康。 */
export async function doctor(options:ProfileOptions,address?:string){
 const profile=loadProfile(options);
 const runtime=address?await readRuntimeDiagnostics(address):null;
 const actions=runtime?.entries.flatMap(item=>[
  ...(item.restartRequired?[{entryId:item.entryId,plugin:item.plugin,scope:item.scope,code:'ROLLING_RESTART_REQUIRED',message:'代码已变更，请执行滚动重启；当前代码实例继续运行。',files:item.changedCodeFiles}]:[]),
  ...(item.invalidSurface?[{entryId:item.entryId,plugin:item.plugin,scope:item.scope,code:'SURFACE_UPDATE_REJECTED',message:'修正无效表面后重试；当前继续使用上次有效注册。',files:[]}]:[])
 ])??[];
 return {checks:runtime?['profile','live-surfaces']:['profile'],profile:options.profile??'platform',isValid:profile.isValid,runtimeVerified:runtime!==null,runtime,diagnostics:profile.diagnostics,actions,healthy:profile.isValid&&actions.length===0};
}

/** 安装收尾与显式组件诊断共用；只检查受影响安装记录和表面，不执行组件或解析 secretRef。 */
export function doctorInstalledComponents(root:string,ids:readonly string[],profile:string,workspace?:PackageWorkspace){
 const plugins=readInstalledState(root).plugins,evidence=readHostEngineVersions(root,workspace),diagnostics:SurfaceDiagnostic[]=[];
 for(const id of [...new Set(ids)]){
  const installed=plugins.find(item=>item.id===id);
  if(!installed){diagnostics.push(marketDiagnostic('.qian/lock.json','/plugins','受影响组件未写入安装锁。','SURFACE_REFERENCE_UNKNOWN'));continue;}
  try{
   verifyInstalledComponent(root,installed);
   const files=(installed.files as JsonValue[]).map(installObject);
   for(const item of files){
    if(typeof item.path!=='string'||item.path!=='.qian-plugin/plugin.json'&&!item.path.endsWith('/.qian-plugin/plugin.json'))continue;
    const relative='.qian/plugins/'+id+'/'+item.path,file=within(root,relative),folder=path.dirname(path.dirname(file));
    const validated=validateOfflineComponent(folder,{profile});diagnostics.push(...validated.diagnostics);
    verifyComponentEngines(installObject(readInstallFile(file)),evidence,relative);
   }
  }catch(error){if(error instanceof MarketplaceFailure)diagnostics.push(...error.diagnostics);else diagnostics.push(marketDiagnostic('.qian/plugins/'+id,'','受影响组件的文件或清单无法安全读取。','SURFACE_PATH_INVALID'));}
 }
 return{checks:['installed-files','component-surfaces','engines'],components:[...new Set(ids)],profile,runtimeVerified:false,healthy:!diagnostics.some(item=>item.severity==='error'),diagnostics,engines:evidence};
}
