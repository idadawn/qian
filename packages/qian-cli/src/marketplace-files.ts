import {readFileSync,readdirSync,existsSync,lstatSync,mkdirSync,writeFileSync,realpathSync,mkdtempSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash} from 'node:crypto';
import {execFile} from 'node:child_process';
import {promisify} from 'node:util';
import {gunzipSync} from 'node:zlib';
import {unzipSync} from 'fflate';
import {extract} from 'tar-stream';
import {isSafeMarketplaceUrl} from '@qian/surfaces';

const execute=promisify(execFile);
export const MAX_BYTES=64*1024*1024;
export interface FileRecord {path:string;bytes:number;sha256:string}
export interface ContentSnapshot {files:Map<string,Buffer>;records:FileRecord[];digest:string}
export function safePath(name:string):string {
 const value=name.replace(/^\.\//,'').replace(/\/$/,'');
 if(!value||value==='.'||value.startsWith('/')||value.includes('\\')||value.includes(':')||/[\x00-\x1f\x7f-\x9f]/.test(value)||value.split('/').some(part=>!part||part==='..'||part==='.'||part==='.git')||value.split('/').length>64)throw new Error('来源路径无效、越界或包含受限目录。');
 return value;
}
export function assertNoLinks(target:string):void {
 let current=path.parse(path.resolve(target)).root;
 for(const part of path.resolve(target).slice(current.length).split(path.sep)){if(!part)continue;current=path.join(current,part);try{if(lstatSync(current).isSymbolicLink())throw new Error('路径不能经过符号链接。');}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}}
}
export function within(root:string,relative:string):string {
 if(relative==='.'||relative==='./'){assertNoLinks(root);return path.resolve(root);}
 const candidate=path.join(root,safePath(relative));assertNoLinks(candidate);
 return candidate;
}
export const sha256=(bytes:Uint8Array):string=>createHash('sha256').update(bytes).digest('hex');
export function snapshot(files:Map<string,Buffer>):ContentSnapshot {
 let total=0;
 const records=[...files.entries()].sort(([a],[b])=>a<b?-1:a>b?1:0).map(([name,bytes])=>{safePath(name);total+=bytes.length;return{path:name,bytes:bytes.length,sha256:sha256(bytes)};});
 if(total>MAX_BYTES||records.length>10000)throw new Error('组件归档超过大小或文件数量限制。');
 // v1 D4：路径、字节数和文件 SHA-256 的逐行清单，UTF-8/LF，按路径排序。
 const manifest=records.map(file=>`${file.path}\t${file.bytes}\t${file.sha256}\n`).join('');
 return{files,records,digest:'sha256:'+sha256(Buffer.from(manifest))};
}
export function readDirectory(root:string):ContentSnapshot {
 assertNoLinks(root);const resolved=realpathSync(root),files=new Map<string,Buffer>();let size=0;
 const visit=(folder:string,depth:number):void=>{
  if(depth>64)throw new Error('组件目录过深。');
  for(const item of readdirSync(folder,{withFileTypes:true})){
   if(item.name==='.git')continue;
   const file=path.join(folder,item.name),info=lstatSync(file);
   if(info.isSymbolicLink()||!info.isDirectory()&&!info.isFile())throw new Error('组件不能包含符号链接或特殊文件。');
   if(info.isDirectory())visit(file,depth+1);
   else{
    size+=info.size;if(size>MAX_BYTES||files.size>=10000)throw new Error('组件目录超过大小或文件数量限制。');
    const bytes=readFileSync(file);const after=lstatSync(file);
    if(after.isSymbolicLink()||after.ino!==info.ino||after.size!==bytes.length||after.mtimeMs!==info.mtimeMs)throw new Error('读取期间组件内容发生变化。');
    files.set(path.relative(resolved,file).split(path.sep).join('/'),bytes);
   }
  }
 };
 visit(resolved,0);return snapshot(files);
}
export function materialize(content:ContentSnapshot,directory:string):void {
 assertNoLinks(directory);mkdirSync(directory,{recursive:true,mode:0o700});
 for(const[name,bytes]of content.files){const file=within(directory,name);mkdirSync(path.dirname(file),{recursive:true});writeFileSync(file,bytes,{flag:'wx',mode:0o600});}
}
function checkZipMetadata(bytes:Buffer):void {
 // 只解析中央目录的类型标志；解压仍由 fflate 完成，禁止 ZIP 中的链接/设备与多卷、超限 ZIP64。
 let end=-1;
 for(let offset=bytes.length-22;offset>=Math.max(0,bytes.length-65557);offset--){if(bytes.readUInt32LE(offset)===0x06054b50&&offset+22+bytes.readUInt16LE(offset+20)===bytes.length){end=offset;break;}}
 if(end<0||bytes.readUInt16LE(end+4)!==0||bytes.readUInt16LE(end+6)!==0)throw new Error('ZIP 目录或卷无效。');
 const count=bytes.readUInt16LE(end+10),directory=bytes.readUInt32LE(end+16),size=bytes.readUInt32LE(end+12);
 if(count>10000||directory===0xffffffff||directory+size>end)throw new Error('ZIP 中央目录超过限制。');
 let offset=directory;
 for(let i=0;i<count;i++){
  if(offset+46>end||bytes.readUInt32LE(offset)!==0x02014b50)throw new Error('ZIP 中央目录无效。');
  const mode=(bytes.readUInt32LE(offset+38)>>>16)&0xf000;
  if(mode!==0&&mode!==0x8000&&mode!==0x4000)throw new Error('ZIP 不能包含链接或特殊文件。');
  offset+=46+bytes.readUInt16LE(offset+28)+bytes.readUInt16LE(offset+30)+bytes.readUInt16LE(offset+32);
 }
 if(offset!==directory+size)throw new Error('ZIP 中央目录长度不一致。');
}
export async function unpack(bytes:Buffer,kind:'zip'|'tgz'|'tar'):Promise<ContentSnapshot>{
 const files=new Map<string,Buffer>();let total=0,count=0;
 const add=(name:string,data:Uint8Array):void=>{const normalized=safePath(name);if(files.has(normalized))throw new Error('归档存在重复路径。');total+=data.length;if(total>MAX_BYTES||++count>10000)throw new Error('归档超过限制。');files.set(normalized,Buffer.from(data));};
 if(kind==='zip'){
  checkZipMetadata(bytes);
  const declared=new Map<string,number>();let expanded=0;
  const output=unzipSync(bytes,{filter:entry=>{
   if(entry.name.endsWith('/')){safePath(entry.name);return false;}
   safePath(entry.name);if(declared.has(entry.name))throw new Error('归档存在重复路径。');
   expanded+=entry.originalSize;if(!Number.isSafeInteger(entry.originalSize)||entry.originalSize<0||expanded>MAX_BYTES||declared.size>=10000)throw new Error('归档超过限制。');declared.set(entry.name,entry.originalSize);return true;
  }});
  for(const[name,data]of Object.entries(output)){if(data.length!==declared.get(name))throw new Error('归档长度不一致。');add(name,data);}
 }else{
  const archive=kind==='tgz'?gunzipSync(bytes,{maxOutputLength:MAX_BYTES}):bytes;
  if(archive.length>MAX_BYTES)throw new Error('归档超过限制。');
  await new Promise<void>((resolve,reject)=>{
   const parser=extract();parser.on('error',reject);parser.on('finish',resolve);
   parser.on('entry',(header,stream,next)=>{
    try{
     if(header.type==='directory'){if(header.name!=='.'&&header.name!=='./')safePath(header.name);stream.on('end',()=>next());stream.resume();return;}
     if(header.type!=='file')throw new Error('归档不能包含链接或特殊文件。');
     safePath(header.name);if(header.size>MAX_BYTES-total||count>=10000)throw new Error('归档超过限制。');
     const chunks:Buffer[]=[];let size=0;
     stream.on('data',(value:unknown)=>{const chunk=Buffer.from(value as Uint8Array);size+=chunk.length;if(size>header.size||size>MAX_BYTES){parser.destroy(new Error('归档长度不一致。'));return;}chunks.push(Buffer.from(chunk));});
     stream.on('error',reject);stream.on('end',()=>{try{add(header.name,Buffer.concat(chunks));next();}catch(error){parser.destroy(error as Error);}});
    }catch(error){parser.destroy(error as Error);}
   });parser.end(archive);
  });
 }
 return snapshot(files);
}
export async function download(url:string,fetcher:typeof fetch=fetch):Promise<Buffer>{
 const address=new URL(url);if(!isSafeMarketplaceUrl(address.toString()))throw new Error('下载地址必须为无凭据片段的 HTTPS。');
 const response=await fetcher(address,{redirect:'error',signal:AbortSignal.timeout(30000)});
 if(!response.ok||!response.body)throw new Error('来源下载失败。');
 const length=Number(response.headers.get('content-length')??0);if(length>MAX_BYTES)throw new Error('下载超过限制。');
 const chunks:Buffer[]=[];let size=0;
 for await(const value of response.body){const chunk=Buffer.from(value);size+=chunk.length;if(size>MAX_BYTES)throw new Error('下载超过限制。');chunks.push(chunk);}
 return Buffer.concat(chunks);
}
export interface GitSnapshot {content:ContentSnapshot;commit:string;refKind:'tag'|'branch'|'commit';resolvedRef:string}
/** 裸仓库获取后用 archive 读取；不 checkout、不运行 hooks/过滤器/组件脚本。 */
export async function readGit(repo:string,reference:string,subdirectory='.',localMirror?:string):Promise<GitSnapshot>{
 const address=new URL(repo);if(!isSafeMarketplaceUrl(address.toString()))throw new Error('Git 来源必须为无凭据片段的 HTTPS。');
 if(!reference||reference.startsWith('-')||/[\x00-\x20\x7f]/.test(reference))throw new Error('Git 引用无效。');
 const directory=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-git-'));
 const source=localMirror?path.resolve(localMirror):repo;
 const env={...process.env,GIT_TERMINAL_PROMPT:'0'};
 const git=async(args:string[]):Promise<Buffer>=>{
  try{const result=await execute('git',['-c','core.hooksPath=/dev/null','-c','core.fsmonitor=false','-c','protocol.ext.allow=never',...args],{cwd:directory,env,encoding:'buffer',timeout:60000,maxBuffer:MAX_BYTES});return Buffer.from(result.stdout);}
  catch{throw new Error('Git 来源或引用无法读取。');}
 };
 try{
  await git(['init','--bare','--template=','.']);
  let refKind:GitSnapshot['refKind']='commit',resolvedRef=reference;
  if(!/^(?:[0-9a-f]{40}|[0-9a-f]{64})$/i.test(reference)){
   const requested=reference.startsWith('refs/')?[reference]:['refs/tags/'+reference,'refs/heads/'+reference];
   const available=(await git(['ls-remote','--refs',source,...requested])).toString('utf8').trim().split('\n').filter(Boolean).map(line=>line.split('\t')[1]);
   resolvedRef=requested.find(item=>available.includes(item))??'';
   if(!resolvedRef)throw new Error('Git 引用不存在。');refKind=resolvedRef.startsWith('refs/tags/')?'tag':'branch';
  }
  await git(['fetch','--no-tags','--depth=1',source,resolvedRef]);
  const commit=(await git(['rev-parse','FETCH_HEAD^{commit}'])).toString('utf8').trim();
  if(refKind==='commit'&&commit.toLowerCase()!==reference.toLowerCase())throw new Error('Git 内容与指定提交不一致。');
  const sub=subdirectory==='.'||subdirectory==='./'?'':safePath(subdirectory);
  const archive=await git(['archive','--format=tar',sub?commit+':'+sub:commit]);
  return{content:await unpack(archive,'tar'),commit,refKind,resolvedRef};
 }finally{rmSync(directory,{recursive:true,force:true});}
}
