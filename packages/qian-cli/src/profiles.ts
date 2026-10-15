import {existsSync, readFileSync, readdirSync, lstatSync, statSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {randomBytes} from 'node:crypto';
import {satisfies, rcompare, validRange} from 'semver';
import {Ajv2020, type ErrorObject} from 'ajv/dist/2020.js';
import {parseSurface, validateSurfaceSchema, type JsonValue, type SurfaceDiagnostic, type SurfaceSyntax} from '@qian/surfaces';
import {compilePattern,isStable,sameKey,UnsupportedPattern,type KeyPattern} from './schema-pattern.js';

type Obj = {[key: string]: JsonValue};
const obj = (value: JsonValue | undefined): Obj => typeof value === 'object' && value !== null && !Array.isArray(value) ? value : {};
const str = (value: JsonValue | undefined): string => typeof value === 'string' ? value : '';
const strings = (value: JsonValue | undefined): string[] => Array.isArray(value) ? value.map(v => String(v)) : [];
const escape = (value: string): string => value.replaceAll('~','~0').replaceAll('/','~1');
const secretReference = (value: JsonValue): boolean => typeof value === 'string' && value.startsWith('secretRef://');
export interface ProfileOptions { workingDirectory: string; profile?: string; patches?: readonly string[]; homeDirectory?: string; includeHomePatch?: boolean; componentDirectories?: readonly string[]; scopePatches?: readonly ScopePatchLayer[] }
export interface ProfileLayer {index: number; kind: string; path: string; ref: string | null; present: boolean}
export interface ProfileEntry {id: string; scope: string; plugin: string; config: JsonValue; layer: number; inject: string[]; provides: string[]; note?: string}
export interface ScopePatchLayer {scope: {kind: string; id: string}; source: string; patches: JsonValue}
export interface DisabledProfileEntry {id: string; scope: string; plugin: string; layer: number; sourceLocation: string}
export interface ProfileSnapshot {disabledEntries: DisabledProfileEntry[];profile: string; layers: ProfileLayer[]; entries: ProfileEntry[]; mountOrder: string[]}
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
      for(const patch of this.options.scopePatches??[])this.scopeLayer(patch);
      const entries=this.resolveEntries();
      const snapshot={profile:this.profile,layers:this.layers,entries,mountOrder:this.order(entries)} as ProfileSnapshot;
      Object.defineProperty(snapshot,'disabledEntries',{value:[...this.entries.values()].filter(e=>e.disabled).map(e=>({id:e.id,scope:e.scope,plugin:e.plugin,layer:e.layer,sourceLocation:this.display(e.sourceFile)+'#'+e.sourcePointer})),enumerable:false});
      return{snapshot,diagnostics:this.diagnostics,isValid:true};
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
    this.addEntries(this.read(file,'patch') as JsonValue[],file,index);
  }
  private scopeLayer(layer:ScopePatchLayer):void{
    const {scope}=layer;
    if(!['tenant','app','agent-session'].includes(scope.kind)||!scope.id||/[\s\p{Cc}:]/u.test(scope.id))this.fail('registry/scope-patches.json','/scope','数据库层必须声明合法的非全局 scope。');
    const file=layer.source;
    if(!file?.trim()||path.isAbsolute(file)||file.split(/[/\\]/).includes('..')||/\p{Cc}/u.test(file))this.fail('registry/scope-patches.json','','数据库层来源必须为安全的相对名称。');
    if(!Array.isArray(layer.patches))this.fail(file,'','scope patch 必须为数组。');
    const raw=JSON.stringify(layer.patches);
    if(Buffer.byteLength(raw)>1_048_576)this.fail(file,'','scope patch 超过大小限制。');
    const parsed=parseSurface(this.display(file),raw,false,this.trace);this.diagnostics.push(...parsed.diagnostics);
    if(!parsed.syntax||parsed.diagnostics.some(d=>d.severity==='error'))throw new CompositionFailed();
    this.sources.set(this.display(file),parsed.syntax);
    const errors=validateSurfaceSchema('patch',this.display(file),parsed.syntax.data,this.trace,parsed.syntax.lines);
    this.diagnostics.push(...errors);if(errors.some(d=>d.severity==='error'))throw new CompositionFailed();
    const index=this.layers.length;this.layers.push({index,kind:'scope-patch',path:this.display(file),ref:null,present:true});
    this.addEntries(parsed.syntax.data as JsonValue[],file,index,scope.kind+':'+scope.id);
  }
  private addEntries(patch:JsonValue[],file:string,index:number,owningScope?:string):void{
    const seen=new Set<string>();
    patch.forEach((value,position)=>{
      const entry=obj(value),pointer='/'+position,id=str(entry.id),scope=str(entry.scope)||owningScope||'global',key=scope+'/'+id,plugin=str(entry.plugin);
      if(owningScope&&scope!==owningScope)this.fail(file,pointer+'/scope','patch 不能改写本层所属 scope。');
      if(seen.has(key))this.fail(file,pointer+'/id','同一层不能重复声明同一 scope/id。');seen.add(key);
      let current=this.entries.get(key);
      if(!current){
        const inherited=owningScope?this.entries.get('global/'+id):undefined;
        if(inherited)current={...inherited,id,scope,config:structuredClone(inherited.config),layer:index};
        else{
          if(!plugin)this.fail(file,pointer+'/plugin','新条目必须声明 plugin。');
          if(!Object.hasOwn(entry,'config'))this.fail(file,pointer+'/config','新条目必须声明 config，可为空对象。');
          current={id,scope,plugin,config:{},layer:index,disabled:false,configFile:file,configPointer:pointer+'/config',sourceFile:file,sourcePointer:pointer};
        }
      }
      const disabled=typeof entry.disabled==='boolean'?entry.disabled:current.disabled;
      this.protect(current,plugin||current.plugin,disabled,file,pointer);
      const global=this.entries.get('global/'+id);if(scope!=='global'&&global)this.protect(global,plugin||current.plugin,disabled,file,pointer);
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
    const schema=JSON.parse(new TextDecoder('utf-8',{fatal:true}).decode(readFileSync(file))) as JsonValue;
    this.patterns(schema,file,'');
    const config=defaults(schema,structuredClone(entry.config));
    // 正则交给共享翻译规则执行（同 ProfileLoader.UsePortablePatterns）；预检没有覆盖的位置遇到不支持的写法时编译抛错，按无法解析处理。
    const validate=new Ajv2020({allErrors:true,strict:false,validateFormats:false,code:{regExp:portableRegExp}}).compile(schema as object);
    if(!validate(config)){
      for(const location of locations(validate.errors??[]))this.error(entry.configFile,entry.configPointer+location,'条目配置不符合组件 config.schema。');
      throw new CompositionFailed();
    }
    // 与 ProfileLoader.Sensitive 同序同义，诊断逐字相同。规则不是对象即返回；x-qian-sensitive 不是布尔值时 .NET 读取失败，这里同样按无法解析处理。
    const sensitive=(value:JsonValue,schemaRule:JsonValue|undefined,pointer:string,depth=0):void=>{
      if(depth>64)throw new Error('schema depth');
      if(typeof schemaRule!=='object'||schemaRule===null||Array.isArray(schemaRule))return;
      const rule=schemaRule,referenced=localReference(rule,schema);if(referenced!==undefined)sensitive(value,referenced,pointer,depth+1);
      const flag=rule['x-qian-sensitive'];if(flag!==undefined&&flag!==null&&typeof flag!=='boolean')throw new Error('x-qian-sensitive');
      if(flag===true&&!secretReference(value))this.fail(entry.configFile,pointer,'敏感配置必须为 secretRef，不能打印或保存明文。');
      for(const name of ['allOf','anyOf','oneOf'])if(Array.isArray(rule[name]))for(const branch of rule[name] as JsonValue[])sensitive(value,branch,pointer,depth+1);
      if(Array.isArray(value)&&rule.items!=null)value.forEach((child,index)=>sensitive(child,rule.items,pointer+'/'+index,depth+1));
      for(const key of Object.keys(obj(value)).sort()){
        const child=obj(value)[key];
        // 键名按两种读法判定，敏感标记取并集：IQianPlugin<TConfig> 按 Web 默认不区分大小写绑定（Password 会绑到 password），
        // 验证器与直接读 JsonElement 的插件区分大小写。所以属性名与动态键都按 OrdinalIgnoreCase 匹配，additionalProperties 只在按原样没有匹配时适用。
        // 配置键、属性名与模式都按序数顺序遍历：JS 对象把整数形式的键排在前面，按各自的迭代顺序时两端报告的首个违规会不同。
        const at=pointer+'/'+escape(key),properties=obj(rule.properties),patterns=obj(rule.patternProperties);let exact=false;
        for(const name of Object.keys(properties).sort())if(sameKey(name,key)){sensitive(child,properties[name],at,depth+1);exact=name===key||exact;}
        if(Object.keys(patterns).length){
          // 动态键按 ECMA-262 判定（见 schema-pattern.ts）。方言判断不同的键名不做匹配，值必须是 secretRef；模式仍逐个编译，不受支持的写法照常失败。
          const stable=isStable(key);
          for(const source of Object.keys(patterns).sort()){
            const shape=patterns[source];
            const folded=this.pattern(source,true,entry.configFile,at);
            if(!stable||!folded.test(key))continue;
            sensitive(child,shape,at,depth+1);
            exact=this.pattern(source,false,entry.configFile,at).test(key)||exact;
          }
          if(!stable&&!secretReference(child))this.fail(entry.configFile,at,'键名含各正则方言判断不同的字符，按敏感配置处理，必须为 secretRef。');
        }
        if(!exact)sensitive(child,rule.additionalProperties,at,depth+1);
      }
    };sensitive(config,schema,entry.configPointer);return config;
  }
  // 与 ProfileLoader.Patterns 同序同义：交给验证器之前，按 schemaKeywords 的顺序检查各子 Schema 位置上的 pattern 与 patternProperties 的键，
  // 映射按键名序数、数组按下标，超出可移植子集的写法在 Schema 文件的对应指针处失败。不跟随 $ref。
  private patterns(schema:JsonValue|undefined,file:string,pointer:string,depth=0):void{
    if(depth>64)throw new Error('schema depth');
    if(typeof schema!=='object'||schema===null||Array.isArray(schema))return;
    if(typeof schema.pattern==='string')this.portable(schema.pattern,file,pointer+'/pattern','pattern');
    for(const keyword of schemaKeywords){
      const value=schema[keyword];
      if(schemaMaps.has(keyword)){
        if(typeof value!=='object'||value===null||Array.isArray(value))continue;
        for(const key of Object.keys(value).sort()){
          const at=pointer+'/'+keyword+'/'+escape(key);
          if(keyword==='patternProperties')this.portable(key,file,at,keyword);
          this.patterns(value[key],file,at,depth+1);
        }
      }
      else if(schemaLists.has(keyword)){if(Array.isArray(value))value.forEach((child,index)=>this.patterns(child,file,pointer+'/'+keyword+'/'+index,depth+1));}
      else this.patterns(value,file,pointer+'/'+keyword,depth+1);
    }
  }
  private portable(source:string,file:string,pointer:string,keyword:string):void{
    try{compilePattern(source);}
    catch(error){if(error instanceof UnsupportedPattern)this.fail(file,pointer,'组件 config.schema 的 '+keyword+' 含不支持的正则写法，只接受 ECMA-262 可移植子集。');throw error;}
  }
  // 超出 ECMA-262 可移植子集或规模上限的写法让装配失败，不跳过敏感标记。
  private pattern(source:string,foldCase:boolean,file:string,pointer:string):KeyPattern{
    try{return compilePattern(source,foldCase);}
    catch(error){if(error instanceof UnsupportedPattern)this.fail(file,pointer,'组件 config.schema 的 patternProperties 含不支持的正则写法，只接受 ECMA-262 可移植子集。');throw error;}
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
// 含子 Schema 的关键字，按序数排列；值为映射或数组的分别列出。同 ProfileLoader.SchemaKeywords。
const schemaKeywords=['$defs','additionalProperties','allOf','anyOf','contains','contentSchema','definitions','dependentSchemas',
  'else','if','items','not','oneOf','patternProperties','prefixItems','properties','propertyNames','then','unevaluatedItems','unevaluatedProperties'];
const schemaMaps=new Set(['$defs','definitions','dependentSchemas','patternProperties','properties']);
const schemaLists=new Set(['allOf','anyOf','oneOf','prefixItems']);
// Ajv 默认按 u 标志构造 RegExp；换成共享翻译规则的匹配器，pattern 与 patternProperties 的键（含 additionalProperties 的判定）与 .NET 同义。
const portableRegExp=Object.assign((source:string):KeyPattern=>compilePattern(source),{code:'compilePattern'});

// 与 ProfileLoader.Locations 同义，把 Ajv 的错误归一为出错实例位置：每个错误报自身位置，含 anyOf、oneOf、if、contains 的汇总错误；
// additionalProperties、unevaluatedProperties、propertyNames 报到键名；contains 略去逐项错误（Ajv 计数达到上限即停止逐项检查）。去重后按序数排序。
function locations(errors:ErrorObject[]):string[]{
  const at=(error:ErrorObject):string=>error.instancePath+(
    error.keyword==='additionalProperties'?'/'+escape(String(error.params.additionalProperty)):
    error.keyword==='unevaluatedProperties'?'/'+escape(String(error.params.unevaluatedProperty)):
    error.keyword==='propertyNames'?'/'+escape(String(error.params.propertyName)):
    error.propertyName!==undefined?'/'+escape(error.propertyName):'');
  const contains=errors.filter(error=>error.keyword==='contains'),found=new Set<string>();
  for(const error of errors){
    const location=at(error);
    if(!contains.some(other=>error.schemaPath.startsWith(other.schemaPath+'/')&&location.startsWith(at(other)+'/')))found.add(location);
  }
  return [...found].sort();
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
