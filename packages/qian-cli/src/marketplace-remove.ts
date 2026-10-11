import {rmSync} from 'node:fs';
import path from 'node:path';
import {stringify} from 'yaml';
import type {JsonValue} from '@qian/surfaces';
import {readInstalledState,readInstallFile,writeInstallFile,installObject,installFailure,installedPackageSets,verifyInstalledComponent,acquireInstallationLease,type InstallObject} from './installed-components.js';
import {within,assertNoLinks} from './marketplace-files.js';
import {collectPackageReferences} from './package-references.js';
import {inspectPackageWorkspace,packageMutationTargets,applyPackageChanges,type PackageWorkspace} from './package-manager.js';
import {planPackageChanges,type PackagePlan,type PackageOwnership} from './package-plan.js';
import {WorkspaceTransaction} from './workspace-transaction.js';
import {PackageCommandFailure} from './package-process.js';
import {loadProfile} from './profiles.js';
import {MarketplaceFailure} from './marketplaces.js';

export interface RemoveOptions {workingDirectory:string;profile?:string;purge?:boolean;dryRun?:boolean}
export async function removeMarketplacePlugins(references:readonly string[],options:RemoveOptions):Promise<{status:'planned'|'removed'|'unchanged';ids:string[]}> {
 const root=path.resolve(options.workingDirectory),profile=options.profile??'minimal';
 if(!/^[a-z][a-z0-9-]*$/.test(profile)||!references.length)installFailure('argv','移除需要组件引用和合法 profile。');
 const lease=options.dryRun?undefined:acquireInstallationLease(root);let transaction:WorkspaceTransaction|undefined;
 try{
  const state=readInstalledState(root),selected=new Map<string,InstallObject>();
  for(const reference of references){
   const at=reference.lastIndexOf('@'),name=at>0?reference.slice(0,at):reference,version=at>0?reference.slice(at+1):undefined;
   const pieces=name.split('/'),market=pieces.length===2?pieces[0]:undefined,local=pieces.length===2?pieces[1]:name;
   const matches=state.plugins.filter(plugin=>(!market||plugin.market===market)&&(!version||plugin.version===version)&&
    (plugin.id===local||plugin.name===local||String(plugin.id).split('.').at(-1)===local));
   if(matches.length>1)installFailure(state.file,'移除引用存在歧义；请使用完整组件 id 或明确市场。');
   if(matches[0])selected.set(String(matches[0].id),matches[0]);
  }
  if(!selected.size)return{status:'unchanged',ids:[]};
  const profileFile=path.join(root,'profiles',profile,'qian.profile.yml'),definition=installObject(readInstallFile(profileFile));
  const patchFile=within(path.dirname(profileFile),typeof definition.patch==='string'?definition.patch:'./qian.patch.yml');
  const current=readInstallFile(patchFile);if(!Array.isArray(current))installFailure(patchFile,'profile patch 必须为数组。');
  let patch:JsonValue[]=current,remaining:InstallObject[]=[];const removed:string[]=[],directories:string[]=[];
  for(const plugin of state.plugins){
   if(!selected.has(String(plugin.id))){remaining.push(plugin);continue;}
   const manifest=verifyInstalledComponent(root,plugin);if(installObject(manifest.policy).required===true)installFailure(state.file,'required 组件不能移除。');
   if(!Array.isArray(plugin.entries))installFailure(state.file,'安装条目记录无效。');
   const entries=plugin.entries.map(installObject),chosen=entries.filter(entry=>entry.profile===profile);
   if(!chosen.length){remaining.push(plugin);continue;}
   for(const entry of chosen){
    if(entry.scope!=='global'||typeof entry.id!=='string')installFailure(state.file,'本地移除只处理 global 条目；租户操作必须通过平台接口。');
    const index=patch.findIndex(value=>installObject(value).id===entry.id&&(installObject(value).scope??'global')==='global');
    if(index<0)installFailure(patchFile,'安装锁中的条目已被手动移除，请先核对宿主状态。');
    const value=installObject(patch[index]);
    if(value.plugin!==String(plugin.id)+'@'+String(plugin.version))installFailure(patchFile,'条目已被替换为其它组件或版本，拒绝改写。');
    patch=options.purge?patch.filter((_,position)=>position!==index):patch.map((item,position)=>position===index?{...value,disabled:true}:item);
   }
   const retained=entries.filter(entry=>entry.profile!==profile);removed.push(String(plugin.id));
   if(retained.length)remaining.push({...plugin,entries:retained});else directories.push(within(root,'.qian/plugins/'+plugin.id));
  }
  if(!removed.length)return{status:'unchanged',ids:[]};
  const beforePackages=installedPackageSets(state.plugins),afterPackages=installedPackageSets(remaining);
  const all=collectPackageReferences(beforePackages);let workspace:PackageWorkspace|undefined,plan:PackagePlan|undefined;
  if(all.length){
   workspace=await inspectPackageWorkspace(root,[...new Set(all.map(item=>item.kind))]);
   if(state.lock.packageState!==undefined&&!Array.isArray(state.lock.packageState))installFailure(state.file,'代码包归属记录无效。');
   plan=planPackageChanges(workspace,beforePackages,afterPackages,(state.lock.packageState??[]) as unknown as PackageOwnership[]);
  }
  if(options.dryRun)return{status:'planned',ids:removed};
  const targets=[{path:patchFile,kind:'file' as const},{path:state.file,kind:'file' as const},...directories.map(directory=>({path:directory,kind:'directory' as const})),
   ...(workspace&&plan?packageMutationTargets(workspace,plan.changes):[])];
  transaction=WorkspaceTransaction.begin(root,targets,'remove '+removed.join(','),lease!.owner);
  return await transaction.run(async()=>{
   for(const plugin of selected.values())verifyInstalledComponent(root,plugin);
   for(const directory of directories){assertNoLinks(directory);rmSync(directory,{recursive:true});}
   if(workspace&&plan)await applyPackageChanges(workspace,plan.changes);
   writeInstallFile(patchFile,Buffer.from(stringify(patch)));
   writeInstallFile(state.file,Buffer.from(JSON.stringify({...state.lock,plugins:remaining,...plan?{packageState:plan.state}:{}},null,2)+'\n'));
   const checked=loadProfile({workingDirectory:root,profile,includeHomePatch:false});if(!checked.isValid)throw new MarketplaceFailure([...checked.diagnostics]);
   return{status:'removed' as const,ids:removed};
  });
 }catch(error){if(!transaction&&error instanceof PackageCommandFailure&&!error.terminationConfirmed)lease?.preserveProcess(error);throw error;}
 finally{lease?.release();}
}
