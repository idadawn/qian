import path from 'node:path';
import {stringify} from 'yaml';
import type {JsonValue,SurfaceDocument} from '@qian/surfaces';
import {snapshot,safePath} from './marketplace-files.js';
import type {ResolvedMarketplacePlugin} from './marketplaces.js';
import {prepareComponentDelivery,type ComponentDelivery} from './component-delivery.js';
import {installObject,installFailure,type InstallObject} from './installed-components.js';

export const surfaceNames=['skills','agents','commands','hooks','mcpServers','tools','ui','metadata','schemas','patch'] as const;
export type SurfaceName=typeof surfaceNames[number];
export interface SurfaceSelection {noCode?:boolean;surfaces?:readonly SurfaceName[];members?:Partial<Record<SurfaceName,readonly string[]>>}
export const surfaceName=(document:SurfaceDocument):SurfaceName=>({skill:'skills',agent:'agents',command:'commands',tool:'tools',mcp:'mcpServers'} as Record<string,SurfaceName>)[document.kind]??document.kind as SurfaceName;

export function parseSurfaceNames(value:string):SurfaceName[] {
 const values=value.split(',');
 if(!values.length||values.some(name=>!surfaceNames.includes(name as SurfaceName)))installFailure('argv','表面选择包含空值或未知名称。');
 return [...new Set(values)] as SurfaceName[];
}

/** 来源整体已验证后才投影；所得清单及全部引用再经同一个表面验证器。 */
export function selectComponentSurfaces(source:ResolvedMarketplacePlugin,original:ComponentDelivery,selection:SurfaceSelection,profile:string,forceProfile=false):{resolved:ResolvedMarketplacePlugin;delivery:ComponentDelivery} {
 if(!selection.noCode&&!selection.surfaces&&!selection.members)return{resolved:source,delivery:original};
 const selected=new Set<SurfaceName>(selection.surfaces??surfaceNames.filter(name=>!selection.noCode||name!=='ui'));
 if(selection.noCode&&selected.has('ui'))installFailure('argv','ui 需要代码运行时，不能与 --no-code 一起选择。');
 if(!selected.size||[...selected].some(name=>!surfaceNames.includes(name)))installFailure('argv','表面选择必须包含已知名称。');
 const files=new Map<string,Buffer>(),manifests=new Map<string,InstallObject>(),active=new Set<string>();
 const root=original.components[0],composition=original.composition;
 for(const entry of original.components){
  const prefix=entry.directory?entry.directory+'/':'',manifest=structuredClone(entry.manifest),pointers=installObject(manifest.surfaces),runtime=installObject(manifest.runtime);
  const docs=entry.component.documents.filter(document=>{
   const kind=surfaceName(document),members=selection.members?.[kind];
   return kind!=='patch'&&selected.has(kind)&&(!members||document.kind==='mcp'||members.includes(document.name));
  });
  const excluded=new Set(entry.component.documents.filter(document=>!docs.includes(document)).map(document=>document.file));
  const add=(relative:string):void=>{
   const name=safePath(relative);if(name==='.qian-plugin/plugin.json'||name.startsWith('components/')||excluded.has(name))return;
   const value=source.content.files.get(prefix+name);if(value)files.set(prefix+name,value);
  };
  const tree=(relative:string):void=>{
   const name=safePath(relative.replace(/^\.\//,''));
   for(const file of source.content.files.keys())if(file===prefix+name||file.startsWith(prefix+name+'/'))add(file.slice(prefix.length));
  };
  const copyLinks=(document:SurfaceDocument):void=>{
   for(const match of document.body.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)){
    const target=match[1].split('#')[0];if(!target||/^[a-z][a-z0-9+.-]*:/i.test(target))continue;
    tree(path.posix.normalize(path.posix.join(path.posix.dirname(document.file),target)));
   }
   if(document.kind==='command')for(const match of document.body.matchAll(/(?:^|\s)@([A-Za-z0-9_./-]+)/g))tree(match[1]);
  };
  for(const document of docs){
   if(document.kind==='skill')tree(path.posix.dirname(document.file));else add(document.file);
   copyLinks(document);
   if(document.kind==='mcp'&&selection.members?.mcpServers){
    const value=installObject(document.data),servers=installObject(value.mcpServers),names=selection.members.mcpServers;
    const filtered=Object.fromEntries(Object.entries(servers).filter(([name])=>names.includes(name)));
    files.set(prefix+document.file,Buffer.from(JSON.stringify({...value,mcpServers:filtered},null,2)+'\n'));
   }
  }
  let hasSelectedAssets=false;
  for(const kind of ['metadata','schemas','ui'] as const)if(selected.has(kind)){
   const before=files.size;tree(typeof pointers[kind]==='string'?pointers[kind]:kind);hasSelectedAssets=hasSelectedAssets||files.size>before;
  }
  // 显示信息及说明是安装清单的附件，不可借它们把被排除的表面重新挂回。
  for(const name of ['README','README.md','AGENTS.md'])add(name);
  for(const value of [installObject(manifest.interface).icon,...Array.isArray(installObject(manifest.interface).screenshots)?installObject(manifest.interface).screenshots as JsonValue[]:[]])if(typeof value==='string')tree(value);
  const nextPointers:InstallObject={};
  for(const[kind,value]of Object.entries(pointers))if(selected.has(kind as SurfaceName)&&kind!=='patch'&&typeof value==='string'){
   const relative=safePath(value),hasFiles=[...files.keys()].some(file=>file===prefix+relative||file.startsWith(prefix+relative+'/'));
   if(hasFiles)nextPointers[kind]=value;
  }
  if(selection.noCode&&manifest.runtime!==undefined){
   delete manifest.runtime;manifest.inject=[];manifest.provides=[];delete manifest.optionalInject;
   // 代码配置仅在用户同时选择 schemas 时保留；普通技能选择不安装代码配置。
   if(selected.has('schemas')&&typeof runtime.configSchema==='string'){
    const schema=source.content.files.get(prefix+safePath(runtime.configSchema));
    if(schema)files.set(prefix+'schemas/config.schema.json',schema);
   }
  }else if(typeof runtime.configSchema==='string')tree(runtime.configSchema);
  if(!selection.noCode&&runtime.form==='ui'&&!selected.has('ui'))installFailure('.qian-plugin/plugin.json','保留 UI 代码时必须同时选择 ui 表面，或显式使用 --no-code。');
  if(docs.length||hasSelectedAssets||!selection.noCode&&runtime.form!==undefined)active.add(entry.component.id);
  manifest.surfaces=nextPointers;manifests.set(entry.component.id,manifest);
 }
 // 组合层是子组件的挂载入口，即使仅选 skills 也需保留经过裁剪的装配信息。
 if(composition){
  const manifest=manifests.get(root.component.id)!,pointer=String(installObject(root.manifest.surfaces).patch??'./config/qian.patch.yml');
  const rows=selection.noCode?original.patch.map(installObject).filter(row=>typeof row.plugin==='string'&&active.has(row.plugin.split('@')[0]))
   .map(row=>({...row,...selected.has('schemas')?{}:{config:{}}})):original.patch;
  if(rows.length){files.set(safePath(pointer),Buffer.from(stringify(rows)));installObject(manifest.surfaces).patch=pointer;}
 }
 for(const entry of original.components){
  const prefix=entry.directory?entry.directory+'/':'';
  if(entry.directory&&!active.has(entry.component.id)&&selection.noCode)continue;
  files.set(prefix+'.qian-plugin/plugin.json',Buffer.from(JSON.stringify(manifests.get(entry.component.id),null,2)+'\n'));
 }
 const owners=[...original.components].sort((a,b)=>b.directory.length-a.directory.length);
 if(selection.noCode)for(const file of files.keys()){
  const owner=owners.find(entry=>entry.directory&&file.startsWith(entry.directory+'/'));
  if(owner&&!active.has(owner.component.id))files.delete(file);
 }
 if(!active.size)installFailure('argv','所选组件不包含请求的可安装表面。');
 for(const[kind,names]of Object.entries(selection.members??{}))for(const name of names??[]){
  const found=original.components.some(entry=>entry.component.documents.some(document=>surfaceName(document)===kind&&
   (document.kind==='mcp'?Object.hasOwn(installObject(installObject(document.data).mcpServers),name):document.name===name)));
  if(!found)installFailure('argv','指定的表面成员不存在。');
 }
 const content=snapshot(files),resolved={...source,content,files:content.records};
 const delivery=prepareComponentDelivery(resolved,profile,forceProfile);
 return{resolved:{...resolved,component:delivery.components[0].component,packages:delivery.packages},delivery};
}
