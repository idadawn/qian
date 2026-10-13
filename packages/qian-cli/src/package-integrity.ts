import {existsSync,readFileSync,realpathSync} from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {within,assertNoLinks} from './marketplace-files.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';
import {collectPackageReferences,packageIdentity,parsePackageReference,type PackageKind,type DeclaredPackageReference} from './package-references.js';
import type {PackageWorkspace} from './package-manager.js';

export interface PackageArtifactEvidence {
 kind:PackageKind;name:string;version:string;digest:string;integrity?:string;
 componentId?:string;componentVersion?:string;contentDigest?:string;
}

export function archiveEvidence(kind:PackageKind,name:string,version:string,bytes:Buffer):PackageArtifactEvidence {
 return{kind,name,version,digest:'sha256:'+createHash('sha256').update(bytes).digest('hex'),
  ...(kind==='npm'?{integrity:'sha512-'+createHash('sha512').update(bytes).digest('base64')}:{})};
}

/** 每轮发现组件后重新检查，防止后发现的子组件给已取包补入冲突摘要。 */
export function verifyDeclaredPackageDigests(references:readonly DeclaredPackageReference[],artifacts:readonly PackageArtifactEvidence[],complete=false):void {
 for(const reference of references){
  const artifact=artifacts.find(item=>packageIdentity(item)===packageIdentity(reference)&&item.version===reference.version);
  if(!artifact){if(complete)installFailure('package-manager','声明的代码包缺少归档摘要。');continue;}
  if(reference.digest&&reference.digest!==artifact.digest)installFailure('.qian-plugin/plugin.json','代码包实际归档与 runtime 声明的 digest 不一致。');
 }
}

/** 使用安装锁中已验证的有限字段；共享包不可在另一个组件安装时被换成同版本不同内容。 */
export function installedPackageArtifacts(plugins:readonly InstallObject[]):PackageArtifactEvidence[] {
 const result=new Map<string,PackageArtifactEvidence>();
 for(const plugin of plugins){
  if(plugin.packageArtifacts===undefined)continue; // 早期草稿锁无此证据，不能凭空补签名或摘要。
  if(!Array.isArray(plugin.packageArtifacts))installFailure('.qian/lock.json','代码包归档记录必须为数组。');
  const sets=installObject(plugin.packages),references=collectPackageReferences([{id:String(plugin.id),packages:{nuget:sets.nuget as string[],npm:sets.npm as string[]}}]);
  const keys=new Set<string>();
  for(const raw of plugin.packageArtifacts){
   const item=installObject(raw);
   if((item.kind!=='nuget'&&item.kind!=='npm')||typeof item.name!=='string'||typeof item.version!=='string'||typeof item.digest!=='string'||!/^sha256:[0-9a-f]{64}$/.test(item.digest))
    installFailure('.qian/lock.json','代码包归档身份或摘要无效。');
   const reference=parsePackageReference(item.kind,item.name+'@'+item.version),key=packageIdentity(reference);
   if(keys.has(key)||!references.some(value=>packageIdentity(value)===key&&value.version===item.version))installFailure('.qian/lock.json','代码包归档记录重复或没有对应的精确包引用。');
   keys.add(key);
   if(item.integrity!==undefined&&(item.kind!=='npm'||typeof item.integrity!=='string'||!/^sha512-[A-Za-z0-9+/]{86}==$/.test(item.integrity)))installFailure('.qian/lock.json','npm 归档完整性记录无效。');
   const value:PackageArtifactEvidence={kind:item.kind,name:item.name,version:item.version,digest:item.digest,...typeof item.integrity==='string'?{integrity:item.integrity}:{}};
   const previous=result.get(key);
   if(previous&&(previous.version!==value.version||previous.digest!==value.digest||previous.integrity!==value.integrity))installFailure('.qian/lock.json','不同组件对共享代码包记录了不一致的归档。');
   result.set(key,value);
  }
 }
 return [...result.values()];
}

function readObject(file:string):InstallObject {
 assertNoLinks(file);
 try{const value:unknown=JSON.parse(readFileSync(file,'utf8'));if(value&&typeof value==='object'&&!Array.isArray(value))return installObject(value);}catch{}
 installFailure('package-manager','包管理器还原记录缺失或无效。');
}

/** 对实际宿主再校验；预检下载与真实还原之间换包必须触发安装事务回滚。 */
export function verifyInstalledPackageArtifacts(workspace:PackageWorkspace,artifacts:readonly PackageArtifactEvidence[]):void {
 for(const artifact of artifacts){
  if(artifact.kind==='nuget'){
   if(!workspace.nuget)installFailure('package-manager','缺少 NuGet 宿主。');
   const assets=readObject(path.join(workspace.nuget.intermediateDirectory,'project.assets.json'));
   const library=installObject(Object.entries(installObject(assets.libraries)).find(([name])=>name.toLowerCase()===(artifact.name+'/'+artifact.version).toLowerCase())?.[1]);
   if(library.type!=='package'||typeof library.path!=='string')installFailure('package-manager','实际 NuGet 还原记录没有目标代码包。');
   let verified=false;
   for(const folder of Object.keys(installObject(assets.packageFolders))){
    if(!existsSync(folder))continue;
    const directory=within(realpathSync(folder),library.path);
    if(!existsSync(directory))continue;
    const archive=within(directory,artifact.name.toLowerCase()+'.'+artifact.version.toLowerCase()+'.nupkg');
    if(!existsSync(archive))installFailure('package-manager','实际 NuGet 包目录缺少归档，不能借用其它缓存的摘要。');
    if(archiveEvidence('nuget',artifact.name,artifact.version,readFileSync(archive)).digest!==artifact.digest)installFailure('package-manager','实际 NuGet 归档与预检或安装锁中的摘要不一致。');
    verified=true;break;
   }
   if(!verified)installFailure('package-manager','实际 NuGet 归档缺失，无法复核摘要。');
  }else{
   if(!workspace.npm||!artifact.integrity)installFailure('package-manager','npm 归档缺少可与实际还原关联的 SHA-512 证据。');
   const shrinkwrap=path.join(workspace.root,'npm-shrinkwrap.json'),lockFile=existsSync(shrinkwrap)?shrinkwrap:path.join(workspace.root,'package-lock.json');
   const lock=readObject(lockFile),entry=installObject(installObject(lock.packages)['node_modules/'+artifact.name]);
   if(entry.link===true||entry.version!==artifact.version||entry.integrity!==artifact.integrity)
    installFailure('package-manager','实际 npm 锁定归档与预检或安装锁中的摘要不一致。');
   // npm 的隐藏锁描述本次 node_modules 还原；不能只相信宿主顶层锁。
   const actual=readObject(path.join(workspace.root,'node_modules/.package-lock.json')),installed=installObject(installObject(actual.packages)['node_modules/'+artifact.name]);
   if(installed.link===true||installed.version!==artifact.version||installed.integrity!==artifact.integrity)
    installFailure('package-manager','实际 npm 安装记录与已验证归档不一致。');
  }
 }
}
