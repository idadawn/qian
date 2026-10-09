#!/usr/bin/env node
import {readFileSync,existsSync} from 'node:fs';
import path from 'node:path';
import {stringify} from 'yaml';
import {parseSurface} from '@qian/surfaces';
import {loadProfile} from './profiles.js';
import {randomBytes} from 'node:crypto';
import type {SurfaceDiagnostic} from '@qian/surfaces';
function argumentError(message: string): SurfaceDiagnostic {
 const pointer='argv#',general='输入不符合该能力的输入 Schema，逐项错误见 errors。';
 return {file:'argv',line:1,severity:'error',error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message:general,detail:general,trace_id:randomBytes(16).toString('hex'),pointer,errors:[{code:'PROFILE_INVALID',message,pointer}]}};
}

const argv=process.argv.slice(2);
let cwd=process.cwd(),profile:string|undefined,format='yaml',entry:string|undefined,home:string|undefined,includeHome=true;
const patches:string[]=[];
let command='';
try{
  for(let i=0;i<argv.length;i++){
    const arg=argv[i];
    const value=():string=>{const next=argv[++i];if(!next||next.startsWith('--'))throw new Error('参数 '+arg+' 缺少值');return next;};
    if(arg==='--cwd')cwd=path.resolve(value());
    else if(arg==='--profile')profile=value();
    else if(arg==='--patch')patches.push(value());
    else if(arg==='--format')format=value();
    else if(arg==='--entry')entry=value();
    else if(arg==='--home')home=value();
    else if(arg==='--no-home')includeHome=false;
    else if(arg==='--json')format='json';
    else if(arg==='--dump-config'||arg==='dump-config')command='dump-config';
    else if(arg==='--resolve'||arg==='--redact'){} // 解析始终校验，且从不解析密钥引用。
    else if(arg==='--help'||arg==='-h'){process.stdout.write('qian [--profile 名称] dump-config [--patch 文件] [--format yaml|json] [--entry ID] [--cwd 目录]\n');process.exit(0);}
    else throw new Error('不支持的命令或参数。');
  }
  if(!command)throw new Error('本阶段提供 dump-config；使用 --help 查看参数。');
  if(!['json','yaml'].includes(format))throw new Error('format 必须为 json 或 yaml');
  if(!profile){const config=path.join(cwd,'.qian/config.yml');if(existsSync(config)){const parsed=parseSurface('.qian/config.yml',readFileSync(config,'utf8'));if(parsed.diagnostics.some(d=>d.severity==='error')){process.stderr.write(JSON.stringify({diagnostics:parsed.diagnostics})+'\n');process.exit(1);}if(parsed.syntax&&typeof parsed.syntax.data==='object'&&parsed.syntax.data&&!Array.isArray(parsed.syntax.data)){const value=parsed.syntax.data.defaultProfile;if(typeof value==='string')profile=value;}}}
  const result=loadProfile({workingDirectory:cwd,profile:profile??'platform',patches,homeDirectory:home,includeHomePatch:includeHome});
  if(!result.isValid){process.stderr.write(JSON.stringify({diagnostics:result.diagnostics},null,2)+'\n');process.exitCode=1;}
  else{
    const snapshot={...result.snapshot!,entries:entry?result.snapshot!.entries.filter(item=>item.id===entry):result.snapshot!.entries,diagnostics:result.diagnostics};
    process.stdout.write(format==='json'?JSON.stringify(snapshot,null,2)+'\n':stringify(snapshot,{lineWidth:0}));
  }
}catch(error){process.stderr.write(JSON.stringify({diagnostics:[argumentError(error instanceof Error?error.message:'无法读取参数')]})+'\n');process.exitCode=1;}
