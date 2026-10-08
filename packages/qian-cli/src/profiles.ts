import {existsSync, readFileSync, readdirSync, lstatSync, statSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {randomBytes} from 'node:crypto';
import {satisfies, rcompare, validRange} from 'semver';
import {Ajv2020} from 'ajv/dist/2020.js';
import {parseSurface, validateSurfaceSchema, type JsonValue, type SurfaceDiagnostic, type SurfaceSyntax} from '@qian/surfaces';

type Obj = {[key: string]: JsonValue};
const obj = (value: JsonValue | undefined): Obj => typeof value === 'object' && value !== null && !Array.isArray(value) ? value : {};
const str = (value: JsonValue | undefined): string => typeof value === 'string' ? value : '';
const strings = (value: JsonValue | undefined): string[] => Array.isArray(value) ? value.map(v => String(v)) : [];
const escape = (value: string): string => value.replaceAll('~','~0').replaceAll('/','~1');
export interface ProfileOptions { workingDirectory: string; profile?: string; patches?: readonly string[]; homeDirectory?: string; includeHomePatch?: boolean; componentDirectories?: readonly string[] }
export interface ProfileLayer {index: number; kind: string; path: string; ref: string | null; present: boolean}
export interface ProfileEntry {id: string; scope: string; plugin: string; config: JsonValue; layer: number; inject: string[]; provides: string[]; note?: string}
export interface ProfileSnapshot {profile: string; layers: ProfileLayer[]; entries: ProfileEntry[]; mountOrder: string[]}
export interface ProfileResult {snapshot: ProfileSnapshot | null; diagnostics: SurfaceDiagnostic[]; isValid: boolean}
interface Component {id:string;version:string;directory:string;manifest:Obj}
interface Working {id:string;scope:string;plugin:string;config:Obj;layer:number;disabled:boolean;note?:string;configFile:string;configPointer:string;sourceFile:string;sourcePointer:string}
class CompositionFailed extends Error {}
/** 本地静态装配；不启动业务 Provider、不解析密钥、不写工作区。 */
export function loadProfile(options: ProfileOptions): ProfileResult { return new Composition(options).load(); }
class Composition {
  private readonly root: string;
  private readonly profile: string;
  private readonly trace = randomBytes(16).toString('hex');
  private readonly diagnostics: SurfaceDiagnostic[]=[];
  private readonly layers: ProfileLayer[]=[];
  private readonly entries = new Map<string,Working>();
  private readonly components: Component[]=[];
  private readonly sources = new Map<string,SurfaceSyntax>();
  private definition: Obj={};
  private profileFile='';
  constructor(private readonly options: ProfileOptions) {this.root=path.resolve(options.workingDirectory);this.profile=options.profile??'platform';}
  load(): ProfileResult {
    try {
      if(!/^[a-z][a-z0-9-]*$/.test(this.profile)) throw new Error('profile');
      this.profileFile=path.join(this.root,'profiles',this.profile,'qian.profile.yml');
      this.definition=obj(this.read(this.profileFile,'profile'));
      if(this.definition.name!==this.profile)this.fail(this.profileFile,'/name','profile 名必须与目录一致。');
      if(['gateway','identity'].includes(this.profile)&&obj(this.definition.hooks).allowCommand===true)this.fail(this.profileFile,'/hooks/allowCommand','此 profile 不允许 command hook。');
      this.discover(path.join(this.root,'plugins'));this.discover(path.join(this.root,'.qian','plugins'));
      for(const directory of this.options.componentDirectories??[])this.discover(path.resolve(this.root,directory));
      for(const reference of strings(this.definition.bundles)) {
        const bundle=this.resolve(reference,this.profileFile,'/bundles');
        const file=this.within(bundle.directory,str(obj(bundle.manifest.surfaces).patch)||'./config/qian.patch.yml');
        this.layer('bundle',file,bundle.id+'@'+bundle.version,true);
      }
      if(this.definition.patch)this.layer('profile-patch',this.within(path.dirname(this.profileFile),str(this.definition.patch)),null,true);
      if(this.options.includeHomePatch!==false){const home=this.options.homeDirectory??process.env.QIAN_HOME??path.join(os.homedir(),'.qian');this.layer('home-patch',path.join(path.resolve(this.root,home),'qian.patch.yml'),null,false);}
      for(const patch of this.options.patches??[])this.layer('overlay',path.resolve(this.root,patch),null,true);
      const entries=this.resolveEntries();
      return{snapshot:{profile:this.profile,layers:this.layers,entries,mountOrder:this.order(entries)},diagnostics:this.diagnostics,isValid:true};
    }catch(error){if(!(error instanceof CompositionFailed))this.error(this.profileFile||'qian.profile.yml','','profile 或组件内容无法解析。');return{snapshot:null,diagnostics:this.diagnostics,isValid:false};}
  }
  private read(file:string,schema:string):JsonValue {
    if(!existsSync(file)||statSync(file).size>1_048_576)this.fail(file,'','文件不存在或超过大小限制。');
    const display=this.display(file),parsed=parseSurface(display,new TextDecoder('utf-8',{fatal:true}).decode(readFileSync(file)),false,this.trace);
    this.diagnostics.push(...parsed.diagnostics);
    if(!parsed.syntax||parsed.diagnostics.some(d=>d.severity==='error'))throw new CompositionFailed();
    this.sources.set(display,parsed.syntax);
    const errors=validateSurfaceSchema(schema,display,parsed.syntax.data,this.trace,parsed.syntax.lines);
    this.diagnostics.push(...errors);if(errors.some(d=>d.severity==='error'))throw new CompositionFailed();
    return parsed.syntax.data;
  }
  private discover(directory:string,depth=0):void {
    if(!existsSync(directory))return;
    if(depth>32)this.fail(directory,'','组件目录深度超过限制。');
    if(lstatSync(directory).isSymbolicLink())this.fail(directory,'','组件目录不能是符号链接。');
    const manifestFile=path.join(directory,'.qian-plugin','plugin.json');
    if(existsSync(manifestFile)) {
      this.within(directory,'./.qian-plugin/plugin.json');
      const manifest=obj(this.read(manifestFile,'plugin')),component={id:str(manifest.id),version:str(manifest.version),directory,manifest};
      if(this.components.some(c=>c.id===component.id&&c.version===component.version))this.fail(manifestFile,'/id','同一组件版本在目录中重复。');
      this.components.push(component);if(this.components.length>1024)this.fail(manifestFile,'','组件数量超过限制。');
      this.discover(path.join(directory,'components'),depth+1);return;
    }
    for(const entry of readdirSync(directory,{withFileTypes:true}).sort((a,b)=>a.name<b.name?-1:a.name>b.name?1:0))if((entry.isDirectory()||entry.isSymbolicLink())&&!['.git','node_modules','bin','obj','dist'].includes(entry.name))this.discover(path.join(directory,entry.name),depth+1);
  }
  private resolve(reference:string,file:string,pointer:string):Component {
    const [id,range='*']=reference.split('@',2);if(!validRange(range))this.fail(file,pointer,'组件版本范围无效。');
    const candidate=this.find(reference);
    if(!candidate)this.fail(file,pointer,'本地目录没有满足版本范围的组件；先安装或提供组件目录。');
    return candidate;
  }
  private find(reference:string):Component|undefined{
    const[id,range='*']=reference.split('@',2);
    if(!validRange(range))return undefined;
    return this.components.filter(c=>c.id===id&&satisfies(c.version,range)).sort((a,b)=>rcompare(a.version,b.version))[0];
  }
  private layer(kind:string,file:string,reference:string|null,required:boolean):void {
    const index=this.layers.length,present=existsSync(file);this.layers.push({index,kind,path:this.display(file),ref:reference,present});if(!present&&!required)return;
    const patch=this.read(file,'patch') as JsonValue[],seen=new Set<string>();
    patch.forEach((value,position)=>{
      const entry=obj(value),pointer='/'+position,id=str(entry.id),scope=str(entry.scope)||'global',key=scope+'/'+id,plugin=str(entry.plugin);
      if(seen.has(key))this.fail(file,pointer+'/id','同一层不能重复声明同一 scope/id。');seen.add(key);
      let current=this.entries.get(key);
      if(!current){
        if(!plugin)this.fail(file,pointer+'/plugin','新条目必须声明 plugin。');
        if(!Object.hasOwn(entry,'config'))this.fail(file,pointer+'/config','新条目必须声明 config，可为空对象。');
        current={id,scope,plugin,config:{},layer:index,disabled:false,configFile:file,configPointer:pointer+'/config',sourceFile:file,sourcePointer:pointer};
        const global=this.entries.get('global/'+id);if(scope!=='global'&&global)this.protect(global,plugin,false,file,pointer);
      }
      const disabled=typeof entry.disabled==='boolean'?entry.disabled:current.disabled;
      this.protect(current,plugin||current.plugin,disabled,file,pointer);
      if(plugin)current.plugin=plugin;
      if(Object.hasOwn(entry,'config')){current.config=structuredClone(obj(entry.config));current.configFile=file;current.configPointer=pointer+'/config';}
      current.disabled=disabled;current.layer=index;current.sourceFile=file;current.sourcePointer=pointer;
      if(typeof entry.note==='string')current.note=entry.note;
      this.entries.set(key,current);
    });
  }
  private protect(current:Working,replacement:string,disabled:boolean,file:string,pointer:string):void{
    const component=this.find(current.plugin);if(!component)return;
    if(obj(component.manifest.policy).required===true&&(disabled||replacement.split('@')[0]!==component.id))this.fail(file,pointer,'required 组件不能禁用或替换为另一个组件。');
  }
  private resolveEntries():ProfileEntry[]{
    const entries:ProfileEntry[]=[];
    for(const entry of this.entries.values())if(!entry.disabled){
      const component=this.resolve(entry.plugin,entry.sourceFile,entry.sourcePointer+'/plugin'),kind=entry.scope==='global'?'global':entry.scope.split(':',2)[0];
      if(!strings(component.manifest.scopes).includes(kind))this.fail(entry.sourceFile,entry.sourcePointer+'/scope','组件不允许此 scope 种类。');
      if(obj(this.definition.build).aot===true&&component.manifest.runtime&&(obj(component.manifest.runtime).form==='function'||component.manifest['x-aot']!==true))this.fail(entry.sourceFile,entry.sourcePointer+'/plugin','AOT profile 含未声明 AOT 兼容的代码组件。');
      const config=this.config(component,entry);
      if(component.manifest['x-skeleton']===true)this.error(entry.sourceFile,entry.sourcePointer+'/plugin','配置骨架不代表业务 Provider 已实现；启动宿主仍需真实代码。','warning');
      entries.push({id:entry.id,scope:entry.scope,plugin:component.id+'@'+component.version,config,layer:entry.layer,inject:strings(component.manifest.inject),provides:strings(component.manifest.provides),...(entry.note!==undefined?{note:entry.note}:{})});
    }
    return entries;
  }
  private config(component:Component,entry:Working):JsonValue{
    const file=this.within(component.directory,str(obj(component.manifest.runtime).configSchema)||'./schemas/config.schema.json');
    if(!existsSync(file)){if(Object.keys(entry.config).length)this.fail(entry.configFile,entry.configPointer,'有配置的组件必须提供 config.schema.json。');return entry.config;}
    if(statSync(file).size>1_048_576)this.fail(file,'','Schema 超过大小限制。');
    const schema=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(readFileSync(file))) as JsonValue,config=defaults(schema,structuredClone(entry.config));
    const validate=new Ajv2020({allErrors:true,strict:false,validateFormats:false}).compile(schema as object);
    if(!validate(config)){
      for(const error of validate.errors??[])this.error(entry.configFile,entry.configPointer+error.instancePath+(error.keyword==='additionalProperties'?'/'+escape(String(error.params.additionalProperty)):''),'条目配置不符合组件 config.schema。');
      throw new CompositionFailed();
    }
    const sensitive=(value:JsonValue,rule:JsonValue|undefined,pointer:string,depth=0):void=>{
      if(depth>64)throw new Error('schema depth');
      const referenced=localReference(obj(rule),schema);if(referenced!==undefined)sensitive(value,referenced,pointer,depth+1);
      if(obj(rule)['x-qian-sensitive']===true&&(typeof value!=='string'||!value.startsWith('secretRef://')))this.fail(entry.configFile,pointer,'敏感配置必须为 secretRef，不能打印或保存明文。');
      for(const name of ['allOf','anyOf','oneOf'])if(Array.isArray(obj(rule)[name]))for(const branch of obj(rule)[name] as JsonValue[])sensitive(value,branch,pointer,depth+1);
      if(Array.isArray(value)&&obj(rule).items!==undefined)value.forEach((child,index)=>sensitive(child,obj(rule).items,pointer+'/'+index,depth+1));
      for(const[key,child]of Object.entries(obj(value))){
        let found=false;const properties=obj(obj(rule).properties);
        if(Object.hasOwn(properties,key)){sensitive(child,properties[key],pointer+'/'+escape(key),depth+1);found=true;}
        for(const[pattern,shape]of Object.entries(obj(obj(rule).patternProperties)))if(new RegExp(pattern).test(key)){sensitive(child,shape,pointer+'/'+escape(key),depth+1);found=true;}
        if(!found)sensitive(child,obj(rule).additionalProperties,pointer+'/'+escape(key),depth+1);
      }
    };sensitive(config,schema,entry.configPointer);return config;
  }
  private order(entries:ProfileEntry[]):string[]{
    const providers=new Map<string,ProfileEntry>();
    for(const entry of entries)for(const key of entry.provides){const id=entry.scope+'/'+key;if(providers.has(id))this.fail(this.profileFile,'/bundles','同 scope 的服务键存在重复 Provider。');providers.set(id,entry);}
    const state=new Map<string,number>(),order:string[]=[];
    const visit=(entry:ProfileEntry):void=>{
      const id=entry.scope+'/'+entry.id;if(state.get(id)===2)return;if(state.get(id)===1)this.fail(this.profileFile,'/bundles','插件依赖存在环路。');state.set(id,1);
      for(const key of entry.inject){const provider=providers.get(entry.scope+'/'+key)??providers.get('global/'+key);if(!provider)this.fail(this.profileFile,'/bundles','缺少 inject 所需的 Provider。');visit(provider);}
      state.set(id,2);order.push(id);
    };for(const entry of entries)visit(entry);return order;
  }
  private error(file:string,pointer:string,message:string,severity:'error'|'warning'='error'):void{
    const display=this.display(file),prefix=display.split('/').map(encodeURIComponent).join('/'),full=prefix+'#'+pointer;
    let location=pointer;const lines=this.sources.get(display)?.lines??{};while(!(location in lines)&&location)location=location.slice(0,location.lastIndexOf('/'));
    const general='输入不符合该能力的输入 Schema，逐项错误见 errors。';
    this.diagnostics.push({file:display,line:lines[location]??1,severity,error:{type:'https://qian.dev/problems/protocol/INPUT_INVALID',title:'输入不符合契约',status:400,code:'INPUT_INVALID',message:general,detail:general,trace_id:this.trace,pointer:full,errors:[{code:'PROFILE_INVALID',message,pointer:full}]}});
  }
  private fail(file:string,pointer:string,message:string):never{this.error(file,pointer,message);throw new CompositionFailed();}
  private display(file:string):string{return path.relative(this.root,path.resolve(this.root,file)).replaceAll('\\','/');}
  private within(directory:string,relative:string):string{
    const full=path.resolve(directory,relative);if(!full.startsWith(path.resolve(directory)+path.sep))throw new Error('outside');
    let current=directory;for(const part of path.relative(directory,full).split(path.sep)){current=path.join(current,part);if(existsSync(current)&&lstatSync(current).isSymbolicLink())throw new Error('link');}return full;
  }
}
function localReference(schema:Obj,root:JsonValue):JsonValue|undefined{
  if(typeof schema.$ref!=='string')return undefined;
  if(!schema.$ref.startsWith('#/'))throw new Error('local JSON Pointer only');
  let result:JsonValue|undefined=root;
  for(const segment of schema.$ref.slice(2).split('/'))result=obj(result)[decodeURIComponent(segment).replaceAll('~1','/').replaceAll('~0','~')];
  if(result===undefined)throw new Error('missing schema reference');return result;
}
function defaults(schema:JsonValue,value:JsonValue,root:JsonValue=schema,depth=0):JsonValue{
  if(depth>64)throw new Error('schema depth');
  const rule=obj(schema),referenced=localReference(rule,root);
  if(referenced!==undefined)value=defaults(referenced,value,root,depth+1);
  if(Array.isArray(rule.allOf))for(const child of rule.allOf)value=defaults(child,value,root,depth+1);
  if(Array.isArray(value)&&rule.items!==undefined)value=value.map(child=>defaults(rule.items,child,root,depth+1));
  if(value!==null&&typeof value==='object'&&!Array.isArray(value)){
    for(const[key,fallback]of Object.entries(obj(rule.default)))if(!Object.hasOwn(value,key))value[key]=structuredClone(fallback);
    for(const[key,shape]of Object.entries(obj(rule.properties))){if(!Object.hasOwn(value,key)&&Object.hasOwn(obj(shape),'default'))value[key]=structuredClone(obj(shape).default);if(Object.hasOwn(value,key))value[key]=defaults(shape,value[key],root,depth+1);}
  }return value;
}
