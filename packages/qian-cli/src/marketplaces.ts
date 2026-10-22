import {readFileSync,existsSync,mkdtempSync,rmSync,mkdirSync,writeFileSync,renameSync,lstatSync,realpathSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createPublicKey,verify,randomBytes,type KeyObject} from 'node:crypto';
import {valid as semverValid} from 'semver';
import {parseSurface,validateMarketplace,validateComponent,isSafeMarketplaceUrl,type JsonValue,type SurfaceDiagnostic,type ParsedComponent} from '@qian/surfaces';
import {readDirectory,readGit,download,unpack,materialize,within,assertNoLinks,sha256,snapshot,type ContentSnapshot,type FileRecord} from './marketplace-files.js';

type Obj={[key:string]:JsonValue};
const obj=(value:unknown):Obj=>value!==null&&typeof value==='object'&&!Array.isArray(value)?value as Obj:{};
const str=(value:unknown):string=>typeof value==='string'?value:'';
export interface MarketRegistration {name:string;source:string;priority:number;trust?:'unverified';origin?:'home'|'project';baseDirectory?:string}
export interface MarketplaceOptions {workingDirectory:string;homeDirectory?:string;includeHome?:boolean;profile?:string;allowUnverified?:boolean;from?:string;fetcher?:typeof fetch;gitMirrors?:Readonly<Record<string,string>>}
export interface MarketEntry {name:string;source:{type:'local'|'url'|'git-subdir';path?:string;url?:string;repo?:string;ref?:string;digest?:string};policy:{installation:'auto'|'confirm'|'restricted';authentication:'ON_INSTALL'|'ON_USE';profiles?:string[]};category:string;interface?:Obj}
interface Catalog {registration:MarketRegistration;data:Obj;entries:MarketEntry[];trusted:boolean;fingerprint:string|null;root:string;content?:ContentSnapshot;commit?:string}
export interface MarketListing {name:string;market:string;priority:number;source:MarketEntry['source'];policy:MarketEntry['policy'];category:string;interface?:Obj;trust:'verified'|'unverified';selected:boolean;validation:'catalog-only';note?:string}
export interface ResolvedMarketplacePlugin {
 name:string;market:string;marketSource:string;marketCommit?:string;marketKey:string|null;source:MarketEntry['source'];version:string;requestedVersion?:string;
 commit?:string;refKind?:'tag'|'branch'|'commit';resolvedRef?:string;digest:string;files:FileRecord[];component:ParsedComponent;content:ContentSnapshot;
 packages:{nuget:string[];npm:string[]};policy:MarketEntry['policy'];trust:'verified'|'unverified';diagnostics:SurfaceDiagnostic[];
}
export class MarketplaceFailure extends Error {constructor(readonly diagnostics:SurfaceDiagnostic[]){super('市场操作未通过验证。');}}
/** 诊断只保留工作区内可定位的路径，绝不返回本机绝对路径。 */
export function diagnosticFile(file:string):string {
 if(!path.isAbsolute(file))return file.replaceAll('\\','/');
 const normalized=file.split(path.sep).join('/');
 for(const segment of ['/.qian/','/profiles/','/plugins/']){const index=normalized.lastIndexOf(segment);if(index>=0)return normalized.slice(index+1);}
 return path.basename(file);
}
function safeRegistrationSource(source:string):boolean {
 if(!source.includes('://'))return true;
 try{const address=new URL(source.startsWith('git+')?source.slice(4):source);if(source.startsWith('git+'))address.hash='';return isSafeMarketplaceUrl(address.toString());}catch{return false;}
}
export function marketDiagnostic(file:string,pointer:string,message:string,code='SURFACE_POLICY_DENIED',severity:'error'|'warning'='error'):SurfaceDiagnostic {
 file=diagnosticFile(file);
 const location=file.replaceAll('\\','/').split('/').map(encodeURIComponent).join('/')+'#'+pointer,detail='输入不符合该能力的输入 Schema，逐项错误见 errors。';
 return{file,line:1,severity,error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message:detail,detail,trace_id:randomBytes(16).toString('hex'),pointer:location,errors:[{code,message,pointer:location}]}};
}
function fail(file:string,pointer:string,message:string,code?:string):never{throw new MarketplaceFailure([marketDiagnostic(file,pointer,message,code)]);}
function readConfig(file:string):Obj {
 if(!existsSync(file))return{};assertNoLinks(file);
 if(lstatSync(file).size>1_048_576)fail(file,'','配置超过大小限制。');
 const parsed=parseSurface(diagnosticFile(file),readFileSync(file,'utf8'));
 if(!parsed.syntax||parsed.diagnostics.some(item=>item.severity==='error'))throw new MarketplaceFailure([...parsed.diagnostics]);
 return obj(parsed.syntax.data);
}
export function marketplaceRegistrations(options:MarketplaceOptions):{markets:MarketRegistration[];keys:{value:string;base:string}[]} {
 const root=path.resolve(options.workingDirectory),home=path.resolve(root,options.homeDirectory??process.env.QIAN_HOME??path.join(os.homedir(),'.qian'));
 const layers:{file:string;base:string;origin:'home'|'project'}[]=[];
 if(options.includeHome!==false)layers.push({file:path.join(home,'marketplaces.json'),base:home,origin:'home'});
 layers.push({file:path.join(root,'.qian/config.yml'),base:root,origin:'project'});
 const markets=new Map<string,MarketRegistration>(),keys:{value:string;base:string}[]=[];
 for(const layer of layers){
  const config=readConfig(layer.file),list=config.marketplaces;
  if(list!==undefined&&!Array.isArray(list))fail(layer.file,'/marketplaces','marketplaces 必须为数组。','SURFACE_SCHEMA_INVALID');
  const names=new Set<string>();
  for(const[index,value]of (Array.isArray(list)?list:[]).entries()){
   const entry=obj(value),name=str(entry.name),source=str(entry.source),priority=entry.priority??100;
   if(!/^[a-z][a-z0-9-]*$/.test(name)||!source||typeof priority!=='number'||!Number.isSafeInteger(priority)||priority<0||entry.trust!==undefined&&entry.trust!=='unverified'||names.has(name))fail(layer.file,'/marketplaces/'+index,'市场登记的名称、来源、优先级或信任标记无效。','SURFACE_SCHEMA_INVALID');
   if(!safeRegistrationSource(source))fail(layer.file,'/marketplaces/'+index+'/source','来源地址不能包含凭据或非法 URL。');
   names.add(name);markets.set(name,{name,source,priority,trust:entry.trust as 'unverified'|undefined,origin:layer.origin,baseDirectory:layer.base});
  }
  const trust=obj(config.trust).keys;
  if(trust!==undefined&&(!Array.isArray(trust)||trust.some(value=>typeof value!=='string')))fail(layer.file,'/trust/keys','信任根必须为公钥路径或 PEM 字符串数组。','SURFACE_SCHEMA_INVALID');
  for(const value of Array.isArray(trust)?trust:[])keys.push({value:str(value),base:layer.base});
 }
 return{markets:[...markets.values()].sort((a,b)=>a.priority-b.priority),keys};
}
/** 登记仅修改本机目录配置；项目同名登记优先于本机，最终按 priority 升序。 */
export function registerMarketplace(options:MarketplaceOptions,registration:MarketRegistration):string {
 const home=path.resolve(options.workingDirectory,options.homeDirectory??process.env.QIAN_HOME??path.join(os.homedir(),'.qian')),file=path.join(home,'marketplaces.json');
 assertNoLinks(file);const config=readConfig(file),entries=Array.isArray(config.marketplaces)?config.marketplaces:[];
 if(!/^[a-z][a-z0-9-]*$/.test(registration.name)||!registration.source||!Number.isSafeInteger(registration.priority)||registration.priority<0)fail(file,'/marketplaces','市场登记无效。');
 if(!safeRegistrationSource(registration.source))fail(file,'/marketplaces','来源地址不能包含凭据或非法 URL。');
 config.marketplaces=[...entries.filter(entry=>obj(entry).name!==registration.name),{name:registration.name,source:registration.source,priority:registration.priority,...registration.trust?{trust:registration.trust}:{}}];
 mkdirSync(home,{recursive:true});const temporary=file+'.'+randomBytes(6).toString('hex')+'.tmp';writeFileSync(temporary,JSON.stringify(config,null,2)+'\n',{flag:'wx',mode:0o600});renameSync(temporary,file);return file;
}
export function parseMarketplaceReference(reference:string):{name:string;market?:string;version?:string;gitTag?:string}{
 const match=/^(?:([a-z][a-z0-9-]*)\/)?([a-z][a-z0-9-]*)(?:@(.+))?$/.exec(reference);
 if(!match)fail('argv','','组件引用必须为 name[@version] 或 market/name[@version]。','SURFACE_SCHEMA_INVALID');
 const version=match[3]?semverValid(match[3]):undefined;
 if(match[3]&&!version)fail('argv','','显式版本必须是精确 SemVer。','SURFACE_SCHEMA_INVALID');
 return{name:match[2],market:match[1],version:version??undefined,gitTag:version?'refs/tags/v'+version:undefined};
}
function publicKey(value:string):{key:KeyObject;fingerprint:string}{
 if(!value.startsWith('-----BEGIN PUBLIC KEY-----')||value.includes('PRIVATE KEY'))throw new Error('只能使用 Ed25519 公钥。');
 const key=createPublicKey(value);if(key.asymmetricKeyType!=='ed25519')throw new Error('只能使用 Ed25519 公钥。');
 return{key,fingerprint:sha256(key.export({type:'spki',format:'der'}))};
}
function readSmall(file:string):Buffer {assertNoLinks(file);if(!existsSync(file)||!lstatSync(file).isFile()||lstatSync(file).size>1_048_576)throw new Error('目录文件不存在或超过限制。');return readFileSync(file);}
async function catalog(registration:MarketRegistration,options:MarketplaceOptions,roots:Map<string,KeyObject>):Promise<Catalog>{
 let root=path.resolve(registration.baseDirectory??options.workingDirectory,registration.source),content:ContentSnapshot|undefined,commit:string|undefined;
 try{
  if(registration.source.startsWith('git+')){
   const address=new URL(registration.source.slice(4)),reference=decodeURIComponent(address.hash.slice(1));address.hash='';if(!reference)throw new Error('Git 市场必须指定引用。');
   const result=await readGit(address.toString(),reference,'.',options.gitMirrors?.[address.toString()]);content=result.content;commit=result.commit;root='';
  }else if(registration.source.startsWith('github-topic:'))throw new Error('社区索引获取由后续目录生成器提供，本切片不调用 GitHub 搜索接口。');
  else if(registration.source.includes('://'))throw new Error('市场登记支持本地目录或 git+https 来源。');
  else if(root.endsWith('/.qian/marketplace.json'))root=path.dirname(path.dirname(root));
  const read=(relative:string):Buffer=>content?content.files.get(relative)??(()=>{throw new Error('目录文件不存在。');})():readSmall(within(root,relative));
  const bytes=read('.qian/marketplace.json'),result=validateMarketplace('.qian/marketplace.json',bytes.toString('utf8'));
  if(!result.isValid||!result.marketplace)throw new MarketplaceFailure([...result.diagnostics]);
  const data=obj(result.marketplace);if(data.name!==registration.name)throw new Error('登记名称与市场名称不一致。');
  let fingerprint:string|null=null;
  const signatureBytes=content?.files.get('.qian/marketplace.json.sig')??(root&&existsSync(within(root,'.qian/marketplace.json.sig'))?read('.qian/marketplace.json.sig'):null);
  if(signatureBytes){
   const encoded=signatureBytes.toString('utf8').trim();
   const signature=signatureBytes.length===64?signatureBytes:/^[A-Za-z0-9+/]+={0,2}$/.test(encoded)?Buffer.from(encoded,'base64'):Buffer.alloc(0);
   for(const value of (obj(data.owner).publicKeys??[]) as string[]){
    const declared=publicKey(value.startsWith('-----BEGIN')?value:read(value.replace(/^\.\//,'')).toString('utf8'));
    if(roots.has(declared.fingerprint)&&signature.length===64&&verify(null,Buffer.from(sha256(bytes),'hex'),declared.key,signature)){fingerprint=declared.fingerprint;break;}
   }
  }
  const trusted=registration.trust!=='unverified'&&fingerprint!==null;
  if(!trusted&&!options.allowUnverified)fail('.qian/marketplace.json','','市场未通过受信任公钥验签；需显式 --allow-unverified，且仅允许 community 无代码组件。','SURFACE_SIGNATURE_INVALID');
  return{registration,data,entries:data.plugins as unknown as MarketEntry[],trusted,fingerprint,root,content,commit};
 }catch(error){if(error instanceof MarketplaceFailure)throw error;fail('.qian/marketplace.json','',error instanceof Error?error.message:'市场无法读取。');}
}
async function catalogs(options:MarketplaceOptions):Promise<{catalogs:Catalog[];diagnostics:SurfaceDiagnostic[]}>{
 const configured=marketplaceRegistrations(options),roots=new Map<string,KeyObject>(),diagnostics:SurfaceDiagnostic[]=[];
 for(const item of configured.keys){try{const parsed=publicKey(item.value.startsWith('-----BEGIN')?item.value:readSmall(path.resolve(item.base,item.value)).toString('utf8'));roots.set(parsed.fingerprint,parsed.key);}catch{fail('.qian/config.yml','/trust/keys','信任根必须为可读取的 Ed25519 公钥。');}}
 const output:Catalog[]=[];
 for(const registration of configured.markets){
  if(options.from&&registration.name!==options.from)continue;
  if(registration.trust==='unverified'&&!options.allowUnverified){diagnostics.push(marketDiagnostic('.qian/config.yml','/marketplaces','未验证市场未参与解析：'+registration.name,'SURFACE_POLICY_DENIED','warning'));continue;}
  output.push(await catalog(registration,options,roots));
 }
 return{catalogs:output,diagnostics};
}
export async function listRemoteMarketplaces(options:MarketplaceOptions):Promise<{entries:MarketListing[];diagnostics:SurfaceDiagnostic[]}>{
 const loaded=await catalogs(options),seen=new Set<string>(),entries:MarketListing[]=[];
 for(const market of loaded.catalogs)for(const entry of market.entries){
  const selected=!seen.has(entry.name);seen.add(entry.name);
  if(!selected)loaded.diagnostics.push(marketDiagnostic('.qian/marketplace.json','/plugins','同名条目按优先级选择；可用 --from 或 market/name 明确来源。','SURFACE_NAME_MISMATCH','warning'));
  entries.push({...entry,market:market.registration.name,priority:market.registration.priority,trust:market.trusted?'verified':'unverified',selected,validation:'catalog-only',...(entry.policy.authentication==='ON_USE'?{note:'S1 仅登记并提示，不自动安装或激活。'}:{})});
 }
 return{entries,diagnostics:loaded.diagnostics};
}
export async function resolveMarketplacePlugin(reference:string,options:MarketplaceOptions):Promise<ResolvedMarketplacePlugin>{
 const parsed=parseMarketplaceReference(reference),requestedMarket=options.from??parsed.market;
 if(options.from&&parsed.market&&options.from!==parsed.market)fail('argv','','--from 与显式市场前缀不一致。');
 const loaded=await catalogs({...options,from:requestedMarket}),matches=loaded.catalogs.flatMap(market=>market.entries.filter(entry=>entry.name===parsed.name&&(!requestedMarket||market.registration.name===requestedMarket)).map(entry=>({market,entry})));
 if(!matches.length)fail('.qian/config.yml','/marketplaces','已登记且可用的市场中未找到组件。','SURFACE_REFERENCE_UNKNOWN');
 const {market,entry}=matches[0],source={...entry.source},diagnostics=[...loaded.diagnostics];
 if(matches.length>1)diagnostics.push(marketDiagnostic('.qian/marketplace.json','/plugins','同名组件采用优先级最靠前的市场；来源会记录到 lock。','SURFACE_NAME_MISMATCH','warning'));
 let content:ContentSnapshot,commit:string|undefined,refKind:ResolvedMarketplacePlugin['refKind'],resolvedRef:string|undefined;
 try{
  if(source.type==='local'){
   if(market.content){const prefix=source.path==='.'||source.path==='./'?'':source.path!.replace(/^\.\//,'').replace(/\/$/,'')+'/';content=snapshot(new Map([...market.content.files].filter(([name])=>name.startsWith(prefix)).map(([name,bytes])=>[name.slice(prefix.length),bytes])));}
   else content=readDirectory(within(market.root,source.path!));
  }else if(source.type==='url'){
   const bytes=await download(source.url!,options.fetcher);if('sha256:'+sha256(bytes)!==source.digest)fail('.qian/marketplace.json','/plugins','归档 digest 不匹配。','SURFACE_DIGEST_MISMATCH');
   content=await unpack(bytes,bytes[0]===0x50&&bytes[1]===0x4b?'zip':'tgz');
  }else{
   const fetched=await readGit(source.repo!,parsed.gitTag??source.ref!,source.path!,options.gitMirrors?.[source.repo!]);content=fetched.content;commit=fetched.commit;refKind=fetched.refKind;resolvedRef=fetched.resolvedRef;
   if(refKind==='branch'&&!source.digest)throw new Error('分支引用必须声明内容 digest。');
   source.ref=parsed.gitTag??source.ref;
  }
  if(source.type!=='url'&&source.digest&&source.digest!==content.digest)fail('.qian/marketplace.json','/plugins','组件目录 digest 不匹配。','SURFACE_DIGEST_MISMATCH');
  const temporary=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-component-'));
  try{
   materialize(content,temporary);
   const validated=validateComponent(temporary,{profile:options.profile??'minimal'});
   diagnostics.push(...validated.diagnostics);if(!validated.isValid||!validated.component)throw new MarketplaceFailure(diagnostics);
   const component=validated.component,manifest=obj(component.manifest),runtime=obj(manifest.runtime),policy=obj(manifest.policy),version=str(manifest.version);
   if(parsed.version&&version!==parsed.version)throw new Error('请求版本与组件清单版本不一致。');
   if(!market.trusted&&(manifest.runtime!==undefined||policy.tier!=='community'))throw new Error('未验证市场只允许 community 且无代码的组件。');
   if(policy.authentication!==entry.policy.authentication)diagnostics.push(marketDiagnostic('.qian-plugin/plugin.json','/policy/authentication','认证策略与市场不一致，采用市场策略。','SURFACE_POLICY_DENIED','warning'));
   const packages={nuget:Array.isArray(runtime.nuget)?runtime.nuget.map(value=>{const item=obj(value);return str(item.id)+'@'+str(item.version);}):[],npm:Array.isArray(runtime.npm)?runtime.npm.map(value=>{const item=obj(value);return str(item.name)+'@'+str(item.version);}):[]};
   return{name:entry.name,market:market.registration.name,marketSource:market.registration.source,marketCommit:market.commit,marketKey:market.fingerprint,source,version,requestedVersion:parsed.version,commit,refKind,resolvedRef,digest:content.digest,files:content.records,component,content,packages,policy:entry.policy,trust:market.trusted?'verified':'unverified',diagnostics};
  }finally{rmSync(temporary,{recursive:true,force:true});}
 }catch(error){if(error instanceof MarketplaceFailure)throw error;fail('.qian/marketplace.json','/plugins',error instanceof Error?error.message:'组件来源验证失败。');}
}
