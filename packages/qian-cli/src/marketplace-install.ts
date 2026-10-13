import {existsSync,readFileSync,mkdirSync,writeFileSync,rmSync,readdirSync,mkdtempSync,realpathSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {stringify} from 'yaml';
import {parseSurface,type JsonValue,type SurfaceDiagnostic} from '@qian/surfaces';
import {resolveMarketplacePlugin,MarketplaceFailure,type MarketplaceOptions,type ResolvedMarketplacePlugin} from './marketplaces.js';
import {within,assertNoLinks,sha256,type FileRecord} from './marketplace-files.js';
import {loadProfile} from './profiles.js';
import {WorkspaceTransaction} from './workspace-transaction.js';
import {collectPackageReferences} from './package-references.js';
import {inspectPackageWorkspace,packageMutationTargets,applyPackageChanges,verifyInstalledPackages,type PackageWorkspace} from './package-manager.js';
import {planPackageChanges,type PackagePlan,type PackageOwnership} from './package-plan.js';
import {PackageCommandFailure} from './package-process.js';
import {readInstalledState,readInstallFile,writeInstallFile,installObject,installFailure,installedPackageSets,removedInstallationEntries,verifyInstalledComponent,acquireInstallationLease,type InstallObject} from './installed-components.js';
import {prepareComponentDelivery,type ComponentDelivery} from './component-delivery.js';
import {resolvePackageDelivery,type PackageArtifactEvidence} from './package-delivery.js';
import {installedPackageArtifacts,verifyInstalledPackageArtifacts} from './package-integrity.js';
import {selectComponentSurfaces,surfaceName,type SurfaceName,type SurfaceSelection} from './surface-selection.js';

export interface DeclarativeInstallOptions extends MarketplaceOptions {yes?:boolean;dryRun?:boolean;noCode?:boolean;surfaces?:readonly SurfaceName[];force?:boolean;resetConfig?:boolean;forceProfile?:boolean}
export interface ComponentInstallRequest {reference:string;selection?:SurfaceSelection}
export interface DeclarativeInstallResult {status:'planned'|'installed'|'unchanged'|'on-use';id:string;version:string;market:string;trust:'verified'|'unverified';files:FileRecord[];packages:{nuget:string[];npm:string[]};diagnostics:SurfaceDiagnostic[];lock?:InstallObject;note?:string;permissions?:JsonValue;secretReferences?:string[]}
export interface BatchInstallResult {status:'planned'|'installed'|'unchanged'|'on-use';plugins:DeclarativeInstallResult[]}
interface PatchPlan {profile:string;definitionFile:string;definition:InstallObject;definitionChanged?:boolean;file:string;entries:JsonValue[];touched:Set<string>}
interface Candidate {resolved:ResolvedMarketplacePlugin;manifest:InstallObject;delivery:ComponentDelivery;artifacts:PackageArtifactEvidence[];selection:SurfaceSelection;sourceArtifacts:PackageArtifactEvidence[];result:DeclarativeInstallResult;previous?:InstallObject;record?:InstallObject;replaceFiles:boolean;needsApproval:boolean}

function entryKey(entry:InstallObject):string {return String(entry.scope??'global')+'/'+String(entry.id);}
function profileName(value:unknown):string {
 if(typeof value!=='string'||!/^[a-z][a-z0-9-]*$/.test(value))installFailure('argv','profile 名无效。');
 return value;
}
function stable(value:JsonValue|undefined):string {
 if(Array.isArray(value))return '['+value.map(stable).sort().join(',')+']';
 if(value&&typeof value==='object')return '{'+Object.keys(value).sort().map(key=>JSON.stringify(key)+':'+stable(value[key])).join(',')+'}';
 return JSON.stringify(value??null);
}
function grants(policy:InstallObject):Set<string> {
 const result=new Set<string>();
 const walk=(value:JsonValue|undefined,key:string):void=>{
  if(Array.isArray(value)){for(const entry of value)result.add(key+':'+stable(entry));}
  else if(value&&typeof value==='object'){for(const[name,entry]of Object.entries(value))walk(entry,key+'/'+name);}
  else if(value!==undefined)result.add(key+':'+stable(value));
 };
 walk(policy.permissions,'permissions');walk(policy.egress,'egress');return result;
}
function requirementGrants(requirements:InstallObject):Set<string> {
 return new Set(Object.entries(requirements).flatMap(([id,policy])=>[...grants(installObject(policy))].map(grant=>id+'/'+grant)));
}
function copyCandidate(candidate:Candidate,destination:string):void {
 assertNoLinks(destination);mkdirSync(destination,{recursive:true});
 for(const file of candidate.result.files){
  const target=within(destination,file.path);mkdirSync(path.dirname(target),{recursive:true});
  writeFileSync(target,candidate.resolved.content.files.get(file.path)!,{flag:'wx',mode:0o600});
 }
}
function originalManifest(root:string,previous:InstallObject):InstallObject|undefined {
 try{
  const expected=Array.isArray(previous.files)?previous.files.map(installObject).find(file=>file.path==='.qian-plugin/plugin.json'):undefined;
  const file=within(root,'.qian/plugins/'+previous.id+'/.qian-plugin/plugin.json'),bytes=readFileSync(file);
  if(!expected||sha256(bytes)!==expected.sha256)return undefined;
  const parsed=parseSurface('.qian-plugin/plugin.json',bytes.toString('utf8'));
  if(parsed.syntax&&!parsed.diagnostics.some(item=>item.severity==='error'))return installObject(parsed.syntax.data);
 }catch{ /* 无法确认原清单时，不能依据被改写的权限跳过确认。 */ }
 return undefined;
}
function makeRecord(candidate:Candidate,entries:JsonValue[]):InstallObject {
 const resolved=candidate.resolved;
 const record:InstallObject={id:resolved.component.id,name:resolved.name,version:resolved.version,market:resolved.market,
  source:resolved.source as unknown as JsonValue,digest:resolved.digest,installedAt:new Date().toISOString(),
  surfaces:[...new Set(candidate.delivery.components.flatMap(entry=>[...entry.component.documents.map(surfaceName),...Object.keys(installObject(entry.manifest.surfaces))]))],
  files:candidate.result.files as unknown as JsonValue,packages:resolved.packages,entries,requirements:candidate.delivery.requirements,
  deliveryDigest:resolved.content.digest,packageArtifacts:candidate.artifacts as unknown as JsonValue,sourcePackageArtifacts:candidate.sourceArtifacts as unknown as JsonValue,
  selection:candidate.selection as unknown as JsonValue,trust:resolved.trust,marketSource:resolved.marketSource};
 if(resolved.commit)record.commit=resolved.commit;if(resolved.marketCommit)record.marketCommit=resolved.marketCommit;if(resolved.marketKey)record.marketKey=resolved.marketKey;
 return record;
}
function patchEntry(plan:PatchPlan,entry:InstallObject,plugin:string,config:JsonValue,enabled:boolean):void {
 const index=plan.entries.findIndex(value=>entryKey(installObject(value))===entryKey(entry));
 const previous=index>=0?installObject(plan.entries[index]):{};
 const replacement={...previous,id:entry.id,scope:entry.scope??'global',plugin,config,...enabled?{disabled:false}:{}};
 if(index>=0)plan.entries[index]=replacement;else plan.entries.push(replacement);
 plan.touched.add(entryKey(entry));
}

/** 兼容单组件调用；实际准备、验证和提交都由同一批量事务实现。 */
export async function installDeclarativeMarketplacePlugin(reference:string,options:DeclarativeInstallOptions):Promise<DeclarativeInstallResult>{
 return (await installMarketplacePlugins([reference],options)).plugins[0];
}

/** 整组先解析与验证，再一次性修改宿主；不会逐个提交而留下半批安装。 */
export async function installMarketplacePlugins(references:readonly (string|ComponentInstallRequest)[],options:DeclarativeInstallOptions):Promise<BatchInstallResult>{
 const root=realpathSync(path.resolve(options.workingDirectory)),profile=profileName(options.profile??'minimal');
 if(!references.length||references.length>100)installFailure('argv','add 需要 1 至 100 个组件引用。');
 const candidates:Candidate[]=[],ids=new Set<string>();
 for(const request of references){
  const reference=typeof request==='string'?request:request.reference,selection:SurfaceSelection={...options.noCode?{noCode:true}:{},...options.surfaces?{surfaces:options.surfaces}:{},...typeof request==='string'?{}:request.selection};
  const source=await resolveMarketplacePlugin(reference,{...options,profile}),delivery=prepareComponentDelivery(source,profile,options.forceProfile);
  const resolved={...source,packages:delivery.packages},manifest=installObject(resolved.component.manifest),id=resolved.component.id;
  if(ids.has(id))installFailure('argv','同一批次不能重复指定组件或混用同组件的多个版本。');ids.add(id);
  const result:DeclarativeInstallResult={status:'planned',id,version:resolved.version,market:resolved.market,trust:resolved.trust,files:delivery.files,
   packages:resolved.packages,diagnostics:[...resolved.diagnostics,...delivery.diagnostics],permissions:delivery.components.length>1?delivery.requirements:installObject(manifest.policy).permissions??{},
   secretReferences:[...new Set([...resolved.content.files.values()].flatMap(bytes=>bytes.toString('utf8').match(/secretRef:\/\/[^\s"'<>},]+/g)??[]))]};
  if(resolved.policy.installation==='restricted')installFailure('.qian/marketplace.json','restricted 条目仅允许平台审核台安装。');
  if(resolved.policy.authentication==='ON_USE'){result.status='on-use';result.note='仅提示使用时认证，不写安装锁或激活组件。';}
  else{
   if(manifest.runtime!==undefined&&installObject(manifest.runtime).form===undefined&&!delivery.composition)installFailure('.qian-plugin/plugin.json','无运行时形态的代码依赖必须由组合包 patch 组装。');
   if(!Array.isArray(manifest.scopes)||!manifest.scopes.includes('global'))installFailure('.qian-plugin/plugin.json','组件不允许本地 global 挂载。');
   const allowed=resolved.policy.profiles;
   if(allowed?.length&&!allowed.includes(profile)&&!options.forceProfile)installFailure('argv','所选 profile 不在市场允许列表；核对计划后可显式 --force-profile。');
  }
  collectPackageReferences([{id,packages:resolved.packages}]);
  candidates.push({resolved,manifest,delivery,artifacts:[],selection,sourceArtifacts:[],result,replaceFiles:false,needsApproval:resolved.policy.installation==='confirm'});
 }
 const active=candidates.filter(candidate=>candidate.result.status!=='on-use');
 if(!active.length)return{status:'on-use',plugins:candidates.map(candidate=>candidate.result)};
 const lease=options.dryRun?undefined:acquireInstallationLease(root);
 let transaction:WorkspaceTransaction|undefined,preview:string|undefined;
 try{
  for(const candidate of active){
   if((candidate.resolved.packages.nuget.length||candidate.resolved.packages.npm.length)&&(!candidate.selection.noCode||candidate.delivery.composition)){
    const prepared=await resolvePackageDelivery(root,candidate.resolved,profile,options.forceProfile,!!candidate.selection.noCode);
    candidate.resolved=prepared.resolved;candidate.delivery=prepared.delivery;
    if(candidate.selection.noCode)candidate.sourceArtifacts=prepared.artifacts;else candidate.artifacts=prepared.artifacts;
   }
   const projected=selectComponentSurfaces(candidate.resolved,candidate.delivery,candidate.selection,profile,options.forceProfile);
   candidate.resolved=projected.resolved;candidate.delivery=projected.delivery;candidate.manifest=installObject(projected.resolved.component.manifest);
   candidate.result.files=projected.delivery.files;candidate.result.packages=projected.delivery.packages;
   candidate.result.permissions=projected.delivery.components.length>1?projected.delivery.requirements:installObject(candidate.manifest.policy).permissions??{};
   candidate.result.secretReferences=[...new Set([...projected.resolved.content.files.values()].flatMap(bytes=>bytes.toString('utf8').match(/secretRef:\/\/[^\s"'<>},]+/g)??[]))];
   candidate.result.diagnostics.push(...projected.delivery.diagnostics);
  }
  const state=readInstalledState(root),patches=new Map<string,PatchPlan>();
  const openPatch=(name:string):PatchPlan=>{
   profileName(name);const cached=patches.get(name);if(cached)return cached;
   const definitionFile=path.join(root,'profiles',name,'qian.profile.yml');assertNoLinks(definitionFile);
   if(!existsSync(definitionFile))installFailure(definitionFile,'先初始化所选 profile，安装器不会猜测宿主组装。');
   const definition=installObject(readInstallFile(definitionFile)),file=within(path.dirname(definitionFile),typeof definition.patch==='string'?definition.patch:'./qian.patch.yml');
   const entries=existsSync(file)?readInstallFile(file):[];if(!Array.isArray(entries))installFailure(file,'profile patch 必须为数组。');
   const plan:PatchPlan={profile:name,definitionFile,definition:{...definition,patch:typeof definition.patch==='string'?definition.patch:'./qian.patch.yml'},definitionChanged:!definition.patch,file,entries:structuredClone(entries),touched:new Set<string>()};
   patches.set(name,plan);return plan;
  };
  const selectedPatch=openPatch(profile);
  for(const candidate of active){
   const {resolved,manifest,result}=candidate,id=result.id,destination=within(root,'.qian/plugins/'+id);
   const previous=state.plugins.find(item=>item.id===id);candidate.previous=previous;
   let oldManifest:InstallObject|undefined,filesChanged=false;
   if(previous){
    try{oldManifest=verifyInstalledComponent(root,previous);}catch(error){if(!options.force)throw error;filesChanged=true;
     oldManifest=originalManifest(root,previous);candidate.needsApproval=candidate.needsApproval||!oldManifest;}
   }else if(existsSync(destination))installFailure(destination,'组件目录存在但没有安装锁，不能覆盖不明来源的文件。');
   if(previous&&candidate.selection.noCode&&installObject(previous.selection).noCode!==true){
    if(filesChanged)installFailure(destination,'原组件文件被修改，无法确认 required 代码保护；先恢复原组件再选择表面。');
    for(const item of (previous.files as JsonValue[]).map(installObject))if(typeof item.path==='string'&&(item.path==='.qian-plugin/plugin.json'||item.path.endsWith('/.qian-plugin/plugin.json'))){
     const original=installObject(readInstallFile(within(destination,item.path)));
     if(installObject(original.policy).required===true&&installObject(original.runtime).form!==undefined)
      installFailure(destination,'不能用 --no-code 替换已安装的 required 代码组件或其子组件。');
    }
   }
   const same=!!previous&&previous.version===result.version&&previous.digest===resolved.digest&&(previous.deliveryDigest??previous.digest)===resolved.content.digest&&
    stable(previous.packageArtifacts??[])===stable(candidate.artifacts as unknown as JsonValue)&&previous.market===resolved.market&&previous.marketSource===resolved.marketSource;
   if(previous&&previous.version===result.version&&!same&&!options.force)installFailure(destination,'同版本来源或内容发生变化，核对后才能用 --force 替换。');
   if(previous&&oldManifest){
    const before=requirementGrants(previous.requirements?installObject(previous.requirements):{[id]:installObject(oldManifest.policy)}),after=requirementGrants(candidate.delivery.requirements);
    if([...after].some(grant=>!before.has(grant)))candidate.needsApproval=true;
   }
   candidate.replaceFiles=!previous||!same||filesChanged;
   const entries=previous?.entries;
   if(entries!==undefined&&!Array.isArray(entries))installFailure(state.file,'组件安装条目记录无效。');
   const retained=(Array.isArray(entries)?entries:[]).map(installObject);
   for(const entry of retained){
    if(entry.scope!=='global'||typeof entry.id!=='string'||!/^[a-z][a-z0-9_.-]*$/.test(entry.id))installFailure(state.file,'本地安装记录包含不支持的 scope 或条目标识。');
    const plan=openPatch(profileName(entry.profile));
    if(entry.kind==='bundle'){
     if(!candidate.delivery.composition)installFailure(state.file,'组合层变成普通组件时，需先卸载原组合层。');
     if(!Array.isArray(plan.definition.bundles)||!plan.definition.bundles.includes(id+'@'+String(previous!.version)))installFailure(plan.definitionFile,'已安装组合包引用被手工改写。');
    }else{
     if(!candidate.delivery.regularEntry)installFailure(state.file,'普通组件变成组合层时，需先卸载原条目。');
     const current=plan.entries.find(value=>entryKey(installObject(value))===entryKey(entry));
     if(!current||installObject(current).plugin!==id+'@'+String(previous!.version))installFailure(plan.file,'已安装条目被手工替换，拒绝改写其它组件。');
    }
   }
   let selected=retained.find(entry=>entry.profile===profile&&entry.kind!=='bundle'),entryChanged=false;
   if(candidate.delivery.composition&&!retained.some(entry=>entry.profile===profile&&entry.kind==='bundle')){
    if(!Array.isArray(selectedPatch.definition.bundles))installFailure(selectedPatch.definitionFile,'profile.bundles 必须为数组。');
    const existing=selectedPatch.definition.bundles.find(value=>String(value).split('@')[0]===id);
    if(existing!==undefined&&existing!==id+'@'+result.version)installFailure(selectedPatch.definitionFile,'组合包已由其它版本范围管理，请先核对 profile。');
    retained.push({profile,scope:'global',id,kind:'bundle'});entryChanged=true;
    const tombstone=removedInstallationEntries(state.lock).find(entry=>entry.id===id&&entry.profile===profile&&entry.kind==='bundle');
    if(tombstone){
     if(!Array.isArray(tombstone.retainedEntries))installFailure(state.file,'组合层保留条目记录无效。');
     const keys=new Set(candidate.delivery.patch.map(installObject).filter(row=>typeof row.plugin==='string').map(entryKey));
     for(const raw of tombstone.retainedEntries){const old=installObject(raw);if(!keys.has(entryKey(old)))continue;
      const index=selectedPatch.entries.findIndex(row=>entryKey(installObject(row))===entryKey(old));if(index<0)continue;
      const row=installObject(selectedPatch.entries[index]);if(row.disabled!==true||row.plugin!==old.plugin)installFailure(selectedPatch.file,'保留的组合层配置已被复用，拒绝覆盖。');
      const {plugin:_plugin,...rest}=row;selectedPatch.entries[index]={...rest,disabled:old.wasDisabled===true};selectedPatch.touched.add(entryKey(old));}
    }
   }
   if(candidate.delivery.regularEntry&&!selected){
    const entryId=candidate.delivery.composition?id+'-surfaces':id.split('.').at(-1)!,existing=selectedPatch.entries.find(value=>entryKey(installObject(value))==='global/'+entryId);
    if(existing&&(installObject(existing).disabled!==true||String(installObject(existing).plugin).split('@')[0]!==id))installFailure(selectedPatch.file,'目标 scope/id 已存在，拒绝覆盖。');
    selected={profile,scope:'global',id:entryId};retained.push(selected);entryChanged=true;
   }
   const selectedCurrent=selected?selectedPatch.entries.find(value=>entryKey(installObject(value))===entryKey(selected!)):undefined;
   if(installObject(selectedCurrent).disabled===true)entryChanged=true;
   const changed=candidate.replaceFiles||entryChanged||!!options.resetConfig;
   if(!changed){result.status='unchanged';result.lock=previous;continue;}
   for(const entry of retained){
    const plan=openPatch(String(entry.profile)),current=installObject(plan.entries.find(value=>entryKey(installObject(value))===entryKey(entry)));
    if(candidate.replaceFiles&&resolved.policy.profiles?.length&&!resolved.policy.profiles.includes(String(entry.profile))&&!options.forceProfile)
     installFailure(plan.file,'升级影响了市场允许列表之外的 profile，需显式 --force-profile。');
    if(entry.kind==='bundle'){
     const bundles=Array.isArray(plan.definition.bundles)?[...plan.definition.bundles]:[];
     const index=bundles.findIndex(value=>String(value).split('@')[0]===id);
     if(index>=0)bundles[index]=id+'@'+result.version;else bundles.push(id+'@'+result.version);
     plan.definition.bundles=bundles;plan.definitionChanged=true;plan.touched.add('#bundle/'+id);
     entry.contributions=candidate.delivery.patch.map(installObject).filter(row=>typeof row.plugin==='string').map(row=>({id:row.id,scope:row.scope??'global',plugin:row.plugin}));
    }else{
     const config=options.resetConfig?{}:current.config??{};
     patchEntry(plan,entry,id+'@'+result.version,config,entry.profile===profile);
    }
   }
   candidate.record=makeRecord(candidate,retained);result.lock=candidate.record;
  }
  const changed=active.filter(candidate=>candidate.record),replacements=new Map(changed.map(candidate=>[candidate.result.id,candidate.record!]));
  const afterPlugins=state.plugins.map(plugin=>replacements.get(String(plugin.id))??plugin);
  for(const candidate of changed)if(!candidate.previous)afterPlugins.push(candidate.record!);
  const beforePackages=installedPackageSets(state.plugins),afterPackages=installedPackageSets(afterPlugins);
  const beforeReferences=collectPackageReferences(beforePackages),afterReferences=collectPackageReferences(afterPackages);
  const afterArtifacts=installedPackageArtifacts(afterPlugins);
  const touchesCode=active.some(candidate=>candidate.resolved.packages.nuget.length||candidate.resolved.packages.npm.length)||changed.some(candidate=>{
   const packages=installObject(candidate.previous?.packages);return Array.isArray(packages.nuget)&&packages.nuget.length||Array.isArray(packages.npm)&&packages.npm.length;
  });
  let workspace:PackageWorkspace|undefined,packagePlan:PackagePlan|undefined;
  if(touchesCode){
   workspace=await inspectPackageWorkspace(root,[...new Set([...beforeReferences,...afterReferences].map(reference=>reference.kind))]);
   if(state.lock.packageState!==undefined&&!Array.isArray(state.lock.packageState))installFailure(state.file,'代码包归属记录必须为数组。');
   packagePlan=planPackageChanges(workspace,beforePackages,afterPackages,(state.lock.packageState??[]) as unknown as PackageOwnership[]);
   if(!changed.length){verifyInstalledPackages(workspace,afterReferences);verifyInstalledPackageArtifacts(workspace,afterArtifacts);}
  }
  if(!changed.length)return{status:'unchanged',plugins:candidates.map(candidate=>candidate.result)};

  // 在独立目录验证整组新组件及所有受升级影响的 profile，不先改动宿主。
  preview=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-install-plan-'));
  const fileChanges=changed.filter(candidate=>candidate.replaceFiles);
  for(const candidate of fileChanges)copyCandidate(candidate,path.join(preview,'.qian/plugins',candidate.result.id));
  const excluded=new Set(fileChanges.map(candidate=>candidate.result.id)),componentDirectories:string[]=[];
  const sourcePlugins=path.join(root,'plugins');if(existsSync(sourcePlugins))componentDirectories.push(sourcePlugins);
  const installedRoot=path.join(root,'.qian/plugins');
  if(existsSync(installedRoot)){assertNoLinks(installedRoot);for(const name of readdirSync(installedRoot))if(!excluded.has(name))componentDirectories.push(within(installedRoot,name));}
  const diagnostics:SurfaceDiagnostic[]=[];
  for(const plan of patches.values()){
   if(!plan.touched.size)continue;
   const definitionFile=path.join(preview,'profiles',plan.profile,'qian.profile.yml'),patchFile=within(path.dirname(definitionFile),String(plan.definition.patch));
   mkdirSync(path.dirname(definitionFile),{recursive:true});writeInstallFile(definitionFile,Buffer.from(stringify(plan.definition)));writeInstallFile(patchFile,Buffer.from(stringify(plan.entries)));
   const checked=loadProfile({workingDirectory:preview,profile:plan.profile,includeHomePatch:false,componentDirectories});
   if(!checked.isValid)throw new MarketplaceFailure([...checked.diagnostics]);diagnostics.push(...checked.diagnostics);
   plan.entries=plan.entries.map(value=>{
    const item=installObject(value),key=entryKey(item),effective=checked.snapshot?.entries.find(entry=>entry.scope+'/'+entry.id===key);
    return plan.touched.has(key)&&effective?{...item,config:effective.config}:value;
   });
  }
  for(const candidate of changed)candidate.result.diagnostics.push(...diagnostics);
  if(options.dryRun)return{status:'planned',plugins:candidates.map(candidate=>candidate.result)};
  if(changed.some(candidate=>candidate.needsApproval)&&!options.yes)installFailure('.qian/marketplace.json','安装或新增反向权限需要确认；先 --dry-run 查看计划，再显式 --yes。');

  const targets=new Map<string,Buffer>();
  for(const plan of patches.values())if(plan.touched.size){
   targets.set(plan.file,Buffer.from(stringify(plan.entries)));
   const actual=readFileSync(plan.definitionFile,'utf8'),desired=stringify(plan.definition);
   if(plan.definitionChanged&&actual!==desired)targets.set(plan.definitionFile,Buffer.from(desired));
  }
  const removedEntries=removedInstallationEntries(state.lock).flatMap(entry=>{
   const candidate=changed.find(item=>item.result.id===entry.id&&(item.record!.entries as JsonValue[]).map(installObject).some(mount=>mount.profile===entry.profile&&mount.scope===entry.scope&&mount.id===entry.entryId));
   if(!candidate)return[entry];
   if(entry.kind==='bundle'&&Array.isArray(entry.retainedEntries)){
    const keys=new Set(candidate.delivery.patch.map(installObject).filter(row=>typeof row.plugin==='string').map(entryKey));
    const leftover=entry.retainedEntries.filter(value=>!keys.has(entryKey(installObject(value))));return leftover.length?[{...entry,retainedEntries:leftover}]:[];
   }
   return[];
  });
  targets.set(state.file,Buffer.from(JSON.stringify({...state.lock,plugins:afterPlugins,...state.lock.removedEntries!==undefined?{removedEntries}:{},...packagePlan?{packageState:packagePlan.state}:{}},null,2)+'\n'));
  const ignoreFile=path.join(root,'.gitignore');assertNoLinks(ignoreFile);const ignore=existsSync(ignoreFile)?readFileSync(ignoreFile,'utf8'):'';
  const additions=['.qian/journal/','.qian/.staging/'].filter(line=>!ignore.split(/\r?\n/).includes(line));
  if(additions.length)targets.set(ignoreFile,Buffer.from(ignore+(ignore&&!ignore.endsWith('\n')?'\n':'')+additions.join('\n')+'\n'));
  transaction=WorkspaceTransaction.begin(root,[...[...targets.keys()].map(file=>({path:file,kind:'file' as const})),
   ...fileChanges.map(candidate=>({path:within(root,'.qian/plugins/'+candidate.result.id),kind:'directory' as const})),
   ...(workspace&&packagePlan?packageMutationTargets(workspace,packagePlan.changes):[])],'add '+changed.map(candidate=>candidate.result.id).join(','),lease!.owner);
  return await transaction.run(async()=>{
   for(const candidate of fileChanges){
    const destination=within(root,'.qian/plugins/'+candidate.result.id);
    if(existsSync(destination))rmSync(destination,{recursive:true});copyCandidate(candidate,destination);
   }
   if(workspace&&packagePlan){
    await applyPackageChanges(workspace,packagePlan.changes);
    const restored=await inspectPackageWorkspace(root,[...new Set(afterReferences.map(reference=>reference.kind))]);verifyInstalledPackages(restored,afterReferences);verifyInstalledPackageArtifacts(restored,afterArtifacts);
   }
   for(const[file,bytes]of targets)writeInstallFile(file,bytes);
   for(const plan of patches.values())if(plan.touched.size){
    const checked=loadProfile({workingDirectory:root,profile:plan.profile,includeHomePatch:false});if(!checked.isValid)throw new MarketplaceFailure([...checked.diagnostics]);
   }
   for(const candidate of changed)candidate.result.status='installed';
   return{status:'installed' as const,plugins:candidates.map(candidate=>candidate.result)};
  });
 }catch(error){if(!transaction&&error instanceof PackageCommandFailure&&!error.terminationConfirmed)lease?.preserveProcess(error);throw error;}
 finally{if(preview&&!transaction?.hasPendingProcess)rmSync(preview,{recursive:true,force:true});lease?.release();}
}
