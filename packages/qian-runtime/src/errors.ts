import {errorCatalog} from './error-catalog.js';
export interface RuntimeErrorDetail {type:string;title:string;status:number;code:string;message:string;detail:string;trace_id:string;pointer?:string}
/** 使用冻结目录中的错误码，过滤与不存在共用 NOT_FOUND。 */
export class RuntimeError extends Error {
 readonly args:Readonly<Record<string,string>>;
 constructor(readonly code:keyof typeof errorCatalog,args:Record<string,string>={},readonly pointer?:string){super(code);this.name='RuntimeError';this.args=Object.freeze({...args});}
 toDetail(traceId=globalThis.crypto.randomUUID().replaceAll('-',''),language:'zh-CN'|'en'='zh-CN'):RuntimeErrorDetail{
  if(!/^[0-9a-f]{32}$/.test(traceId))throw new TypeError('trace-id 必须是 32 位十六进制字符串。');
  const definition=errorCatalog[this.code];const message=definition.message[language].replace(/\{([^}]+)\}/g,(_,key)=>this.args[key]??'{'+key+'}');
  return {type:definition.type,title:definition.title[language],status:definition.status,code:this.code,message,detail:message,trace_id:traceId,...(this.pointer?{pointer:this.pointer}:{})};
 }
}
export class ServiceNotReadyError extends RuntimeError {constructor(){super('NOT_FOUND');this.name='ServiceNotReadyError';}}
export class ParallelDispatchError<T=unknown> extends AggregateError {
 readonly partial:readonly T[];
 constructor(errors:unknown[],partial:T[]){super(errors,'并行分发中的部分监听器失败。');this.name='ParallelDispatchError';this.partial=Object.freeze([...partial]);}
}
