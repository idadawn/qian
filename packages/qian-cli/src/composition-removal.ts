import {mkdtempSync,realpathSync,rmSync,mkdirSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {stringify} from 'yaml';
import type {JsonValue} from '@qian/surfaces';
import {loadProfile} from './profiles.js';
import {MarketplaceFailure} from './marketplaces.js';
import {installObject,installFailure,writeInstallFile,type InstallObject} from './installed-components.js';
import {within} from './marketplace-files.js';

const key=(value:InstallObject):string=>String(value.scope??'global')+'/'+String(value.id);
export interface CompositionRemoval {definition:InstallObject;patch:JsonValue[];retained:JsonValue[]}

/** 组合层撤销后只保留失去来源的配置；其它组合层和显式独立条目仍有优先权。 */
export function planCompositionRemoval(root:string,profile:string,definition:InstallObject,patch:JsonValue[],mount:InstallObject,reference:string,purge:boolean,removedReferences:readonly string[]=[reference],removedContributionKeys:readonly string[]=[]):CompositionRemoval {
 if(!Array.isArray(definition.bundles)||!definition.bundles.includes(reference))installFailure('qian.profile.yml','组合包引用已被手动改写。','SURFACE_LOCAL_MODIFIED');
 if(!Array.isArray(mount.contributions))installFailure('.qian/lock.json','组合层缺少可核对的条目记录。');
 const contributions=mount.contributions.map(installObject),keys=new Set(contributions.map(key));
 const before=loadProfile({workingDirectory:root,profile,includeHomePatch:false});if(!before.isValid)throw new MarketplaceFailure([...before.diagnostics]);
 const next:InstallObject={...definition,bundles:definition.bundles.filter(item=>!removedReferences.includes(String(item)))};
 const temporary=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-remove-plan-'));
 try{
  const profileFile=path.join(temporary,'profiles',profile,'qian.profile.yml'),patchFile=within(path.dirname(profileFile),typeof next.patch==='string'?next.patch:'./qian.patch.yml');
  mkdirSync(path.dirname(profileFile),{recursive:true});
  writeInstallFile(profileFile,Buffer.from(stringify({...next,patch:typeof next.patch==='string'?next.patch:'./qian.patch.yml'})));
  // 配置覆盖依赖组合层提供 plugin；先移开这些覆盖，检查其它来源是否仍提供条目。
  const removedKeys=new Set([...keys,...removedContributionKeys]);
  const basePatch=patch.filter(value=>!removedKeys.has(key(installObject(value)))||typeof installObject(value).plugin==='string');
  writeInstallFile(patchFile,Buffer.from(stringify(basePatch)));
  const remaining=loadProfile({workingDirectory:temporary,profile,includeHomePatch:false,componentDirectories:[path.join(root,'plugins'),path.join(root,'.qian/plugins')]});
  if(!remaining.isValid)throw new MarketplaceFailure([...remaining.diagnostics]);
  const surviving=new Set(remaining.snapshot!.entries.map(entry=>entry.scope+'/'+entry.id)),retained:JsonValue[]=[];
  let result=[...patch];
  for(const contribution of contributions){
   const identity=key(contribution);if(surviving.has(identity))continue;
   const index=result.findIndex(value=>key(installObject(value))===identity),override=index>=0?installObject(result[index]):{};
   if(typeof override.plugin==='string')continue;
   const effective=before.snapshot!.entries.find(entry=>entry.scope+'/'+entry.id===identity);
   if(!effective&&index<0)continue;
   if(purge){if(index>=0)result=result.filter((_,position)=>position!==index);continue;}
   const plugin=effective?.plugin??contribution.plugin;
   if(typeof plugin!=='string')installFailure('.qian/lock.json','不能确认保留配置的组件来源。');
   const row={...override,id:contribution.id,scope:contribution.scope??'global',plugin,config:effective?.config??override.config??{},disabled:true};
   if(index>=0)result[index]=row;else result.push(row);
   retained.push({id:contribution.id,scope:contribution.scope??'global',plugin,wasDisabled:!effective});
  }
  return{definition:next,patch:result,retained};
 }finally{rmSync(temporary,{recursive:true,force:true});}
}
