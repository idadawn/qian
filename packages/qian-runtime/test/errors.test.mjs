import {test} from 'node:test';
import assert from 'node:assert/strict';
import {readFileSync} from 'node:fs';
import {Ajv2020} from 'ajv/dist/2020.js';
import {RuntimeError} from '../dist/index.js';
const schema=JSON.parse(readFileSync(new URL('../../../contracts/v0/error-detail.schema.json',import.meta.url),'utf8'));
const validate=new Ajv2020({strict:false,validateFormats:false}).compile(schema);
test('运行时错误完整符合冻结 ErrorDetail，参数替换与语言一致',()=>{
 for(const language of ['zh-CN','en'])for(const[code,args]of [['DEPENDENCY_MISSING',{key:'X',entry:'consumer'}],['DEPENDENCY_CYCLE',{cycle:'A → B → A'}],['REGISTRATION_CONFLICT',{key:'X',owner:'provider'}],['NOT_FOUND',{}]]){
  const detail=new RuntimeError(code,args).toDetail('a'.repeat(32),language);assert.ok(validate(detail),JSON.stringify(validate.errors));assert.equal(detail.message,detail.detail);assert.ok(!detail.message.includes('{key}'));
 }
 assert.throws(()=>new RuntimeError('NOT_FOUND').toDetail('bad'));
});
