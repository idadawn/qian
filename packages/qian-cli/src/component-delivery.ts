import {mkdtempSync,realpathSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {type ParsedComponent,type JsonValue,type SurfaceDiagnostic} from '@qian/surfaces';
import {validateOfflineComponent} from './component-validation.js';
import {materialize,safePath,type FileRecord} from './marketplace-files.js';
import {MarketplaceFailure,type ResolvedMarketplacePlugin} from './marketplaces.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';
import {collectRuntimePackageReferences,type DeclaredPackageReference} from './package-references.js';

export interface DeliveredComponent {directory:string;component:ParsedComponent;manifest:InstallObject}
export interface ComponentDelivery {
 components:DeliveredComponent[];files:FileRecord[];packages:{nuget:string[];npm:string[]};references:DeclaredPackageReference[];
 composition:boolean;regularEntry:boolean;patch:JsonValue[];requirements:InstallObject;diagnostics:SurfaceDiagnostic[];
}

/** 验证并选择整个组件树的声明表面；源文件不执行，内联组件不继承一个免检标记。 */
export function prepareComponentDelivery(resolved:ResolvedMarketplacePlugin,profile:string,forceProfile=false):ComponentDelivery {
 const temporary=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-delivery-'));
 try{
  materialize(resolved.content,temporary);
  const checked=validateOfflineComponent(temporary,{profile});if(!checked.isValid||!checked.component)throw new MarketplaceFailure([...checked.diagnostics]);
  const components:DeliveredComponent[]=[{directory:'',component:checked.component,manifest:installObject(checked.component.manifest)}],diagnostics:SurfaceDiagnostic[]=[...checked.diagnostics];
  const folders=[...resolved.content.files.keys()].filter(name=>name.startsWith('components/')&&name.endsWith('/.qian-plugin/plugin.json'))
   .map(name=>name.slice(0,-'/.qian-plugin/plugin.json'.length)).sort();
  for(const directory of folders){
   const result=validateOfflineComponent(path.join(temporary,directory),{profile});diagnostics.push(...result.diagnostics);
   if(!result.isValid||!result.component)throw new MarketplaceFailure([...result.diagnostics]);
   components.push({directory,component:result.component,manifest:installObject(result.component.manifest)});
  }
  const names=new Set<string>(),identities=new Set<string>(),requirements:InstallObject={};
  for(const entry of components){
   const identity=entry.component.id+'@'+String(entry.manifest.version),prefix=entry.directory?entry.directory+'/':'';
   if(identities.has(identity))installFailure('.qian-plugin/plugin.json','组件树存在重复的组件版本。');identities.add(identity);
   const policy=installObject(entry.manifest.policy),runtime=installObject(entry.manifest.runtime);
   if(resolved.trust==='unverified'&&(entry.manifest.runtime!==undefined||policy.tier!=='community'))
    installFailure(prefix+'.qian-plugin/plugin.json','未验证来源的全部子组件都必须是 community 无代码组件。');
   if(Array.isArray(policy.profiles)&&policy.profiles.length&&!policy.profiles.includes(profile)&&!forceProfile)
    installFailure(prefix+'.qian-plugin/plugin.json','组件树包含不允许装入所选 profile 的组件，需显式 --force-profile。','SURFACE_PROFILE_DENIED');
   if(runtime.form!==undefined&&!['module','ui','service'].includes(String(runtime.form)))installFailure(prefix+'.qian-plugin/plugin.json','组件树包含 S1 尚不支持的代码形态。');
   requirements[entry.component.id]={permissions:policy.permissions??{},egress:policy.egress??[]};
   names.add(prefix+'.qian-plugin/plugin.json');
   const tree=(relative:string):void=>{
    const name=safePath(relative);
    for(const file of resolved.content.files.keys())if(file===prefix+name||file.startsWith(prefix+name+'/'))names.add(file);
   };
   for(const document of entry.component.documents){
    names.add(prefix+document.file);
    if(document.kind==='skill')tree(path.posix.dirname(document.file));
    for(const match of document.body.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)){
     const target=match[1].split('#')[0];if(target&&!/^[a-z][a-z0-9+.-]*:/i.test(target))tree(path.posix.normalize(path.posix.join(path.posix.dirname(document.file),target)));
    }
    if(document.kind==='command')for(const match of document.body.matchAll(/(?:^|\s)@([A-Za-z0-9_./-]+)/g))tree(match[1]);
   }
   for(const key of ['metadata','schemas','ui'])if(typeof installObject(entry.manifest.surfaces)[key]==='string')tree(String(installObject(entry.manifest.surfaces)[key]));
   if(typeof runtime.configSchema==='string')tree(runtime.configSchema);
   for(const name of resolved.content.files.keys())if(name.startsWith(prefix)&&/^(?:README(?:\.md)?|AGENTS\.md|(?:assets|metadata|schemas)\/)/.test(name.slice(prefix.length)))names.add(name);
  }
  const referenced=collectRuntimePackageReferences(components.map(entry=>({id:entry.component.id,runtime:installObject(entry.manifest.runtime)})));
  const patch=components[0].component.documents.find(document=>document.kind==='patch');
  const composition=!!patch,regularEntry=!composition||installObject(components[0].manifest.runtime).form!==undefined||components[0].component.documents.some(document=>document.kind!=='patch');
  return{components,files:resolved.files.filter(file=>names.has(file.path)),references:referenced,packages:{nuget:referenced.filter(item=>item.kind==='nuget').map(item=>item.specification),
   npm:referenced.filter(item=>item.kind==='npm').map(item=>item.specification)},composition,regularEntry,patch:patch?patch.data as JsonValue[]:[],requirements,diagnostics};
 }finally{rmSync(temporary,{recursive:true,force:true});}
}
