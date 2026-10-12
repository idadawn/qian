import {mkdtempSync,realpathSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {validateComponent,type ParsedComponent,type JsonValue,type SurfaceDiagnostic} from '@qian/surfaces';
import {materialize,type FileRecord} from './marketplace-files.js';
import {MarketplaceFailure,type ResolvedMarketplacePlugin} from './marketplaces.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';
import {collectPackageReferences} from './package-references.js';

export interface DeliveredComponent {directory:string;component:ParsedComponent;manifest:InstallObject}
export interface ComponentDelivery {
 components:DeliveredComponent[];files:FileRecord[];packages:{nuget:string[];npm:string[]};
 composition:boolean;regularEntry:boolean;patch:JsonValue[];requirements:InstallObject;diagnostics:SurfaceDiagnostic[];
}

/** 验证并选择整个组件树的声明表面；源文件不执行，内联组件不继承一个免检标记。 */
export function prepareComponentDelivery(resolved:ResolvedMarketplacePlugin,profile:string,forceProfile=false):ComponentDelivery {
 const temporary=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-delivery-'));
 try{
  materialize(resolved.content,temporary);
  const components:DeliveredComponent[]=[{directory:'',component:resolved.component,manifest:installObject(resolved.component.manifest)}],diagnostics:SurfaceDiagnostic[]=[];
  const folders=[...resolved.content.files.keys()].filter(name=>name.startsWith('components/')&&name.endsWith('/.qian-plugin/plugin.json'))
   .map(name=>name.slice(0,-'/.qian-plugin/plugin.json'.length)).sort();
  for(const directory of folders){
   const result=validateComponent(path.join(temporary,directory),{profile});diagnostics.push(...result.diagnostics);
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
    installFailure(prefix+'.qian-plugin/plugin.json','组件树包含不允许装入所选 profile 的组件，需显式 --force-profile。');
   if(runtime.form!==undefined&&!['module','ui','service'].includes(String(runtime.form)))installFailure(prefix+'.qian-plugin/plugin.json','组件树包含 S1 尚不支持的代码形态。');
   requirements[entry.component.id]={permissions:policy.permissions??{},egress:policy.egress??[]};
   names.add(prefix+'.qian-plugin/plugin.json');
   for(const document of entry.component.documents)names.add(prefix+document.file);
   for(const name of resolved.content.files.keys())if(name.startsWith(prefix)&&/^(?:README(?:\.md)?|AGENTS\.md|(?:assets|metadata|schemas)\/)/.test(name.slice(prefix.length)))names.add(name);
  }
  const referenced=collectPackageReferences(components.map(entry=>{
   const runtime=installObject(entry.manifest.runtime);
   return{id:entry.component.id,packages:{
    nuget:Array.isArray(runtime.nuget)?runtime.nuget.map(value=>{const item=installObject(value);return String(item.id??'')+'@'+String(item.version??'');}):[],
    npm:Array.isArray(runtime.npm)?runtime.npm.map(value=>{const item=installObject(value);return String(item.name??'')+'@'+String(item.version??'');}):[]}};
  }));
  const patch=resolved.component.documents.find(document=>document.kind==='patch');
  const composition=!!patch,regularEntry=!composition||installObject(installObject(resolved.component.manifest).runtime).form!==undefined;
  return{components,files:resolved.files.filter(file=>names.has(file.path)),packages:{nuget:referenced.filter(item=>item.kind==='nuget').map(item=>item.specification),
   npm:referenced.filter(item=>item.kind==='npm').map(item=>item.specification)},composition,regularEntry,patch:patch?patch.data as JsonValue[]:[],requirements,diagnostics};
 }finally{rmSync(temporary,{recursive:true,force:true});}
}
