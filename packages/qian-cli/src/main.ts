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
import {installDeclarativeMarketplacePlugin} from './marketplace-install.js';
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
let remote=false,markets=false,allowUnverified=false,yes=false,dryRun=false,noCode=false;let from:string|undefined;const references:string[]=[];
let command='';let template:string|undefined,componentId:string|undefined,componentName:string|undefined;
let runtimeUrl:string|undefined;
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
    else if(arg==='doctor')command='doctor';
    else if(arg==='init')command='init';
    else if(arg==='list'||arg==='add')command=arg;
    else if(arg==='--remote')remote=true;
    else if(arg==='--markets')markets=true;
    else if(arg==='--allow-unverified')allowUnverified=true;
    else if(arg==='--yes')yes=true;
    else if(arg==='--dry-run')dryRun=true;
    else if(arg==='--no-code')noCode=true;
    else if(arg==='--from')from=value();
    else if(command==='add'&&!arg.startsWith('-'))references.push(arg);
    else if(arg==='--template')template=value();
    else if(arg==='--id')componentId=value();
    else if(arg==='--name')componentName=value();
    else if(arg==='--source')sources.push(value());
    else if(arg==='docs'&&argv[i+1]==='generate'){command='docs-generate';i++;}
    else if(arg==='--json')format='json';
    else if(arg==='--dump-config'||arg==='dump-config')command='dump-config';
    else if(arg==='--resolve'||arg==='--redact'){} // 解析始终校验，且从不解析密钥引用。
    else if(arg==='--help'||arg==='-h'){process.stdout.write('qian [--profile 名称] dump-config [--patch 文件] [--format yaml|json] [--entry ID] [--cwd 目录]\nqian doctor [--url https://host] [--profile 名称] [--json]\nqian docs generate [--profile 名称] [--patch 文件] [--source 源码目录] [--out docs/generated] [--json]\nqian init --template skill-only --id <组件id> --name <名称>\nqian list --remote|--markets [--allow-unverified]\nqian add <market/name@version> --no-code [--yes|--dry-run] [--from 市场]\n');process.exit(0);}
    else throw new Error('不支持的命令或参数。');
  }
  if(!command)throw new Error('使用 --help 查看 dump-config 与 docs generate 参数。');
  if(!['json','yaml'].includes(format))throw new Error('format 必须为 json 或 yaml');
  if(!profile){const config=path.join(cwd,'.qian/config.yml');if(existsSync(config)){const parsed=parseSurface('.qian/config.yml',readFileSync(config,'utf8'));if(parsed.diagnostics.some(d=>d.severity==='error')){process.stderr.write(JSON.stringify({diagnostics:parsed.diagnostics})+'\n');process.exit(1);}if(parsed.syntax&&typeof parsed.syntax.data==='object'&&parsed.syntax.data&&!Array.isArray(parsed.syntax.data)){const value=parsed.syntax.data.defaultProfile;if(typeof value==='string')profile=value;}}}
  if(command==='doctor'){
    const result=await doctor({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:includeHome},runtimeUrl);
    process.stdout.write(format==='json'?JSON.stringify(result,null,2)+'\n':stringify(result,{lineWidth:0}));
    if(!result.healthy)process.exitCode=1;
  }else if(command==='list'||command==='add'){
    const options={workingDirectory:cwd,homeDirectory:home,includeHome,profile:profile??'minimal',allowUnverified,from};
    let result:unknown;
    if(command==='list') {
      if(markets)result=marketplaceRegistrations(options).markets.map(({baseDirectory,...item})=>item);
      else if(remote)result=await listRemoteMarketplaces(options);
      else throw new Error('本阶段 list 支持 --remote / --markets；已安装列表由完整安装任务接入。');
    }else{
      if(references.length!==1)throw new Error('本阶段 add 一次接收一个无代码组件；完整安装与批量操作由 S1-RT-011 接入。');
      result=await installDeclarativeMarketplacePlugin(references[0],{...options,yes,dryRun,noCode});
    }
    process.stdout.write(format==='json'?JSON.stringify(result,null,2)+'\n':stringify(result,{lineWidth:0}));
  }else if(command==='init'){
    if(template!=='skill-only'||!componentId)throw new Error('本阶段 init 支持 --template skill-only --id <组件id>；代码模板由后续安装任务接入。');
    const files=initializeSkillOnly(cwd,componentId,componentName??componentId);
    process.stdout.write(format==='json'?JSON.stringify({files},null,2)+'\n':stringify({files}));
  }else if(command==='docs-generate'){
    const generated=generateDocumentation({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:homeExplicit?includeHome:false,outputDirectory,sourceDirectories:sources.length?sources:undefined});
    if(!generated.isValid){process.stderr.write(JSON.stringify({diagnostics:generated.diagnostics,events:generated.events},null,2)+'\n');process.exitCode=1;}
    else process.stdout.write(format==='json'?JSON.stringify(generated,null,2)+'\n':stringify(generated,{lineWidth:0}));
  }else{
  const result=loadProfile({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:includeHome});
  if(!result.isValid){process.stderr.write(JSON.stringify({diagnostics:result.diagnostics},null,2)+'\n');process.exitCode=1;}
  else{
    const snapshot={...result.snapshot!,entries:entry?result.snapshot!.entries.filter(item=>item.id===entry):result.snapshot!.entries,diagnostics:result.diagnostics};
    process.stdout.write(format==='json'?JSON.stringify(snapshot,null,2)+'\n':stringify(snapshot,{lineWidth:0}));
  }
  }
}catch(error){process.stderr.write(JSON.stringify({diagnostics:error instanceof MarketplaceFailure||error instanceof DoctorFailure?error.diagnostics:[argumentError(error instanceof Error?error.message:'无法读取参数')]})+'\n');process.exitCode=1;}
