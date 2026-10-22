import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {compilePattern,translatePattern,isStable,sameKey,maxSize,cachedPatterns,UnsupportedPattern,KeyPattern} from '../dist/schema-pattern.js';
// 向量由 tests/Qian.Common.Tests 按 .NET 的 SchemaPattern 与 OrdinalIgnoreCase 生成；这里逐条比对 TS 移植。
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../..');
const vectors=JSON.parse(readFileSync(path.join(root,'tck/profiles/key-vectors.json'),'utf8'));
const bits=pattern=>vectors.keys.map(key=>pattern.test(key)?'1':'0').join('');
test('翻译结果与在固定键名上的匹配与 .NET 逐条一致，拒绝的写法两端相同',()=>{
 assert.equal(maxSize,vectors.maxSize);
 for(const vector of vectors.patterns){
  if(vector.rejected){for(const fold of [false,true])assert.throws(()=>translatePattern(vector.pattern,fold),UnsupportedPattern,vector.pattern);continue;}
  assert.equal(translatePattern(vector.pattern),vector.plain,vector.pattern);
  assert.equal(translatePattern(vector.pattern,true),vector.folded,vector.pattern);
  assert.equal(bits(compilePattern(vector.pattern)),vector.matches,vector.pattern);
  assert.equal(bits(compilePattern(vector.pattern,true)),vector.foldedMatches,vector.pattern);
 }
});
test('值层按 ECMA-262 不带标志的语义逐码元匹配：与 .NET 的翻译结果和 V8 的 RegExp 逐例一致',()=>{
 const valueBits=matcher=>vectors.values.map(value=>matcher.test(value)?'1':'0').join('');
 for(const vector of vectors.patterns){
  if(vector.rejected)continue;
  assert.equal(valueBits(compilePattern(vector.pattern)),vector.valueMatches,vector.pattern);
  assert.equal(valueBits(new RegExp(vector.pattern)),vector.valueMatches,vector.pattern);
 }
 // 单字符写法在全部码元上与 V8 相同，含行终止符、非 ASCII 空白与残缺代理项。
 for(const token of ['.','\\s','\\S','\\w','\\W','\\d','\\D']){
  const ours=compilePattern('^'+token+'$'),engine=new RegExp('^'+token+'$');
  for(let code=0;code<=0xFFFF;code++){const text=String.fromCharCode(code);if(ours.test(text)!==engine.test(text))assert.fail(token+' 在 U+'+code.toString(16)+' 上与 V8 不同');}
 }
});
test('键名稳定性与 OrdinalIgnoreCase 相等与 .NET 逐条一致',()=>{
 const unstable=new Set(vectors.unstable.flatMap(([low,high])=>Array.from({length:high-low+1},(_,offset)=>low+offset)));
 for(let code=0;code<=0xFFFF;code++)if(isStable(String.fromCharCode(code))===unstable.has(code))assert.fail('U+'+code.toString(16)+' 的稳定性与 .NET 不同');
 for(const {left,right,equal} of vectors.equality)assert.equal(sameKey(left,right),equal,JSON.stringify([left,right]));
});
test('规模按展开后计数：长写法恰在上限时接受，多一个字符即拒绝',()=>{
 const concat='^a$'.repeat(333)+'a',alternation='^(?:'+Array.from({length:499},(_,index)=>String.fromCharCode(0x4E00+index)+'a').join('|')+')$';
 for(const pattern of [concat,alternation]){
  assert.doesNotThrow(()=>translatePattern(pattern,true));
  assert.throws(()=>translatePattern(pattern+'a'),UnsupportedPattern);
 }
});
test('编译实例按模式与是否折叠分别复用、执行的正是翻译结果，失败不进缓存，缓存有上限',()=>{
 const plain=compilePattern('^legacy_'),folded=compilePattern('^legacy_',true);
 assert.equal(compilePattern('^legacy_'),plain);assert.equal(compilePattern('^legacy_',true),folded);assert.notEqual(plain,folded);
 assert.ok(plain instanceof KeyPattern);assert.equal(plain.source,translatePattern('^legacy_'));assert.equal(folded.source,translatePattern('^legacy_',true));
 assert.ok(!plain.test('Legacy_x'));assert.ok(folded.test('LEGACY_X'));
 for(const pattern of ['(?=x)','a{2,1}','a{20000}'])for(const fold of [false,true,false])assert.throws(()=>compilePattern(pattern,fold),UnsupportedPattern);
 for(let index=0;index<=128;index++)compilePattern('^bounded_'+index+'$',index%2===0);
 assert.ok(cachedPatterns()>=1&&cachedPatterns()<=128);
});
test('匹配为线性时间：折叠后分支重叠的模式遇到长键名也不回溯',()=>{
 // ^(?:[a-z]|[A-Z])+$ 折叠后两个分支相同；回溯引擎在 24 个字母加 ! 上已需数十毫秒，每多两个字母慢约 4 倍。
 const pattern=compilePattern('^(?:[a-z]|[A-Z])+$',true),started=performance.now();
 assert.equal(pattern.test('a'.repeat(20000)+'!'),false);
 assert.equal(pattern.test('aB'.repeat(10000)),true);
 assert.ok(performance.now()-started<5000);
});
