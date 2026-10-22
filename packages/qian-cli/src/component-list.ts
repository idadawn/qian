import {gt} from 'semver';
import type {SurfaceDiagnostic} from '@qian/surfaces';
import {listInstalledComponents,readInstalledState,installObject,installFailure,verifyInstalledComponent,readInstallFile,type InstallObject} from './installed-components.js';
import {resolveMarketplacePlugin,type MarketplaceOptions} from './marketplaces.js';
import {safePath,within} from './marketplace-files.js';
import type {SurfaceName} from './surface-selection.js';

/** 只读显示安装记录；过期比较仍走相同市场签名、来源摘要和组件校验。 */
export async function listLocalComponents(options:MarketplaceOptions,settings:{outdated?:boolean;surfaces?:true|readonly SurfaceName[]}={}){
 const installed=readInstalledState(options.workingDirectory).plugins,listing=listInstalledComponents(options.workingDirectory),diagnostics:SurfaceDiagnostic[]=[];
 const plugins:InstallObject[]=[];
 for(const item of listing.plugins){
  const record=installed.find(value=>value.id===item.id)!;
  if(Array.isArray(settings.surfaces)&&!(item.surfaces as string[]).some(name=>settings.surfaces!==true&&settings.surfaces?.includes(name as SurfaceName)))continue;
  if(settings.outdated){
   if(typeof record.name!=='string')installFailure('.qian/lock.json','过期比较需要原始市场安装名。','SURFACE_REFERENCE_UNKNOWN');
   const latest=await resolveMarketplacePlugin(record.name,{...options,from:String(record.market)});diagnostics.push(...latest.diagnostics);
   if(latest.component.id!==item.id)installFailure('.qian/marketplace.json','市场安装名已指向不同组件，不能据此比较更新。','SURFACE_NAME_MISMATCH');
   if(!gt(latest.version,String(item.version)))continue;
   item.latestVersion=latest.version;item.outdated=true;
  }
  if(settings.surfaces){
   const selected=Array.isArray(settings.surfaces)?new Set<string>(settings.surfaces):undefined;
   if(!Array.isArray(record.files))installFailure('.qian/lock.json','表面文件记录必须为数组。');
   verifyInstalledComponent(options.workingDirectory,record);
   const files=record.files.map(value=>{const file=installObject(value);if(typeof file.path!=='string')installFailure('.qian/lock.json','表面文件路径无效。','SURFACE_PATH_INVALID');return safePath(file.path);});
   const defaults:Record<string,string>={skills:'skills',agents:'agents',commands:'commands',hooks:'hooks/hooks.json',mcpServers:'.mcp.json',tools:'tools',ui:'ui',metadata:'metadata',schemas:'schemas',patch:'config/qian.patch.yml'};
   item.surfaceFiles=files.filter(name=>name==='.qian-plugin/plugin.json'||name.endsWith('/.qian-plugin/plugin.json')).flatMap(name=>{
    const prefix=name.slice(0,-'.qian-plugin/plugin.json'.length),manifest=installObject(readInstallFile(within(options.workingDirectory,'.qian/plugins/'+item.id+'/'+name))),pointers=installObject(manifest.surfaces);
    return Object.entries(defaults).flatMap(([surface,fallback])=>{
     if(!(item.surfaces as string[]).includes(surface)||selected&&!selected.has(surface))return[];
     const pointer=prefix+safePath(typeof pointers[surface]==='string'?pointers[surface]:fallback);
     return files.filter(file=>file===pointer||file.startsWith(pointer+'/')).map(file=>({surface,path:file}));
    });
   });
  }
  plugins.push(item);
 }
 return{plugins,...settings.outdated?{compared:installed.length,diagnostics}:{}};
}
