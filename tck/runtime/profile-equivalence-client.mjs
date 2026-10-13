// 直接消费 T23 场景的 HTTP 客户端；业务 Provider 仍在被测 .NET 宿主运行。
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const fixture=JSON.parse(readFileSync(new URL('./profile-equivalence.json',import.meta.url),'utf8'));
let input='';for await(const chunk of process.stdin){input+=chunk;if(input.length>65536)throw new Error('输入超限。');}
const call=JSON.parse(input),step=fixture.steps.find(item=>item.op==='request'&&item.id===call.id);assert.ok(step);
const base=new URL(call.baseUrl);assert.ok(['127.0.0.1','[::1]'].includes(base.hostname));
let route=step.path;for(const[name,id]of Object.entries(call.ids))route=route.replaceAll('{'+name+'}',id);
const headers={};if(!step.anonymous)headers.Authorization='Bearer '+call.token;
if(step.body)headers['Content-Type']='application/json';
const response=await fetch(new URL(route,base),{method:step.method,headers,body:step.body?JSON.stringify(step.body):undefined,redirect:'error',signal:AbortSignal.timeout(10000)});
assert.equal(response.status,step.status);const text=await response.text(),body=text?JSON.parse(text):{};
if(step.name){assert.equal(body.name,step.name);assert.equal(body.tenantId,call.tenantId);}
if(step.same)assert.equal(body.id,call.ids[step.same]);
if(step.names){assert.equal(body.total,step.names.length);assert.deepEqual(body.items.map(item=>item.name).sort(),step.names);}
process.stdout.write(JSON.stringify({status:response.status,body})+'\n');
