import {spawnSync} from 'node:child_process';
import {existsSync,lstatSync,mkdirSync,readFileSync,writeFileSync,renameSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {randomBytes} from 'node:crypto';
import type {SurfaceDiagnostic} from '@qian/surfaces';
import type {ProfileOptions} from './profiles.js';

export interface DocumentationOptions extends ProfileOptions {outputDirectory?:string;sourceDirectories?:string[]}
export interface DocumentationResult {files:string[];events:unknown[];diagnostics:SurfaceDiagnostic[];isValid:boolean}
function failure(message:string):DocumentationResult{
 const pointer='docs/generated#',general='输入不符合该能力的输入 Schema，逐项错误见 errors。';
 return {files:[],events:[],isValid:false,diagnostics:[{file:'docs/generated',line:1,severity:'error',error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message:general,detail:general,trace_id:randomBytes(16).toString('hex'),pointer,errors:[{code:'DOC_SOURCE_INVALID',message,pointer}]}}]};
}
function within(root:string,relative:string):string{
 const full=path.resolve(root,relative);
 if(full!==root&&!full.startsWith(root+path.sep))throw new Error('输出必须在当前工作区内。');
 let current=root;
 for(const part of path.relative(root,full).split(path.sep)){current=path.join(current,part);if(existsSync(current)&&lstatSync(current).isSymbolicLink())throw new Error('输出路径不能经过符号链接。');}
 return full;
}
/** 由受信任的随 CLI 工具读取 C# 语义树，不构建或执行目标组件。 */
export function generateDocumentation(options:DocumentationOptions):DocumentationResult{
 const root=path.resolve(options.workingDirectory);
 try{
  const toolRoot=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..','tools/Qian.Docs.Tool');
  const build=spawnSync('dotnet',['build',toolRoot,'-c','Release','--nologo','--verbosity','quiet'],{encoding:'utf8',timeout:120000,maxBuffer:8*1024*1024});
  if(build.error||build.status!==0)return failure('随 CLI 的文档工具构建失败；请检查 .NET SDK 与仓库包还原。');
  const input={workingDirectory:root,profile:options.profile??'platform',patches:options.patches??[],homeDirectory:options.homeDirectory??null,includeHomePatch:options.includeHomePatch??false,sourceDirectories:options.sourceDirectories??['packages','plugins','src']};
  const run=spawnSync('dotnet',[path.join(toolRoot,'bin/Release/net10.0/Qian.Docs.Tool.dll')],{input:JSON.stringify(input),encoding:'utf8',timeout:120000,maxBuffer:16*1024*1024});
  if(run.error||!run.stdout)return failure('文档工具未返回完整结果。');
  const result=JSON.parse(run.stdout) as {documents:Record<string,string>;events:unknown[];diagnostics:SurfaceDiagnostic[];isValid:boolean};
  if(run.status!==0||!result.isValid)return{files:[],events:result.events,diagnostics:result.diagnostics,isValid:false};
  const out=within(root,options.outputDirectory??'docs/generated');
  const names=['config-catalog.md','module-graph.md','event-graph.md','seam-graph.md'];
  if(names.some(name=>typeof result.documents[name]!=='string'))return failure('文档生成结果缺少必需文件。');
  // 先检查所有目标；拒绝覆盖人工文件。之后按批次回滚，避免只更新一半。
  const originals=new Map<string,Buffer|undefined>();
  for(const name of names){const target=within(root,path.join(out,name));const previous=existsSync(target)?readFileSync(target):undefined;if(previous&&!previous.toString('utf8').includes('由 `qian docs generate` 派生'))throw new Error('目标存在人工文档，请选择新的输出目录。');originals.set(target,previous);}
  mkdirSync(out,{recursive:true});const changed:string[]=[],temps:string[]=[];
  try{
   for(const name of names){const target=path.join(out,name),temporary=target+'.'+randomBytes(8).toString('hex')+'.tmp';temps.push(temporary);writeFileSync(temporary,result.documents[name],{flag:'wx'});renameSync(temporary,target);changed.push(target);}
  }catch(error){for(const target of changed.reverse()){const previous=originals.get(target);if(previous)writeFileSync(target,previous);else if(existsSync(target))unlinkSync(target);}throw error;}
  finally{for(const file of temps)if(existsSync(file))unlinkSync(file);}
  return{files:names.map(name=>path.relative(root,path.join(out,name)).replaceAll('\\','/')),events:result.events,diagnostics:result.diagnostics,isValid:true};
 }catch(error){return failure(error instanceof Error?error.message:'文档生成失败。');}
}
