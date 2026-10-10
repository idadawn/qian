import {createRequire} from 'node:module';
import {mkdtempSync,readFileSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createServer} from 'node:http';
import {pathToFileURL,fileURLToPath} from 'node:url';
const workspace=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const resolve=createRequire(path.join(workspace,'packages/qian-runtime/package.json'));
const {build}=await import(pathToFileURL(resolve.resolve('vite')).href);
const {chromium}=resolve('@playwright/test');
const out=mkdtempSync(path.join(os.tmpdir(),'qian-runtime-browser-'));
await build({configFile:false,logLevel:'error',build:{lib:{entry:path.join(workspace,'packages/qian-runtime/src/index.ts'),formats:['es'],fileName:()=> 'runtime.js'},outDir:out,emptyOutDir:true}});
const server=createServer((req,res)=>{if(req.url==='/runtime.js'){res.setHeader('Content-Type','text/javascript');res.end(readFileSync(path.join(out,'runtime.js')));}else{res.setHeader('Content-Type','text/html');res.end('<!doctype html><title>运行时验收</title>');}});
await new Promise(r=>server.listen(0,'127.0.0.1',r));const port=server.address().port;
const browser=await chromium.launch({headless:true});
try{const page=await browser.newPage();await page.goto('http://127.0.0.1:'+port);const result=await page.evaluate(async()=>{
 const{PluginLoader,defineEvent}=await import('/runtime.js');const loader=new PluginLoader(),trace=[];
 await loader.mount([{entry:{id:'base',plugin:'test.base',version:'1.0.0',scope:'global',layer:0,config:{}},provides:['files:upload'],plugin:{name:'test.base',apply(ctx){ctx.provide('files','upload','global');ctx.effect(()=>trace.push('base'));}}},{entry:{id:'tenant',plugin:'test.tenant',version:'1.0.0',scope:'tenant:a',layer:0,config:{}},provides:['files:upload'],plugin:{name:'test.tenant',apply(ctx){ctx.provide('files','upload','tenant-a');ctx.effect(()=>trace.push('tenant'));}}}]);
 const ctx=loader.getContext('base');const a=ctx.scoped('tenant:a').inject('files','upload'),b=ctx.scoped('tenant:b').inject('files','upload');
 const event=defineEvent('browser/bail','bail');ctx.on(event,'bail',()=>false);const value=await ctx.dispatch(event,null);await loader.dispose();return{a,b,value,trace,remaining:loader.dumpConfig().length};
 });if(JSON.stringify(result)!==JSON.stringify({a:'tenant-a',b:'global',value:false,trace:['tenant','base'],remaining:0}))throw new Error(JSON.stringify(result));console.log('真实 Chromium 通过：scope 遮蔽、bail false、逆序卸载、无条目残留。');}
finally{await browser.close();await new Promise(r=>server.close(r));rmSync(out,{recursive:true,force:true});}
