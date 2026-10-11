import {Context as UpstreamContext} from './vendor/cordis.js';
import type {Disposer} from './contracts.js';
export interface BackendScope {readonly ready:Promise<void>;stop():Promise<void>}
/** 上游负责插件 fork 与服务存储；Qian 的顺序、可见性和错误语义由适配层约束。 */
export class Backend {
 #root=new UpstreamContext();#next=0;
 constructor(){this.#root.on('internal/error',()=>{});this.#root.on('internal/warning',()=>{});}
 start(apply:()=>Promise<void>,dispose:()=>Promise<void>):BackendScope{
  let resolve!:()=>void,reject!:(error:unknown)=>void,cleanup:Promise<void>|undefined;
  const ready=new Promise<void>((yes,no)=>{resolve=yes;reject=no;});
  const stop=()=>cleanup??=dispose();
  const fork=this.#root.plugin({name:'qian-entry-'+(++this.#next),apply(ctx:UpstreamContext){
   ctx.effect(()=>()=>{void stop().catch(()=>{});});
   return Promise.resolve().then(apply).then(resolve,error=>{reject(error);});
  }},{});
  return {ready,stop:()=>{fork.dispose();return stop();}};
 }
 store(value:unknown):{id:string;remove:Disposer}{const id='qian-value-'+(++this.#next);const remove=this.#root.set(id,{value});return{id,remove};}
 get(id:string):unknown{return this.#root.get(id)?.value;}
 async stop():Promise<void>{await this.#root.stop();}
}
