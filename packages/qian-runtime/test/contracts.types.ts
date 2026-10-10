import {defineEvent,type Context,type ServiceKey} from '../dist/index.js';
declare const ctx:Context;
const event=defineEvent<string,string>('test/value','bail');
ctx.on(event,'bail',payload=>payload);
// @ts-expect-error 调用点模式必须与已知声明一致。
ctx.on(event,'waterfall',payload=>payload);
const count='count' as ServiceKey<number>;
const result:number=ctx.inject(count);
// @ts-expect-error 品牌服务键必须保持返回值类型。
const invalid:string=ctx.inject(count);
void result;void invalid;
