import {readFileSync} from 'node:fs';
import path from 'node:path';
import {randomBytes} from 'node:crypto';
import {parseSurface,validateSurfaceSchema,type JsonValue,type SurfaceDiagnostic,type ErrorDetail} from '@qian/surfaces';

type RegistryError=Omit<ErrorDetail,'detail'|'pointer'|'errors'>&{detail?:string;pointer?:string;instance?:string;allowed?:JsonValue[];errors?:{code:string;message:string;pointer:string;allowed?:JsonValue[]}[]};
type RegistryDiagnostic={file:string;line:number;severity:'error';error:RegistryError};
const codePattern=/^[A-Z][A-Z0-9]*(_[A-Z0-9]+)*$/;
const pointerPattern=/^[^#\s]*#(\/([^~/]|~[01])*)*$/;
const record=(value:unknown):value is Record<string,unknown>=>value!==null&&typeof value==='object'&&!Array.isArray(value);
const nonempty=(value:unknown):value is string=>typeof value==='string'&&value.length>0;
const validCode=(value:unknown)=>nonempty(value)&&value.length<=64&&codePattern.test(value);
const validPointer=(value:unknown)=>typeof value==='string'&&value.length<=1024&&pointerPattern.test(value);
const allowed=(value:unknown)=>value===undefined||Array.isArray(value)&&value.length>0&&value.length<=100;
/** 只转发冻结 ErrorDetail 的标准字段；畸形响应不能冒充服务端业务错误。 */
function remoteError(value:unknown,status:number):value is RegistryError {
 if(!record(value)||Object.keys(value).some(key=>!['type','title','status','detail','instance','code','message','trace_id','pointer','allowed','errors'].includes(key))||
  !validCode(value.code)||value.status!==status||!nonempty(value.message)||!nonempty(value.title)||
  typeof value.trace_id!=='string'||!/^[0-9a-f]{32}$/.test(value.trace_id)||
  value.type!=='https://qian.dev/problems/'+([409,422].includes(status)?'business':'protocol')+'/'+value.code||
  value.detail!==undefined&&value.detail!==value.message||value.instance!==undefined&&!nonempty(value.instance)||
  value.pointer!==undefined&&!validPointer(value.pointer)||!allowed(value.allowed))return false;
 if(value.errors!==undefined){
  if(!Array.isArray(value.errors)||!value.errors.length||value.errors.length>100||!validPointer(value.pointer))return false;
  if(value.errors.some(item=>!record(item)||Object.keys(item).some(key=>!['code','message','pointer','allowed'].includes(key))||
   !validCode(item.code)||!nonempty(item.message)||!validPointer(item.pointer)||!allowed(item.allowed)))return false;
 }
 return true;
}

export class TenantScopeFailure extends Error {
 constructor(readonly diagnostics:readonly (SurfaceDiagnostic|RegistryDiagnostic)[]){super('租户 scope 操作失败。');}
}
const fail=(message:string,code='INPUT_INVALID',status=400):never=>{
 throw new TenantScopeFailure([{file:'registry',line:1,severity:'error',error:{type:'https://qian.dev/problems/'+([409,422].includes(status)?'business':'protocol')+'/'+code,title:'租户 scope 操作失败',status,code,message,detail:message,trace_id:randomBytes(16).toString('hex'),
  ...(code==='INPUT_INVALID'?{pointer:'registry#',errors:[{code:'SURFACE_SCHEMA_INVALID',message,pointer:'registry#'}]}:{})}}]);
};
type Patch={id:string;[key:string]:JsonValue};
interface ScopeResult {scope:string;entryId:string;revision:number;state:string;reasonCode:string|null}
export interface TenantScopeOptions {workingDirectory:string;scope:string;url?:string;entry?:string;revision?:number;patches:readonly string[];dryRun:boolean}

/** S1 运营入口：凭据仅取环境变量，安装包/开通须由平台完成；不跟随重定向，不在 409 后读取新修订重试。 */
export async function changeTenantScope(command:'add'|'remove',references:readonly string[],options:TenantScopeOptions){
 const tenant=options.scope.slice('tenant:'.length);
 if(!/^tenant:[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/.test(options.scope))fail('--scope 只接受 tenant:<小写规范UUID>。');
 if(options.revision!==undefined&&(!Number.isSafeInteger(options.revision)||options.revision<0||options.revision>2147483647))fail('--revision 必须是非负整数。');
 const token=process.env.QIAN_ACCESS_TOKEN;if(!token?.trim())fail('运营 CLI 需要 QIAN_ACCESS_TOKEN 与平台连接。','UNAUTHENTICATED',401);
 let base:URL;
 try{
  base=new URL(options.url??process.env.QIAN_PLATFORM_URL??'');
  if(base.username||base.password||base.search||base.hash||!['https:','http:'].includes(base.protocol)||
   base.protocol==='http:'&&!['127.0.0.1','[::1]','localhost'].includes(base.hostname))throw new Error();
 }catch{fail('平台地址须为无凭据、查询和片段的 HTTPS URL（--url 或 QIAN_PLATFORM_URL）；HTTP 仅限本机回环。');}
 const endpoint=(suffix:string)=>new URL(base!.toString().replace(/\/$/,'')+suffix);
 async function request(suffix:string,body?:unknown):Promise<unknown>{
  let response:Response;
  try{response=await fetch(endpoint(suffix),{method:body===undefined?'GET':'PUT',headers:{Authorization:'Bearer '+token!,...(body===undefined?{}:{'Content-Type':'application/json'})},
   ...(body===undefined?{}:{body:JSON.stringify(body)}),redirect:'error',cache:'no-store',signal:AbortSignal.timeout(15000)});}
  catch{fail('无法连接平台；操作结果可能未知，请检查 scope 状态与修订后再决定是否重试。','UNAVAILABLE',503);}
  const reader=response!.body?.getReader();if(!reader)fail('平台响应为空。','UNAVAILABLE',503);
  const chunks:Uint8Array[]=[];let size=0;
  try{while(true){const{done,value}=await reader!.read();if(done)break;size+=value.length;if(size>1048576)fail('平台响应超过大小限制。','UNAVAILABLE',503);chunks.push(value);}}
  catch(error){if(error instanceof TenantScopeFailure)throw error;fail('读取平台响应失败；请核对远端状态和修订。','UNAVAILABLE',503);}
  finally{try{await reader!.cancel();}catch{}}
  let value:unknown;try{value=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(Buffer.concat(chunks)));}catch{fail('平台响应不符合 JSON 契约。','UNAVAILABLE',503);}
  if(!response!.ok){
   if(remoteError(value,response!.status))throw new TenantScopeFailure([{file:'registry',line:1,severity:'error',error:value}]);
   fail('平台错误响应不符合 ErrorDetail 契约。','UNAVAILABLE',503);
  }
  return value;
 }
 let patches:Patch[]=[];
 for(const file of options.patches){
  if(command!=='add')fail('remove 不接受 --patch；使用条目 ID。');
  const parsed=parseSurface(file,readFileSync(path.resolve(options.workingDirectory,file),'utf8'));
  if(!parsed.syntax||parsed.diagnostics.some(item=>item.severity==='error'))throw new TenantScopeFailure(parsed.diagnostics);
  const data=Array.isArray(parsed.syntax!.data)?parsed.syntax!.data:[parsed.syntax!.data];
  const diagnostics=validateSurfaceSchema('patch',file,data);
  if(diagnostics.some(item=>item.severity==='error'))throw new TenantScopeFailure(diagnostics);
  patches.push(...data as Patch[]);
 }
 if(patches.length&&references.length)fail('--patch 与组件引用不能混用。');
 if(!patches.length)patches=references.map((reference):Patch=>{
  if(command==='remove')return{id:reference,disabled:true};
  const match=/^([a-z0-9]+(?:[.-][a-z0-9]+)*)@([0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?)$/.exec(reference);
  if(!match)fail('租户 add 需平台已安装并开通的精确组件版本（component@version）。');
  return{id:options.entry??match![1]!,plugin:reference,config:{}};
 });
 if(!patches.length)fail('至少指定一个组件、条目或 --patch 文件。');
 // 单条 HTTP 契约没有批量事务；禁止先成功一部分再遗留半成品。
 if(patches.length!==1)fail('S1 租户 scope 每次只操作一个条目；不支持远端批量事务。');
 if((options.entry!==undefined||options.revision!==undefined)&&patches.length!==1)fail('--entry 与 --revision 只用于单条目操作。');
 if(options.entry&&patches[0]!.id!==options.entry)fail('--entry 必须与 patch 条目一致。');
 if(new Set(patches.map(item=>item.id)).size!==patches.length)fail('条目 ID 不能重复。');
 const diagnostics=validateSurfaceSchema('patch','registry',patches);
 if(diagnostics.some(item=>item.severity==='error'))throw new TenantScopeFailure(diagnostics);
 if(patches.some(item=>item.scope!==undefined&&item.scope!==options.scope))fail('patch.scope 必须与 --scope 一致。');
 const view=await request('/api/registry/scopes/'+encodeURIComponent(options.scope)+'/entries') as {items?:{entryId:string;revision:number|null}[]};
 if(!view||!Array.isArray(view.items)||view.items.some(item=>!item||typeof item.entryId!=='string'||item.revision!==null&&(!Number.isSafeInteger(item.revision)||item.revision<1)))fail('平台 scope 视图不符合契约。','UNAVAILABLE',503);
 const revisions=new Map(view.items!.map(item=>[item.entryId,item.revision??0]));
 if(command==='remove'&&options.revision===undefined&&!revisions.has(patches[0]!.id))return{scope:options.scope,entries:[{entryId:patches[0]!.id,state:'Unchanged'}]};
 const ordered=command==='remove'?[...patches].reverse():patches;
 const plan=ordered.map(patch=>({patch,revision:options.revision??revisions.get(patch.id)??0}));
 if(options.dryRun)return{scope:options.scope,dryRun:true,entries:plan.map(item=>({entryId:item.patch.id,revision:item.revision,operation:command}))};
 const results:ScopeResult[]=[];
 for(const item of plan){
  const result=await request('/api/registry/tenants/'+tenant+'/patches/'+encodeURIComponent(options.scope)+'/'+encodeURIComponent(item.patch.id),item) as ScopeResult;
  if(!result||result.scope!==options.scope||result.entryId!==item.patch.id||!Number.isSafeInteger(result.revision)||result.revision<1||!['Ready','Unmounted','Failed'].includes(result.state))fail('平台写入结果不符合契约；请核对状态后重试。','UNAVAILABLE',503);
  if(result.state==='Failed')fail('条目装配失败并已撤回；请查看 Lifecycle 诊断。','PRECONDITION_FAILED',412);
  results.push(result);
 }
 return{scope:options.scope,entries:results};
}
