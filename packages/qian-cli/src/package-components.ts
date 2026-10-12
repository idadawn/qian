import {existsSync,mkdirSync,mkdtempSync,readFileSync,realpathSync,rmSync,writeFileSync} from 'node:fs';
import path from 'node:path';
import {runPackageCommand,PackageCommandFailure} from './package-process.js';
import {inspectPackageWorkspace,npmExecutable} from './package-manager.js';
import {collectPackageReferences,type PackageReference} from './package-references.js';
import {assertNoLinks,within,unpack,snapshot,type ContentSnapshot} from './marketplace-files.js';
import {installFailure,installObject} from './installed-components.js';
import {archiveEvidence,type PackageArtifactEvidence} from './package-integrity.js';

export interface PackageComponent {reference:PackageReference;component:ContentSnapshot;artifactDigest:string}
export interface PackageComponentFetch {components:PackageComponent[];artifacts:PackageArtifactEvidence[];dispose():void}
const xml=(value:string):string=>value.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;');

/** 只在隔离目录还原或取归档；不改宿主依赖、不复制凭据配置、不执行组件入口。 */
export async function fetchPackageComponents(rootDirectory:string,packages:{nuget:readonly string[];npm:readonly string[]}):Promise<PackageComponentFetch> {
 const root=realpathSync(rootDirectory),references=collectPackageReferences([{id:'qian.package-plan',packages}]),components:PackageComponent[]=[],artifacts:PackageArtifactEvidence[]=[];
 if(!references.length)return{components,artifacts,dispose(){}};
 const parent=path.join(root,'.qian/.staging');assertNoLinks(parent);mkdirSync(parent,{recursive:true,mode:0o700});
 const ignore=path.join(parent,'.gitignore');assertNoLinks(ignore);if(!existsSync(ignore))writeFileSync(ignore,'*\n',{flag:'wx',mode:0o600});
 const directory=mkdtempSync(path.join(parent,'packages-'));let retain=false;
 const cleanup=():void=>{if(!retain){assertNoLinks(directory);rmSync(directory,{recursive:true,force:true});}};
 try{
  const nuget=references.filter(reference=>reference.kind==='nuget');
  if(nuget.length){
   const workspace=await inspectPackageWorkspace(root,['nuget']),folder=path.join(directory,'nuget');mkdirSync(folder);
   // NuGet 配置按宿主目录继承；MSBuild 工程导入在这里封闭，防止借用宿主构建脚本。
   for(const name of ['Directory.Build.props','Directory.Build.targets','Directory.Packages.props'])writeFileSync(path.join(folder,name),'<Project />');
   const project=path.join(folder,'Inspect.csproj');
   writeFileSync(project,'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFrameworks>'+xml(workspace.nuget!.targetFrameworks.join(';'))+'</TargetFrameworks><ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup>'+nuget.map(reference=>'<PackageReference Include="'+xml(reference.name)+'" Version="['+xml(reference.version)+']" />').join('')+'</ItemGroup></Project>');
   await runPackageCommand('dotnet',['restore',project,'--packages',path.join(folder,'packages'),'--disable-parallel',
    '-p:ImportDirectoryBuildProps=false','-p:ImportDirectoryBuildTargets=false','-p:ImportDirectoryPackagesProps=false','-p:ManagePackageVersionsCentrally=false'],folder,'dotnet');
   const assetsFile=path.join(folder,'obj/project.assets.json');assertNoLinks(assetsFile);
   const assets=JSON.parse(readFileSync(assetsFile,'utf8')) as {libraries:Record<string,{type?:string;path?:string;files?:string[]}>;packageFolders:Record<string,unknown>};
   const requested=new Set(nuget.map(reference=>(reference.name+'/'+reference.version).toLowerCase()));
   for(const[key,library]of Object.entries(assets.libraries??{})){
    if(!requested.has(key.toLowerCase())||library.type!=='package'||!library.path)continue;
    const slash=key.lastIndexOf('/'),reference:PackageReference={kind:'nuget',name:key.slice(0,slash),version:key.slice(slash+1),specification:key.slice(0,slash)+'@'+key.slice(slash+1)};
    let found=false;
    for(const packageFolder of Object.keys(assets.packageFolders??{})){
     if(!existsSync(packageFolder))continue;
     const packageRoot=within(realpathSync(packageFolder),library.path);
     if(!existsSync(packageRoot))continue;
     const archive=within(packageRoot,reference.name.toLowerCase()+'.'+reference.version.toLowerCase()+'.nupkg');
     if(!existsSync(archive))installFailure('package-manager','实际 NuGet 包目录缺少归档，不能借用其它缓存的摘要。');
     const bytes=readFileSync(archive),evidence=archiveEvidence('nuget',reference.name,reference.version,bytes),content=await unpack(bytes,'zip');
     artifacts.push(evidence);
     if(content.files.has('qian/.qian-plugin/plugin.json')){
      const component=snapshot(new Map([...content.files].filter(([name])=>name.startsWith('qian/')).map(([name,value])=>[name.slice(5),value])));
      components.push({reference,component,artifactDigest:evidence.digest});
     }
     found=true;requested.delete(key.toLowerCase());break;
    }
    if(!found)installFailure('package-manager','已还原代码包归档缺失，不能建立来源证据。');
   }
   if(requested.size)installFailure('package-manager','未能取得全部精确引用的 NuGet 包归档。');
  }
  for(const reference of references.filter(item=>item.kind==='npm')){
   const folder=path.join(directory,'npm-'+components.length+'-'+reference.name.replaceAll('/','_'));assertNoLinks(folder);mkdirSync(folder,{recursive:true});
   const output=await runPackageCommand(process.execPath,[npmExecutable(),'pack',reference.specification,'--ignore-scripts','--json','--pack-destination',folder,'--global=false'],root,'npm');
   let packed:{filename?:string;name?:string;version?:string}[];try{packed=JSON.parse(output);}catch{installFailure('package-manager','npm 归档结果无效。');}
   if(packed.length!==1||packed[0].name!==reference.name||packed[0].version!==reference.version||typeof packed[0].filename!=='string'||path.basename(packed[0].filename)!==packed[0].filename)
    installFailure('package-manager','npm 归档身份或路径与声明不一致。');
   const archive=within(folder,packed[0].filename!),bytes=readFileSync(archive),content=await unpack(bytes,'tgz');
   const files=new Map([...content.files].filter(([name])=>name.startsWith('package/')).map(([name,value])=>[name.slice(8),value]));
   const packageJson=files.get('package.json');if(!packageJson)installFailure('package-manager','npm 归档缺少 package.json。');
   let manifest:Record<string,unknown>;try{manifest=JSON.parse(packageJson.toString('utf8'));}catch{installFailure('package-manager','npm 包声明无效。');}
   if(!manifest||manifest.name!==reference.name||manifest.version!==reference.version)installFailure('package-manager','npm 归档内身份与声明不一致。');
   const evidence=archiveEvidence('npm',reference.name,reference.version,bytes);artifacts.push(evidence);
   const declaration=installObject(manifest.qian).component;
   const componentPath=typeof declaration==='string'?declaration.replace(/^\.\//,''):files.has('qian/.qian-plugin/plugin.json')?'qian/.qian-plugin/plugin.json':undefined;
   if(!componentPath)continue;
   if(componentPath!=='.qian-plugin/plugin.json'&&!componentPath.endsWith('/.qian-plugin/plugin.json'))installFailure('package.json','qian.component 必须指向组件清单。');
   const prefix=componentPath.slice(0,-'.qian-plugin/plugin.json'.length);
   const component=snapshot(new Map([...files].filter(([name])=>name.startsWith(prefix)).map(([name,value])=>[name.slice(prefix.length),value])));
   if(!component.files.has('.qian-plugin/plugin.json'))installFailure('package.json','qian.component 指向的清单不存在。');
   components.push({reference,component,artifactDigest:evidence.digest});
  }
  return{components,artifacts,dispose:cleanup};
 }catch(error){if(error instanceof PackageCommandFailure&&!error.terminationConfirmed)retain=true;cleanup();throw error;}
}
