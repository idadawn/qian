import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,writeFileSync,readFileSync,existsSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {spawn} from 'node:child_process';
import {once} from 'node:events';
import {setTimeout as delay} from 'node:timers/promises';
import {runPackageCommand,PackageCommandFailure,packageProcessGroupExited} from '../dist/package-process.js';
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
