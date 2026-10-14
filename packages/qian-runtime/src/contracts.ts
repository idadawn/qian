/** Definition 拥有的服务键；不透明字符串，不强制改变共享 TCK 的大小写。 */
export type ServiceKey<T=unknown> = string & {readonly __type?: T};
export type ScopeKey = 'global' | `${'tenant'|'app'|'agent-session'}:${string}`;
export type DispatchMode = 'emit'|'waterfall'|'parallel'|'serial'|'bail';
export type Disposer = () => void | Promise<void>;
/** provide 返回的注册句柄：调用即撤销且幂等；replace 在原注册位原子替换值，解析方只看到旧值或新值，scope、顺序与独占性不变；以 inject 依赖该键的条目与撤销后再登记一样重新 apply；已撤销或上下文已卸载时抛错。 */
export type RegistrationHandle<T=unknown> = Disposer & {readonly replace:(value:T)=>void};
export type PluginState = 'Pending'|'Applying'|'Ready'|'Failed';
export interface QianEvent<P,R=void,M extends DispatchMode=DispatchMode> {readonly name:string;readonly mode:M;readonly terminal?: (payload:P)=>R|Promise<R>;readonly __p?:P;readonly __r?:R}
export type HandlerFor<P,R> = (payload:P,next:()=>Promise<R>)=>unknown|Promise<unknown>;
export interface PluginEntry<C=unknown> {readonly id:string;readonly plugin:string;readonly version:string;readonly scope:ScopeKey;readonly layer:number;readonly config:C}
export interface QianPlugin<C=unknown> {readonly name:string;readonly inject?:readonly ServiceKey[];readonly optionalInject?:readonly ServiceKey[];apply(ctx:Context,config:C):void|Promise<void>}
export interface PluginDefinition<C=unknown> {readonly entry:PluginEntry<C>;readonly plugin:QianPlugin<C>;readonly provides:readonly ServiceKey[]}
export interface Registration<T=unknown> {readonly key:ServiceKey<T>;readonly name:string;readonly scope:ScopeKey;readonly ownerEntryId:string;readonly value:T}
export interface Context {
 readonly entry:PluginEntry;readonly scope:ScopeKey;
 provide<T>(key:ServiceKey<T>,value:T):RegistrationHandle<T>;
 provide<T>(key:ServiceKey<T>,name:string,value:T):RegistrationHandle<T>;
 inject<T>(key:ServiceKey<T>,name?:string):T;
 tryInject<T>(key:ServiceKey<T>,name?:string):T|undefined;
 whenReady<T>(key:ServiceKey<T>,signal?:AbortSignal):Promise<T>;
 effect(disposer:Disposer):void;
 on<P,R,M extends DispatchMode>(event:QianEvent<P,R,M>,mode:NoInfer<M>,handler:HandlerFor<P,R>,priority?:number):Disposer;
 dispatch<P,R>(event:QianEvent<P,R>,payload:P,terminal?:()=>R|Promise<R>,signal?:AbortSignal):Promise<R>;
 scoped(scope:ScopeKey):Context;
 restrict(scope:ScopeKey,keep:(registration:Registration)=>boolean):Disposer;
 list<T>(key:ServiceKey<T>):readonly Registration<T>[];
 readonly [key:`${Lowercase<string>}`]:unknown;
}
export interface RuntimeOptions {readonly onError?:(error:unknown)=>void;readonly onPending?:(entry:PluginEntry,keys:readonly ServiceKey[])=>void;readonly pendingTimeoutMs?:number}
export function defineEvent<P,R=void>(name:string,mode:'emit',terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R,'emit'>;
export function defineEvent<P,R=void>(name:string,mode:'waterfall',terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R,'waterfall'>;
export function defineEvent<P,R=void>(name:string,mode:'parallel',terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R,'parallel'>;
export function defineEvent<P,R=void>(name:string,mode:'serial',terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R,'serial'>;
export function defineEvent<P,R=void>(name:string,mode:'bail',terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R,'bail'>;
export function defineEvent<P,R=void>(name:string,mode:DispatchMode,terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R>;
export function defineEvent<P,R=void>(name:string,mode:DispatchMode,terminal?:(payload:P)=>R|Promise<R>):QianEvent<P,R>{
 if(!name.trim()||!['emit','waterfall','parallel','serial','bail'].includes(mode))throw new TypeError('事件名称或分发模式无效。');
 return Object.freeze({name,mode,...(terminal?{terminal}:{})});
}
