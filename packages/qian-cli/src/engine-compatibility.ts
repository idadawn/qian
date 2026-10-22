import {existsSync,readFileSync} from 'node:fs';
import path from 'node:path';
import {satisfies,valid,validRange} from 'semver';
import type {PackageWorkspace} from './package-manager.js';
import {assertNoLinks} from './marketplace-files.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';
import {marketDiagnostic,MarketplaceFailure} from './marketplaces.js';

export interface EngineEvidence {engine:'qian'|'kun'|'yue';version:string;package:string;source:string}
const runtimePackages:Record<string,EngineEvidence['engine']>={'qian.runtime':'qian','kun.core':'kun','@qian/runtime':'qian','@idadawn/qian-runtime':'qian','@yue/core':'yue','@idadawn/yue-core':'yue'};
/** 版本只来自宿主实际还原结果与实际 npm 包，不能以 CLI 的 package.json 代替。 */
export function engineEvidenceFromAssets(data:unknown,source:string):EngineEvidence[] {
 const result:EngineEvidence[]=[];
 for(const[name,entry]of Object.entries(installObject(installObject(data).libraries))){
  if(!['package','project'].includes(String(installObject(entry).type)))continue;
  const slash=name.lastIndexOf('/'),pkg=name.slice(0,slash).toLowerCase(),version=name.slice(slash+1),engine=runtimePackages[pkg];
  if(engine&&valid(version))result.push({engine,version,package:pkg,source});
 }
 return result;
}
export function readHostEngineVersions(root:string,workspace?:PackageWorkspace):EngineEvidence[] {
 const assets=workspace?.nuget?path.join(workspace.nuget.intermediateDirectory,'project.assets.json'):path.join(root,'obj/project.assets.json');
 assertNoLinks(assets);const evidence:EngineEvidence[]=[];
 if(existsSync(assets)){
  let data:unknown;try{data=JSON.parse(readFileSync(assets,'utf8'));}catch{installFailure('project.assets.json','宿主运行时版本记录无效。','SURFACE_ENGINE_INCOMPATIBLE');}
  evidence.push(...engineEvidenceFromAssets(data,path.relative(root,assets).split(path.sep).join('/')));
 }
 for(const[pkg,engine]of Object.entries(runtimePackages).filter(([name])=>name.startsWith('@'))){
  const relative='node_modules/'+pkg+'/package.json',file=path.join(root,relative);assertNoLinks(file);if(!existsSync(file))continue;
  let data:InstallObject;try{data=installObject(JSON.parse(readFileSync(file,'utf8')));}catch{installFailure(relative,'实际 npm 运行时包声明无效。','SURFACE_ENGINE_INCOMPATIBLE');}
  if(data.name!==pkg||typeof data.version!=='string'||!valid(data.version))installFailure(relative,'实际 npm 运行时包身份或版本无效。','SURFACE_ENGINE_INCOMPATIBLE');
  evidence.push({engine,version:data.version,package:pkg,source:relative});
 }
 return evidence;
}
export function mergeEngineEvidence(current:readonly EngineEvidence[],planned:readonly EngineEvidence[]):EngineEvidence[] {
 const replaced=new Set(planned.map(item=>item.package));
 return [...current.filter(item=>!replaced.has(item.package)),...planned].filter((item,index,all)=>all.findIndex(other=>other.package===item.package&&other.version===item.version)===index);
}
/** 无代码投影不再要求被移除的 Kun/UI 运行时，但 qian 范围仍约束承载表面的实际 Runtime。 */
export function verifyComponentEngines(manifest:InstallObject,evidence:readonly EngineEvidence[],file='.qian-plugin/plugin.json',allowPendingRestore=false):void {
 const runtime=installObject(manifest.runtime),surfaces=installObject(manifest.surfaces);
 for(const[engine,range]of Object.entries(installObject(manifest.engines))){
  if(typeof range!=='string'||!validRange(range))throw new MarketplaceFailure([marketDiagnostic(file,'/engines/'+engine,'engines 必须为可解析的 SemVer 范围。','SURFACE_ENGINE_INCOMPATIBLE')]);
  if(engine==='kun'&&(!Array.isArray(runtime.nuget)||!runtime.nuget.length)||engine==='yue'&&surfaces.ui===undefined)continue;
  const versions=evidence.filter(item=>item.engine===engine);
  if(!versions.length&&allowPendingRestore)continue;
  if(!versions.length)throw new MarketplaceFailure([marketDiagnostic(file,'/engines/'+engine,'无法从所选 profile 的实际宿主还原记录确认 '+engine+' 版本；先还原宿主运行时依赖。','SURFACE_ENGINE_INCOMPATIBLE')]);
  if(versions.some(item=>!satisfies(item.version,range)))throw new MarketplaceFailure([marketDiagnostic(file,'/engines/'+engine,'组件要求 '+engine+' '+range+'，宿主实际版本为 '+[...new Set(versions.map(item=>item.version))].join('、')+'。','SURFACE_ENGINE_INCOMPATIBLE')]);
 }
}
