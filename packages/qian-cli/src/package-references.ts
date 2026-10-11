import {valid} from 'semver';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';

export type PackageKind='nuget'|'npm';
export interface PackageReference {kind:PackageKind;name:string;version:string;specification:string}
export interface OwnedPackageReference extends PackageReference {owners:string[]}
export interface ComponentPackages {id:string;packages:{nuget:readonly string[];npm:readonly string[]}}

function invalid(pointer:string,message:string):never {
 throw new MarketplaceFailure([marketDiagnostic('.qian-plugin/plugin.json',pointer,message,'SURFACE_SCHEMA_INVALID')]);
}

/** 包名与精确版本在启动包管理器前校验；不接受命令参数、文件、Git 或版本范围。 */
export function parsePackageReference(kind:PackageKind,specification:string,pointer='/runtime/'+kind):PackageReference {
 if(typeof specification!=='string')invalid(pointer,'代码依赖必须为包名与精确版本。');
 const separator=specification.lastIndexOf('@');
 if(separator<=0)invalid(pointer,'代码依赖缺少包名或精确版本。');
 const name=specification.slice(0,separator),version=specification.slice(separator+1);
 if(kind==='nuget') {
  if(!/^[A-Za-z0-9_][A-Za-z0-9._-]{0,99}$/.test(name))invalid(pointer,'NuGet 包名无效。');
 }else if(kind==='npm') {
  if(name.length>214||!/^(?:@[a-z0-9][a-z0-9._-]*\/)?[a-z0-9][a-z0-9._-]*$/.test(name))invalid(pointer,'npm 包名无效。');
 }else invalid(pointer,'不支持的包类型。');
 if(!/^[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/.test(version)||valid(version)===null)
  invalid(pointer,'代码依赖必须固定到合法的精确版本；不能使用范围、路径或 URL。');
 return{kind,name,version,specification};
}

/** NuGet 包名不区分大小写；npm 包名按其规范保持大小写敏感。 */
export function packageIdentity(reference:Pick<PackageReference,'kind'|'name'>):string {
 return reference.kind+':'+(reference.kind==='nuget'?reference.name.toLowerCase():reference.name);
}

/** 汇总安装后仍存活的组件，先发现共享包冲突，再允许事务修改宿主。 */
export function collectPackageReferences(components:readonly ComponentPackages[]):OwnedPackageReference[] {
 const references=new Map<string,OwnedPackageReference>();
 for(const component of components) {
  for(const kind of ['nuget','npm'] as const) {
   const packages=component.packages?.[kind];
   if(!Array.isArray(packages))invalid('/runtime/'+kind,'组件的包引用必须为数组。');
   for(const [index,specification]of packages.entries()) {
    const reference=parsePackageReference(kind,specification,'/runtime/'+kind+'/'+index),key=packageIdentity(reference);
    const current=references.get(key);
    if(current&&current.version!==reference.version)
     throw new MarketplaceFailure([marketDiagnostic('.qian/lock.json','/plugins','组件对同一个代码包要求不同的精确版本；请先协调组件版本。')]);
    if(current) { if(!current.owners.includes(component.id))current.owners.push(component.id); }
    else references.set(key,{...reference,owners:[component.id]});
   }
  }
 }
 return [...references.values()].map(reference=>({...reference,owners:reference.owners.sort()}))
  .sort((a,b)=>packageIdentity(a)<packageIdentity(b)?-1:packageIdentity(a)>packageIdentity(b)?1:0);
}
