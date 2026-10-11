import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {execFileSync} from 'node:child_process';
import {readFileSync} from 'node:fs';
import path from 'node:path';

const pkg=JSON.parse(readFileSync(new URL('../package.json',import.meta.url),'utf8'));
assert.equal(process.env.GITHUB_REF,'refs/heads/main');
assert.equal(process.env.GITHUB_REPOSITORY,'idadawn/qian');
assert.equal(pkg.name,'@idadawn/qian-runtime');
assert.equal(pkg.version,process.env.RELEASE_VERSION);
assert(/^\d+\.\d+\.\d+$/.test(pkg.version));
assert(process.env.NODE_AUTH_TOKEN,'缺少 Actions 包发布凭据');
const archive=path.join(process.env.RUNNER_TEMP,'qian-runtime',`idadawn-qian-runtime-${pkg.version}.tgz`);
const integrity='sha512-'+createHash('sha512').update(readFileSync(archive)).digest('base64');
// 包 REST 元数据不保证包含 repository；仓库声明由实际上传制品证明。
const artifact=JSON.parse(execFileSync('tar',['-xOf',archive,'package/package.json'],{encoding:'utf8'}));
assert.equal(artifact.name,pkg.name);assert.equal(artifact.version,pkg.version);
assert.equal(artifact.repository.url,'https://github.com/idadawn/qian.git');
assert.equal(artifact.repository.directory,'packages/qian-runtime');
const url='https://npm.pkg.github.com/'+encodeURIComponent(pkg.name);
async function metadata(){
 const response=await fetch(url,{headers:{Authorization:`Bearer ${process.env.NODE_AUTH_TOKEN}`},cache:'no-store'});
 if(response.status===404)return null;
 assert.equal(response.status,200,'无法核对私有源包版本，停止发布');
 return (await response.json()).versions?.[pkg.version];
}
const existing=await metadata();
if(existing)assert.equal(existing.dist.integrity,integrity,'同版本制品不同，禁止覆盖或假称成功');
else execFileSync('npm',['publish',archive,'--registry=https://npm.pkg.github.com','--access=restricted','--ignore-scripts'],{stdio:'inherit'});
const published=await metadata();assert(published,'发布后未查到版本');assert.equal(published.dist.integrity,integrity);
const response=await fetch('https://api.github.com/users/idadawn/packages/npm/qian-runtime',{headers:{Authorization:`Bearer ${process.env.NODE_AUTH_TOKEN}`,Accept:'application/vnd.github+json','X-GitHub-Api-Version':'2022-11-28'}});
assert.equal(response.status,200,'无法核对包可见性');
const record=await response.json();assert.equal(record.visibility,'private');
console.log(JSON.stringify({package:pkg.name,version:pkg.version,visibility:record.visibility,sourceRepository:artifact.repository.url,integrity,revision:process.env.GITHUB_SHA}));
