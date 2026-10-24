/** 校验错误：与 Ajv ErrorObject（allErrors、未开 verbose）同形的五个字段。 */
export interface SchemaError { instancePath: string; schemaPath: string; keyword: string; params: Record<string, unknown>; message: string }
/** 校验函数：返回全部错误，空数组即通过。 */
export type SchemaValidator = (value: unknown) => SchemaError[];
type Check = (value: unknown, path: string, errors: SchemaError[]) => void;
type Group = 'number' | 'string' | 'array' | 'object';
interface Context { root: unknown; shared: Map<string, { check?: Check }> }

const META = 'https://json-schema.org/draft/2020-12/schema';
const TYPES = new Set(['object', 'array', 'string', 'number', 'integer', 'boolean', 'null']);
const isString = (value: unknown) => typeof value === 'string';
const isBoolean = (value: unknown) => typeof value === 'boolean';
const isObject = (value: unknown): value is Record<string, unknown> => !!value && typeof value === 'object' && !Array.isArray(value);
const anything = () => true;
// 只作说明、不参与判定的关键字，只查参数类型（contentSchema 只查是不是 Schema，不展开，与 Ajv 一样不拿它校验）；x- 扩展的白名单由 assertFormSchema 另行把关。
const ANNOTATIONS: Record<string, (argument: unknown) => boolean> = {
  title: isString, description: isString, $comment: isString, default: anything, examples: Array.isArray, deprecated: isBoolean, readOnly: isBoolean, writeOnly: isBoolean,
  contentMediaType: isString, contentEncoding: isString, contentSchema: (argument) => isBoolean(argument) || isObject(argument),
  'x-sensitivity': anything, 'x-ref': anything, 'x-widget-hint': anything,
};
// 链式引用里不挡路的关键字：Ajv 只让登记了规则的关键字挡住「只有 $ref」的目标继续解析，说明词、$defs 与 x- 扩展都不算，$comment 登记了规则、算。
// 与 Ajv 一样按键是否存在判断，不看取值是不是 undefined。
const transparent = (name: string) => name === '$ref' || name === '$defs' || name.startsWith('x-') || (name !== '$comment' && Object.hasOwn(ANNOTATIONS, name));
// 求值顺序与 Ajv 一致：先是不分类型的关键字，再按 number → string → array → object 分组；组内按关键字登记顺序，而不是 Schema 里的书写顺序。
const ANY = ['$ref', 'const', 'enum', 'oneOf'];
const GROUPS: Record<Group, readonly string[]> = {
  number: ['maximum', 'minimum', 'exclusiveMaximum', 'exclusiveMinimum', 'format'],
  string: ['maxLength', 'minLength', 'pattern', 'format'],
  array: ['maxItems', 'minItems', 'items'],
  object: ['required', 'additionalProperties', 'properties'],
};
const KEYWORDS = new Set(['type', ...ANY, ...Object.values(GROUPS).flat()]);
const LIMITS: Record<string, [comparison: string, fails: (value: number, limit: number) => boolean]> = {
  maximum: ['<=', (value, limit) => value > limit], minimum: ['>=', (value, limit) => value < limit],
  exclusiveMaximum: ['<', (value, limit) => value >= limit], exclusiveMinimum: ['>', (value, limit) => value <= limit],
};

// 格式与 ajv-formats 的 full 模式同义：日期校验月份天数与闰年，时间必须带时区，闰秒只许落在 UTC 23:59:60。
const DATE = /^(\d\d\d\d)-(\d\d)-(\d\d)$/;
const TIME = /^(\d\d):(\d\d):(\d\d(?:\.\d+)?)(z|([+-])(\d\d)(?::?(\d\d))?)?$/i;
const DAYS = [0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
const UUID = /^(?:urn:uuid:)?[0-9a-f]{8}-(?:[0-9a-f]{4}-){3}[0-9a-f]{12}$/i;
function date(text: string): boolean {
  const match = DATE.exec(text);
  if (!match) return false;
  const year = Number(match[1]), month = Number(match[2]), day = Number(match[3]);
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  return month >= 1 && month <= 12 && day >= 1 && day <= (month === 2 && leap ? 29 : DAYS[month]!);
}
function time(text: string): boolean {
  const match = TIME.exec(text);
  if (!match?.[4]) return false;
  const hour = Number(match[1]), minute = Number(match[2]), second = Number(match[3]);
  const sign = match[5] === '-' ? -1 : 1, offsetHour = Number(match[6] ?? 0), offsetMinute = Number(match[7] ?? 0);
  if (offsetHour > 23 || offsetMinute > 59) return false;
  if (hour <= 23 && minute <= 59 && second < 60) return true;
  const utcMinute = minute - offsetMinute * sign;
  const utcHour = hour - offsetHour * sign - (utcMinute < 0 ? 1 : 0);
  return (utcHour === 23 || utcHour === -1) && (utcMinute === 59 || utcMinute === -1) && second < 61;
}
const FORMATS: Record<string, (text: string) => boolean> = {
  date, time, 'date-time': (text) => { const parts = text.split(/t|\s/i); return parts.length === 2 && date(parts[0]!) && time(parts[1]!); }, uuid: (text) => UUID.test(text),
};

const unsupported = (at: string): never => { throw new Error('form.error.unsupportedKeyword', { cause: at }); };
const invalid = (at: string): never => { throw new Error('form.error.schema', { cause: at }); };
const pointer = (key: string) => key.replace(/~/g, '~0').replace(/\//g, '~1');
const count = (value: unknown, at: string) => typeof value === 'number' && Number.isInteger(value) && value >= 0 ? value : invalid(at);
const error = (instancePath: string, schemaPath: string, keyword: string, params: Record<string, unknown>, message: string): SchemaError => ({ instancePath, schemaPath, keyword, params, message });
function isType(value: unknown, type: string): boolean {
  switch (type) {
    case 'null': return value === null;
    case 'array': return Array.isArray(value);
    case 'object': return isObject(value);
    // 与 Ajv 的 strictNumbers: false 一致：NaN 与 ±Infinity 也是 number，±Infinity 还算 integer。
    case 'integer': return typeof value === 'number' && !(value % 1) && !Number.isNaN(value);
    default: return typeof value === type;
  }
}
function equal(a: unknown, b: unknown): boolean {
  if (a === b) return true;
  if (!a || !b || typeof a !== 'object' || typeof b !== 'object') return a !== a && b !== b;
  if (Array.isArray(a) !== Array.isArray(b)) return false;
  const keys = Object.keys(a);
  return keys.length === Object.keys(b).length && keys.every((key) => Object.hasOwn(b, key) && equal(Reflect.get(a, key), Reflect.get(b, key)));
}
function hasRef(value: unknown): boolean {
  return !!value && typeof value === 'object' && Object.entries(value).some(([key, item]) => key === '$ref' || hasRef(item));
}

/**
 * 把 JSON Schema 解释成校验函数（S1-YUE-014）：不生成代码、不调用 Function / eval，外壳 CSP 不放行 'unsafe-eval' 也能用；低代码表单的 Schema 来自运行期，无法预编译。
 * 只接受 JSON Schema 2020-12 的一个可解释子集，即 SchemaForm 的数据 Schema 与 form / list / widget 元数据 Schema 实际用到的关键字：
 * type、enum、const、$ref（本文档内的 JSON Pointer）、oneOf；properties、required、additionalProperties；items、minItems、maxItems；
 * minLength、maxLength（按码点）、pattern（Unicode 正则）、format（date、time、date-time、uuid）；minimum、maximum、exclusiveMinimum、exclusiveMaximum；
 * 外加 $schema / $id（只在根上）、$defs、只作说明的关键字（含 contentMediaType / contentEncoding / contentSchema）与本 spec 的三个 x- 扩展；取值为 undefined 的成员一律按缺失处理。
 * 子集内的判定、错误顺序与错误对象与此前的 Ajv 2020（allErrors、strict: false、ajv-formats full）逐条一致，由差分测试对照；
 * 子集外的关键字、格式与引用一律拒绝（form.error.unsupportedKeyword），参数不合法同样拒绝（form.error.schema），cause 是出问题的 Schema 位置，而不是静默放过。
 */
export function compileSchema(schema: unknown): SchemaValidator {
  const context: Context = { root: schema, shared: new Map() };
  // 根先登记，# 引用（自引用）在编译期间就能取到它。
  const root: { check?: Check } = {};
  context.shared.set('', root);
  const check = compile(schema, '#', '#', 0, context);
  root.check = check;
  return (value) => { const errors: SchemaError[] = []; check(value, '', errors); return errors; };
}

function compile(schema: unknown, at: string, location: string, depth: number, context: Context): Check {
  if (depth > 32) invalid(location);
  if (schema === true) return () => undefined;
  if (schema === false) return (_value, path, errors) => { errors.push(error(path, `${at}/false schema`, 'false schema', {}, 'boolean schema is false')); };
  if (!isObject(schema)) return invalid(location);
  for (const [name, argument] of Object.entries(schema)) {
    // 与 Ajv 一致，取值为 undefined 的成员当作没写（代码拼出的 Schema 常把可选字段留成 undefined）。
    if (argument === undefined) continue;
    const where = `${location}/${pointer(name)}`;
    if (Object.hasOwn(ANNOTATIONS, name)) { if (!ANNOTATIONS[name]!(argument)) invalid(where); } else if (name === '$schema' || name === '$id') {
      if (depth > 0) unsupported(where);
      if (typeof argument !== 'string') invalid(where);
      if (name === '$schema' && argument !== META) unsupported(where);
    } else if (name === '$defs') {
      if (!isObject(argument)) invalid(where);
      for (const [key, definition] of Object.entries(argument as Record<string, unknown>)) compile(definition, `${at}/$defs/${encodeURIComponent(pointer(key))}`, `${where}/${pointer(key)}`, depth + 1, context);
    } else if (!KEYWORDS.has(name)) unsupported(where);
  }

  const types = schema.type === undefined ? [] : Array.isArray(schema.type) ? schema.type as unknown[] : [schema.type];
  if (schema.type !== undefined && (types.length === 0 || new Set(types).size !== types.length || types.some((type) => typeof type !== 'string' || !TYPES.has(type)))) invalid(`${location}/type`);
  const present = (group: Group) => GROUPS[group].some((name) => schema[name] !== undefined);
  // 单一类型且带该类型的关键字时，Ajv 在该类型的组里报类型错误（排在不分类型的关键字之后），否则在一切关键字之前报。
  const single = types.length === 1 && Object.hasOwn(GROUPS, types[0] as string) && present(types[0] as Group) ? types[0] as Group : undefined;
  const upfront = types.length > 0 && !single;
  const typeError: Check = (_value, path, errors) => { errors.push(error(path, `${at}/type`, 'type', { type: schema.type }, `must be ${String(schema.type)}`)); };

  const build = (name: string, group?: Group): Check | undefined => {
    const argument = schema[name];
    const where = `${location}/${name}`;
    const keywordPath = `${at}/${name}`;
    const fail = (errors: SchemaError[], path: string, params: Record<string, unknown>, message: string) => { errors.push(error(path, keywordPath, name, params, message)); };
    switch (name) {
      case '$ref': return reference(argument, where, depth, context);
      case 'const': return (value, path, errors) => { if (!equal(value, argument)) fail(errors, path, { allowedValue: argument }, 'must be equal to constant'); };
      case 'enum': {
        if (!Array.isArray(argument) || argument.length === 0) invalid(where);
        const values = argument as unknown[];
        return (value, path, errors) => { if (!values.some((item) => item !== null && typeof item === 'object' ? equal(value, item) : value === item)) fail(errors, path, { allowedValues: argument }, 'must be equal to one of the allowed values'); };
      }
      case 'oneOf': {
        if (!Array.isArray(argument) || argument.length === 0) invalid(where);
        const branches = (argument as unknown[]).map((branch, index) => compile(branch, `${keywordPath}/${index}`, `${where}/${index}`, depth + 1, context));
        return (value, path, errors) => {
          const before = errors.length;
          let passing: number | [number, number] | null = null;
          // 与 Ajv 一致：找到第二个通过的分支就停，其后的分支不再求值。
          for (const [index, branch] of branches.entries()) {
            const start = errors.length;
            branch(value, path, errors);
            if (errors.length !== start) continue;
            if (typeof passing === 'number') { passing = [passing, index]; break; }
            passing = index;
          }
          if (typeof passing === 'number') errors.length = before;
          else fail(errors, path, { passingSchemas: passing }, 'must match exactly one schema in oneOf');
        };
      }
      case 'maximum': case 'minimum': case 'exclusiveMaximum': case 'exclusiveMinimum': {
        if (typeof argument !== 'number' || !Number.isFinite(argument)) invalid(where);
        const limit = argument as number;
        const [comparison, fails] = LIMITS[name]!;
        return (value, path, errors) => { if (fails(value as number, limit) || Number.isNaN(value)) fail(errors, path, { comparison, limit }, `must be ${comparison} ${limit}`); };
      }
      case 'format': {
        if (typeof argument !== 'string') invalid(where);
        const test = Object.hasOwn(FORMATS, argument as string) ? FORMATS[argument as string]! : unsupported(where);
        // 子集里只有字符串格式：number 组里的 format 只影响类型错误的位置，不做判定。
        if (group === 'number') return undefined;
        return (value, path, errors) => { if (!test(value as string)) fail(errors, path, { format: argument }, `must match format "${String(argument)}"`); };
      }
      case 'maxLength': case 'minLength': {
        const limit = count(argument, where);
        const more = name === 'maxLength';
        return (value, path, errors) => { const length = [...(value as string)].length; if (more ? length > limit : length < limit) fail(errors, path, { limit }, `must NOT have ${more ? 'more' : 'fewer'} than ${limit} characters`); };
      }
      case 'pattern': {
        if (typeof argument !== 'string') invalid(where);
        let pattern: RegExp;
        try { pattern = new RegExp(argument as string, 'u'); } catch { return invalid(where); }
        return (value, path, errors) => { if (!pattern.test(value as string)) fail(errors, path, { pattern: argument }, `must match pattern "${String(argument)}"`); };
      }
      case 'maxItems': case 'minItems': {
        const limit = count(argument, where);
        const more = name === 'maxItems';
        return (value, path, errors) => { const length = (value as unknown[]).length; if (more ? length > limit : length < limit) fail(errors, path, { limit }, `must NOT have ${more ? 'more' : 'fewer'} than ${limit} items`); };
      }
      case 'items': {
        const item = compile(argument, keywordPath, where, depth + 1, context);
        return (value, path, errors) => { const items = value as unknown[]; for (let index = 0; index < items.length; index++) item(items[index], `${path}/${index}`, errors); };
      }
      case 'required': {
        if (!Array.isArray(argument) || new Set(argument).size !== argument.length || argument.some((key) => typeof key !== 'string')) invalid(where);
        const names = argument as string[];
        // 属性存在与否按 Ajv 默认口径：取值不是 undefined（react-hook-form 里清空的数字与日期输入就是 undefined）。
        return (value, path, errors) => { for (const key of names) if ((value as Record<string, unknown>)[key] === undefined) fail(errors, path, { missingProperty: key }, `must have required property '${key}'`); };
      }
      case 'additionalProperties': {
        const declared = new Set(isObject(schema.properties) ? Object.keys(schema.properties).filter((key) => key !== '__proto__') : []);
        if (argument === true) return undefined;
        if (argument === false) return (value, path, errors) => { for (const key of Object.keys(value as object)) if (!declared.has(key)) fail(errors, path, { additionalProperty: key }, 'must NOT have additional properties'); };
        const extra = compile(argument, keywordPath, where, depth + 1, context);
        return (value, path, errors) => { for (const key of Object.keys(value as object)) if (!declared.has(key)) extra((value as Record<string, unknown>)[key], `${path}/${pointer(key)}`, errors); };
      }
      case 'properties': {
        if (!isObject(argument)) invalid(where);
        const properties = Object.entries(argument as Record<string, unknown>).filter(([key]) => key !== '__proto__')
          .map(([key, property]) => [key, compile(property, `${keywordPath}/${encodeURIComponent(pointer(key))}`, `${where}/${pointer(key)}`, depth + 1, context)] as const);
        return (value, path, errors) => {
          for (const [key, property] of properties) {
            const item = (value as Record<string, unknown>)[key];
            if (item !== undefined) property(item, `${path}/${pointer(key)}`, errors);
          }
        };
      }
    }
    return undefined;
  };

  const any = ANY.filter((name) => schema[name] !== undefined).map((name) => build(name)!);
  const groups = (Object.keys(GROUPS) as Group[]).filter(present)
    .map((group) => [group, GROUPS[group].filter((name) => schema[name] !== undefined).flatMap((name) => build(name, group) ?? [])] as const);
  return (value, path, errors) => {
    if (upfront && !types.some((type) => isType(value, type as string))) typeError(value, path, errors);
    for (const check of any) check(value, path, errors);
    for (const [group, checks] of groups) {
      if (isType(value, group)) for (const check of checks) check(value, path, errors);
      else if (group === single) typeError(value, path, errors);
    }
  };
}

/**
 * $ref 只认本文档内的 JSON Pointer 片段（# 或 #/…），远程、锚点与基于 $id 的引用不在子集内。目标除 $ref 外只有不挡路的关键字（transparent）时沿引用继续解析，成环即拒绝。
 * schemaPath 与 Ajv 一致：目标里不再含引用时就地展开，路径接在 $ref 原文之后；目标自身含引用（递归定义）时共享一份校验函数，路径从 # 重新开始。
 */
function reference(ref: unknown, at: string, depth: number, context: Context): Check {
  if (typeof ref !== 'string') return invalid(at);
  const seen = new Set<string>();
  let current = ref;
  let target: unknown;
  let key: string;
  for (;;) {
    let tokens: string[] = [];
    if (current !== '#' && current !== '#/') {
      if (!current.startsWith('#/')) return unsupported(at);
      try { tokens = current.slice(2).split('/').map((token) => decodeURIComponent(token).replace(/~1/g, '/').replace(/~0/g, '~')); } catch { return invalid(at); }
    }
    key = tokens.map((token) => '/' + pointer(token)).join('');
    target = context.root;
    for (const token of tokens) {
      target = target && typeof target === 'object' ? Reflect.get(target, token) : undefined;
      if (target === undefined) return invalid(at);
    }
    if (!isObject(target) || typeof target.$ref !== 'string' || !Object.keys(target).every(transparent)) break;
    if (seen.has(key)) return invalid(at);
    seen.add(key);
    current = target.$ref;
  }
  if (!hasRef(target)) return compile(target, ref, `#${key}`, depth + 1, context);
  let entry = context.shared.get(key);
  if (!entry) {
    entry = {};
    context.shared.set(key, entry);
    entry.check = compile(target, '#', `#${key}`, depth + 1, context);
  }
  const shared = entry;
  return (value, path, errors) => shared.check!(value, path, errors);
}
