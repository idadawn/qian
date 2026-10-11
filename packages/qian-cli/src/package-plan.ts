import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import {collectPackageReferences,packageIdentity,parsePackageReference,type ComponentPackages,type PackageReference} from './package-references.js';
import {workspaceDependency,type PackageWorkspace,type PackageChanges} from './package-manager.js';

/** managed=false 表示原本属于宿主；最后一个组件移除时仍须保留。 */
export interface PackageOwnership extends PackageReference {managed:boolean}
export interface PackagePlan {changes:PackageChanges;state:PackageOwnership[]}
function failure(message:string):never {throw new MarketplaceFailure([marketDiagnostic('.qian/lock.json','/packageState',message)]);}

/** 在最终组件集合上计算共享依赖，只有 CLI 创建的直接引用才由 CLI 移除。 */
export function planPackageChanges(workspace:PackageWorkspace,before:readonly ComponentPackages[],after:readonly ComponentPackages[],state:readonly PackageOwnership[]):PackagePlan {
 const previous=new Map(collectPackageReferences(before).map(item=>[packageIdentity(item),item]));
 const next=new Map(collectPackageReferences(after).map(item=>[packageIdentity(item),item]));
 const ownership=new Map<string,PackageOwnership>();
 for(const item of state){
  if(typeof item.managed!=='boolean')failure('代码包归属记录无效。');
  const parsed=parsePackageReference(item.kind,item.specification),key=packageIdentity(parsed);
  if(parsed.name!==item.name||parsed.version!==item.version||ownership.has(key))failure('代码包归属记录不一致或重复。');
  ownership.set(key,{...parsed,managed:item.managed});
 }
 if([...ownership.keys()].some(key=>!previous.has(key)))failure('代码包归属记录没有对应的已安装组件，需先修复安装状态。');
 for(const[key,reference]of previous){
  const item=ownership.get(key),current=workspaceDependency(workspace,reference);
  if(!item||item.version!==reference.version||current?.version!==reference.version)failure('宿主依赖与安装锁不一致，拒绝覆盖手工修改。');
 }
 const add:PackageReference[]=[],remove:PackageReference[]=[],result:PackageOwnership[]=[];
 for(const[key,reference]of next){
  const owned=ownership.get(key),current=workspaceDependency(workspace,reference);
  if(current&&reference.kind==='npm'&&current.section!=='dependencies')failure('组件需要 npm 运行时依赖，不能借用开发、可选或 peer 依赖。');
  if(owned){
   if(owned.version!==reference.version){
    if(!owned.managed)failure('组件升级会改写宿主原有依赖，请先协调宿主版本。');
    add.push(reference);
   }
   result.push({...reference,managed:owned.managed});
  }else if(current){
   if(current.version!==reference.version)failure('宿主已声明不同版本的依赖，拒绝静默改写。');
   result.push({...reference,managed:false});
  }else{add.push(reference);result.push({...reference,managed:true});}
 }
 for(const[key,reference]of previous)if(!next.has(key)&&ownership.get(key)!.managed)remove.push(reference);
 return{changes:{add,remove,ensure:[...next.values()]},state:result};
}
