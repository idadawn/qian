import {existsSync,readFileSync,mkdirSync,writeFileSync,renameSync,unlinkSync,rmSync,lstatSync,openSync,closeSync} from 'node:fs';
import path from 'node:path';
import {randomBytes} from 'node:crypto';
import {stringify} from 'yaml';
import {parseSurface,type JsonValue,type SurfaceDiagnostic} from '@qian/surfaces';
import {resolveMarketplacePlugin,marketDiagnostic,MarketplaceFailure,type MarketplaceOptions} from './marketplaces.js';
import {within,assertNoLinks,sha256,readDirectory,type FileRecord} from './marketplace-files.js';
import {loadProfile} from './profiles.js';
import {WorkspaceTransaction} from './workspace-transaction.js';
import {collectPackageReferences,type ComponentPackages} from './package-references.js';
import {inspectPackageWorkspace,packageMutationTargets,applyPackageChanges,verifyInstalledPackages,type PackageWorkspace} from './package-manager.js';
import {planPackageChanges,type PackagePlan,type PackageOwnership} from './package-plan.js';
import {PackageCommandFailure} from './package-process.js';

type Obj={[key:string]:JsonValue};
const object=(value:unknown):Obj=>value!==null&&typeof value==='object'&&!Array.isArray(value)?value as Obj:{};
export interface DeclarativeInstallOptions extends MarketplaceOptions {yes?:boolean;dryRun?:boolean;noCode?:boolean}
export interface DeclarativeInstallResult {status:'planned'|'installed'|'unchanged'|'on-use';id:string;version:string;market:string;trust:'verified'|'unverified';files:FileRecord[];packages:{nuget:string[];npm:string[]};diagnostics:SurfaceDiagnostic[];lock?:Obj;note?:string;permissions?:JsonValue;secretReferences?:string[]}
function failure(file:string,message:string):never{throw new MarketplaceFailure([marketDiagnostic(file,'',message)]);}
function parse(file:string):JsonValue {
 const result=parseSurface(file,readFileSync(file,'utf8'));if(!result.syntax||result.diagnostics.some(d=>d.severity==='error'))throw new MarketplaceFailure([...result.diagnostics]);return result.syntax.data;
}
function atomic(file:string,bytes:Buffer):void {
 assertNoLinks(file);mkdirSync(path.dirname(file),{recursive:true});const temporary=file+'.'+randomBytes(6).toString('hex')+'.tmp';
 try{writeFileSync(temporary,bytes,{flag:'wx',mode:0o600});renameSync(temporary,file);}finally{if(existsSync(temporary))unlinkSync(temporary);}
}
/** 经同一签名/内容校验后安装表面与真实代码包；运行态是否 Ready 由宿主另行验证。 */
export async function installDeclarativeMarketplacePlugin(reference:string,options:DeclarativeInstallOptions):Promise<DeclarativeInstallResult>{
 const root=path.resolve(options.workingDirectory),profile=options.profile??'minimal';
 if(!/^[a-z][a-z0-9-]*$/.test(profile))failure('argv','profile 名无效。');
 const resolved=await resolveMarketplacePlugin(reference,{...options,profile}),manifest=object(resolved.component.manifest),id=resolved.component.id;
 const result:DeclarativeInstallResult={status:'planned',id,version:resolved.version,market:resolved.market,trust:resolved.trust,files:[],packages:resolved.packages,diagnostics:resolved.diagnostics,permissions:object(manifest.policy).permissions??{},secretReferences:[...new Set([...resolved.content.files.values()].flatMap(bytes=>bytes.toString('utf8').match(/secretRef:\/\/[^\s"'<>},]+/g)??[]))]};
 if(resolved.policy.installation==='restricted')failure('.qian/marketplace.json','restricted 条目仅允许后续平台审核台安装。');
 if(resolved.policy.authentication==='ON_USE')return{...result,status:'on-use',note:'S1 仅登记该市场条目并提示使用时认证；未写入安装锁或激活组件。'};
 if(manifest.runtime!==undefined&&options.noCode)failure('.qian-plugin/plugin.json','代码组件的 --no-code 表面切片尚未接入；本入口不会忽略该参数而安装代码。');
 if(manifest.runtime!==undefined&&!['module','ui','service'].includes(String(object(manifest.runtime).form)))failure('.qian-plugin/plugin.json','该代码形态不在 S1 安装范围内。');
 const requested=collectPackageReferences([{id,packages:resolved.packages}]);
 const scopes=Array.isArray(manifest.scopes)?manifest.scopes:[];if(!scopes.includes('global'))failure('.qian-plugin/plugin.json','当前入口只写 global 条目，组件未允许该 scope。');
 let release=():void=>{},transaction:WorkspaceTransaction|undefined,keepLease=false;
 const leaseOwner=randomBytes(16).toString('hex'),lease=path.join(root,'.qian/.install.lock');
 if(!options.dryRun){
  assertNoLinks(lease);mkdirSync(path.dirname(lease),{recursive:true});
  try{const handle=openSync(lease,'wx',0o600);try{writeFileSync(handle,JSON.stringify({pid:process.pid,id:leaseOwner,startedAt:new Date().toISOString()}));}finally{closeSync(handle);}
   release=()=>{try{assertNoLinks(lease);if(existsSync(lease)&&JSON.parse(readFileSync(lease,'utf8')).id===leaseOwner)unlinkSync(lease);}catch{ /* 不删除被改写或无法确认的锁。 */ }};}
  catch{failure(lease,'已有安装操作或残留安装锁；检查 journal 后重试。');}
 }
 try{
 const destination=path.join(root,'.qian/plugins',id),lockFile=path.join(root,'.qian/lock.json'),profileFile=path.join(root,'profiles',profile,'qian.profile.yml');
 assertNoLinks(destination);assertNoLinks(lockFile);assertNoLinks(profileFile);
 if(!existsSync(profileFile))failure(profileFile,'需先初始化所选 profile；本入口不创建或覆盖宿主配置。');
 const definition=object(parse(profileFile)),patchRelative=typeof definition.patch==='string'?definition.patch:'./qian.patch.yml',patchFile=within(path.dirname(profileFile),patchRelative);
 const patch=existsSync(patchFile)?parse(patchFile):[];
 if(!Array.isArray(patch))failure(patchFile,'profile patch 必须为条目数组。');
 const lock=existsSync(lockFile)?object(parse(lockFile)):{plugins:[]};
 if(lock.plugins!==undefined&&!Array.isArray(lock.plugins))failure(lockFile,'lock.plugins 必须为数组。');
 const locks=Array.isArray(lock.plugins)?lock.plugins:[],previous=locks.map(object).find(item=>item.id===id);
 const names=new Set(['.qian-plugin/plugin.json',...resolved.component.documents.map(document=>document.file)]);
 for(const name of resolved.content.files.keys())if(/^(?:README(?:\.md)?|AGENTS\.md|(?:assets|metadata|schemas)\/)/.test(name))names.add(name);
 const records=resolved.files.filter(file=>names.has(file.path));result.files=records;
 if(previous&&previous.version===resolved.version&&previous.digest===resolved.digest&&previous.market===resolved.market&&previous.marketSource===resolved.marketSource&&existsSync(destination)){
  if(JSON.stringify(readDirectory(destination).records)!==JSON.stringify(records))failure(destination,'已安装文件集合或内容被修改，拒绝覆盖。');
  for(const file of records){const target=within(destination,file.path);if(!existsSync(target)||!lstatSync(target).isFile()||sha256(readFileSync(target))!==file.sha256)failure(target,'已安装表面被本地修改，拒绝覆盖。');}
  if(requested.length){const workspace=await inspectPackageWorkspace(root,[...new Set(requested.map(item=>item.kind))]);verifyInstalledPackages(workspace,requested);}
  return{...result,status:'unchanged',lock:previous};
 }
 if(previous||existsSync(destination))failure(destination,'已有组件或锁记录；升级/替换需由完整安装事务处理，当前入口不覆盖。');
 const entryId=id.split('.').at(-1)!;
 const retainedIndex=patch.findIndex(item=>object(item).id===entryId&&(object(item).scope??'global')==='global'),retained=retainedIndex>=0?object(patch[retainedIndex]):undefined;
 if(retained&&(retained.disabled!==true||String(retained.plugin).split('@')[0]!==id))failure(patchFile,'目标 scope/id 已存在，拒绝覆盖配置。');
 const replacement={...retained,id:entryId,plugin:id+'@'+resolved.version,scope:'global',config:retained?.config??{},...(retained?{disabled:false}:{})};
 const newPatch:JsonValue[]=retained?[...patch.slice(0,retainedIndex),replacement,...patch.slice(retainedIndex+1)]:[...patch,replacement];
 const targetIndex=retained?retainedIndex:newPatch.length-1;
 const record:Obj={id,name:resolved.name,version:resolved.version,market:resolved.market,source:resolved.source as unknown as JsonValue,digest:resolved.digest,installedAt:new Date().toISOString(),surfaces:[...new Set(resolved.component.documents.map(document=>({skill:'skills',agent:'agents',command:'commands',tool:'tools',mcp:'mcpServers'} as Record<string,string>)[document.kind]??document.kind))],files:records as unknown as JsonValue,packages:resolved.packages,entries:[{profile,scope:'global',id:entryId}],trust:resolved.trust,marketSource:resolved.marketSource};
 if(resolved.commit)record.commit=resolved.commit;if(resolved.marketCommit)record.marketCommit=resolved.marketCommit;if(resolved.marketKey)record.marketKey=resolved.marketKey;
 result.lock=record;
 let workspace:PackageWorkspace|undefined,packagePlan:PackagePlan|undefined;
 if(requested.length){
  const components:ComponentPackages[]=locks.map(value=>{
   const item=object(value),packages=object(item.packages);
   if(typeof item.id!=='string'||!Array.isArray(packages.nuget)||!Array.isArray(packages.npm))failure(lockFile,'已安装组件的包引用记录无效。');
   return{id:item.id,packages:{nuget:packages.nuget as string[],npm:packages.npm as string[]}};
  });
  const after=[...components,{id,packages:resolved.packages}],all=collectPackageReferences(after);
  workspace=await inspectPackageWorkspace(root,[...new Set(all.map(item=>item.kind))]);
  if(lock.packageState!==undefined&&!Array.isArray(lock.packageState))failure(lockFile,'代码包归属记录必须为数组。');
  packagePlan=planPackageChanges(workspace,components,after,(lock.packageState??[]) as unknown as PackageOwnership[]);
 }
 if(options.dryRun)return result;
 if(resolved.policy.installation==='confirm'&&!options.yes)failure('.qian/marketplace.json','此条目需确认。先用 --dry-run 查看文件与权限需求，再显式 --yes。');
 const before=loadProfile({workingDirectory:root,profile,includeHomePatch:false});if(!before.isValid)throw new MarketplaceFailure([...before.diagnostics]);
 const targets=new Map<string,Buffer>([[patchFile,Buffer.from(stringify(newPatch))],[lockFile,Buffer.from(JSON.stringify({...lock,plugins:[...locks,record],...(packagePlan?{packageState:packagePlan.state}:{})},null,2)+'\n')]]);
 if(!definition.patch)targets.set(profileFile,Buffer.from(stringify({...definition,patch:patchRelative})));
 const ignoreFile=path.join(root,'.gitignore');assertNoLinks(ignoreFile);const ignore=existsSync(ignoreFile)?readFileSync(ignoreFile,'utf8'):'';
 const additions=['.qian/journal/','.qian/.staging/'].filter(line=>!ignore.split(/\r?\n/).includes(line));
 if(additions.length)targets.set(ignoreFile,Buffer.from(ignore+(ignore&&!ignore.endsWith('\n')?'\n':'')+additions.join('\n')+'\n'));
 const marker=Date.now()+'-'+randomBytes(6).toString('hex'),staging=path.join(root,'.qian/.staging',marker);
 assertNoLinks(staging);
 transaction=WorkspaceTransaction.begin(root,[...[...targets.keys()].map(file=>({path:file,kind:'file' as const})),
  {path:destination,kind:'directory'},...(workspace&&packagePlan?packageMutationTargets(workspace,packagePlan.changes):[])],'add '+id,leaseOwner);
 try{
  return await transaction.run(async()=>{
  mkdirSync(staging,{recursive:true});
  for(const file of records){const target=within(staging,file.path);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,resolved.content.files.get(file.path)!,{flag:'wx',mode:0o600});}
  mkdirSync(path.dirname(destination),{recursive:true});renameSync(staging,destination);
  if(workspace&&packagePlan){await applyPackageChanges(workspace,packagePlan.changes);verifyInstalledPackages(await inspectPackageWorkspace(root,[...new Set((packagePlan.changes.ensure??[]).map(item=>item.kind))]),packagePlan.changes.ensure??[]);}
  for(const[file,bytes]of targets)atomic(file,bytes);
  const after=loadProfile({workingDirectory:root,profile,includeHomePatch:false});if(!after.isValid)throw new MarketplaceFailure([...after.diagnostics]);
  const installedEntry=after.snapshot?.entries.find(entry=>entry.id===entryId&&entry.scope==='global');
  if(installedEntry){newPatch[targetIndex]={...object(newPatch[targetIndex]),config:installedEntry.config};atomic(patchFile,Buffer.from(stringify(newPatch)));}
  return{...result,status:'installed' as const,diagnostics:[...result.diagnostics,...after.diagnostics]};
  });
 }finally{if(!transaction.hasPendingProcess){assertNoLinks(staging);rmSync(staging,{recursive:true,force:true});}}
 }catch(error){
  if(!transaction&&error instanceof PackageCommandFailure&&!error.terminationConfirmed){keepLease=true;
   if(!options.dryRun)atomic(lease,Buffer.from(JSON.stringify({pid:process.pid,id:leaseOwner,pendingProcess:{pid:error.pid,tool:error.tool}})));}
  throw error;
 }finally{if(!transaction&&!keepLease)release();}
}
