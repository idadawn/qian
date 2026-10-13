import {test} from 'node:test';
import assert from 'node:assert/strict';
import {mkdtempSync,realpathSync,writeFileSync,readFileSync,rmSync} from 'node:fs';
import path from 'node:path';
import os from 'node:os';
import {inspectPackageWorkspace,applyPackageChanges,packageMutationTargets,workspaceDependency} from '../dist/package-manager.js';
import {WorkspaceTransaction} from '../dist/workspace-transaction.js';
import {planPackageChanges} from '../dist/package-plan.js';
import {parsePackageReference} from '../dist/package-references.js';

const component=id=>({id,packages:{nuget:['Kun.Core@2026.10.12'],npm:[]}});
test('真实共享 NuGet 引用仅在最后一个组件移除时删除',async()=>{
 const root=mkdtempSync(path.join(realpathSync(os.tmpdir()),'qian-ownership-'));
 try{
  writeFileSync(path.join(root,'Host.csproj'),'<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>');
  let before=[],state=[];
  async function apply(after){
   const workspace=await inspectPackageWorkspace(root,['nuget']),plan=planPackageChanges(workspace,before,after,state);
   const transaction=WorkspaceTransaction.begin(root,packageMutationTargets(workspace,plan.changes),'packages');
   await transaction.run(()=>applyPackageChanges(workspace,plan.changes));before=after;state=plan.state;return plan;
  }
  assert.equal((await apply([component('qian.first')])).state[0].managed,true);
  assert.equal((await apply([component('qian.first'),component('qian.second')])).changes.add.length,0);
  assert.equal((await apply([component('qian.second')])).changes.remove.length,0);
  assert.ok(readFileSync(path.join(root,'Host.csproj'),'utf8').includes('Kun.Core'));
  assert.equal((await apply([])).changes.remove.length,1);
  assert.equal(workspaceDependency(await inspectPackageWorkspace(root,['nuget']),parsePackageReference('nuget','Kun.Core@2026.10.12')),undefined);
 }finally{rmSync(root,{recursive:true,force:true});}
});

test('宿主原有依赖只借用不删除，也不以组件升级覆盖它',()=>{
 const workspace={root:'/unused',nuget:{dependencies:[{name:'Kun.Core',version:'2026.10.12'}]}};
 const initial=planPackageChanges(workspace,[],[component('qian.first')],[]);
 assert.equal(initial.state[0].managed,false);assert.equal(initial.changes.add.length,0);assert.equal(initial.changes.ensure.length,1);
 assert.equal(planPackageChanges(workspace,[component('qian.first')],[],initial.state).changes.remove.length,0);
 const upgraded={id:'qian.first',packages:{nuget:['Kun.Core@2026.10.13'],npm:[]}};
 assert.throws(()=>planPackageChanges(workspace,[component('qian.first')],[upgraded],initial.state));
});
