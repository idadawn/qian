import {spawn} from 'node:child_process';
import {existsSync,readFileSync,readdirSync,realpathSync,writeFileSync} from 'node:fs';
import path from 'node:path';
import {MarketplaceFailure,marketDiagnostic} from './marketplaces.js';
import {assertNoLinks,within} from './marketplace-files.js';
import {parsePackageReference,packageIdentity,type PackageReference,type PackageKind} from './package-references.js';

interface Dependency {name:string;version:string;definition?:string;section?:string}
export interface NugetWorkspace {project:string;centralFile?:string;intermediateDirectory:string;lockFile:string;dependencies:Dependency[]}
export interface NpmWorkspace {manifest:string;dependencies:Dependency[]}
export interface PackageWorkspace {root:string;nuget?:NugetWorkspace;npm?:NpmWorkspace}
export interface PackageChanges {add:readonly PackageReference[];remove:readonly PackageReference[];ensure?:readonly PackageReference[]}
export interface PackageMutationTarget {path:string;kind:'file'|'directory'}

function invalid(file:string,message:string):never {
 throw new MarketplaceFailure([marketDiagnostic(file,'',message)]);
}
function unavailable(tool:string):never {
 const diagnostic=marketDiagnostic('package-manager','','包管理器未完成操作；检查工具、包源与宿主配置。','UNAVAILABLE');
 const message='依赖的包管理器不可用或操作失败。';
 diagnostic.error={...diagnostic.error,type:'https://qian.dev/problems/protocol/UNAVAILABLE',code:'UNAVAILABLE',
  title:'服务暂不可用',status:503,message,detail:message,errors:[{code:'UNAVAILABLE',message:tool+' 未完成操作。',pointer:'package-manager#'}]};
 throw new MarketplaceFailure([diagnostic]);
}

/** 不使用 shell，不转发原始输出；错误中的包源 URL、凭据或脚本输出不会进入 CLI 诊断。 */
async function command(executable:string,args:readonly string[],root:string,label:string,timeout=120_000):Promise<string> {
 return await new Promise<string>((resolve,reject)=>{
  const child=spawn(executable,[...args],{cwd:root,shell:false,detached:process.platform!=='win32',
   stdio:['ignore','pipe','pipe'],env:{...process.env,CI:'true',DOTNET_NOLOGO:'1',DOTNET_CLI_TELEMETRY_OPTOUT:'1',
    MSBUILDDISABLENODEREUSE:'1',DOTNET_CLI_USE_MSBUILD_SERVER:'0',UseSharedCompilation:'false',NPM_CONFIG_UPDATE_NOTIFIER:'false'}});
  let stdout='',size=0,failed=false,settled=false,escalation:ReturnType<typeof setTimeout>|undefined;
  const signal=(value:NodeJS.Signals):void=>{
   try{if(child.pid&&process.platform!=='win32')process.kill(-child.pid,value);else child.kill(value);}catch{ /* 子进程可能已退出。 */ }
  };
  const stop=():void=>{if(failed)return;failed=true;signal('SIGTERM');escalation=setTimeout(()=>signal('SIGKILL'),1000);escalation.unref();};
  const timer=setTimeout(stop,timeout);timer.unref();
  const finish=(code:number|null):void=>{
   if(settled)return;settled=true;clearTimeout(timer);if(escalation)clearTimeout(escalation);
   if(failed||code!==0){try{unavailable(label);}catch(error){reject(error);}}else resolve(stdout);
  };
  child.stdout.setEncoding('utf8');
  child.stdout.on('data',(text:string)=>{size+=Buffer.byteLength(text);if(size>16*1024*1024)stop();else stdout+=text;});
  child.stderr.on('data',(bytes:Buffer)=>{size+=bytes.length;if(size>16*1024*1024)stop();});
  child.once('error',()=>{failed=true;finish(null);});
  child.once('close',finish);
 });
}

function npmExecutable():string {
 const supplied=process.env.npm_execpath;
 if(supplied&&path.basename(supplied)==='npm-cli.js'&&existsSync(supplied))return realpathSync(supplied);
 for(const directory of (process.env.PATH??'').split(path.delimiter)){
  if(!directory)continue;
  for(const name of process.platform==='win32'?['npm.cmd','npm']:['npm']){
   const entry=path.join(directory,name);if(!existsSync(entry))continue;
   const resolved=realpathSync(entry);
   if(path.basename(resolved)==='npm-cli.js')return resolved;
   const candidate=path.join(path.dirname(resolved),'node_modules/npm/bin/npm-cli.js');
   if(existsSync(candidate))return realpathSync(candidate);
  }
 }
 return unavailable('npm');
}
function json(file:string):Record<string,unknown> {
 try{const value:unknown=JSON.parse(readFileSync(file,'utf8'));if(value&&typeof value==='object'&&!Array.isArray(value))return value as Record<string,unknown>;}catch{ /* 不回显原始 JSON。 */ }
 return invalid(path.basename(file),'宿主配置不是合法 JSON 对象。');
}
function pinned(value:string):string {
 const match=/^\[([^,\[\]]+)\]$/.exec(value);return match?.[1]??value;
}

/** 读取实际 MSBuild 计算结果与 npm 直接依赖；不以正则猜测 XML 的条件/中央版本。 */
export async function inspectPackageWorkspace(directory:string,kinds:readonly PackageKind[]):Promise<PackageWorkspace> {
 const root=realpathSync(path.resolve(directory)),result:PackageWorkspace={root};assertNoLinks(root);
 if(kinds.includes('nuget')){
  const projects=readdirSync(root).filter(name=>name.endsWith('.csproj'));
  if(projects.length!==1)invalid('argv','NuGet 安装需要在恰有一个 csproj 的宿主目录执行。');
  const project=path.join(root,projects[0]);assertNoLinks(project);
  const text=await command('dotnet',['msbuild',project,'-nologo',
   '-getProperty:ManagePackageVersionsCentrally,DirectoryPackagesPropsPath,MSBuildProjectExtensionsPath,NuGetLockFilePath',
   '-getItem:PackageReference,PackageVersion'],root,'dotnet');
  let evaluated:{Properties:Record<string,string>;Items:Record<string,Record<string,string>[]>};
  try{evaluated=JSON.parse(text);}catch{return unavailable('dotnet');}
  if(!evaluated.Properties||!evaluated.Items)unavailable('dotnet');
  const central=new Map((evaluated.Items.PackageVersion??[]).map(item=>[item.Identity.toLowerCase(),item.Version]));
  const dependencies=(evaluated.Items.PackageReference??[]).map(item=>({name:item.Identity,
   version:pinned(item.VersionOverride||item.Version||central.get(item.Identity.toLowerCase())||''),definition:item.DefiningProjectFullPath}));
  result.nuget={project,dependencies,
   ...(evaluated.Properties.ManagePackageVersionsCentrally==='true'?{centralFile:evaluated.Properties.DirectoryPackagesPropsPath}:{}),
   intermediateDirectory:path.resolve(root,evaluated.Properties.MSBuildProjectExtensionsPath||'obj'),
   lockFile:path.resolve(root,evaluated.Properties.NuGetLockFilePath||'packages.lock.json')};
 }
 if(kinds.includes('npm')){
  const manifest=path.join(root,'package.json');assertNoLinks(manifest);
  const data=existsSync(manifest)?json(manifest):{},manager=data.packageManager;
  if(typeof manager==='string'&&!manager.startsWith('npm@'))invalid('package.json','此宿主声明了其它包管理器；请在 npm 消费宿主中安装 npm 组件。');
  const dependencies:Dependency[]=[];
  for(const section of ['dependencies','devDependencies','optionalDependencies','peerDependencies']){
   const entries=data[section];if(entries===undefined)continue;
   if(!entries||typeof entries!=='object'||Array.isArray(entries))invalid('package.json','宿主依赖集合无效。');
   for(const[name,version]of Object.entries(entries)){
    if(typeof version!=='string')invalid('package.json','宿主依赖版本必须为字符串。');
    dependencies.push({name,version,section});
   }
  }
  result.npm={manifest,dependencies};
 }
 return result;
}

/** 所有可变宿主文件必须先交给事务备份；拒绝把隐式改动带到工作区外。 */
export function packageMutationTargets(workspace:PackageWorkspace,changes:PackageChanges):PackageMutationTarget[] {
 const targets:PackageMutationTarget[]=[];
 const add=(file:string,kind:'file'|'directory'):void=>{
  const relative=path.relative(workspace.root,file);
  if(!relative||relative.startsWith('.qian'+path.sep))invalid('package-manager','包管理器输出不能覆盖工作区根或 Qian 安装状态。');
  const checked=within(workspace.root,relative.split(path.sep).join('/'));assertNoLinks(checked);targets.push({path:checked,kind});
 };
 const kinds=new Set([...changes.add,...changes.remove,...changes.ensure??[]].map(reference=>reference.kind));
 if(kinds.has('nuget')){
  const state=workspace.nuget;if(!state)invalid('package-manager','未准备 NuGet 宿主。');
  add(state.project,'file');if(state.centralFile)add(state.centralFile,'file');add(state.lockFile,'file');add(state.intermediateDirectory,'directory');
 }
 if(kinds.has('npm')){
  if(!workspace.npm)invalid('package-manager','未准备 npm 宿主。');
  add(path.join(workspace.root,'package.json'),'file');add(path.join(workspace.root,'package-lock.json'),'file');
  add(path.join(workspace.root,'npm-shrinkwrap.json'),'file');add(path.join(workspace.root,'node_modules'),'directory');
 }
 return targets;
}

/** 执行已确认的变化；调用者必须持有安装锁且已备份 packageMutationTargets。 */
export async function applyPackageChanges(workspace:PackageWorkspace,changes:PackageChanges):Promise<void> {
 packageMutationTargets(workspace,changes);
 for(const reference of [...changes.add,...changes.remove,...changes.ensure??[]]){
  const verified=parsePackageReference(reference.kind,reference.specification);
  if(verified.name!==reference.name||verified.version!==reference.version)invalid('package-manager','包计划与精确声明不一致。');
 }
 const nugetAdd=changes.add.filter(reference=>reference.kind==='nuget'),nugetRemove=changes.remove.filter(reference=>reference.kind==='nuget');
 const nugetVerify=[...nugetAdd,...(changes.ensure??[]).filter(reference=>reference.kind==='nuget')];
 if(nugetAdd.length||nugetRemove.length||nugetVerify.length){
  const state=workspace.nuget!;
  for(const reference of [...nugetRemove].reverse())await command('dotnet',['remove',state.project,'package',reference.name],workspace.root,'dotnet');
  for(const reference of nugetAdd)await command('dotnet',['add',state.project,'package',reference.name,'--version','['+reference.version+']','--no-restore'],workspace.root,'dotnet');
  await command('dotnet',['restore',state.project,'--use-lock-file','--force-evaluate','-p:NuGetLockFilePath='+state.lockFile],workspace.root,'dotnet');
  const assets=path.join(state.intermediateDirectory,'project.assets.json');assertNoLinks(assets);
  const libraries=json(assets).libraries;
  if(!libraries||typeof libraries!=='object'||Array.isArray(libraries))unavailable('dotnet');
  const restored=new Set(Object.entries(libraries).filter(([,value])=>value&&typeof value==='object'&&(value as {type?:string}).type==='package').map(([name])=>name.toLowerCase()));
  for(const reference of nugetVerify)if(!restored.has((reference.name+'/'+reference.version).toLowerCase()))unavailable('dotnet');
 }
 const npmAdd=changes.add.filter(reference=>reference.kind==='npm'),npmRemove=changes.remove.filter(reference=>reference.kind==='npm');
 const npmVerify=[...npmAdd,...(changes.ensure??[]).filter(reference=>reference.kind==='npm')];
 if(npmAdd.length||npmRemove.length||npmVerify.length){
  const executable=npmExecutable(),common=['--prefix',workspace.root,'--global=false','--location=project','--ignore-scripts','--no-audit','--no-fund','--package-lock=true'];
  if(!existsSync(workspace.npm!.manifest))writeFileSync(workspace.npm!.manifest,JSON.stringify({private:true},null,2)+'\n',{flag:'wx',mode:0o600});
  if(npmRemove.length)await command(process.execPath,[executable,'uninstall','--save-prod',...common,...[...npmRemove].reverse().map(reference=>reference.name)],workspace.root,'npm');
  if(npmAdd.length)await command(process.execPath,[executable,'install','--save-prod','--save-exact',...common,...npmAdd.map(reference=>reference.specification)],workspace.root,'npm');
  else if(npmVerify.length)await command(process.execPath,[executable,'install',...common],workspace.root,'npm');
  const data=json(workspace.npm!.manifest),dependencies=data.dependencies as Record<string,unknown>|undefined;
  for(const reference of npmVerify){
   const installed=within(workspace.root,'node_modules/'+reference.name+'/package.json');assertNoLinks(installed);
   if(dependencies?.[reference.name]!==reference.version||json(installed).version!==reference.version)unavailable('npm');
  }
 }
}

export function workspaceDependency(workspace:PackageWorkspace,reference:PackageReference):Dependency|undefined {
 return (reference.kind==='nuget'?workspace.nuget?.dependencies:workspace.npm?.dependencies)
  ?.find(item=>packageIdentity({kind:reference.kind,name:item.name})===packageIdentity(reference));
}
