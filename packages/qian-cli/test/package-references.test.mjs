import {test} from 'node:test';
import assert from 'node:assert/strict';
import {parsePackageReference,collectPackageReferences,collectRuntimePackageReferences} from '../dist/package-references.js';

test('精确引用支持 CalVer、预发布与 npm scope，保留发布者声明',()=>{
 assert.deepEqual(parsePackageReference('nuget','Kun.Core@2026.10.12'),{kind:'nuget',name:'Kun.Core',version:'2026.10.12',specification:'Kun.Core@2026.10.12'});
 assert.equal(parsePackageReference('npm','@idadawn/qian-runtime@0.1.0-beta.1').name,'@idadawn/qian-runtime');
 assert.equal(parsePackageReference('npm','semver@7.7.4').version,'7.7.4');
});

test('包管理器启动前拒绝参数注入、范围、别名、Git 与本地路径',()=>{
 const invalid=[['npm','--global@1.0.0'],['npm','@scope/name@^1.0.0'],['npm','name@file:../outside'],
  ['npm','name@git+https://example.test/repo'],['npm','name@npm:other@1.0.0'],['npm','name@1.0.0\n--ignore-scripts=false'],
  ['nuget','Bad Name@1.0.0'],['nuget','Package@[1.0.0,2.0.0)'],['nuget','Package@1.*'],['nuget','Package@01.0.0'],
  ['nuget','@1.0.0'],['npm','name@https://user:DO-NOT-LEAK@example.test/pkg']];
 for(const[kind,reference]of invalid)assert.throws(()=>parsePackageReference(kind,reference),error=>{
  assert.equal(error.diagnostics[0].error.code,'INPUT_INVALID');
  assert.equal(error.diagnostics[0].error.errors[0].code,'SURFACE_SCHEMA_INVALID');
  assert.ok(!JSON.stringify(error.diagnostics).includes('DO-NOT-LEAK'));
  return true;
 });
});

test('共享包按真实名称语义去重并记录全部所有者，冲突不会静默改写版本',()=>{
 const components=[
  {id:'qian.first',packages:{nuget:['Kun.Core@2026.10.12'],npm:['@idadawn/qian-runtime@0.1.0']}},
  {id:'qian.second',packages:{nuget:['kun.core@2026.10.12'],npm:[]}}
 ];
 const collected=collectPackageReferences(components);
 assert.equal(collected.length,2);
 assert.deepEqual(collected.find(item=>item.kind==='nuget').owners,['qian.first','qian.second']);
 assert.throws(()=>collectPackageReferences([...components,{id:'qian.third',packages:{nuget:['Kun.Core@2026.10.11'],npm:[]}}]),error=>error.diagnostics[0].error.code==='INPUT_INVALID');
 assert.deepEqual(collectPackageReferences(components.slice(1))[0].owners,['qian.second']);
});

test('损坏的 lock 包集合也被结构化拒绝',()=>{
 assert.throws(()=>collectPackageReferences([{id:'qian.bad',packages:{nuget:null,npm:[]}}]),error=>error.diagnostics[0].error.code==='INPUT_INVALID');
});

test('清单摘要随精确引用保留，共享声明不能借大小写或省略字段绕过冲突',()=>{
 const first={id:'qian.first',runtime:{nuget:[{id:'Kun.Core',version:'2026.10.12',digest:'sha256:'+'a'.repeat(64)}]}};
 const second={id:'qian.second',runtime:{nuget:[{id:'kun.core',version:'2026.10.12'}]}};
 const references=collectRuntimePackageReferences([first,second]);
 assert.equal(references[0].digest,first.runtime.nuget[0].digest);assert.deepEqual(references[0].owners,['qian.first','qian.second']);
 const conflicting=structuredClone(second);conflicting.runtime.nuget[0].digest='sha256:'+'b'.repeat(64);
 assert.throws(()=>collectRuntimePackageReferences([first,conflicting]),error=>JSON.stringify(error.diagnostics).includes('摘要声明冲突'));
 assert.throws(()=>collectRuntimePackageReferences([{id:'test.invalid',runtime:{npm:[{name:'semver',version:'7.7.4',digest:'not-a-digest'}]}}]));
});
