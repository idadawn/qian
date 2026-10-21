import test from 'node:test';
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {spawn} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const cli=fileURLToPath(new URL('../dist/main.js',import.meta.url));
const tenant='01900000-0000-7000-8000-000000000001',scope='tenant:'+tenant;
const error=(code,status)=>({type:'https://qian.dev/problems/'+([409,422].includes(status)?'business':'protocol')+'/'+code,title:code,status,code,message:'操作拒绝',detail:'操作拒绝',trace_id:'a'.repeat(32)});
async function withServer(run){
 const requests=[];let respond;
 const server=createServer(async(req,res)=>{let body='';for await(const chunk of req)body+=chunk;requests.push({method:req.method,url:req.url,authorization:req.headers.authorization,body:body?JSON.parse(body):null});respond(req,res,requests.at(-1));});
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 try{await run('http://127.0.0.1:'+server.address().port,requests,fn=>respond=fn);}finally{await new Promise(resolve=>server.close(resolve));}
}
async function command(url,args,token='test-operational-token'){
 const child=spawn(process.execPath,[cli,...args,'--scope',scope,'--url',url,'--json','--no-home'],{env:{...process.env,QIAN_ACCESS_TOKEN:token},stdio:['ignore','pipe','pipe']});
 let out='',err='';child.stdout.on('data',chunk=>out+=chunk);child.stderr.on('data',chunk=>err+=chunk);
 const code=await new Promise(resolve=>child.on('close',resolve));return{code,out,err};
}
const send=(res,value,status=200)=>{res.writeHead(status,{'content-type':'application/json'});res.end(JSON.stringify(value));};

test('租户运营 CLI 只发单条 PUT，revision 来自授权视图且不改写请求租户',async()=>withServer(async(url,requests,set)=>{
 set((req,res,seen)=>req.method==='GET'?send(res,{items:[{entryId:'notes',revision:4}]}):send(res,{scope,entryId:'notes',revision:5,state:'Ready',reasonCode:null}));
 const result=await command(url,['add','acme.notes@1.0.0','--entry','notes']);assert.equal(result.code,0,result.err);
 assert.equal(requests.length,2);assert.equal(requests[0].method,'GET');assert.equal(requests[1].method,'PUT');
 assert.equal(requests[1].url,`/api/registry/tenants/${tenant}/patches/${encodeURIComponent(scope)}/notes`);
 assert.deepEqual(requests[1].body,{revision:4,patch:{id:'notes',plugin:'acme.notes@1.0.0',config:{}}});
 assert.ok(requests.every(req=>req.authorization==='Bearer test-operational-token'));assert.ok(!result.out.includes('test-operational-token'));
}));

test('多条租户变更在任何 HTTP 请求前拒绝，不遗留部分成功',async()=>withServer(async(url,requests,set)=>{
 set((req,res)=>send(res,{}));
 for(const args of [['add','acme.one@1.0.0','acme.two@1.0.0'],['remove','one','two']]){
  const result=await command(url,args);assert.equal(result.code,1);assert.equal(JSON.parse(result.err).diagnostics[0].error.code,'INPUT_INVALID');
 }
 assert.equal(requests.length,0);
}));

test('409 保留 business ErrorDetail 与 trace，不读新修订也不重试写入',async()=>withServer(async(url,requests,set)=>{
 set((req,res)=>req.method==='GET'?send(res,{items:[{entryId:'notes',revision:8}]}):send(res,error('CONFLICT',409),409));
 const result=await command(url,['add','acme.notes@1.0.0','--entry','notes','--revision','3']);assert.equal(result.code,1);
 const failure=JSON.parse(result.err).diagnostics[0].error;assert.equal(failure.type,'https://qian.dev/problems/business/CONFLICT');assert.equal(failure.trace_id,'a'.repeat(32));
 assert.equal(requests.length,2);assert.equal(requests[1].body.revision,3);
}));

test('重复 remove 已不可见条目保持不变；显式旧 revision 仍由服务端拒绝',async()=>withServer(async(url,requests,set)=>{
 set((req,res)=>req.method==='GET'?send(res,{items:[]}):send(res,error('CONFLICT',409),409));
 for(let attempt=0;attempt<2;attempt++){const result=await command(url,['remove','notes']);assert.equal(result.code,0);assert.equal(JSON.parse(result.out).entries[0].state,'Unchanged');}
 assert.ok(requests.every(req=>req.method==='GET'));
 const stale=await command(url,['remove','notes','--revision','1']);assert.equal(stale.code,1);assert.equal(requests.at(-1).body.revision,1);
 assert.deepEqual(requests.at(-1).body.patch,{id:'notes',disabled:true});
}));

test('缺凭据、带凭据 URL 和重定向不泄漏令牌；远端非契约错误转 UNAVAILABLE',async()=>withServer(async(url,requests,set)=>{
 const absent=await command(url,['remove','notes'],'');assert.equal(JSON.parse(absent.err).diagnostics[0].error.code,'UNAUTHENTICATED');assert.equal(requests.length,0);
 const invalid=await command('https://operator:secret@example.test',['remove','notes']);assert.equal(invalid.code,1);assert.ok(!invalid.err.includes('secret'));assert.equal(requests.length,0);
 set((req,res)=>{res.writeHead(307,{location:url+'/redirected'});res.end();});
 const redirected=await command(url,['remove','notes']);assert.equal(redirected.code,1);assert.equal(JSON.parse(redirected.err).diagnostics[0].error.code,'UNAVAILABLE');assert.equal(requests.length,1);
 set((req,res)=>send(res,{message:'sensitive raw failure'},409));const malformed=await command(url,['remove','notes']);assert.equal(malformed.code,1);assert.ok(!malformed.err.includes('sensitive raw failure'));
}));

test('ErrorDetail 校验详情完整透传；畸形字段拒绝而不显示未知数据',async()=>withServer(async(url,requests,set)=>{
 const detail={...error('INPUT_INVALID',400),instance:'/api/registry/tenants/example',pointer:'#/patch/config/mode',allowed:['read','write'],errors:[{code:'SURFACE_SCHEMA_INVALID',message:'mode 不在允许范围',pointer:'#/patch/config/mode',allowed:['read','write']}]};
 set((req,res)=>req.method==='GET'?send(res,{items:[]}):send(res,detail,400));
 const result=await command(url,['add','acme.notes@1.0.0']);assert.equal(result.code,1);assert.deepEqual(JSON.parse(result.err).diagnostics[0].error,detail);
 set((req,res)=>send(res,{...detail,errors:[{...detail.errors[0],pointer:'invalid',secret:'do-not-echo'}]},400));
 const malformed=await command(url,['add','acme.notes@1.0.0']);assert.equal(malformed.code,1);assert.equal(JSON.parse(malformed.err).diagnostics[0].error.code,'UNAVAILABLE');assert.ok(!malformed.err.includes('do-not-echo'));
}));
