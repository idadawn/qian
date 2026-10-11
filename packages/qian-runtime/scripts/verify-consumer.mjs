import assert from 'node:assert/strict';
import {execFileSync} from 'node:child_process';
import {mkdtempSync,readFileSync,writeFileSync,rmSync} from 'node:fs';
import {tmpdir} from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

// 在仓库外安装真实制品，禁止 workspace 链接代替消费验收。
const source=process.argv[2];
assert(source?.startsWith('file:') || /^npm:@idadawn\/qian-runtime@\d+\.\d+\.\d+$/.test(source),'需要本地 tgz 或固定版本的私有 npm alias');
const expected=JSON.parse(readFileSync(new URL('../package.json',import.meta.url),'utf8'));
const directory=mkdtempSync(path.join(tmpdir(),'qian-runtime-consumer-'));
try {
 writeFileSync(path.join(directory,'package.json'),JSON.stringify({private:true,type:'module',dependencies:{'@qian/runtime':source}}));
 writeFileSync(path.join(directory,'user.npmrc'),'registry=https://registry.npmjs.org/\n@idadawn:registry=https://npm.pkg.github.com\n'+(process.env.NODE_AUTH_TOKEN?'//npm.pkg.github.com/:_authToken=${NODE_AUTH_TOKEN}\n':''),{mode:0o600});
 const env={...process.env,NPM_CONFIG_USERCONFIG:path.join(directory,'user.npmrc'),NPM_CONFIG_CACHE:path.join(directory,'npm-cache')};
 execFileSync('npm',['install','--ignore-scripts','--no-audit','--no-fund'],{cwd:directory,env,stdio:'inherit'});
 const installed=path.join(directory,'node_modules/@qian/runtime');
 const pkg=JSON.parse(readFileSync(path.join(installed,'package.json'),'utf8'));
 assert.equal(pkg.name,'@idadawn/qian-runtime');assert.equal(pkg.version,expected.version);
 assert.match(readFileSync(path.join(installed,'README.md'),'utf8'),/模型体验/);
 assert.match(readFileSync(path.join(installed,'README.md'),'utf8'),/已知限制与延期项/);
 assert.match(readFileSync(path.join(installed,'LICENSE'),'utf8'),/Apache License/);
 assert.match(readFileSync(path.join(installed,'dist/vendor/LICENSE.cordis.txt'),'utf8'),/MIT License/);
 if(source.startsWith('npm:')){
  const lock=JSON.parse(readFileSync(path.join(directory,'package-lock.json'),'utf8'));
  assert.equal(new URL(lock.packages['node_modules/@qian/runtime'].resolved).hostname,'npm.pkg.github.com');
 }
 writeFileSync(path.join(directory,'smoke.mjs'),`import assert from 'node:assert/strict';
import {PluginLoader,defineEvent} from '@qian/runtime';
const runtime=new PluginLoader(),released=[],event=defineEvent('consumer/check','bail');
await runtime.mount([{entry:{id:'probe',plugin:'qian.probe',version:'1.0.0',scope:'global',layer:0,config:{}},provides:['message'],plugin:{name:'qian.probe',apply(ctx){ctx.provide('message','installed');ctx.effect(()=>released.push('first'));ctx.effect(()=>released.push('last'));ctx.on(event,'bail',()=>false);}}}]);
assert.equal(runtime.getContext('probe').inject('message'),'installed');
assert.equal(await runtime.getContext('probe').dispatch(event,null),false);
await runtime.dispose();assert.deepEqual(released,['last','first']);assert.deepEqual(runtime.dumpConfig(),[]);
`);
 writeFileSync(path.join(directory,'smoke.mts'),`import {PluginLoader,type ServiceKey} from '@qian/runtime';
const key='count' as ServiceKey<number>;
const count:number=new PluginLoader().getContext('probe').inject(key);
// @ts-expect-error 类型声明必须保留品牌服务键的返回类型。
const invalid:string=count;
void invalid;
`);
 execFileSync(process.execPath,['smoke.mjs'],{cwd:directory,env,stdio:'inherit'});
 const tsc=fileURLToPath(new URL('../node_modules/typescript/bin/tsc',import.meta.url));
 execFileSync(process.execPath,[tsc,'--noEmit','--strict','--target','ES2022','--module','NodeNext','--moduleResolution','NodeNext','smoke.mts'],{cwd:directory,env,stdio:'inherit'});
 console.log(JSON.stringify({package:pkg.name,version:pkg.version,logicalImport:'@qian/runtime',source:source.startsWith('npm:')?'private-registry':'local-archive',runtime:true,types:true,licenses:true}));
} finally {rmSync(directory,{recursive:true,force:true});}
