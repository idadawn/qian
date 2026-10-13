import {parseSurface} from '@qian/surfaces';
import type {ResolvedMarketplacePlugin} from './marketplaces.js';
import {MarketplaceFailure} from './marketplaces.js';
import {snapshot,sha256,type ContentSnapshot} from './marketplace-files.js';
import {prepareComponentDelivery,type ComponentDelivery} from './component-delivery.js';
import {fetchPackageComponents} from './package-components.js';
import {packageIdentity} from './package-references.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';
import {verifyDeclaredPackageDigests,type PackageArtifactEvidence} from './package-integrity.js';

export type {PackageArtifactEvidence} from './package-integrity.js';
export interface PackageDelivery {resolved:ResolvedMarketplacePlugin;delivery:ComponentDelivery;artifacts:PackageArtifactEvidence[]}
function manifest(content:ContentSnapshot):InstallObject {
 const bytes=content.files.get('.qian-plugin/plugin.json');if(!bytes)installFailure('package-manager','包内组件清单缺失。');
 const parsed=parseSurface('.qian-plugin/plugin.json',bytes.toString('utf8'));
 if(!parsed.syntax||parsed.diagnostics.some(item=>item.severity==='error'))throw new MarketplaceFailure([...parsed.diagnostics]);
 const result=installObject(parsed.syntax.data);
 if(typeof result.id!=='string'||!/^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+$/.test(result.id)||typeof result.version!=='string')installFailure('package-manager','包内组件标识无效。');
 return result;
}
function addComponent(files:Map<string,Buffer>,content:ContentSnapshot,rootId:string):{id:string;version:string} {
 const value=manifest(content),id=String(value.id),version=String(value.version);
 const existing=[...files].filter(([name])=>name==='.qian-plugin/plugin.json'||name.startsWith('components/')&&name.endsWith('/.qian-plugin/plugin.json')).find(([,bytes])=>{
  const parsed=parseSurface('.qian-plugin/plugin.json',bytes.toString('utf8'));return parsed.syntax&&installObject(parsed.syntax.data).id===id;
 });
 const prefix=id===rootId?'':existing?existing[0].slice(0,-'.qian-plugin/plugin.json'.length):'components/'+id+'/';
 if(existing){
  const original=parseSurface('.qian-plugin/plugin.json',existing[1].toString('utf8'));
  if(!original.syntax||installObject(original.syntax.data).version!==version)installFailure('package-manager','包内组件版本与已有声明冲突。');
 }
 for(const[name,bytes]of content.files){
  const target=prefix+name,old=files.get(target);
  if(old&&sha256(old)!==sha256(bytes))installFailure('package-manager','包内组件内容与市场或其它包中的同名内容不一致。');
  files.set(target,bytes);
 }
 return{id,version};
}

/** 从声明引用的真实包补齐组件内容，再走同一个组件树验证器。 */
export async function resolvePackageDelivery(root:string,source:ResolvedMarketplacePlugin,profile:string,forceProfile=false,metadataOnly=false):Promise<PackageDelivery> {
 let resolved=source,delivery=prepareComponentDelivery(source,profile,forceProfile);const seen=new Set<string>(),artifacts:PackageArtifactEvidence[]=[];
 for(let round=0;round<32;round++){
  verifyDeclaredPackageDigests(delivery.references,artifacts);
  const requested=delivery.references.filter(reference=>!seen.has(packageIdentity(reference)+'@'+reference.version));
  if(!requested.length){verifyDeclaredPackageDigests(delivery.references,artifacts,true);return{resolved:{...resolved,packages:delivery.packages},delivery,artifacts};}
  if(seen.size+requested.length>256)installFailure('package-manager','组件包引用超过 256 个，拒绝继续递归获取。');
  for(const reference of requested)seen.add(packageIdentity(reference)+'@'+reference.version);
  const fetched=await fetchPackageComponents(root,{nuget:requested.filter(item=>item.kind==='nuget').map(item=>item.specification),npm:requested.filter(item=>item.kind==='npm').map(item=>item.specification)},metadataOnly);
  try{
   artifacts.push(...fetched.artifacts);verifyDeclaredPackageDigests(delivery.references,artifacts);
   const files=new Map(resolved.content.files);
   for(const item of fetched.components){
    const identity=addComponent(files,item.component,source.component.id);
    const evidence=artifacts.find(artifact=>packageIdentity(artifact)===packageIdentity(item.reference)&&artifact.version===item.reference.version)!;
    Object.assign(evidence,{componentId:identity.id,componentVersion:identity.version,contentDigest:item.component.digest});
   }
   const content=snapshot(files);resolved={...resolved,content,files:content.records};
   delivery=prepareComponentDelivery(resolved,profile,forceProfile);
  }finally{fetched.dispose();}
 }
 installFailure('package-manager','组件包引用递归过深。');
}
