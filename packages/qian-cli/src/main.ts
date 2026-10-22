#!/usr/bin/env node
import {readFileSync,existsSync} from 'node:fs';
import path from 'node:path';
import {stringify} from 'yaml';
import {parseSurface} from '@qian/surfaces';
import {loadProfile} from './profiles.js';
import {generateDocumentation} from './docs.js';
import {doctor,DoctorFailure} from './doctor.js';
import {initializeSkillOnly} from './init-docs.js';
import {listRemoteMarketplaces,marketplaceRegistrations,MarketplaceFailure} from './marketplaces.js';
import {installMarketplacePlugins} from './marketplace-install.js';
import {removeMarketplacePlugins} from './marketplace-remove.js';
import {listLocalComponents} from './component-list.js';
import {recoverWorkspaceTransaction} from './workspace-transaction.js';
import {packageCommandTimeout} from './package-process.js';
import {changeTenantScope,TenantScopeFailure} from './tenant-scope.js';
import {parseSurfaceNames,type SurfaceName} from './surface-selection.js';
import {randomBytes} from 'node:crypto';
import type {SurfaceDiagnostic} from '@qian/surfaces';
function argumentError(message: string): SurfaceDiagnostic {
 const pointer='argv#',general='输入不符合该能力的输入 Schema，逐项错误见 errors。';
 return {file:'argv',line:1,severity:'error',error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message:general,detail:general,trace_id:randomBytes(16).toString('hex'),pointer,errors:[{code:'PROFILE_INVALID',message,pointer}]}};
}

const argv=process.argv.slice(2);
let cwd=process.cwd(),profile:string|undefined,format='yaml',entry:string|undefined,home:string|undefined,includeHome=true;
const patches:string[]=[];
const sources:string[]=[];let outputDirectory:string|undefined,homeExplicit=false;
let remote=false,markets=false,allowUnverified=false,yes=false,dryRun=false,noCode=false,purge=false,force=false,resetConfig=false,forceProfile=false;let from:string|undefined;const references:string[]=[];
let command='';let template:string|undefined,componentId:string|undefined,componentName:string|undefined;
let surfaces:SurfaceName[]|undefined,listSurfaces:true|SurfaceName[]|undefined,rawSurfaces:string|true|undefined,outdated=false,fix=false;
let runtimeUrl:string|undefined;
let tenantScope:string|undefined,revision:number|undefined;
try{
  for(let i=0;i<argv.length;i++){
    const arg=argv[i];
    const value=():string=>{const next=argv[++i];if(!next||next.startsWith('--'))throw new Error('参数 '+arg+' 缺少值');return next;};
    if(arg==='--cwd')cwd=path.resolve(value());
    else if(arg==='--profile')profile=value();
    else if(arg==='--patch')patches.push(value());
    else if(arg==='--format')format=value();
    else if(arg==='--entry')entry=value();
    else if(arg==='--home'){home=value();homeExplicit=true;}
    else if(arg==='--no-home'){includeHome=false;homeExplicit=true;}
    else if(arg==='--out')outputDirectory=value();
    else if(arg==='--url')runtimeUrl=value();
    else if(arg==='--scope')tenantScope=value();
    else if(arg==='--revision')revision=Number(value());
    else if(!command&&arg==='doctor')command='doctor';
    else if(!command&&arg==='init')command='init';
    else if(!command&&(arg==='list'||arg==='add'||arg==='remove'))command=arg;
    else if(arg==='--remote')remote=true;
    else if(arg==='--markets')markets=true;
    else if(arg==='--outdated')outdated=true;
    else if(arg==='--fix')fix=true;
    else if(arg==='--allow-unverified')allowUnverified=true;
    else if(arg==='--yes')yes=true;
    else if(arg==='--dry-run')dryRun=true;
    else if(arg==='--no-code')noCode=true;
    else if(arg==='--surfaces'){const next=argv[i+1];rawSurfaces=next&&!next.startsWith('-')&&(!!command||!['list','add','remove','doctor'].includes(next))?value():true;}
    else if(arg==='--purge')purge=true;
    else if(arg==='--force')force=true;
    else if(arg==='--reset-config')resetConfig=true;
    else if(arg==='--force-profile')forceProfile=true;
    else if(arg==='--from')from=value();
    else if((command==='add'||command==='remove')&&!arg.startsWith('-'))references.push(arg);
    else if(arg==='--template')template=value();
    else if(arg==='--id')componentId=value();
    else if(arg==='--name')componentName=value();
    else if(arg==='--source')sources.push(value());
    else if(!command&&arg==='docs'&&argv[i+1]==='generate'){command='docs-generate';i++;}
    else if(arg==='--json')format='json';
    else if(arg==='--dump-config'||!command&&arg==='dump-config')command='dump-config';
    else if(arg==='--resolve'||arg==='--redact'){} // 解析始终校验，且从不解析密钥引用。
    else if(arg==='--help'||arg==='-h'){process.stdout.write('qian [--profile 名称] dump-config [--patch 文件] [--format yaml|json] [--entry ID] [--cwd 目录]\nqian doctor [--fix] [--url https://host] [--profile 名称] [--json]\nqian docs generate [--profile 名称] [--patch 文件] [--source 源码目录] [--out docs/generated] [--json]\nqian init --template skill-only --id <组件id> --name <名称>\nqian list [--outdated] [--surfaces [skills,commands]] [--remote|--markets] [--allow-unverified] [--json]\nqian add <market/name@version> [--surfaces skills,commands] [--no-code] [--yes|--dry-run] [--from 市场]\nqian remove <组件id或安装名>... [--profile 名称] [--purge|--dry-run] [--json]\nqian add <组件@精确版本> --scope tenant:<id> [--entry ID] [--revision N] --url https://host [--json]\nqian add --patch 文件 --scope tenant:<id> --url https://host [--json]\nqian remove <条目ID> --scope tenant:<id> [--revision N] --url https://host [--json]\n');process.exit(0);}
    else throw new Error('不支持的命令或参数。');
  }
  if(rawSurfaces!==undefined){if(command==='list')listSurfaces=rawSurfaces===true?true:parseSurfaceNames(rawSurfaces);else if(command==='add'&&typeof rawSurfaces==='string')surfaces=parseSurfaceNames(rawSurfaces);else throw new Error('--surfaces 用于 add 时必须提供逗号分隔的表面名称，或用于 list 显示表面。');}
  if(!command)throw new Error('使用 --help 查看 dump-config 与 docs generate 参数。');
  if(!['json','yaml'].includes(format))throw new Error('format 必须为 json 或 yaml');
  if(tenantScope&&command!=='add'&&command!=='remove')throw new Error('--scope 当前只支持运营 add / remove。');
  if(revision!==undefined&&!tenantScope)throw new Error('--revision 必须与 --scope 一起使用。');
  if(fix&&command!=='doctor')throw new Error('--fix 只支持 doctor。');
  if(outdated&&command!=='list')throw new Error('--outdated 只支持 list。');
  if(command==='list'&&(outdated||listSurfaces)&&(remote||markets))throw new Error('--outdated / --surfaces 用于本地已安装组件，不能与 --remote / --markets 混用。');
  if(!profile){const config=path.join(cwd,'.qian/config.yml');if(existsSync(config)){const parsed=parseSurface('.qian/config.yml',readFileSync(config,'utf8'));if(parsed.diagnostics.some(d=>d.severity==='error')){process.stderr.write(JSON.stringify({diagnostics:parsed.diagnostics})+'\n');process.exit(1);}if(parsed.syntax&&typeof parsed.syntax.data==='object'&&parsed.syntax.data&&!Array.isArray(parsed.syntax.data)){const value=parsed.syntax.data.defaultProfile;if(typeof value==='string')profile=value;}}}
  if(command==='doctor'){
    const recovery=fix?await recoverWorkspaceTransaction(cwd):undefined;
    const result=await doctor({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:includeHome},runtimeUrl);
    const report={...result,...recovery?{recovery}:{}};
    process.stdout.write(format==='json'?JSON.stringify(report,null,2)+'\n':stringify(report,{lineWidth:0}));
    if(!result.healthy)process.exitCode=1;
  }else if(command==='list'||command==='add'||command==='remove'){
    if(tenantScope&&(command==='add'||command==='remove')){
      if(surfaces||noCode||purge||force||resetConfig||forceProfile||from||allowUnverified||remote||markets)throw new Error('租户 scope 操作不接受本地包安装、表面选择或 purge 参数。');
      const result=await changeTenantScope(command,references,{workingDirectory:cwd,scope:tenantScope,url:runtimeUrl,entry,revision,patches,dryRun});
      process.stdout.write(format==='json'?JSON.stringify(result,null,2)+'\n':stringify(result,{lineWidth:0}));
    }else{
    // 包管理器超时配置在任何改动之前校验，不让非法值拖到事务中途才失败。
    if(command!=='list')packageCommandTimeout();
    const options={workingDirectory:cwd,homeDirectory:home,includeHome,profile:profile??'minimal',allowUnverified,from};
    let result:unknown;
    if(command==='list') {
      if(markets)result=marketplaceRegistrations(options).markets.map(({baseDirectory,...item})=>item);
      else if(remote)result=await listRemoteMarketplaces(options);
      else result=await listLocalComponents(options,{outdated,surfaces:listSurfaces});
    }else if(command==='remove')result=await removeMarketplacePlugins(references,{workingDirectory:cwd,profile:profile??'minimal',purge,dryRun});
    else{
      const installed=await installMarketplacePlugins(references,{...options,yes,dryRun,noCode,surfaces,force,resetConfig,forceProfile});
      result=references.length===1?{...installed.plugins[0],diagnostics:[...installed.plugins[0].diagnostics,...installed.diagnostics??[]],...installed.verification?{verification:installed.verification}:{}}:installed;
    }
    process.stdout.write(format==='json'?JSON.stringify(result,null,2)+'\n':stringify(result,{lineWidth:0}));
    }
  }else if(command==='init'){
    if(template!=='skill-only'||!componentId)throw new Error('本阶段 init 支持 --template skill-only --id <组件id>；代码模板由后续安装任务接入。');
    const files=initializeSkillOnly(cwd,componentId,componentName??componentId);
    process.stdout.write(format==='json'?JSON.stringify({files},null,2)+'\n':stringify({files}));
  }else if(command==='docs-generate'){
    const generated=generateDocumentation({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:homeExplicit?includeHome:false,outputDirectory,sourceDirectories:sources.length?sources:undefined});
    if(!generated.isValid){process.stderr.write(JSON.stringify({diagnostics:generated.diagnostics,events:generated.events})+'\n');process.exitCode=1;}
    else process.stdout.write(format==='json'?JSON.stringify(generated,null,2)+'\n':stringify(generated,{lineWidth:0}));
  }else{
  const result=loadProfile({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:includeHome});
  if(!result.isValid){process.stderr.write(JSON.stringify({diagnostics:result.diagnostics})+'\n');process.exitCode=1;}
  else{
    const snapshot={...result.snapshot!,entries:entry?result.snapshot!.entries.filter(item=>item.id===entry):result.snapshot!.entries,diagnostics:result.diagnostics};
    process.stdout.write(format==='json'?JSON.stringify(snapshot,null,2)+'\n':stringify(snapshot,{lineWidth:0}));
  }
  }
}catch(error){process.stderr.write(JSON.stringify({diagnostics:error instanceof MarketplaceFailure||error instanceof DoctorFailure||error instanceof TenantScopeFailure?error.diagnostics:[argumentError(error instanceof Error?error.message:'无法读取参数')]})+'\n');process.exitCode=1;}
