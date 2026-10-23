import {existsSync, readFileSync, readdirSync, lstatSync, statSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {randomBytes} from 'node:crypto';
import {satisfies, rcompare, validRange} from 'semver';
import {Ajv2020, Name, _, type CodeKeywordDefinition, type ErrorObject} from 'ajv/dist/2020.js';
import {not} from 'ajv/dist/compile/codegen/index.js';
import {resetErrorsCount} from 'ajv/dist/compile/errors.js';
import {alwaysValidSchema} from 'ajv/dist/compile/util.js';
import {parseSurface, validateSurfaceSchema, compilePattern, isStable, sameKey, UnsupportedPattern, type JsonValue, type KeyPattern, type SurfaceDiagnostic, type SurfaceSyntax} from '@qian/surfaces';

type Obj = {[key: string]: JsonValue};
const obj = (value: JsonValue | undefined): Obj => typeof value === 'object' && value !== null && !Array.isArray(value) ? value : {};
const str = (value: JsonValue | undefined): string => typeof value === 'string' ? value : '';
const strings = (value: JsonValue | undefined): string[] => Array.isArray(value) ? value.map(v => String(v)) : [];
const escape = (value: string): string => value.replaceAll('~','~0').replaceAll('/','~1');
const secretReference = (value: JsonValue): boolean => typeof value === 'string' && value.startsWith('secretRef://');
export interface ProfileOptions { workingDirectory: string; profile?: string; patches?: readonly string[]; homeDirectory?: string; includeHomePatch?: boolean; componentDirectories?: readonly string[]; scopePatches?: readonly ScopePatchLayer[] }
export interface ProfileLayer {index: number; kind: string; path: string; ref: string | null; present: boolean}
export interface ProfileEntry {id: string; scope: string; plugin: string; config: JsonValue; layer: number; inject: string[]; optionalInject: string[]; provides: string[]; note?: string}
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
      entries.push({id:entry.id,scope:entry.scope,plugin:component.id+'@'+component.version,config,layer:entry.layer,inject:strings(component.manifest.inject),optionalInject:strings(component.manifest.optionalInject),provides:strings(component.manifest.provides),...(entry.note!==undefined?{note:entry.note}:{})});
    }
    return entries;
  }
  private config(component:Component,entry:Working):JsonValue{
    const file=this.within(component.directory,str(obj(component.manifest.runtime).configSchema)||'./schemas/config.schema.json');
    if(!existsSync(file)){if(Object.keys(entry.config).length)this.fail(entry.configFile,entry.configPointer,'有配置的组件必须提供 config.schema.json。');return entry.config;}
    if(statSync(file).size>1_048_576)this.fail(file,'','Schema 超过大小限制。');
    // 与 ProfileLoader.ValidateConfig 同序：数值与层数、预检、元 Schema、默认值、验证。
    const text=new TextDecoder('utf-8',{fatal:true}).decode(readFileSync(file)),schema=JSON.parse(text) as JsonValue;
    uniqueKeys(text);portableJson(schema);
    this.check(schema,file);
    const ajv=validator();
    if(schema===null||!ajv.validateSchema(schema as object))this.fail(file,'','组件 config.schema 不符合 JSON Schema 2020-12 的元 Schema。');
    const config=defaults(schema,structuredClone(entry.config));
    // 预检已跟随 $ref 走遍验证器会求值的位置；这里编译仍抛错时按无法解析处理。
    const validate=ajv.compile(schema as object);
    portableJson(config);
    if(!validate(config)){
      for(const location of locations(validate.errors??[],config))this.error(entry.configFile,entry.configPointer+location,'条目配置不符合组件 config.schema。');
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
          // 动态键按 ECMA-262 判定（见 @qian/surfaces 的 schema-pattern.ts）。方言判断不同的键名不做匹配，值必须是 secretRef；模式仍逐个编译，不受支持的写法照常失败。
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
  // 与 ProfileLoader.Check 同序同义的预检。从根开始按 schemaKeywords 的顺序走遍各子 Schema 位置，映射按键名序数、数组按下标。
  // 不合规的写法在 Schema 文件的对应指针处失败，规则见 ProfileLoader.Check；$ref 的目标都在这次遍历之内。走完后检查循环引用（见 cycles）。
  private check(root:JsonValue,file:string):void{
    const visited=new Map<string,Obj>(),references=new Map<string,string>();
    const visit=(schema:JsonValue|undefined,pointer:string,depth:number):void=>{
      if(depth>64)throw new Error('schema depth');
      if(typeof schema!=='object'||schema===null||Array.isArray(schema)||visited.has(pointer))return;
      visited.set(pointer,schema);
      if(Object.hasOwn(schema,'$schema')&&schema.$schema!=='https://json-schema.org/draft/2020-12/schema'&&schema.$schema!=='https://json-schema.org/draft/2020-12/schema#')
        this.fail(file,pointer+'/$schema','组件 config.schema 只接受 JSON Schema 2020-12，$schema 须为 https://json-schema.org/draft/2020-12/schema。');
      for(const keyword of unsupported)if(Object.hasOwn(schema,keyword))this.fail(file,pointer+'/'+keyword,'组件 config.schema 不支持 '+keyword+' 关键字。');
      if(Object.hasOwn(schema,'enum')&&!(Array.isArray(schema.enum)&&schema.enum.length>0))this.fail(file,pointer+'/enum','组件 config.schema 的 enum 须为非空数组。');
      if(typeof schema.pattern==='string')this.portable(schema.pattern,file,pointer+'/pattern','pattern');
      for(const keyword of schemaKeywords){
        const value=schema[keyword];
        if(keyword==='$ref'){
          if(typeof value!=='string')continue;
          const reference=target(root,value);
          if(!reference||!(typeof reference.value==='boolean'||typeof reference.value==='object'&&reference.value!==null&&!Array.isArray(reference.value)))
            this.fail(file,pointer+'/$ref','组件 config.schema 的 $ref 须为 #/ 开头、指向本文档内子 Schema 位置（对象或布尔值）的 JSON Pointer。');
          references.set(pointer,reference.pointer);
        }
        else if(schemaMaps.has(keyword)){
          if(typeof value!=='object'||value===null||Array.isArray(value))continue;
          for(const key of Object.keys(value).sort()){
            const at=pointer+'/'+keyword+'/'+escape(key);
            // Ajv 为防原型污染略过这两个映射里的 __proto__ 键，.NET 照常求值。
            if((keyword==='properties'||keyword==='patternProperties')&&key==='__proto__')this.fail(file,at,'组件 config.schema 的 '+keyword+' 不能以 __proto__ 为键名。');
            if(keyword==='patternProperties')this.portable(key,file,at,keyword);
            visit(value[key],at,depth+1);
          }
        }
        else if(schemaLists.has(keyword)){if(Array.isArray(value))value.forEach((child,index)=>visit(child,pointer+'/'+keyword+'/'+index,depth+1));}
        else visit(value,pointer+'/'+keyword,depth+1);
      }
    };
    visit(root,'',0);
    this.cycles([...visited.keys()],visited,references,file);
  }
  // 同 ProfileLoader.Cycles：子 Schema 经 $ref 与 inPlace 中的关键字回到自身时会在同一实例上无限递归，两端出错的时机不同。
  // 按预检的访问顺序逐个出发，沿 inPlace 的顺序深度优先（显式栈），遇到回到栈上 Schema 的边即在该边处失败。
  private cycles(order:string[],visited:Map<string,Obj>,references:Map<string,string>,file:string):void{
    const edges=(pointer:string):[string,string][]=>{
      const rule=visited.get(pointer)!,result:[string,string][]=[];
      for(const keyword of schemaKeywords.filter(keyword=>inPlace.has(keyword))){
        const at=pointer+'/'+keyword,value=rule[keyword];
        if(keyword==='$ref'){const target=references.get(pointer);if(target!==undefined)result.push([at,target]);}
        else if(schemaLists.has(keyword)){if(Array.isArray(value))value.forEach((_,index)=>result.push([at+'/'+index,at+'/'+index]));}
        else if(schemaMaps.has(keyword)){if(typeof value==='object'&&value!==null&&!Array.isArray(value))for(const key of Object.keys(value).sort())result.push([at+'/'+escape(key),at+'/'+escape(key)]);}
        else if(Object.hasOwn(rule,keyword))result.push([at,at]);
      }
      return result;
    };
    const done=new Map<string,boolean>();
    for(const start of order){
      if(done.has(start))continue;
      const stack:{pointer:string;edges:[string,string][];next:number}[]=[{pointer:start,edges:edges(start),next:0}];done.set(start,false);
      while(stack.length){
        const frame=stack[stack.length-1];
        if(frame.next===frame.edges.length){done.set(frame.pointer,true);stack.pop();continue;}
        const[at,target]=frame.edges[frame.next++];
        if(!visited.has(target))continue;
        if(done.has(target)){if(!done.get(target))this.fail(file,at,'组件 config.schema 的引用构成循环：子 Schema 不经属性或数组项又回到了自身。');continue;}
        done.set(target,false);stack.push({pointer:target,edges:edges(target),next:0});
      }
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
    // optionalInject 是软边：Provider 在树里时先于消费方装载，缺席不报错；经软边回到装载中的条目不算环路，只是不再提前（与 .NET 装配器一致）。
    const visit=(entry:ProfileEntry,soft:boolean):boolean=>{
      const id=entry.scope+'/'+entry.id,current=state.get(id)??0;if(current===2)return true;
      if(current===1){if(soft)return false;this.fail(this.profileFile,'/bundles','插件依赖存在环路。');}
      state.set(id,1);
      for(const key of entry.inject){const provider=providers.get(entry.scope+'/'+key)??providers.get('global/'+key);if(!provider)this.fail(this.profileFile,'/bundles','缺少 inject 所需的 Provider。');if(!visit(provider,soft)){state.set(id,0);return false;}}
      for(const key of entry.optionalInject){const provider=providers.get(entry.scope+'/'+key)??providers.get('global/'+key);if(provider)visit(provider,true);}
      state.set(id,2);order.push(id);return true;
    };for(const entry of entries)visit(entry,false);return order;
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
// 含子 Schema 的关键字与 $ref，按序数排列；值为映射或数组的分别列出。同 ProfileLoader.SchemaKeywords。
const schemaKeywords=['$defs','$ref','additionalProperties','allOf','anyOf','contains','contentSchema','definitions','dependentSchemas',
  'else','if','items','not','oneOf','patternProperties','prefixItems','properties','propertyNames','then'];
const schemaMaps=new Set(['$defs','definitions','dependentSchemas','patternProperties','properties']);
const schemaLists=new Set(['allOf','anyOf','oneOf','prefixItems']);
// 在同一实例上求值子 Schema 的关键字。同 ProfileLoader.InPlace。
const inPlace=new Set(['$ref','allOf','anyOf','dependentSchemas','else','if','not','oneOf','then']);
// 两端验证器解释不同、或会让文档内 JSON Pointer 引用失去单一含义的关键字，按序数排列。同 ProfileLoader.Unsupported。
const unsupported=['$anchor','$async','$dynamicAnchor','$dynamicRef','$id','$recursiveAnchor','$recursiveRef','$vocabulary','dependencies','nullable','unevaluatedItems','unevaluatedProperties'];
// Ajv 默认按 u 标志构造 RegExp；换成共享翻译规则的匹配器，pattern 与 patternProperties 的键（含 additionalProperties 的判定）与 .NET 同义。
const portableRegExp=Object.assign((source:string):KeyPattern=>compilePattern(source),{code:'compilePattern'});

// 与 .NET 端（ProfileLoader.AjvSemantics）对齐的 Ajv。ownProperties 只认自有属性：默认按 data[key] !== undefined 判断属性存在，
// constructor、toString 等继承的名称会被当成已提供。另替换以下关键字：
// - multipleOf 按十进制精确判断，同 JsonSchema.Net 的 JsonMath.Divides；Ajv 默认用浮点取余，0.3 不算 0.1 的倍数。
// - const、enum、uniqueItems 按 canonical 的规约形式比较，同 ProfileLoader.JsEquality。Ajv 的 fast-deep-equal 遇到自带 toString 或 valueOf 键的对象
//   会调用它们而抛错；uniqueItems 在 items 只声明标量类型时还跳过类型不符的项，prefixItems 位置上的重复对象因此不报。
// - contains 与 propertyNames 失败时只留自身的一条错误，丢弃子 Schema 的逐项错误：子 Schema 经未内联的 $ref 时，这些错误不带外层关联，归不到数组或键名上。
// - oneOf 因两个以上分支同时成立而失败时只留自身的一条错误：Ajv 第二个分支成立后不再求值其余分支，已求值分支的错误两端无法一致。
function validator():Ajv2020{
  const ajv=new Ajv2020({allErrors:true,strict:false,validateFormats:false,ownProperties:true,code:{regExp:portableRegExp}});
  // Ajv2020 构造时强制开启已求值属性与项的跟踪，只供 unevaluatedItems、unevaluatedProperties 使用，二者已在预检拒绝。
  // 这套跟踪在 8.20.0 有缺陷：anyOf 的分支含 if/then、同级又有 patternProperties 时，验证抛 TypeError。关掉它，不影响其余关键字的判断。
  ajv.opts.unevaluated=false;
  const contains=ajv.getKeyword('contains') as CodeKeywordDefinition,oneOf=ajv.getKeyword('oneOf') as CodeKeywordDefinition,propertyNames=ajv.getKeyword('propertyNames') as CodeKeywordDefinition;
  for(const keyword of ['const','contains','enum','multipleOf','oneOf','propertyNames','uniqueItems'])ajv.removeKeyword(keyword);
  ajv.addKeyword({keyword:'multipleOf',type:'number',schemaType:'number',errors:false,validate:(divisor:number,value:number)=>divides(value,divisor)});
  ajv.addKeyword({keyword:'uniqueItems',type:'array',schemaType:'boolean',errors:false,validate:(unique:boolean,items:JsonValue[])=>!unique||new Set(items.map(canonical)).size===items.length});
  ajv.addKeyword({keyword:'const',errors:false,compile:(expected:JsonValue)=>{const key=canonical(expected);return(data:JsonValue)=>canonical(data)===key;}});
  ajv.addKeyword({keyword:'enum',schemaType:'array',errors:false,compile:(values:JsonValue[])=>{const keys=new Set(values.map(canonical));return(data:JsonValue)=>keys.has(canonical(data));}});
  ajv.addKeyword({...contains,code(cxt){
    const result=cxt.result.bind(cxt);
    cxt.result=(condition,success,failure)=>result(condition,success,failure??(()=>{cxt.reset();cxt.error();}));
    contains.code(cxt);
  }});
  ajv.addKeyword({...oneOf,code(cxt){
    const result=cxt.result.bind(cxt);
    cxt.result=(condition,success,failure)=>result(condition,success,()=>{cxt.gen.if(_`Array.isArray(${cxt.params.passing})`,()=>cxt.reset());failure?.();});
    oneOf.code(cxt);
  }});
  ajv.addKeyword({...propertyNames,code(cxt){
    const{gen,schema,data,it}=cxt;
    if(alwaysValidSchema(it,schema))return;
    const valid=gen.name('valid');
    gen.forIn('key',data,key=>{
      cxt.setParams({propertyName:key});
      const errors=gen.const('_errs',new Name('errors'));
      cxt.subschema({keyword:'propertyNames',data:key,dataTypes:['string'],propertyName:key,compositeRule:true},valid);
      gen.if(not(valid),()=>{resetErrorsCount(gen,errors);cxt.error(true);});
    });
    cxt.ok(valid);
  }});
  return ajv;
}
// 十进制精确判断 value 是否为 divisor 的整数倍，作用在双精度的最短十进制表示上，同 .NET 端对归一后数值的 JsonMath.Divides。
function divides(value:number,divisor:number):boolean{
  const decimal=(number:number):[bigint,number]=>{
    const[digits,exponent='0']=Math.abs(number).toString().split('e'),[whole,fraction='']=digits.split('.');
    return[BigInt(whole+fraction),Number(exponent)-fraction.length];
  };
  const[a,x]=decimal(value),[b,y]=decimal(divisor),scale=Math.min(x,y);
  return b!==0n&&a*10n**BigInt(x-scale)%(b*10n**BigInt(y-scale))===0n;
}
// JS 值相等的规约形式，同 ProfileLoader.JsEquality：对象按键名排序，数值经 JSON.stringify（-0 记为 0）。
const canonical=(value:JsonValue):string=>Array.isArray(value)?'['+value.map(canonical).join(',')+']':
  typeof value==='object'&&value!==null?'{'+Object.keys(value).sort().map(key=>JSON.stringify(key)+':'+canonical(value[key])).join(',')+'}':JSON.stringify(value);
// 同 .NET 的 JsonNode.Parse 与 ProfileLoader.Doubles：嵌套超过 64 层、数值超出双精度范围、字符串或键名含残缺代理项时按无法解析处理。
function portableJson(value:JsonValue,depth=0):void{
  if(typeof value==='number'&&!Number.isFinite(value))throw new Error('number');
  if(typeof value==='string')wellFormed(value);
  if(typeof value!=='object'||value===null)return;
  if(depth>=64)throw new Error('depth');
  if(Array.isArray(value))for(const child of value)portableJson(child,depth+1);
  else for(const[key,child]of Object.entries(value)){wellFormed(key);portableJson(child,depth+1);}
}
function wellFormed(text:string):void{
  for(let index=0;index<text.length;index++){
    const unit=text.charCodeAt(index),next=text.charCodeAt(index+1);
    if(unit>=0xd800&&unit<=0xdbff&&next>=0xdc00&&next<=0xdfff)index++;
    else if(unit>=0xd800&&unit<=0xdfff)throw new Error('lone surrogate');
  }
}
// JSON.parse 对重复键取后者，.NET 的 JsonNode 遇到重复键失败：按 JSON 语法扫一遍，同一对象内键名（解码后）重复时按无法解析处理。调用前 JSON.parse 已确认语法有效。
function uniqueKeys(text:string):void{
  const objects:(Set<string>|undefined)[]=[];
  for(let index=0;index<text.length;index++){
    const char=text[index];
    if(char==='{'||char==='[')objects.push(char==='{'?new Set():undefined);
    else if(char==='}'||char===']')objects.pop();
    else if(char==='"'){
      let end=index+1;while(text[end]!=='"')end+=text[end]==='\\'?2:1;
      let next=end+1;while(text[next]===' '||text[next]==='\t'||text[next]==='\n'||text[next]==='\r')next++;
      const keys=objects[objects.length-1];
      if(keys&&text[next]===':'){const key=JSON.parse(text.slice(index,end+1)) as string;if(keys.has(key))throw new Error('duplicate key');keys.add(key);}
      index=end;
    }
  }
}

// 与 ProfileLoader.Locations 同义，把 Ajv 的错误归一为出错实例位置：每个错误报自身位置，含 anyOf、oneOf、if、contains 的汇总错误；
// additionalProperties、propertyNames 报到键名。prefixItems 之后的 items 为 false 时 Ajv 只在数组上报一条，params.limit 为前缀的项数，
// 这里展开为其后的各项，同 .NET 逐项报告。去重后按序数排序。
function locations(errors:ErrorObject[],data:JsonValue):string[]{
  const found=new Set<string>();
  for(const{instancePath,keyword,params}of errors){
    if(keyword==='items'&&typeof params.limit==='number'){
      const array=instancePath.split('/').slice(1).reduce<JsonValue>((value,segment)=>{const key=segment.replaceAll('~1','/').replaceAll('~0','~');return Array.isArray(value)?value[Number(key)]:obj(value)[key];},data);
      for(let index=params.limit;index<(array as JsonValue[]).length;index++)found.add(instancePath+'/'+index);
    }
    else found.add(instancePath+(
      keyword==='additionalProperties'?'/'+escape(String(params.additionalProperty)):
      keyword==='propertyNames'?'/'+escape(String(params.propertyName)):''));
  }
  return [...found].sort();
}
// 文档内引用的目标与其规范指针，同 ProfileLoader.Target：只接受 #/ 开头、字符合乎 URI 片段的 JSON Pointer；百分号编码须为合法 UTF-8，
// 解码后不含 /，~ 只能出现在 ~0、~1 中。目标须为子 Schema 位置：从根起逐段经 schemaKeywords 中的关键字到达（$ref 除外），
// 映射再经键名，数组再经不带前导零的十进制下标。Ajv 把 #/ 当作根，按 JSON Pointer 则指向根下的空键名，这里不接受。
function target(root:JsonValue,reference:string):{value:JsonValue,pointer:string}|undefined{
  if(!reference.startsWith('#/')||!/^(?:[A-Za-z0-9._~!$&'()*+,;=:@/?-]|%[0-9A-Fa-f]{2})*$/.test(reference.slice(1)))return undefined;
  // container 为 undefined 时位于子 Schema 上，否则位于该映射或数组关键字的值上。
  let value:JsonValue=root,pointer='',container:string|undefined;
  for(const raw of reference.slice(2).split('/')){
    let segment:string;
    try{segment=decodeURIComponent(raw);}catch{return undefined;}
    if(segment.includes('/')||/~(?![01])/.test(segment))return undefined;
    segment=segment.replaceAll('~1','/').replaceAll('~0','~');
    const map=typeof value==='object'&&value!==null&&!Array.isArray(value)?value:undefined;
    if(container===undefined&&map&&segment!=='$ref'&&schemaKeywords.includes(segment)&&Object.hasOwn(map,segment)){value=map[segment];container=schemaMaps.has(segment)||schemaLists.has(segment)?segment:undefined;}
    else if(container!==undefined&&schemaMaps.has(container)&&map&&Object.hasOwn(map,segment)){value=map[segment];container=undefined;}
    else if(container!==undefined&&schemaLists.has(container)&&Array.isArray(value)&&/^(?:0|[1-9][0-9]{0,8})$/.test(segment)&&Number(segment)<value.length){value=value[Number(segment)];container=undefined;}
    else return undefined;
    pointer+='/'+escape(segment);
  }
  return container===undefined?{value,pointer}:undefined;
}
// 预检已确认走到的 $ref 都能解析；目标为 JSON null 时同没有引用。
function localReference(schema:Obj,root:JsonValue):JsonValue|undefined{
  if(typeof schema.$ref!=='string')return undefined;
  const reference=target(root,schema.$ref);
  if(!reference)throw new Error('local JSON Pointer only');
  return reference.value??undefined;
}
function defaults(schema:JsonValue,value:JsonValue,root:JsonValue=schema,depth=0):JsonValue{
  if(depth>64)throw new Error('schema depth');
  const rule=obj(schema),referenced=localReference(rule,root);
  if(referenced!==undefined)value=defaults(referenced,value,root,depth+1);
  if(Array.isArray(rule.allOf))for(const child of rule.allOf)value=defaults(child,value,root,depth+1);
  if(Array.isArray(value)&&rule.items!==undefined)value=value.map(child=>defaults(rule.items,child,root,depth+1));
  if(value!==null&&typeof value==='object'&&!Array.isArray(value)){
    // 按自有属性写入：键名为 __proto__ 时直接赋值会改原型而不是添加属性。
    const put=(target:Obj,key:string,child:JsonValue):void=>{Object.defineProperty(target,key,{value:child,writable:true,enumerable:true,configurable:true});};
    for(const[key,fallback]of Object.entries(obj(rule.default)))if(!Object.hasOwn(value,key))put(value,key,structuredClone(fallback));
    for(const[key,shape]of Object.entries(obj(rule.properties))){if(!Object.hasOwn(value,key)&&Object.hasOwn(obj(shape),'default'))put(value,key,structuredClone(obj(shape).default));if(Object.hasOwn(value,key))put(value,key,defaults(shape,value[key],root,depth+1));}
  }return value;
}
