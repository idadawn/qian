import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {once} from 'node:events';
import {spawn} from 'node:child_process';
import {mkdtempSync,readFileSync,writeFileSync,mkdirSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {PluginLoader} from '../../qian-runtime/dist/index.js';
import {HotReloadSurfacePlugin,readSurfaceRuntimeDiagnostics} from '../../qian-surfaces/dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const spec=JSON.parse(readFileSync(path.join(root,'tck/runtime/hotreload.json'),'utf8'));
const token='local-diagnostic-test-token';
async function run(url,authenticated=true){
 const env={...process.env};delete env.QIAN_ACCESS_TOKEN;if(authenticated)env.QIAN_ACCESS_TOKEN=token;
 const child=spawn(process.execPath,[path.join(root,'packages/qian-cli/dist/main.js'),'doctor','--cwd',root,'--profile','minimal','--no-home','--url',url,'--json'],{env});
 let stdout='',stderr='';child.stdout.setEncoding('utf8').on('data',data=>stdout+=data);child.stderr.setEncoding('utf8').on('data',data=>stderr+=data);
 const timeout=setTimeout(()=>child.kill('SIGKILL'),15000);
 try{const[code]=await once(child,'close');assert.ok(!(stdout+stderr).includes(token));return{code,data:JSON.parse(stdout||stderr)};}finally{clearTimeout(timeout);}
}
test('真实 TS 文件监视状态经本机宿主进入 doctor，源码变化提示重启，卸载后不残留',async()=>{
 const directory=mkdtempSync(path.join(tmpdir(),'qian-doctor-'));const runtime=new PluginLoader();let applies=0,source;
 const server=createServer((request,response)=>{
  if(request.url!=='/api/lifecycle/diagnostics/surfaces'){response.writeHead(404).end();return;}
  if(request.headers.authorization!=='Bearer '+token){response.writeHead(401).end();return;}
  response.setHeader('Content-Type','application/json');response.end(JSON.stringify(readSurfaceRuntimeDiagnostics(source)));
 });
 try{
  const write=(file,value)=>{const target=path.join(directory,file);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,value);};
  write('.qian-plugin/plugin.json',JSON.stringify({id:spec.plugin,version:'1.0.0',description:'实际文件监视状态和 CLI 诊断的验收组件。',author:{name:'Qian'},license:'Apache-2.0',interface:{displayName:'诊断',category:'testing'},inject:[],provides:['Code'],policy:{tier:'official',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global','tenant'],runtime:{form:'module',entry:'Code'}}));
  write(`skills/${spec.skill}/SKILL.md`,`---\nname: ${spec.skill}\ndescription: ${spec.oldDescription}\n---\n检查内容。\n`);
  write(spec.codeFile,'original source');
  const plugin=new HotReloadSurfacePlugin(directory,{profile:'minimal',executionPlane:'server'},{inject:[],apply(ctx){applies++;ctx.provide('Code',{});}});
  const entry=(id,plugin,scope)=>({id,plugin,version:'1.0.0',scope,layer:0,config:{}});
  await runtime.mount([{entry:entry('host','test.host','global'),plugin:{name:'host',inject:[],apply(ctx){source=ctx.scoped(spec.scope);}},provides:[]},{entry:entry(spec.entry,spec.plugin,spec.scope),plugin,provides:['Code',...plugin.surfaceProvides]}]);
  server.listen(0,'127.0.0.1');await once(server,'listening');const url=`http://127.0.0.1:${server.address().port}`;
  const initial=await run(url);assert.equal(initial.code,0,JSON.stringify(initial.data));assert.ok(initial.data.runtimeVerified);assert.deepEqual(initial.data.actions,[]);
  write(spec.codeFile,'private source must not leak');const deadline=Date.now()+spec.deadlineMs;
  while(!plugin.status.restartRequired){assert.ok(Date.now()<deadline);await new Promise(resolve=>setTimeout(resolve,20));}
  const changed=await run(url);assert.equal(changed.code,1);assert.equal(changed.data.actions[0].code,'ROLLING_RESTART_REQUIRED');assert.deepEqual(changed.data.actions[0].files,[spec.codeFile]);assert.ok(!JSON.stringify(changed).includes('private source'));assert.equal(applies,1);
  const skillFile=`skills/${spec.skill}/SKILL.md`,validSkill=readFileSync(path.join(directory,skillFile),'utf8');
  write(skillFile,'invalid');let until=Date.now()+spec.deadlineMs;
  while(!plugin.status.diagnostics.some(item=>item.severity==='error')){assert.ok(Date.now()<until);await new Promise(resolve=>setTimeout(resolve,20));}
  const invalid=await run(url);assert.equal(invalid.code,1);assert.ok(invalid.data.actions.some(item=>item.code==='SURFACE_UPDATE_REJECTED'));
  write(skillFile,validSkill);until=Date.now()+spec.deadlineMs;
  while(plugin.status.diagnostics.some(item=>item.severity==='error')){assert.ok(Date.now()<until);await new Promise(resolve=>setTimeout(resolve,20));}
  const recovered=await run(url);assert.ok(!recovered.data.actions.some(item=>item.code==='SURFACE_UPDATE_REJECTED'));
  const denied=await run(url,false);assert.equal(denied.code,1);assert.equal(denied.data.diagnostics[0].error.code,'INPUT_INVALID');
  await runtime.unmount(spec.entry,spec.scope);const removed=await run(url);assert.equal(removed.code,0);assert.deepEqual(removed.data.runtime.entries,[]);
 }finally{await runtime.dispose();server.closeAllConnections();if(server.listening)await new Promise(resolve=>server.close(resolve));rmSync(directory,{recursive:true,force:true});}
});
test('doctor 拒绝重定向、超大与畸形响应，不能把读取失败报告为健康',async()=>{
 let mode='redirect';let forwarded=0;
 const server=createServer((request,response)=>{
  if(request.url==='/leak'){forwarded++;response.end('{}');return;}
  if(mode==='redirect'){response.writeHead(302,{Location:'/leak'}).end();return;}
  if(mode==='large'){response.end('x'.repeat(1048577));return;}
  response.end(JSON.stringify({protocol:'qian.surface-diagnostics/v1',scope:'tenant:a',entries:[{entryId:'x',plugin:'test.x',scope:'tenant:b',revision:0,restartRequired:false,changedCodeFiles:[],invalidSurface:false}]}));
 });
 try{
  server.listen(0,'127.0.0.1');await once(server,'listening');const url=`http://127.0.0.1:${server.address().port}`;
  for(mode of ['redirect','large','scope']){const result=await run(url);assert.equal(result.code,1,mode);assert.equal(result.data.diagnostics[0].error.code,'INPUT_INVALID');}
  assert.equal(forwarded,0);
  const malformed=await run('https://user:secret@example.test/?token=private');assert.equal(malformed.code,1);assert.ok(!JSON.stringify(malformed).includes('secret'));
 }finally{server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}
});
