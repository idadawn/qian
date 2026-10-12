import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,mkdirSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {createHash} from 'node:crypto';
import {createServer} from 'node:http';
import {once} from 'node:events';
import {spawnSync} from 'node:child_process';
import {fetchPackageComponents} from '../dist/package-components.js';
import {npmExecutable} from '../dist/package-manager.js';

test('真实 npm pack 协议获取组件内容且不修改宿主依赖',async()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-npm-component-')),source=path.join(root,'source'),host=path.join(root,'host'),archiveRoot=path.join(root,'archives');
 const name='qian-component-fetch-fixture',version='1.0.0';let server,fetched;
 try{
  mkdirSync(path.join(source,'qian/.qian-plugin'),{recursive:true});mkdirSync(path.join(source,'qian/skills/check'),{recursive:true});mkdirSync(host);mkdirSync(archiveRoot);
  writeFileSync(path.join(source,'package.json'),JSON.stringify({name,version,files:['qian'],qian:{component:'qian/.qian-plugin/plugin.json'}}));
  writeFileSync(path.join(source,'qian/.qian-plugin/plugin.json'),JSON.stringify({id:'test.package-skill',version,description:'从包内发现的声明式核对指引组件，不包含业务 Provider。',author:{name:'Fixture'},license:'Apache-2.0',interface:{displayName:'包内指引',category:'operations'},inject:[],provides:[],policy:{tier:'private',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global']}));
  writeFileSync(path.join(source,'qian/skills/check/SKILL.md'),'---\nname: check\ndescription: 当用户需要只读核对当前配置与安装来源并生成简短检查说明时使用，不执行修改操作。\n---\n核对安装来源。\n');
  const packed=spawnSync(process.execPath,[npmExecutable(),'pack','--ignore-scripts','--json','--pack-destination',archiveRoot],{cwd:source,encoding:'utf8',timeout:30000});
  assert.equal(packed.status,0,packed.stderr);const archive=readFileSync(path.join(archiveRoot,JSON.parse(packed.stdout)[0].filename));
  const integrity='sha512-'+createHash('sha512').update(archive).digest('base64');
  server=createServer((request,response)=>{
   const origin='http://127.0.0.1:'+server.address().port;
   if(request.url==='/artifact.tgz'){response.setHeader('content-type','application/octet-stream');response.end(archive);return;}
   if(request.url==='/'+name){response.setHeader('content-type','application/json');response.end(JSON.stringify({name,'dist-tags':{latest:version},versions:{[version]:{name,version,dist:{tarball:origin+'/artifact.tgz',integrity,shasum:createHash('sha1').update(archive).digest('hex')}}}}));return;}
   response.statusCode=404;response.end();
  });server.listen(0,'127.0.0.1');await once(server,'listening');
  const initial='{"private":true}\n';writeFileSync(path.join(host,'package.json'),initial);
  writeFileSync(path.join(host,'.npmrc'),'registry=http://127.0.0.1:'+server.address().port+'/\nfetch-retries=0\n');
  fetched=await fetchPackageComponents(host,{nuget:[],npm:[name+'@'+version]});
  assert.equal(fetched.components.length,1);const value=fetched.components[0];
  assert.equal(value.reference.name,name);assert.equal(JSON.parse(value.component.files.get('.qian-plugin/plugin.json')).id,'test.package-skill');
  assert.ok(value.component.files.has('skills/check/SKILL.md'));assert.equal(value.artifactDigest,'sha256:'+createHash('sha256').update(archive).digest('hex'));
  assert.equal(readFileSync(path.join(host,'package.json'),'utf8'),initial);assert.equal(existsSync(path.join(host,'node_modules')),false);assert.equal(existsSync(path.join(host,'package-lock.json')),false);
 }finally{fetched?.dispose();if(server){server.closeAllConnections();await new Promise(resolve=>server.close(resolve));}rmSync(root,{recursive:true,force:true});}
});
