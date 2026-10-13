// 产品 HTTP 联调消费者；不把这个客户端当成 TS 平台 Provider 或 TS scope 实现。
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
const fixture=JSON.parse(readFileSync(new URL('./capability-filter.json',import.meta.url),'utf8'));
let input='';for await(const chunk of process.stdin){input+=chunk;if(input.length>65536)throw new Error('输入超限。');}
const request=JSON.parse(input),step=fixture.steps[request.step];assert.equal(step.op,'probe');
const base=new URL(request.baseUrl);assert.ok(['127.0.0.1','[::1]'].includes(base.hostname));
const headers={Authorization:'Bearer '+request.token};
async function get(route){const response=await fetch(new URL(route,base),{headers,redirect:'error',signal:AbortSignal.timeout(10000)});assert.equal(response.status,200);return response;}
async function invoke(id){const[component,name]=id.split('/');return fetch(new URL(`/api/capabilities/${component}/${name}/invoke`,base),{method:'POST',headers:{...headers,'Content-Type':'application/json'},body:JSON.stringify(request.command),redirect:'error',signal:AbortSignal.timeout(10000)});}
const catalog=await(await get('/api/capabilities')).json();
const ids=catalog.items.map(item=>item.id).sort();assert.deepEqual(ids,step.visible.map(name=>fixture.component+'/'+name).sort());
const prompt=await(await get('/api/acceptance/prompt')).text();
const promptIds=fixture.capabilities.filter(id=>prompt.includes('工具：'+id+'\n')).sort();assert.deepEqual(promptIds,ids);
const result=await invoke(fixture.target);assert.equal(result.status,step.status);
let errorEquivalent=null;
if(result.status===404){
 const absent=await invoke(fixture.unknown);assert.equal(absent.status,404);
 const normalize=body=>{const{trace_id,instance,...rest}=body;return rest;};
 const hidden=normalize(await result.json()),unknown=normalize(await absent.json());assert.equal(hidden.code,'NOT_FOUND');assert.deepEqual(hidden,unknown);errorEquivalent=true;
}else{const customer=await result.json();assert.equal(customer.tenantId,request.tenantId);assert.equal(customer.id,request.customerId);}
process.stdout.write(JSON.stringify({id:step.id,catalog:ids,prompt:promptIds,status:result.status,errorEquivalent})+'\n');
