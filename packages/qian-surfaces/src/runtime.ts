import {watch, type FSWatcher, readdirSync, readFileSync, lstatSync} from 'node:fs';
import path from 'node:path';
import {createHash,randomBytes} from 'node:crypto';
import {validateComponent, type JsonValue, type SurfaceDocument, type SurfaceValidationOptions, type SurfaceValidationResult, type SurfaceDiagnostic} from './index.js';

/** 实际运行时上下文的结构端口；不在表面库创建另一套运行时。 */
export interface SurfaceContext {
 readonly entry:{readonly id:string;readonly plugin:string;readonly version:string}; readonly scope:string;
 provide<T>(key:string,value:T):()=>void|Promise<void>;
 provide<T>(key:string,name:string,value:T):()=>void|Promise<void>;
 effect(dispose:()=>void|Promise<void>):void;
}
/** 热重载状态，不包含文件内容。 */
export interface SurfaceReloadStatus {readonly revision:number;readonly restartRequired:boolean;readonly changedCodeFiles:readonly string[];readonly diagnostics:readonly SurfaceDiagnostic[]}
/** 实际监视器对外输出的无正文诊断。 */
export interface SurfaceRuntimeEntry {readonly entryId:string;readonly plugin:string;readonly scope:string;readonly revision:number;readonly restartRequired:boolean;readonly changedCodeFiles:readonly string[];readonly invalidSurface:boolean}
/** 当前 scope 经过运行时过滤后的诊断报告。 */
export interface SurfaceRuntimeReport {readonly protocol:'qian.surface-diagnostics/v1';readonly scope:string;readonly entries:readonly SurfaceRuntimeEntry[]}
/** 拥有运行时注册读取能力的宿主上下文。 */
export interface SurfaceDiagnosticContext {readonly scope:string;list(key:string):readonly {readonly name:string;readonly value:unknown}[]}
/** 宿主管理面应先完成身份验证和授权，再用可信 scope 调用；不读取文件或缓存报告。 */
export function readSurfaceRuntimeDiagnostics(context:SurfaceDiagnosticContext):SurfaceRuntimeReport{
 const entries=[...context.list('SurfaceReloadDiagnostics')].sort((a,b)=>a.name<b.name?-1:a.name>b.name?1:0).map(item=>(item.value as {read():SurfaceRuntimeEntry}).read());
 return freeze({protocol:'qian.surface-diagnostics/v1',scope:context.scope,entries});
}
type Registration={key:string;name:string;document:SurfaceDocument};
const obj=(value:JsonValue|undefined):Record<string,JsonValue>=>value!==null&&typeof value==='object'&&!Array.isArray(value)?value:{};
const stable=(value:unknown):string=>JSON.stringify(value,(_key,item)=>item&&typeof item==='object'&&!Array.isArray(item)?Object.fromEntries(Object.entries(item).sort(([a],[b])=>a<b?-1:a>b?1:0)):item);
const freeze=<T>(value:T):T=>{if(value&&typeof value==='object'){for(const child of Object.values(value))freeze(child);Object.freeze(value);}return value;};
function plan(result:SurfaceValidationResult,ctx?:SurfaceContext):Map<string,Registration>{
 const component=result.component;if(!result.isValid||!component)throw new Error('INPUT_INVALID');
 const manifest=obj(component.manifest);
 if(ctx&&(component.id!==ctx.entry.plugin||manifest.version!==ctx.entry.version||!(manifest.scopes as JsonValue[]).includes(ctx.scope.split(':')[0])))throw new Error('INPUT_INVALID');
 const entries=new Map<string,Registration>();
 const add=(key:string,name:string,document:SurfaceDocument)=>{const id=key+':'+name;if(entries.has(id))throw new Error('INPUT_INVALID');entries.set(id,{key,name,document:freeze(structuredClone(document))});};
 for(const document of component.documents){
  const key=({skill:'Skills',agent:'Agents',command:'Commands',hooks:'Hooks',mcp:'McpServers',tool:'Tools'} as Record<string,string>)[document.kind];if(!key)continue;
  if(document.kind==='mcp')for(const [name,data]of Object.entries(obj(obj(document.data).mcpServers))){const profiles=obj(data).profiles;if(!Array.isArray(profiles)||profiles.includes(component.profile))add(key,component.id+'/'+name,{...document,name,data});}
  else{add(key,component.id+'/'+document.name,document);if(document.kind==='skill')add('Tools',component.id+'/'+document.name,document);}
 }
 return entries;
}
/** 同次 Apply 的表面集合；替换不重放代码服务或不变的表面。 */
export class SurfaceRegistrationSession {
 readonly #context:SurfaceContext;#active=new Map<string,Registration&{dispose:()=>void|Promise<void>}>();#closed=false;
 private constructor(context:SurfaceContext){this.#context=context;}
 static async create(context:SurfaceContext,initial:SurfaceValidationResult):Promise<SurfaceRegistrationSession>{const session=new SurfaceRegistrationSession(context);await session.replace(initial);return session;}
 async replace(result:SurfaceValidationResult):Promise<number>{
  if(this.#closed)throw new Error('上下文已卸载。');const next=plan(result,this.#context);
  const changed=[...next].filter(([key,value])=>!this.#active.has(key)||stable(this.#active.get(key)!.document)!==stable(value.document));
  const removed=[...this.#active].filter(([key])=>!next.has(key)||changed.some(([name])=>name===key));
  for(const [key,value]of [...removed].reverse()){await value.dispose();this.#active.delete(key);}
  const installed:string[]=[];
  try{for(const [key,value]of changed){this.#active.set(key,{...value,dispose:this.#context.provide(value.key,value.name,value.document)});installed.push(key);}}
  catch(error){for(const key of installed.reverse()){await this.#active.get(key)!.dispose();this.#active.delete(key);}for(const [key,value]of removed)this.#active.set(key,{...value,dispose:this.#context.provide(value.key,value.name,value.document)});throw error;}
  return removed.length+changed.length;
 }
 async dispose():Promise<void>{if(this.#closed)return;this.#closed=true;for(const value of [...this.#active.values()].reverse())await value.dispose();this.#active.clear();}
}
/** 开发态文件监视：声明式内容可更新，代码变化只给出滚动重启状态。 */
export class HotReloadSurfacePlugin<TContext extends SurfaceContext=SurfaceContext> {
 readonly name='qian.declarative-surfaces';readonly inject:readonly string[];readonly surfaceProvides:readonly string[];
 readonly #directory:string;readonly #options:SurfaceValidationOptions;readonly #initial:SurfaceValidationResult;
 readonly #code?:{readonly inject?:readonly string[];apply(context:TContext,config:unknown):void|Promise<void>;validateSurfaces?(context:TContext,candidate:SurfaceValidationResult):void|Promise<void>};
 #status:SurfaceReloadStatus=freeze({revision:0,restartRequired:false,changedCodeFiles:[],diagnostics:[]});
 constructor(directory:string,options:SurfaceValidationOptions={},code?:{readonly inject?:readonly string[];apply(context:TContext,config:unknown):void|Promise<void>;validateSurfaces?(context:TContext,candidate:SurfaceValidationResult):void|Promise<void>}){
  this.#directory=path.resolve(directory);this.#options=options;this.#code=code;this.#initial=validateComponent(this.#directory,options);if(!this.#initial.isValid)throw new Error('INPUT_INVALID');
  this.inject=Object.freeze([...(code?.inject??obj(this.#initial.component!.manifest).inject as string[])]);this.surfaceProvides=Object.freeze([...plan(this.#initial).keys(),'SurfaceReloadDiagnostics:'+this.#initial.component!.id]);
 }
 get status():SurfaceReloadStatus{return this.#status;}
 async apply(context:TContext,config:unknown):Promise<void>{
  const previous=snapshot(this.#directory);const initial=validateComponent(this.#directory,this.#options);
  if(!initial.isValid||stable(initial.component!.manifest)!==stable(this.#initial.component!.manifest))throw new Error('INPUT_INVALID');
  await this.#code?.apply(context,config);const session=await SurfaceRegistrationSession.create(context,initial);context.effect(()=>session.dispose());
  let active=true;
  context.provide('SurfaceReloadDiagnostics',context.entry.plugin,{read:():SurfaceRuntimeEntry=>{
   if(!active)throw new Error('NOT_FOUND');
   return freeze({entryId:context.entry.id,plugin:context.entry.plugin,scope:context.scope,revision:this.#status.revision,restartRequired:this.#status.restartRequired,changedCodeFiles:[...this.#status.changedCodeFiles],invalidSurface:this.#status.diagnostics.some(item=>item.severity==='error')});
  }});
  context.effect(()=>{active=false;});
  let dirty=true,ticks=0,closed=false;let running=Promise.resolve();let watcher:FSWatcher;
  const refresh=async()=>{
   const before=snapshot(this.#directory);if(stable([...previous])===stable([...before])&&!this.#status.diagnostics.some(item=>item.severity==='error'))return;
   const result=validateComponent(this.#directory,this.#options);const after=snapshot(this.#directory);if(stable([...before])!==stable([...after]))return;
   const changed=[...new Set([...previous.keys(),...after.keys()])].filter(file=>previous.get(file)!==after.get(file));
   const code=changed.filter(file=>file.startsWith('src/')||file.endsWith('.dll')||file.endsWith('.csproj'));
   if(result.component&&stable(result.component.manifest)!==stable(initial.component!.manifest))code.push('.qian-plugin/plugin.json');
   const restart=[...new Set([...this.#status.changedCodeFiles,...code])].sort();this.#status=freeze({...this.#status,restartRequired:restart.length>0,changedCodeFiles:restart,diagnostics:result.isValid?this.#status.diagnostics:result.diagnostics});
   if(!result.isValid||code.includes('.qian-plugin/plugin.json'))return;
   const component=result.component!;const generated=obj(obj(component.manifest)['x-qian-generated']).tools;
   let safe=result;
   if(Array.isArray(generated)){
    const originals=initial.component!.documents.filter(document=>document.kind==='tool'&&generated.includes(document.file));
    if(component.documents.some(document=>document.kind==='tool'&&generated.includes(document.file)&&!originals.some(old=>old.file===document.file&&stable(old.data)===stable(document.data)))||originals.some(old=>!component.documents.some(document=>document.file===old.file&&stable(document.data)===stable(old.data))))
     this.#status=freeze({...this.#status,restartRequired:true,changedCodeFiles:[...new Set([...this.#status.changedCodeFiles,'tools (generated)'])]});
    safe={...result,component:{...component,documents:[...component.documents.filter(document=>document.kind!=='tool'||!generated.includes(document.file)),...originals]}};
   }
   await this.#code?.validateSurfaces?.(context,safe);
   const count=await session.replace(safe);previous.clear();for(const [key,value]of after)previous.set(key,value);
   this.#status=freeze({...this.#status,revision:this.#status.revision+(count?1:0),diagnostics:result.diagnostics});
  };
  watcher=watch(this.#directory,{recursive:true},()=>{dirty=true;});watcher.on('error',()=>{dirty=true;});
  const timer=setInterval(()=>{if(closed||(!dirty&&++ticks<10))return;dirty=false;ticks=0;running=running.then(()=>closed?undefined:refresh()).catch(()=>{
   const message='输入不符合该能力的输入 Schema，逐项错误见 errors。';
   this.#status=freeze({...this.#status,diagnostics:[{file:'runtime',line:1,severity:'error',error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message,detail:message,trace_id:randomBytes(16).toString('hex'),pointer:'runtime#',errors:[{code:'SURFACE_REFERENCE_UNKNOWN',message:'表面更新未通过运行时检查，继续保留上次有效注册。',pointer:'runtime#'}]}}]});dirty=true;
  });},200);timer.unref();
  context.effect(async()=>{closed=true;clearInterval(timer);watcher.close();await running;});
 }
}
function snapshot(root:string):Map<string,string>{
 const files=new Map<string,string>();let total=0;
 const walk=(directory:string)=>{if(lstatSync(directory).isSymbolicLink())throw new Error('INPUT_INVALID');for(const name of readdirSync(directory).sort()){
  if(['.git','bin','obj','node_modules','.cache'].includes(name))continue;const full=path.join(directory,name);const info=lstatSync(full);if(info.isSymbolicLink())throw new Error('INPUT_INVALID');
  if(info.isDirectory())walk(full);else if(info.isFile()){total+=info.size;if(files.size>=10000||total>64*1024*1024)throw new Error('INPUT_INVALID');files.set(path.relative(root,full).replaceAll('\\','/'),createHash('sha256').update(readFileSync(full)).digest('hex'));}
 }};walk(root);return files;
}
