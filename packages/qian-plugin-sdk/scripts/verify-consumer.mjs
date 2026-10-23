import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { lstatSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('../../../', import.meta.url));
const [mode, artifactDirectory] = process.argv.slice(2);
assert(['archive', 'registry'].includes(mode), '需要 archive 或 registry 消费验证模式');
const paths = ['packages/qian-runtime', 'packages/yue/tokens', 'packages/yue/contracts', 'packages/yue/sdk', 'packages/qian-plugin-sdk'];
const packages = paths.map((source) => JSON.parse(readFileSync(path.join(root, source, 'package.json'), 'utf8')));
const sdk = packages.at(-1);
const artifacts = mode === 'archive' ? JSON.parse(readFileSync(path.join(path.resolve(artifactDirectory), 'packages.json'), 'utf8')) : [];
const directory = mkdtempSync(path.join(tmpdir(), 'qian-plugin-sdk-consumer-'));
try {
  const dependencies = { react: '19.3.0', 'react-dom': '19.3.0' };
  for (const pkg of packages) {
    const name = pkg === sdk ? '@qian/plugin-sdk' : pkg.name;
    dependencies[name] = mode === 'archive' ? 'file:' + artifacts.find((item) => item.name === pkg.name).archive
      : pkg === sdk ? `npm:${pkg.name}@${pkg.version}` : pkg.version;
  }
  writeFileSync(path.join(directory, 'package.json'), JSON.stringify({ private: true, type: 'module', dependencies }));
  const configuration = 'registry=https://registry.npmjs.org/\n@idadawn:registry=https://npm.pkg.github.com\n'
    + (process.env.NODE_AUTH_TOKEN ? '//npm.pkg.github.com/:_authToken=${NODE_AUTH_TOKEN}\n' : '');
  writeFileSync(path.join(directory, 'user.npmrc'), configuration, { mode: 0o600 });
  const env = { ...process.env, NPM_CONFIG_USERCONFIG: path.join(directory, 'user.npmrc'), NPM_CONFIG_CACHE: path.join(directory, 'npm-cache') };
  execFileSync('npm', ['install', '--ignore-scripts', '--no-audit', '--no-fund'], { cwd: directory, env, stdio: 'inherit' });
  const lock = JSON.parse(readFileSync(path.join(directory, 'package-lock.json'), 'utf8'));
  for (const pkg of packages) {
    const installedName = pkg === sdk ? '@qian/plugin-sdk' : pkg.name;
    const installedPath = path.join(directory, 'node_modules', installedName);
    assert(!lstatSync(installedPath).isSymbolicLink(), '不得用工作区链接代替消费制品');
    const installed = JSON.parse(readFileSync(path.join(installedPath, 'package.json'), 'utf8'));
    assert.equal(installed.name, pkg.name); assert.equal(installed.version, pkg.version);
    assert.equal(installed.repository.directory, pkg.repository.directory);
    if (mode === 'registry') assert.equal(new URL(lock.packages['node_modules/' + installedName].resolved).hostname, 'npm.pkg.github.com');
  }
  writeFileSync(path.join(directory, 'smoke.mjs'), `
import assert from 'node:assert/strict';
import {PlatformClient,ShellHost,SLOT_IDS} from '@qian/plugin-sdk';
import {adaptFederation} from '@qian/plugin-sdk/federation';
assert.equal(typeof adaptFederation,'function');assert.equal(SLOT_IDS.length,6);
let intercepted=0,navigated='',registered=0;const requests=[];
const client=new PlatformClient({baseUrl:'https://consumer.test',getTenantId:()=> 'tenant-a',middleware:[async(r,next)=>{intercepted++;return next(r)}],transport:async(r)=>{requests.push(r);return Response.json({saved:true})}});
const host=new ShellHost({yueVersion:'0.1.0',baseUrl:'https://consumer.test',client,
 federation:{registerRemotes(){registered++},loadRemote:async()=>({default:()=>null}),preloadRemote:async()=>{},removeRemote(){}},
 getContext:()=>({tenant:{id:'tenant-a',name:'验收'},membership:{id:'member-a',permissions:['read','write']},app:null,locale:'zh-CN',theme:'light'}),
 isMounted:()=>true,isEntitled:()=>true,hasPermission:()=>true,navigate:(r)=>{navigated=r},notify(){},audit:async()=>{throw Error('unexpected audit')}});
await host.start();host.registerRemote({id:'qian.consumer',version:'1.0.0',scope:'tenant:tenant-a',manifest:{$schema:'https://qian.dev/schemas/ui/v2',federation:{manifest:'/mf-manifest.json',exposes:['Page']},engines:{yue:'^0.1.0'},
 contributions:[{slot:'qian.shell.nav/v1',id:'page',title:'页面',description:'消费者验证页面',permission:'read',expose:'Page',route:'/page'}],
 bridge:{slots:['qian.shell.nav/v1','qian.shell.command/v1'],navigate:['/page'],notify:false,capabilities:[{id:'qian.consumer/save',permission:'write',sideEffect:'write'}]}}});
assert.equal(registered,0);await host.loadSlot('qian.shell.nav/v1');assert.equal(registered,1);
const item=host.list('qian.shell.nav/v1')[0];assert.equal(item.remoteId,'qian.consumer');assert(Object.isFrozen(item.bridge.getContext().membership.permissions));
assert.deepEqual(host.bridge.getContext(item.bridge.caller),item.bridge.getContext());assert.throws(()=>host.bridge.getContext({...item.bridge.caller}),/NOT_FOUND/);
host.bridge.navigate(item.bridge.caller,'/page');assert.equal(navigated,'/page');
const key='11111111-1111-4111-8111-111111111111';assert.deepEqual(await host.bridge.invoke(item.bridge.caller,'qian.consumer/save',{value:1},{idempotencyKey:key}),{saved:true});
assert.equal(intercepted,1);assert.equal(requests[0].headers.get('Idempotency-Key'),key);assert.equal((await requests[0].json()).idempotencyKey,key);
await host.dispose();assert.throws(()=>item.bridge.getContext(),/NOT_FOUND/);assert.throws(()=>host.bridge.getContext(item.bridge.caller),/NOT_FOUND/);
`);
  writeFileSync(path.join(directory, 'smoke.mts'), `
import {ShellHost,type ShellHostOptions,type RemoteRegistration,type PlatformClientOptions} from '@qian/plugin-sdk';
import {adaptFederation} from '@qian/plugin-sdk/federation';
const host=new ShellHost({} as ShellHostOptions);void ({} as PlatformClientOptions);void adaptFederation;
host.registerRemote({} as RemoteRegistration);
// @ts-expect-error 不存在的插槽不能通过发布声明检查。
host.loadSlot('qian.shell.unknown/v1');
`);
  execFileSync(process.execPath, ['smoke.mjs'], { cwd: directory, env, stdio: 'inherit' });
  execFileSync(process.execPath, [path.join(root, 'node_modules/typescript/bin/tsc'), '--noEmit', '--strict', '--skipLibCheck', '--target', 'ES2022', '--module', 'NodeNext', '--moduleResolution', 'NodeNext', 'smoke.mts'], { cwd: directory, env, stdio: 'inherit' });
  console.log(JSON.stringify({ package: sdk.name, version: sdk.version, source: mode, logicalImport: '@qian/plugin-sdk', cohort: packages.map((pkg) => pkg.name + '@' + pkg.version), runtime: true, types: true, middleware: true }));
} finally { rmSync(directory, { recursive: true, force: true }); }
