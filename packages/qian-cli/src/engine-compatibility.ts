import {existsSync,readFileSync} from 'node:fs';
import path from 'node:path';
import {satisfies,valid,validRange} from 'semver';
import {parseSurface} from '@qian/surfaces';
import type {PackageWorkspace,PackageChanges} from './package-manager.js';
import {assertNoLinks,within,sha256} from './marketplace-files.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';
import {marketDiagnostic,MarketplaceFailure} from './marketplaces.js';

export interface EngineEvidence {engine:'qian'|'kun'|'yue';version:string;package:string;source:string}
export interface EnginePlan {evidence:EngineEvidence[];pending:EngineEvidence['engine'][]}
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
/** 候选包的独立依赖图不是宿主最终图；仅对精确直接引用给出预检结论，传递版本交给末检。 */
export function planEngineVerification(current:readonly EngineEvidence[],workspace?:PackageWorkspace,changes?:PackageChanges):EnginePlan {
 if(!workspace||!changes)return{evidence:[...current],pending:[]};
 const kind=(pkg:string)=>pkg.startsWith('@')?'npm':'nuget';
 const key=(pkg:string)=>kind(pkg)==='nuget'?pkg.toLowerCase():pkg;
 const planned=[...changes.add,...changes.ensure??[]],removed=new Set(changes.remove.map(item=>key(item.name)));
 const restoring=new Set([...planned,...changes.remove].map(item=>item.kind)),evidence:EngineEvidence[]=[];
 for(const item of current){
  const changed=planned.find(reference=>key(reference.name)===item.package);
  if(changed||removed.has(item.package))continue;
  const dependencies=kind(item.package)==='nuget'?workspace.nuget?.dependencies:workspace.npm?.dependencies;
  const direct=dependencies?.find(dependency=>key(dependency.name)===item.package);
  if(!restoring.has(kind(item.package))||direct&&valid(direct.version)&&direct.version===item.version)evidence.push(item);
 }
 for(const reference of planned){
  const pkg=key(reference.name),engine=runtimePackages[pkg];if(!engine)continue;
  if(!evidence.some(item=>item.package===pkg))evidence.push({engine,version:reference.version,package:pkg,source:'package-plan/'+reference.kind});
 }
 // 某生态发生还原时，其未由精确直接引用钉定的传递运行时可能变化；即使候选预检有版本也不能代替宿主结果。
 const possible:EngineEvidence['engine'][]=[...restoring.has('nuget')?['qian','kun'] as const:[],...restoring.has('npm')?['qian','yue'] as const:[]];
 const pending=[...new Set(possible)].filter(engine=>!evidence.some(item=>item.engine===engine));
 return{evidence,pending};
}
/** 实际 Runtime 改变时，只用安装锁中的清单与挂载记录定位额外受影响的组件/profile。 */
export function affectedEngineInstallations(root:string,plugins:readonly InstallObject[],before:readonly EngineEvidence[],after:readonly EngineEvidence[]):Map<string,Set<string>> {
 const versions=(values:readonly EngineEvidence[],engine:string)=>JSON.stringify(values.filter(item=>item.engine===engine).map(item=>item.package+'@'+item.version).sort());
 const changed=new Set([...before,...after].map(item=>item.engine).filter(engine=>versions(before,engine)!==versions(after,engine)));
 const affected=new Map<string,Set<string>>();if(!changed.size)return affected;
 for(const record of plugins){
  if(!Array.isArray(record.files))installFailure('.qian/lock.json','组件文件记录无效。','SURFACE_SCHEMA_INVALID');
  let relevant=false;
  for(const value of record.files){
   const item=installObject(value);if(typeof item.path!=='string'||item.path!=='.qian-plugin/plugin.json'&&!item.path.endsWith('/.qian-plugin/plugin.json'))continue;
   const relative='.qian/plugins/'+record.id+'/'+item.path,file=within(root,relative);let bytes:Buffer;
   try{bytes=readFileSync(file);}catch{installFailure(relative,'已安装清单缺失或不可读，不能判断运行时升级影响。','SURFACE_LOCAL_MODIFIED');}
   if(bytes.length!==item.bytes||sha256(bytes)!==item.sha256)installFailure(relative,'已安装清单被修改，不能判断运行时升级影响。','SURFACE_LOCAL_MODIFIED');
   const parsed=parseSurface(relative,bytes.toString('utf8'));
   if(!parsed.syntax||parsed.diagnostics.some(item=>item.severity==='error'))throw new MarketplaceFailure([...parsed.diagnostics]);
   const manifest=installObject(parsed.syntax.data),runtime=installObject(manifest.runtime),surfaces=installObject(manifest.surfaces);
   relevant=Object.keys(installObject(manifest.engines)).some(engine=>changed.has(engine as EngineEvidence['engine'])&&
    (engine!=='kun'||Array.isArray(runtime.nuget)&&runtime.nuget.length>0)&&(engine!=='yue'||surfaces.ui!==undefined));
   if(relevant)break;
  }
  if(!relevant)continue;
  if(!Array.isArray(record.entries))installFailure('.qian/lock.json','组件挂载记录无效。','SURFACE_SCHEMA_INVALID');
  for(const value of record.entries){const entry=installObject(value);
   if(typeof entry.profile!=='string'||!/^[a-z][a-z0-9-]*$/.test(entry.profile))installFailure('.qian/lock.json','组件 profile 记录无效。','SURFACE_SCHEMA_INVALID');
   const ids=affected.get(entry.profile)??new Set<string>();ids.add(String(record.id));affected.set(entry.profile,ids);
  }
 }
 return affected;
}
/** 无代码投影不再要求被移除的 Kun/UI 运行时，但 qian 范围仍约束承载表面的实际 Runtime。 */
export function verifyComponentEngines(manifest:InstallObject,evidence:readonly EngineEvidence[],file='.qian-plugin/plugin.json',pending:readonly EngineEvidence['engine'][]=[]):EngineEvidence['engine'][] {
 const runtime=installObject(manifest.runtime),surfaces=installObject(manifest.surfaces);
 const deferred:EngineEvidence['engine'][]=[];
 for(const[engine,range]of Object.entries(installObject(manifest.engines))){
  if(typeof range!=='string'||!validRange(range))throw new MarketplaceFailure([marketDiagnostic(file,'/engines/'+engine,'engines 必须为可解析的 SemVer 范围。','SURFACE_ENGINE_INCOMPATIBLE')]);
  if(engine==='kun'&&(!Array.isArray(runtime.nuget)||!runtime.nuget.length)||engine==='yue'&&surfaces.ui===undefined)continue;
  const versions=evidence.filter(item=>item.engine===engine);
  if(!versions.length&&pending.includes(engine as EngineEvidence['engine'])){deferred.push(engine as EngineEvidence['engine']);continue;}
  if(!versions.length)throw new MarketplaceFailure([marketDiagnostic(file,'/engines/'+engine,'无法从所选 profile 的实际宿主还原记录确认 '+engine+' 版本；先还原宿主运行时依赖。','SURFACE_ENGINE_INCOMPATIBLE')]);
  if(versions.some(item=>!satisfies(item.version,range)))throw new MarketplaceFailure([marketDiagnostic(file,'/engines/'+engine,'组件要求 '+engine+' '+range+'，宿主已确认或计划钉定版本为 '+[...new Set(versions.map(item=>item.version))].join('、')+'。','SURFACE_ENGINE_INCOMPATIBLE')]);
 }
 return deferred;
}
