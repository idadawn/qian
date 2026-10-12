import {existsSync,readFileSync,writeFileSync,mkdirSync,renameSync,unlinkSync,openSync,closeSync} from 'node:fs';
import path from 'node:path';
import {randomBytes} from 'node:crypto';
import {valid} from 'semver';
import {parseSurface,type JsonValue} from '@qian/surfaces';
import {assertNoLinks,within,readDirectory} from './marketplace-files.js';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import type {ComponentPackages} from './package-references.js';
import {collectPackageReferences} from './package-references.js';
import type {PackageCommandFailure} from './package-process.js';

export type InstallObject={[key:string]:JsonValue};
export const installObject=(value:unknown):InstallObject=>value!==null&&typeof value==='object'&&!Array.isArray(value)?value as InstallObject:{};
export function installFailure(file:string,message:string):never {throw new MarketplaceFailure([marketDiagnostic(file,'',message)]);}
export function readInstallFile(file:string):JsonValue {
 assertNoLinks(file);const result=parseSurface(file,readFileSync(file,'utf8'));
 if(!result.syntax||result.diagnostics.some(item=>item.severity==='error'))throw new MarketplaceFailure([...result.diagnostics]);
 return result.syntax.data;
}
export function writeInstallFile(file:string,bytes:Buffer):void {
 assertNoLinks(file);mkdirSync(path.dirname(file),{recursive:true});const temporary=file+'.'+randomBytes(6).toString('hex')+'.tmp';
 try{writeFileSync(temporary,bytes,{flag:'wx',mode:0o600});renameSync(temporary,file);}finally{if(existsSync(temporary))unlinkSync(temporary);}
}
export function readInstalledState(root:string):{file:string;lock:InstallObject;plugins:InstallObject[]} {
 const file=path.join(root,'.qian/lock.json');assertNoLinks(file);
 const lock=existsSync(file)?installObject(readInstallFile(file)):{plugins:[]};
 if(!Array.isArray(lock.plugins))installFailure(file,'lock.plugins 必须为数组。');
 const plugins=lock.plugins.map(installObject),ids=new Set<string>();
 for(const plugin of plugins){
  if(typeof plugin.id!=='string'||!/^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+$/.test(plugin.id)||ids.has(plugin.id)||
   typeof plugin.version!=='string'||valid(plugin.version)===null||typeof plugin.digest!=='string'||!/^sha256:[0-9a-f]{64}$/.test(plugin.digest))
   installFailure(file,'安装锁中的组件标识、版本、摘要或唯一性无效。');
  ids.add(plugin.id);
 }
 return{file,lock,plugins};
}
export function installedPackageSets(plugins:readonly InstallObject[]):ComponentPackages[] {
 return plugins.map(plugin=>{
  const packages=installObject(plugin.packages);
  if(typeof plugin.id!=='string'||!Array.isArray(packages.nuget)||!Array.isArray(packages.npm))installFailure('.qian/lock.json','组件包引用记录无效。');
  return{id:plugin.id,packages:{nuget:packages.nuget as string[],npm:packages.npm as string[]}};
 });
}
/** 卸载后仅保留定位信息，配置仍只在原 profile patch 中。 */
export function removedInstallationEntries(lock:InstallObject):InstallObject[] {
 if(lock.removedEntries===undefined)return[];
 if(!Array.isArray(lock.removedEntries))installFailure('.qian/lock.json','已移除条目记录无效。');
 return lock.removedEntries.map(value=>{
  const entry=installObject(value);
  if(typeof entry.id!=='string'||!/^[a-z][a-z0-9-]*(?:\.[a-z][a-z0-9-]*)+$/.test(entry.id)||
   typeof entry.version!=='string'||valid(entry.version)===null||typeof entry.profile!=='string'||!/^[a-z][a-z0-9-]*$/.test(entry.profile)||
   entry.scope!=='global'||typeof entry.entryId!=='string'||!/^[a-z][a-z0-9_.-]*$/.test(entry.entryId))installFailure('.qian/lock.json','已移除条目标识无效。');
  return{...entry};
 });
}
export function verifyInstalledComponent(root:string,plugin:InstallObject):InstallObject {
 const directory=within(root,'.qian/plugins/'+plugin.id),snapshot=readDirectory(directory);
 if(!Array.isArray(plugin.files))installFailure('.qian/lock.json','组件文件记录无效。');
 const expected=plugin.files.map(value=>{const file=installObject(value);return[file.path,file.bytes,file.sha256];});
 const actual=snapshot.records.map(file=>[file.path,file.bytes,file.sha256]);
 if(JSON.stringify(expected)!==JSON.stringify(actual))installFailure(directory,'已安装文件被修改、缺失或增加，拒绝丢弃本地内容。');
 const bytes=snapshot.files.get('.qian-plugin/plugin.json');if(!bytes)installFailure(directory,'组件清单缺失。');
 const parsed=parseSurface('.qian-plugin/plugin.json',bytes.toString('utf8'));
 if(!parsed.syntax||parsed.diagnostics.some(item=>item.severity==='error'))throw new MarketplaceFailure([...parsed.diagnostics]);
 return installObject(parsed.syntax.data);
}
export interface InstallationLease {owner:string;release():void;preserveProcess(error:PackageCommandFailure):void}
export function acquireInstallationLease(root:string):InstallationLease {
 const file=path.join(root,'.qian/.install.lock'),owner=randomBytes(16).toString('hex');assertNoLinks(file);mkdirSync(path.dirname(file),{recursive:true});
 try{const descriptor=openSync(file,'wx',0o600);try{writeFileSync(descriptor,JSON.stringify({pid:process.pid,id:owner}));}finally{closeSync(descriptor);}}
 catch{installFailure(file,'已有安装事务或残留锁，检查日志后再继续。');}
 const owned=():InstallObject|undefined=>{try{assertNoLinks(file);if(!existsSync(file))return;const value=JSON.parse(readFileSync(file,'utf8'));if(value.id===owner&&value.pid===process.pid)return value;}catch{}return undefined;};
 return{owner,release(){const value=owned();if(!value||value.pendingProcess)return;
   if(typeof value.journal==='string'){
    try{const journal=installObject(readInstallFile(within(root,value.journal)));if(!['committed','rolled-back'].includes(String(journal.state)))return;}catch{return;}
   }
   unlinkSync(file);
  },preserveProcess(error){const value=owned();if(value)writeInstallFile(file,Buffer.from(JSON.stringify({...value,pendingProcess:{pid:error.pid??null,tool:error.tool}})));}};
}

function displayedSource(value:JsonValue|undefined):InstallObject {
 const source=installObject(value),result:InstallObject={};
 for(const key of ['type','path','url','repo','ref','digest'])if(typeof source[key]==='string'){
  let text=source[key] as string;
  if(key==='url'||key==='repo'){
   try{const url=new URL(text);url.username='';url.password='';url.search='';url.hash='';text=url.toString();}catch{text='[invalid-url]';}
  }
  result[key]=text;
 }
 return result;
}
/** 列表只输出已知来源字段，不输出配置或任意 lock 扩展内容。 */
export function listInstalledComponents(root:string):{plugins:InstallObject[]} {
 const plugins=readInstalledState(path.resolve(root)).plugins;collectPackageReferences(installedPackageSets(plugins));
 return{plugins:plugins.map(plugin=>{
  if(typeof plugin.market!=='string'||!/^[a-z][a-z0-9-]*$/.test(plugin.market)||!Array.isArray(plugin.entries)||!Array.isArray(plugin.surfaces))installFailure('.qian/lock.json','安装来源或条目记录无效。');
  const entries=plugin.entries.map(value=>{
   const entry=installObject(value);
   if(typeof entry.id!=='string'||!/^[a-z][a-z0-9_.-]*$/.test(entry.id)||typeof entry.profile!=='string'||!/^[a-z][a-z0-9-]*$/.test(entry.profile)||
    typeof entry.scope!=='string'||! /^(?:global|(?:tenant|app|agent-session):[A-Za-z0-9_.-]+)$/.test(entry.scope))installFailure('.qian/lock.json','安装条目标识无效。');
   return{id:entry.id,profile:entry.profile,scope:entry.scope,...entry.kind==='bundle'?{kind:'bundle'}:{}};
  });
  if(plugin.surfaces.some(value=>typeof value!=='string'||!/^[A-Za-z][A-Za-z0-9-]{0,63}$/.test(value)))installFailure('.qian/lock.json','表面列表无效。');
  const packages=installedPackageSets([plugin])[0].packages;
  return{id:plugin.id,version:plugin.version,market:plugin.market,source:displayedSource(plugin.source),digest:plugin.digest,
   surfaces:plugin.surfaces,entries,packages:{nuget:[...packages.nuget],npm:[...packages.npm]}};
 })};
}
