import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {setTimeout as delay} from 'node:timers/promises';
import {runPackageCommand,PackageCommandFailure,packageProcessGroupExited,packageCommandTimeout,DEFAULT_PACKAGE_TIMEOUT_SECONDS} from '../dist/package-process.js';
import {WorkspaceTransaction} from '../dist/workspace-transaction.js';

test('POSIX 超时停止整个所属进程组，父进程关闭后后代不能继续写文件',{skip:process.platform==='win32'},async()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-process-')),heartbeat=path.join(root,'heartbeat');let group;
 try{
  const descendant=`const fs=require('node:fs');setInterval(()=>fs.appendFileSync(${JSON.stringify(heartbeat)},'x'),10);`;
  const parent=`const{spawn}=require('node:child_process');spawn(process.execPath,['-e',${JSON.stringify(descendant)}],{stdio:'ignore'}).unref();setInterval(()=>{},1000);`;
  await assert.rejects(runPackageCommand(process.execPath,['-e',parent],root,'fixture',1000),error=>{
   assert.ok(error instanceof PackageCommandFailure);group=error.pid;assert.equal(error.terminationConfirmed,true);return true;
  });
  assert.equal(await packageProcessGroupExited(group),true);assert.ok(existsSync(heartbeat));
  const stopped=readFileSync(heartbeat,'utf8');await delay(150);assert.equal(readFileSync(heartbeat,'utf8'),stopped);
 }finally{if(group&&!await packageProcessGroupExited(group)){try{process.kill(-group,'SIGKILL');}catch{}}
  rmSync(root,{recursive:true,force:true});}
});

test('未确认进程停止时不提前回滚，退出后才恢复并释放锁',{skip:process.platform==='win32'},async()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-pending-process-')),file=path.join(root,'package.json');
 const child=spawn(process.execPath,['-e','setInterval(()=>{},1000)'],{cwd:root,detached:true,stdio:'ignore'});await once(child,'spawn');
 try{
  writeFileSync(file,'before');const transaction=WorkspaceTransaction.begin(root,[{path:file,kind:'file'}],'install');
  await assert.rejects(transaction.run(async()=>{writeFileSync(file,'working');throw new PackageCommandFailure('fixture',child.pid,false);}));
  assert.equal(readFileSync(file,'utf8'),'working');assert.ok(existsSync(path.join(root,'.qian/.install.lock')));
  await assert.rejects(transaction.rollback());assert.equal(readFileSync(file,'utf8'),'working');
  const exited=once(child,'exit');process.kill(-child.pid,'SIGKILL');await exited;
  await transaction.rollback();assert.equal(readFileSync(file,'utf8'),'before');assert.equal(existsSync(path.join(root,'.qian/.install.lock')),false);
 }finally{if(child.exitCode===null&&child.signalCode===null){const exited=once(child,'exit');child.kill('SIGKILL');await exited;}
  rmSync(root,{recursive:true,force:true});}
});

test('失败输出不进入结构化诊断，成功 UTF-8 输出保持完整',async()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-command-output-'));
 try{
  await assert.rejects(runPackageCommand(process.execPath,['-e','console.error("DO-NOT-LEAK");process.exit(2)'],root,'fixture'),error=>{
   assert.ok(!JSON.stringify(error.diagnostics).includes('DO-NOT-LEAK'));assert.equal(error.diagnostics[0].error.code,'UNAVAILABLE');return true;
  });
  assert.equal(await runPackageCommand(process.execPath,['-e','process.stdout.write("中文结果")'],root,'fixture'),'中文结果');
 }finally{rmSync(root,{recursive:true,force:true});}
});

test('QIAN_PACKAGE_TIMEOUT 以整数秒配置包管理器超时：缺省 600 秒，其它写法给出结构化错误',()=>{
 assert.equal(DEFAULT_PACKAGE_TIMEOUT_SECONDS,600);
 assert.equal(packageCommandTimeout({}),600_000);assert.equal(packageCommandTimeout({QIAN_PACKAGE_TIMEOUT:''}),600_000);
 assert.equal(packageCommandTimeout({QIAN_PACKAGE_TIMEOUT:'1'}),1000);assert.equal(packageCommandTimeout({QIAN_PACKAGE_TIMEOUT:'86400'}),86_400_000);
 for(const value of ['0','-5','1.5','30s',' 30','007','abc','86401','100000']){
  assert.throws(()=>packageCommandTimeout({QIAN_PACKAGE_TIMEOUT:value}),error=>{
   const[diagnostic,...rest]=error.diagnostics;assert.deepEqual(rest,[]);
   assert.equal(diagnostic.file,'QIAN_PACKAGE_TIMEOUT');assert.equal(diagnostic.error.code,'INPUT_INVALID');
   assert.deepEqual(diagnostic.error.errors.map(item=>[item.code,item.pointer]),[['SURFACE_SCHEMA_INVALID','QIAN_PACKAGE_TIMEOUT#']]);return true;
  },value);
 }
});

test('超时且停止未确认时先提示保留证据，再给出秒数和配置方式',()=>{
 const message=new PackageCommandFailure('dotnet',undefined,false,5000).diagnostics[0].error.errors[0].message;
 assert.match(message,/尚未确认停止.*必须保留.*未在 5 秒内完成.*QIAN_PACKAGE_TIMEOUT/);
 assert.doesNotMatch(new PackageCommandFailure('dotnet',undefined,false).diagnostics[0].error.errors[0].message,/QIAN_PACKAGE_TIMEOUT/);
});

test('未显式传入超时时按 QIAN_PACKAGE_TIMEOUT 停止真实子进程，只有超时的诊断给出秒数与配置方式',async()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-command-timeout-')),previous=process.env.QIAN_PACKAGE_TIMEOUT;
 try{
  process.env.QIAN_PACKAGE_TIMEOUT='1';const started=Date.now();
  await assert.rejects(runPackageCommand(process.execPath,['-e','setInterval(()=>{},1000)'],root,'fixture'),error=>{
   assert.ok(error instanceof PackageCommandFailure);assert.equal(error.terminationConfirmed,true);assert.equal(error.timeoutMilliseconds,1000);
   const[item]=error.diagnostics[0].error.errors;assert.equal(item.code,'SURFACE_PACKAGE_UNAVAILABLE');assert.match(item.message,/未在 1 秒内完成/);assert.match(item.message,/QIAN_PACKAGE_TIMEOUT/);return true;
  });
  assert.ok(Date.now()-started<10_000,'超时须按配置的秒数生效');
  // 非零退出不是超时：沿用原来的通用提示，不误导用户去加长超时。
  await assert.rejects(runPackageCommand(process.execPath,['-e','process.exit(3)'],root,'fixture'),error=>{
   assert.equal(error.timeoutMilliseconds,undefined);assert.doesNotMatch(error.diagnostics[0].error.errors[0].message,/QIAN_PACKAGE_TIMEOUT/);return true;
  });
  // 非法配置在启动子进程之前就以结构化错误拒绝。
  process.env.QIAN_PACKAGE_TIMEOUT='soon';
  await assert.rejects(runPackageCommand(process.execPath,['-e','process.exit(0)'],root,'fixture'),error=>!(error instanceof PackageCommandFailure)&&error.diagnostics[0].file==='QIAN_PACKAGE_TIMEOUT');
 }finally{if(previous===undefined)delete process.env.QIAN_PACKAGE_TIMEOUT;else process.env.QIAN_PACKAGE_TIMEOUT=previous;rmSync(root,{recursive:true,force:true});}
});
