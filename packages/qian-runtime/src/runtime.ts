import {Backend,type BackendScope} from './backend.js';
import type {Context,Disposer,HandlerFor,PluginDefinition,PluginEntry,PluginState,QianEvent,Registration,RuntimeOptions,ScopeKey,ServiceKey} from './contracts.js';
import {ParallelDispatchError,RuntimeError,ServiceNotReadyError} from './errors.js';

interface Group {entry:PluginEntry;active:boolean;closed:boolean;effects:Disposer[];ordinals:Map<string,number>;context:Context;backend?:BackendScope}
interface View {group:Group;scope:ScopeKey}
interface Service {key:string;view:View;storage:string;remove:Disposer}
interface Listener {event:QianEvent<unknown,unknown>;view:View;name:string;priority:number;sequence:number;storage:string;remove:Disposer}
interface Restriction {view:View;scope:ScopeKey;keep:(registration:Registration)=>boolean}
interface Node {definition:PluginDefinition;entry:PluginEntry;inject:readonly string[];provides:readonly string[];state:PluginState;group?:Group;dependencies:Service[];sequence:number;removing:boolean;failure?:unknown;pendingSince:number;warned:boolean}
const scopeValid=(scope:ScopeKey):void=>{if(scope==='global')return;const index=scope.indexOf(':');if(!['tenant','app','agent-session'].includes(scope.slice(0,index))||!scope.slice(index+1).trim())throw new TypeError('scope 必须为 global 或有效的扁平 scope。');};
const named=(key:string,name?:string):string=>{if(!key.trim()||(name!==undefined&&(!name.trim()||key.includes(':'))))throw new TypeError('服务键或具名注册名称无效。');return name===undefined?key:key+':'+name;};
const identity=(entry:PluginEntry):string=>JSON.stringify([entry.scope,entry.id]);
const conflict=(key:string,owner:string)=>new RuntimeError('REGISTRATION_CONFLICT',{key,owner});
const once=(effect:Disposer):Disposer=>{let done=false;return()=>{if(done)return;done=true;return effect();};};
const frozen=<T>(value:T):T=>{if(value&&typeof value==='object'){for(const child of Object.values(value))frozen(child);Object.freeze(value);}return value;};

/** 串行生命周期；内部 fork、作用域资源和服务值存放在固定的上游实现中。 */
export class PluginLoader {
 #backend=new Backend();#nodes:Node[]=[];#services:Service[]=[];#listeners:Listener[]=[];#restrictions:Restriction[]=[];
 #tail:Promise<void>=Promise.resolve();#disposed=false;#sequence=0;#listenerSequence=0;#waiters=new Set<()=>void>();#timer:ReturnType<typeof setInterval>|undefined;
 constructor(private readonly options:RuntimeOptions={}){
  const timeout=options.pendingTimeoutMs??30000;
  if(options.onPending&&timeout>0){this.#timer=setInterval(()=>{for(const node of this.#nodes)if(node.state==='Pending'&&!node.warned&&Date.now()-node.pendingSince>=timeout){node.warned=true;options.onPending?.(node.entry,this.getPendingDependencies(node.entry.id,node.entry.scope));}},Math.min(timeout,1000));}
 }
 getState(id:string,scope:ScopeKey='global'):PluginState{return this.#node(id,scope).state;}
 getFailure(id:string,scope:ScopeKey='global'):unknown{return this.#node(id,scope).failure;}
 getContext(id:string,scope:ScopeKey='global'):Context{const group=this.#node(id,scope).group;if(!group)throw new ServiceNotReadyError();return group.context;}
 getPendingDependencies(id:string,scope:ScopeKey='global'):readonly ServiceKey[]{const n=this.#node(id,scope);return n.inject.filter(key=>!this.#find(key,scope));}
 dumpConfig():readonly PluginEntry[]{return Object.freeze(this.#nodes.filter(n=>!n.removing).map(n=>n.entry));}
 mount(definitions:readonly PluginDefinition[],signal?:AbortSignal):Promise<void>{return this.#queue(async()=>{
  if(this.#disposed)throw new Error('运行时已卸载。');signal?.throwIfAborted();
  const added:Node[]=definitions.map(definition=>({definition,entry:frozen(structuredClone(definition.entry)),inject:[...definition.plugin.inject??[]],provides:[...definition.provides],state:'Pending' as PluginState,dependencies:[],sequence:0,removing:false,pendingSince:Date.now(),warned:false}));
  const all=[...this.#nodes,...added];
  for(const node of added){scopeValid(node.entry.scope);if(!node.entry.id.trim())throw new TypeError('条目 id 不能为空。');if(all.some(n=>n!==node&&identity(n.entry)===identity(node.entry)))throw conflict(node.entry.id,node.entry.id);}
  this.#validateGraph(all,added);this.#nodes.push(...added);
  try{await this.#reconcile(signal);}catch(error){if(signal?.aborted){for(const n of added){n.removing=true;if(n.group)n.group.active=false;}this.#invalidate();await this.#cleanPending();for(const n of [...added].sort((a,b)=>b.sequence-a.sequence))await this.#stop(n);this.#nodes=this.#nodes.filter(n=>!added.includes(n as typeof added[number]));this.#signal();}throw error;}
 });}
 unmount(id:string,scope:ScopeKey='global'):Promise<void>{return this.#queue(async()=>{const node=this.#node(id,scope);node.removing=true;if(node.group)node.group.active=false;this.#invalidate();await this.#cleanPending();await this.#stop(node);this.#nodes=this.#nodes.filter(n=>n!==node);this.#signal();await this.#reconcile();});}
 async reload(definition:PluginDefinition):Promise<void>{await this.unmount(definition.entry.id,definition.entry.scope);await this.mount([definition]);}
 async whenIdle():Promise<void>{while(true){const tail=this.#tail;await tail;if(tail===this.#tail)return;}}
 dispose():Promise<void>{return this.#queue(async()=>{if(this.#disposed)return;this.#disposed=true;for(const node of this.#nodes)node.removing=true;if(this.#timer)clearInterval(this.#timer);for(const n of [...this.#nodes].sort((a,b)=>b.sequence-a.sequence))await this.#stop(n);this.#nodes=[];this.#signal();await this.#backend.stop();});}
 #node(id:string,scope:ScopeKey):Node{const node=this.#nodes.find(n=>n.entry.id===id&&n.entry.scope===scope);if(!node)throw new ServiceNotReadyError();return node;}
 #queue(action:()=>Promise<void>):Promise<void>{const next=this.#tail.then(action);this.#tail=next.catch(()=>{});return next;}
 #report(error:unknown):void{try{this.options.onError?.(error);}catch{/* 诊断回调不能破坏隔离与撤销。 */}}
 #signal():void{for(const wake of this.#waiters)wake();this.#waiters.clear();}
 #notify():void{this.#invalidate();this.#signal();if(!this.#disposed)void this.#queue(()=>this.#reconcile()).catch(error=>this.#report(error));}
 #active(view:View,requester?:View):boolean{return !view.group.closed&&(view.group.active||view.group===requester?.group);}
 #describe(service:Service):Registration{const index=service.key.indexOf(':');return Object.freeze({key:index<0?service.key:service.key.slice(0,index),name:index<0?'':service.key.slice(index+1),scope:service.view.scope,ownerEntryId:service.view.group.entry.id,value:this.#backend.get(service.storage)});}
 #describeListener(listener:Listener):Registration{return Object.freeze({key:listener.event.name,name:listener.name,scope:listener.view.scope,ownerEntryId:listener.view.group.entry.id,value:this.#backend.get(listener.storage)});}
 #kept(registration:Registration,scope:ScopeKey,requester?:View):boolean{
  if(registration.scope!=='global')return true;
  for(const restriction of this.#restrictions)if(restriction.scope===scope&&this.#active(restriction.view,requester))try{if(!restriction.keep(registration))return false;}catch(error){this.#report(error);return false;}
  return true;
 }
 #visible(scope:ScopeKey,requester?:View):Service[]{const active=this.#services.filter(s=>this.#active(s.view,requester));const local=active.filter(s=>scope!=='global'&&s.view.scope===scope);return [...local,...active.filter(s=>s.view.scope==='global'&&!local.some(l=>l.key===s.key)&&this.#kept(this.#describe(s),scope,requester))];}
 #find(key:string,scope:ScopeKey,requester?:View):Service|undefined{return this.#visible(scope,requester).find(s=>s.key===key);}
 #listenerVisible(listener:Listener,scope:ScopeKey):boolean{
  if(!this.#listeners.includes(listener)||!this.#active(listener.view))return false;
  if(scope!=='global'&&listener.view.scope===scope)return true;
  return listener.view.scope==='global'&&this.#kept(this.#describeListener(listener),scope)&&!this.#listeners.some(l=>scope!=='global'&&l.view.scope===scope&&this.#active(l.view)&&l.event.name===listener.event.name&&l.name===listener.name);
 }
 #definition(event:QianEvent<unknown,unknown>):void{const old=this.#listeners.find(l=>l.event.name===event.name);if(old&&old.event!==event)throw conflict(event.name,old.view.group.entry.id);}
 #validateGraph(all:Node[],added:Node[]):void{
  const edges=new Map<Node,Node[]>();for(const node of all){const dependencies:Node[]=[];for(const key of node.inject){const provider=all.find(n=>n.entry.scope===node.entry.scope&&n.provides.includes(key))??all.find(n=>n.entry.scope==='global'&&n.provides.includes(key));if(!provider){if(!added.includes(node))continue;throw new RuntimeError('DEPENDENCY_MISSING',{key,entry:node.entry.id},'/entries/'+node.entry.id.replaceAll('~','~0').replaceAll('/','~1')+'/inject');}dependencies.push(provider);}edges.set(node,dependencies);}
  const visited=new Set<Node>(),stack:Node[]=[];const visit=(node:Node):void=>{const index=stack.indexOf(node);if(index>=0)throw new RuntimeError('DEPENDENCY_CYCLE',{cycle:[...stack.slice(index),node].map(n=>n.entry.id).join(' → ')},'/entries/'+node.entry.id.replaceAll('~','~0').replaceAll('/','~1')+'/inject');if(visited.has(node))return;visited.add(node);stack.push(node);for(const dep of edges.get(node)??[])visit(dep);stack.pop();};for(const node of all)visit(node);
 }
 #invalidate():void{let changed:boolean;do{changed=false;for(const node of this.#nodes)if(node.state==='Ready'&&!node.removing&&!node.dependencies.every(d=>this.#find(d.key,node.entry.scope)===d)){node.group!.active=false;node.state='Pending';node.pendingSince=Date.now();node.warned=false;changed=true;}}while(changed);}
 async #cleanPending():Promise<void>{for(const n of this.#nodes.filter(n=>n.state==='Pending'&&n.group).sort((a,b)=>b.sequence-a.sequence))await this.#stop(n);}
 async #stop(node:Node):Promise<void>{const group=node.group;if(!group)return;group.active=false;group.closed=true;this.#signal();try{await group.backend?.stop();}catch(error){node.failure=node.failure?new AggregateError([node.failure,error],'撤销失败'):error;this.#report(error);}finally{node.group=undefined;}}
 async #disposeEffects(group:Group):Promise<void>{const errors:unknown[]=[];for(const effect of group.effects.splice(0).reverse())try{await effect();}catch(error){errors.push(error);}if(errors.length)throw new AggregateError(errors,'副作用撤销失败。');}
 async #reconcile(signal?:AbortSignal):Promise<void>{
  await this.#cleanPending();while(!this.#disposed){const node=this.#nodes.find(n=>!n.removing&&n.state==='Pending'&&n.inject.every(k=>this.#find(k,n.entry.scope)));if(!node)return;signal?.throwIfAborted();
   const group={entry:node.entry,active:false,closed:false,effects:[],ordinals:new Map()} as unknown as Group;group.context=this.#context({group,scope:node.entry.scope});node.group=group;node.dependencies=node.inject.map(k=>this.#find(k,node.entry.scope)!);node.state='Applying';node.sequence=++this.#sequence;
   try{group.backend=this.#backend.start(()=>Promise.resolve(node.definition.plugin.apply(group.context,node.entry.config)),()=>this.#disposeEffects(group));await group.backend.ready;signal?.throwIfAborted();
    if(!node.provides.every(k=>this.#services.some(s=>s.key===k&&s.view.scope===node.entry.scope&&s.view.group===group)))throw new Error('条目未注册声明的 provides。');
    if(!node.dependencies.every(d=>this.#find(d.key,node.entry.scope,{group,scope:node.entry.scope})===d))throw new Error('Apply 期间改变了自身依赖可见性。');
    node.state='Ready';group.active=true;this.#notify();
   }catch(error){node.failure=error;node.state='Failed';await this.#stop(node);if(signal?.aborted)throw error;}
   await this.#cleanPending();
  }
 }
 #context(view:View):Context{
  const loader=this;const ensure=():void=>{if(view.group.closed)throw new Error('上下文已卸载。');};
  const addEffect=(effect:Disposer):void=>{ensure();view.group.effects.push(once(effect));};
  const track=(cleanup:Disposer):Disposer=>{ensure();const remove=once(()=>{view.group.effects=view.group.effects.filter(effect=>effect!==remove);return cleanup();});view.group.effects.push(remove);return remove;};
  const target={
   get entry(){return view.group.entry;},get scope(){return view.scope;},
   provide<T>(key:ServiceKey<T>,...args:[T]|[string,T]):Disposer{ensure();const name=args.length===2?args[0] as string:undefined;const value=args.length===2?args[1]:args[0];if(value===null||value===undefined)throw new TypeError('注册值不能为 null 或 undefined。');const full=named(key,name);const old=loader.#services.find(s=>s.key===full&&s.view.scope===view.scope);if(old)throw conflict(full,old.view.group.entry.id);const stored=loader.#backend.store(value);const service:Service={key:full,view,storage:stored.id,remove:stored.remove};loader.#services.push(service);const remove=track(()=>{loader.#services=loader.#services.filter(s=>s!==service);service.remove();loader.#notify();});loader.#notify();return remove;},
   tryInject<T>(key:ServiceKey<T>,name?:string):T|undefined{ensure();const service=loader.#find(named(key,name),view.scope,view);return service?loader.#backend.get(service.storage) as T:undefined;},
   inject<T>(key:ServiceKey<T>,name?:string):T{const value=target.tryInject(key,name);if(value===undefined)throw name===undefined?new ServiceNotReadyError():new RuntimeError('NOT_FOUND');return value;},
   async whenReady<T>(key:ServiceKey<T>,signal?:AbortSignal):Promise<T>{while(true){ensure();signal?.throwIfAborted();const value=target.tryInject(key);if(value!==undefined)return value;await new Promise<void>((resolve,reject)=>{const done=()=>{signal?.removeEventListener('abort',abort);loader.#waiters.delete(done);resolve();};const abort=()=>{loader.#waiters.delete(done);reject(signal!.reason);};if(signal?.aborted){reject(signal.reason);return;}loader.#waiters.add(done);signal?.addEventListener('abort',abort,{once:true});});}},
   effect:addEffect,
   scoped(scope:ScopeKey):Context{ensure();scopeValid(scope);return loader.#context({group:view.group,scope});},
   restrict(scope:ScopeKey,keep:(registration:Registration)=>boolean):Disposer{ensure();scopeValid(scope);if(scope==='global')throw new TypeError('restriction 目标不能为 global。');const row={view,scope,keep};loader.#restrictions.push(row);const remove=track(()=>{loader.#restrictions=loader.#restrictions.filter(r=>r!==row);loader.#notify();});loader.#notify();return remove;},
   list<T>(key:ServiceKey<T>):readonly Registration<T>[]{ensure();return Object.freeze([...loader.#visible(view.scope,view).map(s=>loader.#describe(s)),...loader.#listeners.filter(l=>loader.#listenerVisible(l,view.scope)).map(l=>loader.#describeListener(l))].filter(r=>r.key===key) as Registration<T>[]);},
   on<P,R>(event:QianEvent<P,R>,mode:QianEvent<P,R>['mode'],handler:HandlerFor<P,R>,priority=0):Disposer{ensure();if(!event?.name?.trim()||typeof handler!=='function')throw new TypeError('事件或监听器无效。');if(mode!==event.mode)throw new TypeError('调用点事件模式与 Definition 不一致。');if(!Number.isFinite(priority))throw new TypeError('priority 无效。');loader.#definition(event as QianEvent<unknown,unknown>);const ordinal=view.group.ordinals.get(event.name)??0;view.group.ordinals.set(event.name,ordinal+1);const stored=loader.#backend.store(handler);const row:Listener={event:event as QianEvent<unknown,unknown>,view,name:view.group.entry.id+'/'+ordinal,priority,sequence:++loader.#listenerSequence,storage:stored.id,remove:stored.remove};loader.#listeners.push(row);const remove=track(()=>{loader.#listeners=loader.#listeners.filter(l=>l!==row);row.remove();});return remove;},
   async dispatch<P,R>(event:QianEvent<P,R>,payload:P,terminal?:()=>R|Promise<R>,signal?:AbortSignal):Promise<R>{
    ensure();signal?.throwIfAborted();loader.#definition(event as QianEvent<unknown,unknown>);const listeners=loader.#listeners.filter(l=>l.event.name===event.name&&loader.#listenerVisible(l,view.scope)).sort((a,b)=>b.priority-a.priority||a.sequence-b.sequence);
    const handler=(listener:Listener)=>loader.#backend.get(listener.storage) as HandlerFor<P,R>;
    const next=async(index:number):Promise<R>=>{signal?.throwIfAborted();while(index<listeners.length&&!loader.#listenerVisible(listeners[index],view.scope))index++;if(index===listeners.length){if(terminal)return terminal();if(event.terminal)return event.terminal(payload);throw new TypeError('waterfall 必须指定链尾。');}return await handler(listeners[index])(payload,()=>next(index+1)) as R;};
    if(event.mode==='waterfall')return next(0);
    if(event.mode==='parallel'){const outcomes=await Promise.all(listeners.map(async listener=>{try{signal?.throwIfAborted();if(!loader.#listenerVisible(listener,view.scope))return{success:false};return{success:true,value:await handler(listener)(payload,()=>Promise.reject(new Error('非 waterfall 事件不能使用 next。')))};}catch(error){return{success:false,error};}}));const partial=outcomes.filter(o=>o.success).map(o=>o.value);const failed=outcomes.filter(o=>'error'in o).map(o=>o.error);if(failed.length)throw new ParallelDispatchError(failed,partial);return partial as R;}
    const results:unknown[]=[];for(const listener of listeners){signal?.throwIfAborted();if(!loader.#listenerVisible(listener,view.scope))continue;try{const value=await handler(listener)(payload,()=>Promise.reject(new Error('非 waterfall 事件不能使用 next。')));if(event.mode==='bail'&&value!==null&&value!==undefined)return value as R;if(event.mode==='serial')results.push(value);}catch(error){if(event.mode!=='emit')throw error;loader.#report(error);}}
    return (event.mode==='serial'?results:undefined) as R;
   }
  };
  return new Proxy(target,{get(object,key){if(key==='then')return undefined;if(Reflect.has(object,key))return Reflect.get(object,key);return typeof key==='string'?target.inject(key):undefined;},set(){throw new TypeError('上下文为只读；使用 provide 注册服务。');}}) as Context;
 }
}
