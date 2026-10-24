import { ShellError } from './errors';

/** 客户端动作参数的校验函数：输入是否满足登记时声明的参数 Schema。 */
export type ParameterValidator = (value: unknown) => boolean;
type Check = (value: unknown) => boolean;

const TYPES = new Set(['object', 'array', 'string', 'number', 'integer', 'boolean', 'null']);
// 只作说明、不参与判定的关键字。
const ANNOTATIONS = new Set(['$schema', '$comment', 'title', 'description', 'default', 'examples', 'deprecated', 'readOnly', 'writeOnly']);
const KEYWORDS = new Set(['type', 'enum', 'const', 'properties', 'required', 'additionalProperties', 'minProperties', 'maxProperties', 'items', 'minItems', 'maxItems', 'uniqueItems',
  'minLength', 'maxLength', 'pattern', 'minimum', 'maximum', 'exclusiveMinimum', 'exclusiveMaximum', 'multipleOf']);

const invalid = (): never => { throw new ShellError('INPUT_INVALID', {}, '/parameters'); };
const isObject = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value);
const count = (value: unknown) => typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : invalid();
const finite = (value: unknown) => typeof value === 'number' && Number.isFinite(value) ? value : invalid();
const length = (text: string) => [...text].length;
function equal(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (!a || !b || typeof a !== 'object' || typeof b !== 'object' || Array.isArray(a) !== Array.isArray(b)) return false;
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((key) => Object.prototype.hasOwnProperty.call(b, key) && equal(Reflect.get(a, key), Reflect.get(b, key)));
}
function typeOf(value: unknown): string {
  if (value === null) return 'null';
  if (Array.isArray(value)) return 'array';
  return typeof value;
}
function matchesType(value: unknown, type: string) {
  if (type === 'integer') return typeof value === 'number' && Number.isInteger(value);
  if (type === 'number') return typeof value === 'number' && Number.isFinite(value);
  return typeOf(value) === type;
}

/**
 * 把客户端动作的参数 Schema 解释成校验函数（S1-YUE-012）：不生成代码、不调用 Function / eval，严格 CSP 下可用，也不把 JSON Schema 编译器带进外壳首屏。
 * 只接受 JSON Schema 2020-12 的一个可解释子集——类型、枚举 / 常量、对象（properties、required、additionalProperties、属性数）、数组（items、长度、唯一）、
 * 字符串（长度按码点、pattern 按 Unicode 正则）、数值（范围、倍数）与纯说明关键字；$ref、组合（allOf / anyOf / oneOf / not）、条件、format 等其它关键字一律拒绝登记，
 * 而不是静默忽略。最终的输入校验仍在服务端。
 */
export function compileParameters(schema: unknown): ParameterValidator {
  return compile(schema, 0);
}

function compile(schema: unknown, depth: number): Check {
  if (depth > 32) invalid();
  if (schema === true) return () => true;
  if (schema === false) return () => false;
  if (!isObject(schema)) return invalid();
  const checks: Check[] = [];
  for (const [keyword, argument] of Object.entries(schema)) {
    if (ANNOTATIONS.has(keyword)) continue;
    if (!KEYWORDS.has(keyword)) invalid();
    switch (keyword) {
      case 'type': {
        const types = Array.isArray(argument) ? argument : [argument];
        if (types.length === 0 || new Set(types).size !== types.length || types.some((type) => typeof type !== 'string' || !TYPES.has(type))) invalid();
        checks.push((value) => types.some((type) => matchesType(value, type as string)));
        break;
      }
      case 'enum': {
        if (!Array.isArray(argument) || argument.length === 0) invalid();
        const values = argument as unknown[];
        checks.push((value) => values.some((item) => equal(item, value)));
        break;
      }
      case 'const': checks.push((value) => equal(argument, value)); break;
      case 'properties': {
        if (!isObject(argument)) invalid();
        const properties = Object.entries(argument as Record<string, unknown>).map(([name, child]) => [name, compile(child, depth + 1)] as const);
        checks.push((value) => !isObject(value) || properties.every(([name, check]) => !Object.prototype.hasOwnProperty.call(value, name) || check(value[name])));
        break;
      }
      case 'required': {
        if (!Array.isArray(argument) || new Set(argument).size !== argument.length || argument.some((name) => typeof name !== 'string')) invalid();
        const names = argument as string[];
        checks.push((value) => !isObject(value) || names.every((name) => Object.prototype.hasOwnProperty.call(value, name)));
        break;
      }
      case 'additionalProperties': {
        const check = compile(argument, depth + 1);
        const declared = new Set(isObject(schema.properties) ? Object.keys(schema.properties) : []);
        checks.push((value) => !isObject(value) || Object.keys(value).every((name) => declared.has(name) || check(value[name])));
        break;
      }
      case 'minProperties': { const limit = count(argument); checks.push((value) => !isObject(value) || Object.keys(value).length >= limit); break; }
      case 'maxProperties': { const limit = count(argument); checks.push((value) => !isObject(value) || Object.keys(value).length <= limit); break; }
      case 'items': { const check = compile(argument, depth + 1); checks.push((value) => !Array.isArray(value) || value.every((item) => check(item))); break; }
      case 'minItems': { const limit = count(argument); checks.push((value) => !Array.isArray(value) || value.length >= limit); break; }
      case 'maxItems': { const limit = count(argument); checks.push((value) => !Array.isArray(value) || value.length <= limit); break; }
      case 'uniqueItems': {
        if (typeof argument !== 'boolean') invalid();
        if (argument) checks.push((value) => !Array.isArray(value) || value.every((item, index) => value.findIndex((other) => equal(other, item)) === index));
        break;
      }
      case 'minLength': { const limit = count(argument); checks.push((value) => typeof value !== 'string' || length(value) >= limit); break; }
      case 'maxLength': { const limit = count(argument); checks.push((value) => typeof value !== 'string' || length(value) <= limit); break; }
      case 'pattern': {
        if (typeof argument !== 'string') invalid();
        let pattern: RegExp;
        try { pattern = new RegExp(argument as string, 'u'); } catch { return invalid(); }
        checks.push((value) => typeof value !== 'string' || pattern.test(value));
        break;
      }
      case 'minimum': { const limit = finite(argument); checks.push((value) => typeof value !== 'number' || value >= limit); break; }
      case 'maximum': { const limit = finite(argument); checks.push((value) => typeof value !== 'number' || value <= limit); break; }
      case 'exclusiveMinimum': { const limit = finite(argument); checks.push((value) => typeof value !== 'number' || value > limit); break; }
      case 'exclusiveMaximum': { const limit = finite(argument); checks.push((value) => typeof value !== 'number' || value < limit); break; }
      case 'multipleOf': {
        const divisor = finite(argument);
        if (divisor <= 0) invalid();
        checks.push((value) => typeof value !== 'number' || Number.isInteger(value / divisor));
        break;
      }
    }
  }
  return (value) => checks.every((check) => check(value));
}
