import { readFileSync, readdirSync, existsSync, statSync, lstatSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash, randomBytes } from 'node:crypto';
import { Ajv2020, type ErrorObject, type ValidateFunction } from 'ajv/dist/2020.js';
import { parseTree, type Node as JsonTree, type ParseError } from 'jsonc-parser';
import { parseDocument as yamlDocument, isAlias, isMap, isSeq, isScalar, visit, LineCounter } from 'yaml';

/** JSON 数据；校验器不执行任何表面内容。 */
export type JsonValue = null | boolean | number | string | JsonValue[] | { [key: string]: JsonValue };
/** 冻结 ErrorDetail 协议，不把源文件行号插入其中。 */
export interface ErrorDetail {
  type: string; title: string; status: number; code: string; message: string; detail: string; trace_id: string;
  pointer: string; errors: { code: string; message: string; pointer: string }[];
}
export interface SurfaceDiagnostic { file: string; line: number; severity: 'error' | 'warning'; error: ErrorDetail }
export interface SurfaceDocument { kind: string; name: string; file: string; data: JsonValue; body: string }
export interface ParsedComponent { id: string; profile: string; manifest: JsonValue; documents: readonly SurfaceDocument[] }
export interface SurfaceValidationResult { component: ParsedComponent | null; diagnostics: readonly SurfaceDiagnostic[]; isValid: boolean }
export interface SurfaceSyntax { data: JsonValue; body: string; lines: Readonly<Record<string, number>> }
export interface SurfaceParseResult { syntax: SurfaceSyntax | null; diagnostics: readonly SurfaceDiagnostic[] }
export interface SurfaceValidationOptions {
  profile?: string; executionPlane?: 'human-cli' | 'server'; allowCommandHooks?: boolean;
  tools?: readonly string[]; skills?: readonly string[]; events?: readonly string[]; capabilities?: readonly string[];
  catalogComplete?: boolean; traceId?: string;
}
const codes = { syntax: 'SURFACE_SYNTAX_INVALID', schema: 'SURFACE_SCHEMA_INVALID', name: 'SURFACE_NAME_MISMATCH', reference: 'SURFACE_REFERENCE_UNKNOWN', path: 'SURFACE_PATH_INVALID', policy: 'SURFACE_POLICY_DENIED', secret: 'SURFACE_SECRET_LITERAL', hash: 'SURFACE_HASH_MISMATCH' };
type Obj = { [key: string]: JsonValue };
const obj = (value: JsonValue | undefined): Obj => value !== null && typeof value === 'object' && !Array.isArray(value) ? value : {};
const str = (value: JsonValue | undefined): string => typeof value === 'string' ? value : '';
const strings = (value: JsonValue | undefined): string[] => Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : [];
const token = (value: string): string => value.replaceAll('~', '~0').replaceAll('/', '~1');
const trace = (value?: string): string => value ?? randomBytes(16).toString('hex');
const lineAt = (source: string, offset: number): number => source.slice(0, offset).split('\n').length;
function closest(lines: Readonly<Record<string, number>>, pointer: string): number {
  while (!(pointer in lines) && pointer) pointer = pointer.slice(0, pointer.lastIndexOf('/'));
  return lines[pointer] ?? 1;
}
function diagnostic(file: string, pointer: string, line: number, code: string, reason: string, traceId: string, severity: 'error' | 'warning' = 'error'): SurfaceDiagnostic {
  const full = file.replaceAll('\\', '/').split('/').map(encodeURIComponent).join('/') + '#' + pointer;
  const message = '输入不符合该能力的输入 Schema，逐项错误见 errors。';
  return { file, line: Math.max(1, line), severity, error: { type: 'https://qian.dev/problems/protocol/INPUT_INVALID', title: '输入不符合契约', status: 400, code: 'INPUT_INVALID', message, detail: message, trace_id: traceId, pointer: full, errors: [{ code, message: reason, pointer: full }] } };
}
/** 是否含未成对的 UTF-16 代理项。 */
function unpaired(text: string): boolean {
  for (let index = 0; index < text.length; index++) {
    const unit = text.charCodeAt(index);
    if (unit >= 0xd800 && unit <= 0xdbff && (text.charCodeAt(index + 1) & 0xfc00) === 0xdc00) index++;
    else if (unit >= 0xd800 && unit <= 0xdfff) return true;
  }
  return false;
}
/** 解析 JSON、YAML 或 Markdown front matter，并保留行号。与 SurfaceReader.Parse 同序同义，致命错误只报它自己。 */
export function parseSurface(file: string, content: string, markdown = false, traceId = trace()): SurfaceParseResult {
  const lines: Record<string, number> = Object.create(null);
  const diagnostics: SurfaceDiagnostic[] = [];
  let source = content.replace(/^\uFEFF/, '').replaceAll('\r\n', '\n'), body = '', offset = 0;
  const fail = (line: number, reason: string): SurfaceParseResult => ({ syntax: null, diagnostics: [diagnostic(file, '', line, codes.syntax, reason, traceId)] });
  try {
    // 字面量的未成对代理项只可能来自直接传入的字符串（文件按 UTF-8 严格解码），按值无效处理，同 .NET 的严格编码。
    if (unpaired(source)) throw new Error('surrogate');
    if (Buffer.byteLength(source) > 1_048_576) throw new Error('size');
    if (markdown) {
      if (!source.startsWith('---\n')) throw new Error('front matter');
      const end = source.indexOf('\n---', 4);
      if (end < 0 || (source.length > end + 4 && source[end + 4] !== '\n')) throw new Error('front matter');
      body = source.slice(end + 5); source = source.slice(4, end); offset = 1;
    }
    let data: JsonValue;
    if (!markdown && file.endsWith('.json')) {
      const errors: ParseError[] = [];
      const tree = parseTree(source, errors, { allowTrailingComma: false, disallowComments: true });
      if (!tree || errors.length) return fail(lineAt(source, errors[0]?.offset ?? 0), 'JSON 语法无效。');
      // 键名与字符串值含未成对的代理项转义时记下首个所在行，走完整篇再报：过深与非有限数值优先，同 SurfaceReader.Text。
      let surrogate = 0;
      const walk = (node: JsonTree, pointer: string, depth: number): void => {
        if (depth > 64) throw new Error('depth');
        lines[pointer] ??= lineAt(source, node.offset);
        if (node.type === 'object') {
          const seen = new Set<string>();
          for (const property of node.children ?? []) {
            const [key, value] = property.children!;
            const name = String(key.value), child = pointer + '/' + token(name);
            lines[child] = lineAt(source, key.offset);
            if (!surrogate && unpaired(name)) surrogate = lines[child];
            if (seen.has(name)) diagnostics.push(diagnostic(file, child, lines[child], codes.syntax, 'JSON 对象存在重复键。', traceId));
            seen.add(name); walk(value, child, depth + 1);
          }
        } else if (node.type === 'array') (node.children ?? []).forEach((value, index) => walk(value, pointer + '/' + index, depth + 1));
        else if (node.type === 'string' && !surrogate && unpaired(node.value)) surrogate = lineAt(source, node.offset);
        else if (node.type === 'number' && !Number.isFinite(node.value)) throw new Error('number');
      };
      walk(tree, '', 0);
      if (surrogate) return fail(surrogate, 'JSON 字符串或键名含未成对的代理项。');
      data = JSON.parse(source) as JsonValue;
    } else {
      const counter = new LineCounter();
      const document = yamlDocument(source, { version: '1.2', uniqueKeys: true, lineCounter: counter });
      // 同 SurfaceReader：YamlDotNet 边扫描边检查，语法错误、alias 与含未成对代理项的标量（键与值，YamlDotNet 拒绝这样的转义并指向标量起点）
      // 按出现顺序先到先报；重复键在整篇读完后才检查。
      const stops: { offset: number; alias: boolean }[] = [];
      for (const error of document.errors) if (error.code !== 'DUPLICATE_KEY') stops.push({ offset: error.pos[0], alias: false });
      visit(document, {
        Alias: (_, node) => { stops.push({ offset: node.range?.[0] ?? 0, alias: true }); },
        Scalar: (_, node) => { if (typeof node.value === 'string' && unpaired(node.value)) stops.push({ offset: node.range?.[0] ?? 0, alias: false }); },
      });
      const stop = stops.sort((a, b) => a.offset - b.offset)[0] ?? (document.errors.length ? { offset: document.errors[0].pos[0], alias: false } : undefined);
      if (stop?.alias) throw new Error('alias');
      if (stop) return fail(counter.linePos(stop.offset).line + offset, 'YAML 语法无效或存在重复键。');
      const walk = (node: unknown, pointer: string, depth: number): JsonValue => {
        if (depth > 64 || isAlias(node)) throw new Error('alias/depth');
        const tag = (node as {tag?: string} | null)?.tag;
        if (tag && tag !== '!' && !tag.startsWith('tag:yaml.org,2002:')) throw new Error('tag');
        const range = (node as { range?: number[] } | null)?.range;
        lines[pointer] ??= (range ? counter.linePos(range[0]).line : 1) + offset;
        if (isMap(node)) {
          const pairs: [string, JsonValue][] = [];
          for (const pair of node.items) {
            if (!isScalar(pair.key)) throw new Error('key');
            const name = String(pair.key.source ?? pair.key.value ?? '');
            const child = pointer + '/' + token(name);
            lines[child] = counter.linePos(pair.key.range?.[0] ?? 0).line + offset;
            pairs.push([name, walk(pair.value, child, depth + 1)]);
          }
          return Object.fromEntries(pairs);
        }
        if (isSeq(node)) return node.items.map((value, index) => walk(value, pointer + '/' + index, depth + 1));
        if (node === null) return null;
        if (!isScalar(node) || typeof node.value === 'number' && !Number.isFinite(node.value)) throw new Error('scalar');
        return node.value as JsonValue;
      };
      data = walk(document.contents, '', 0);
    }
    return { syntax: { data, body, lines }, diagnostics };
  } catch { return fail(1 + offset, '文件语法、大小、嵌套深度或值无效。'); }
}
const resources = new URL('./schemas/', import.meta.url);
const rules = JSON.parse(readFileSync(new URL('surface-rules.json', resources), 'utf8')) as { defaults: Record<string, string>; agentEvents: string[]; qianEvents: string[]; shells: string[] };
const ajv = new Ajv2020({ allErrors: true, strict: false, validateFormats: false });
const schemas = new Map<string, ValidateFunction>();
for (const name of ['surface-diagnostics', 'capability-entry', 'permissions', 'plugin', 'skill', 'agent', 'command', 'hooks', 'mcp', 'tool', 'patch', 'profile', 'marketplace']) {
  const schema = JSON.parse(readFileSync(new URL(name + '.schema.json', resources), 'utf8'));
  ajv.addSchema(schema); schemas.set(name, ajv.getSchema(schema.$id)!);
}
/** 验证内置 Schema；profile 装配器可复用 patch 等契约。 */
export function validateSurfaceSchema(schema: string, file: string, data: JsonValue, traceId = trace(), lines: Readonly<Record<string, number>> = { '': 1 }): SurfaceDiagnostic[] {
  const validate = schemas.get(schema);
  if (!validate) throw new Error('未知 Schema：' + schema);
  if (validate(data)) return [];
  const errors = new Map<string, SurfaceDiagnostic>();
  for (const error of validate.errors ?? []) {
    if (['allOf', 'anyOf', 'oneOf', 'if', 'then', 'else', '$ref'].includes(error.keyword)) continue;
    let pointer = error.instancePath;
    if (error.keyword === 'required') pointer += '/' + token(error.params.missingProperty as string);
    if (error.keyword === 'additionalProperties') pointer += '/' + token(error.params.additionalProperty as string);
    errors.set(pointer, diagnostic(file, pointer, closest(lines, pointer), codes.schema, '字段不符合 Schema 约束（' + error.keyword + '）。', traceId));
  }
  if (!errors.size) errors.set('', diagnostic(file, '', 1, codes.schema, '文档不符合 Schema。', traceId));
  return [...errors.values()].sort((a, b) => ordinal(a.error.pointer, b.error.pointer));
}
const isSchema = (data: JsonValue): boolean => {
  const declared = obj(data).$schema;
  return (!declared || declared === 'https://json-schema.org/draft/2020-12/schema') && !!ajv.getSchema('https://json-schema.org/draft/2020-12/schema')?.(data);
};
const ordinal = (a: string, b: string): number => a < b ? -1 : a > b ? 1 : 0;
/** RFC 8785 工具描述哈希，与冻结 capability-entry 契约一致。 */
export function computeToolHash(tool: JsonValue): string {
  const data = obj(tool);
  function canonical(value: JsonValue): string {
    if (value === null || typeof value === 'boolean') return JSON.stringify(value);
    if (typeof value === 'number') { if (!Number.isFinite(value)) throw new Error('JCS number'); return JSON.stringify(value); }
    if (typeof value === 'string') {
      for (let i = 0; i < value.length; i++) {
        const code = value.charCodeAt(i);
        if (code >= 0xD800 && code <= 0xDBFF) { const next = value.charCodeAt(++i); if (!(next >= 0xDC00 && next <= 0xDFFF)) throw new Error('JCS unicode'); }
        else if (code >= 0xDC00 && code <= 0xDFFF) throw new Error('JCS unicode');
      }
      return JSON.stringify(value);
    }
    if (Array.isArray(value)) return '[' + value.map(canonical).join(',') + ']';
    return '{' + Object.keys(value).sort().map(key => canonical(key) + ':' + canonical(value[key])).join(',') + '}';
  }
  return createHash('sha256').update(canonical({ description: data.description, inputSchema: data.inputSchema, name: data.name })).digest('hex');
}

/** 目录解析入口；提供独立 API 与 .NET 相同的语义。 */
export class SurfaceValidator {
  parse(file: string, content: string, markdown = false, traceId?: string): SurfaceParseResult { return parseSurface(file, content, markdown, trace(traceId)); }
  validateSchema(schema: string, file: string, data: JsonValue, traceId?: string): SurfaceDiagnostic[] { return validateSurfaceSchema(schema, file, data, trace(traceId)); }
  validateComponent(directory: string, options: SurfaceValidationOptions = {}): SurfaceValidationResult { return new ValidationContext(path.resolve(directory), options).run(); }
}
/** 便捷组件校验入口。 */
export function validateComponent(directory: string, options: SurfaceValidationOptions = {}): SurfaceValidationResult { return new SurfaceValidator().validateComponent(directory, options); }

class ValidationContext {
  private readonly diagnostics: SurfaceDiagnostic[] = [];
  private readonly documents: SurfaceDocument[] = [];
  private readonly sources = new Map<string, SurfaceSyntax>();
  private readonly traceId: string;
  private manifest: Obj = {};
  private id = '';
  private tier = '';
  private readonly profile: string;
  constructor(private readonly root: string, private readonly options: SurfaceValidationOptions) { this.traceId = trace(options.traceId); this.profile = options.profile ?? 'minimal'; }
  private error(file: string, pointer: string, code: string, reason: string, severity: 'error' | 'warning' = 'error'): void {
    this.diagnostics.push(diagnostic(file, pointer, closest(this.sources.get(file)?.lines ?? {}, pointer), code, reason, this.traceId, severity));
  }
  private relative(file: string): string { return path.relative(this.root, file).replaceAll('\\', '/'); }
  private safe(relative: string, file: string, pointer: string, required: boolean): string | null {
    try {
      relative = relative.replaceAll('\\', '/');
      if (path.isAbsolute(relative) || relative.includes(':') || relative.includes('\0')) throw new Error('path');
      const full = path.resolve(this.root, relative);
      if (full !== this.root && !full.startsWith(this.root + path.sep)) throw new Error('outside');
      let current = this.root;
      for (const segment of path.relative(this.root, full).split(path.sep).filter(Boolean)) {
        if (!existsSync(current) || !statSync(current).isDirectory() || !readdirSync(current).includes(segment)) { if (required) throw new Error('missing'); return null; }
        current = path.join(current, segment);
        if (lstatSync(current).isSymbolicLink()) throw new Error('link');
      }
      return full;
    } catch { this.error(file, pointer, codes.path, '路径必须是组件内存在的相对路径，不能经过符号链接。'); return null; }
  }
  private read(file: string, schema: string | null, markdown: boolean, diagnosticFile = '.qian-plugin/plugin.json', pointer = ''): SurfaceSyntax | null {
    const full = this.safe(file, diagnosticFile, pointer, true);
    if (!full) return null;
    try {
      if (!statSync(full).isFile() || statSync(full).size > 1_048_576) throw new Error('file');
      const content = new TextDecoder('utf-8', { fatal: true }).decode(readFileSync(full));
      const parsed = parseSurface(file, content, markdown, this.traceId);
      this.diagnostics.push(...parsed.diagnostics);
      if (!parsed.syntax || parsed.diagnostics.length) return null;
      this.sources.set(file, parsed.syntax);
      const errors = schema === null ? [] : validateSurfaceSchema(schema, file, parsed.syntax.data, this.traceId, parsed.syntax.lines);
      this.diagnostics.push(...errors);
      return errors.length ? null : parsed.syntax;
    } catch { this.error(file, '', codes.path, '文件无法读取或超过大小限制。'); return null; }
  }
  run(): SurfaceValidationResult {
    const manifest = this.read('.qian-plugin/plugin.json', 'plugin', false);
    if (!manifest || this.diagnostics.some(d => d.severity === 'error')) return this.result(null);
    this.manifest = obj(manifest.data); this.id = str(this.manifest.id); this.tier = str(obj(this.manifest.policy).tier);
    this.manifestRules();
    for (const [surface, fallback] of Object.entries(rules.defaults)) {
      const pointers = obj(this.manifest.surfaces), declared = Object.hasOwn(pointers, surface);
      const full = this.safe(declared ? str(pointers[surface]) : fallback, '.qian-plugin/plugin.json', '/surfaces/' + surface, declared);
      if (!full) continue;
      if (['skills', 'agents', 'commands', 'tools'].includes(surface)) {
        if (!statSync(full).isDirectory()) { this.error('.qian-plugin/plugin.json', '/surfaces/' + surface, codes.path, '表面指针必须指向目录。'); continue; }
        const names = readdirSync(full).sort();
        if (names.length > 1024) { this.error('.qian-plugin/plugin.json', '/surfaces/' + surface, codes.path, '表面目录条目过多。'); continue; }
        for (const child of names) {
          const item = path.join(full, child), candidate = surface === 'skills' ? path.join(item, 'SKILL.md') : item;
          if (surface === 'skills') { if (!statSync(item).isDirectory() || !existsSync(candidate)) continue; }
          else if (!item.endsWith(surface === 'tools' ? '.json' : '.md')) continue;
          const kind = ({ skills: 'skill', agents: 'agent', commands: 'command', tools: 'tool' } as Record<string, string>)[surface];
          const file = this.relative(candidate), source = this.read(file, kind, kind !== 'tool', '.qian-plugin/plugin.json', '/surfaces/' + surface);
          if (!source) continue;
          const name = kind === 'tool' ? str(obj(source.data).name) : kind === 'skill' ? path.basename(path.dirname(candidate)) : path.basename(candidate, path.extname(candidate));
          this.documents.push({ kind, name, file, data: source.data, body: source.body });
        }
      } else {
        const kind = surface === 'mcpServers' ? 'mcp' : surface, file = this.relative(full);
        const source = this.read(file, kind, false, '.qian-plugin/plugin.json', '/surfaces/' + surface);
        if (source) this.documents.push({ kind, name: kind, file, data: source.data, body: source.body });
      }
    }
    this.readMetadata();
    this.semanticRules();
    return this.result({ id: this.id, profile: this.profile, manifest: this.manifest, documents: this.documents });
  }
  private readMetadata():void {
    const pointers=obj(this.manifest.surfaces),declared=Object.hasOwn(pointers,'metadata');
    const full=this.safe(declared?str(pointers.metadata):'./metadata','.qian-plugin/plugin.json','/surfaces/metadata',declared);
    if(!full)return;
    if(!statSync(full).isDirectory()){this.error('.qian-plugin/plugin.json','/surfaces/metadata',codes.path,'metadata 表面必须指向目录。');return;}
    const files=readdirSync(full).sort();
    if(files.length>1024){this.error('.qian-plugin/plugin.json','/surfaces/metadata',codes.path,'表面目录条目过多。');return;}
    for(const item of files.filter(file=>file.endsWith('.json'))){
      const file=this.relative(path.join(full,item)),source=this.read(file,null,false,'.qian-plugin/plugin.json','/surfaces/metadata');
      if(source)this.documents.push({kind:'metadata',name:path.basename(item,'.json'),file,data:source.data,body:source.body});
    }
  }
  private result(component: ParsedComponent | null): SurfaceValidationResult {
    const unique = new Map<string, SurfaceDiagnostic>();
    for (const d of this.diagnostics) unique.set([d.file, d.error.pointer, d.error.errors[0].code, d.severity].join('|'), d);
    const diagnostics = [...unique.values()].sort((a, b) => (a.severity === b.severity ? 0 : a.severity === 'error' ? -1 : 1) || ordinal(a.file, b.file) || ordinal(a.error.pointer, b.error.pointer)).slice(0, 100);
    return { component, diagnostics, isValid: component !== null && diagnostics.every(d => d.severity !== 'error') };
  }
  private manifestRules(): void {
    const file = '.qian-plugin/plugin.json', policy = obj(this.manifest.policy), runtime = obj(this.manifest.runtime), form = str(runtime.form);
    if (this.id.startsWith('qian.') && !['system', 'official'].includes(this.tier)) this.error(file, '/id', codes.policy, 'qian. 前缀保留给平台与官方组件。');
    if (strings(this.manifest.inject).some(key => strings(this.manifest.provides).includes(key))) this.error(file, '/provides', codes.policy, 'inject 与 provides 不能相交。');
    if (policy.required === true && this.tier !== 'system') this.error(file, '/policy/required', codes.policy, 'required 只允许平台插件声明。');
    for (const [key, pointer] of Object.entries(obj(this.manifest.surfaces))) this.safe(str(pointer), file, '/surfaces/' + key, true);
    if (existsSync(path.join(this.root, 'src')) && !form) this.error(file, '/runtime/form', codes.schema, '存在 src 时必须声明运行时形态。');
    if (this.tier === 'community' && Object.keys(runtime).length) this.error(file, '/runtime', codes.policy, 'S1 的 community 组件必须无代码。');
    if (form === 'module' && !['system', 'official'].includes(this.tier)) this.error(file, '/runtime/form', codes.policy, '共享进程代码只允许 system/official。');
    if (form === 'service' && (!['system', 'official'].includes(this.tier) || policy.sandbox !== 'workload')) this.error(file, '/policy/sandbox', codes.policy, 'S1 service 必须为受信任工作负载。');
    if (['function', 'package'].includes(form)) this.error(file, '/runtime/form', codes.policy, '该运行时形态保留给 S2。', 'warning');
    if (Array.isArray(runtime.nuget) && runtime.nuget.length && !str(obj(this.manifest.engines).kun)) this.error(file, '/engines/kun', codes.schema, 'NuGet 组件必须声明 Kun 版本范围。');
    if (typeof runtime.configSchema === 'string') {
      const full = this.safe(runtime.configSchema, file, '/runtime/configSchema', true);
      if (full) {
        const parsed = parseSurface(this.relative(full), readFileSync(full, 'utf8'), false, this.traceId);
        this.diagnostics.push(...parsed.diagnostics);
        if (parsed.syntax && !isSchema(parsed.syntax.data)) this.error(this.relative(full), '', codes.schema, '配置 Schema 无效。');
      }
    }
  }
  private semanticRules(): void {
    const tools = new Set(this.options.tools ?? []), skills = new Set(this.options.skills ?? []), identities = new Set<string>();
    for (const doc of this.documents) { if (doc.kind === 'tool') tools.add(this.id + '/' + doc.name); if (doc.kind === 'skill') skills.add(this.id + '/' + doc.name); }
    for (const doc of this.documents) {
      const identity = doc.kind + '/' + doc.name;
      if (identities.has(identity)) this.error(doc.file, '/name', codes.name, '表面名称重复。'); identities.add(identity);
      if (['skill', 'agent', 'command'].includes(doc.kind)) this.markdown(doc, tools, skills);
      if (doc.kind === 'tool') this.tool(doc);
      if (doc.kind === 'hooks') this.hooks(doc);
      if (doc.kind === 'mcp') this.mcp(doc);
      if (doc.kind === 'patch') {
        const entries = new Set<string>();
        (doc.data as JsonValue[]).forEach((entry, index) => {
          const data = obj(entry), id = (str(data.scope) || 'global') + '/' + str(data.id);
          if (entries.has(id)) this.error(doc.file, '/' + index + '/id', codes.name, '同一 patch 中条目身份重复。'); entries.add(id);
        });
      }
      this.secrets(doc.data, doc.file, '', '');
    }
  }
  private reference(file: string, pointer: string, reference: string, available: Set<string>): void {
    if (reference.endsWith('/*') ? [...available].some(value => value.startsWith(reference.slice(0, -1))) : available.has(reference)) return;
    const external = reference.includes('/') && !reference.startsWith(this.id + '/');
    this.error(file, pointer, codes.reference, '工具或技能引用不存在于当前目录。', external && !this.options.catalogComplete ? 'warning' : 'error');
  }
  private markdown(doc: SurfaceDocument, tools: Set<string>, skills: Set<string>): void {
    const data = obj(doc.data), field = doc.kind === 'agent' ? 'tools' : 'allowed-tools', allowed = strings(data[field]);
    if (doc.kind !== 'command' && str(data.name) !== doc.name) this.error(doc.file, '/name', codes.name, 'name 必须与目录或文件名一致。');
    if (!/^[a-z][a-z0-9-]*$/.test(doc.name)) this.error(doc.file, '/name', codes.name, '表面名称必须为小写短横线形式。');
    if (!doc.body.trim()) this.error(doc.file, '', codes.schema, 'Markdown 正文不能为空。');
    allowed.forEach((value, index) => { if (!(doc.kind === 'command' && value.startsWith('shell('))) this.reference(doc.file, '/' + field + '/' + index, value, tools); });
    strings(data.skills).forEach((value, index) => this.reference(doc.file, '/skills/' + index, value, skills));
    for (const match of doc.body.matchAll(/!`([^`]+)`/g)) {
      const command = match[1].trim();
      const permitted = allowed.some(value => value.startsWith('shell(') && value.endsWith(')') && (command === value.slice(6, -1) || command.startsWith(value.slice(6, -1) + ' ')));
      if (doc.kind !== 'command' || (this.options.executionPlane ?? 'human-cli') !== 'human-cli' || !['system', 'official'].includes(this.tier) || ['gateway', 'identity'].includes(this.profile) || this.profile === 'platform' && !this.options.allowCommandHooks || !permitted || /[|&;<>\r\n]/.test(command) || command.includes('$(')) this.error(doc.file, '/allowed-tools', codes.policy, '内联 shell 仅限受信任组件的人类 CLI，并需覆盖固定 argv 前缀。');
    }
    for (const match of doc.body.matchAll(/\[[^\]]*\]\(([^)]+)\)/g)) {
      const target = match[1].split('#')[0];
      if (!target || /^[a-z][a-z0-9+.-]*:/i.test(target)) continue;
      this.safe(path.join(path.dirname(doc.file), target), doc.file, '', true);
    }
    if (doc.kind === 'command') for (const match of doc.body.matchAll(/(?:^|\s)@([A-Za-z0-9_./-]+)/g)) this.safe(match[1], doc.file, '', true);
  }
  private tool(doc: SurfaceDocument): void {
    const data = obj(doc.data);
    if (path.basename(doc.file, '.json') !== doc.name) this.error(doc.file, '/name', codes.name, '工具名称必须与文件名一致。');
    const visit = (value: JsonValue, pointer: string): void => {
      if (Array.isArray(value)) value.forEach((child, index) => visit(child, pointer + '/' + index));
      else for (const [key, child] of Object.entries(obj(value))) {
        const location = pointer + '/' + token(key);
        if (key === 'properties') for (const name of Object.keys(obj(child))) if (/^tenant([_-]?id)?$/i.test(name)) this.error(doc.file, location + '/' + token(name), codes.policy, '输入 Schema 不允许租户字段。');
        visit(child, location);
      }
    };
    visit(data.inputSchema, '/inputSchema');
    if (!isSchema(data.inputSchema)) this.error(doc.file, '/inputSchema', codes.schema, '输入 JSON Schema 无效。');
    try { if (computeToolHash(data) !== data.schemaSha256) this.error(doc.file, '/schemaSha256', codes.hash, '工具描述哈希与 JCS 内容不一致。'); }
    catch { this.error(doc.file, '/inputSchema', codes.schema, '工具内容不符合 JCS 数值或 Unicode 约束。'); }
    const binding = obj(data.binding), capabilities = this.options.capabilities ?? [];
    if (binding.kind === 'http') this.egress(doc.file, '/binding/url', str(binding.url));
    if (binding.kind === 'command' && !capabilities.includes(str(binding.id))) this.error(doc.file, '/binding/id', codes.reference, '命令能力未登记。');
    if (binding.kind === 'service' && (![...strings(this.manifest.inject), ...strings(this.manifest.provides)].includes(str(binding.key)) || !capabilities.includes(str(binding.key) + '.' + str(binding.method)))) this.error(doc.file, '/binding', codes.reference, '服务键或方法未在目录中登记。');
  }
  private hooks(doc: SurfaceDocument): void {
    for (const [event, groups] of Object.entries(obj(obj(doc.data).hooks))) {
      const pointer = '/hooks/' + token(event);
      if (!rules.qianEvents.includes(event) && !(this.options.events ?? []).includes(event)) {
        if (/^[A-Z][A-Za-z0-9]+$/.test(event)) { if (!rules.agentEvents.includes(event)) this.error(doc.file, pointer, codes.reference, '未知 Agent 宿主事件，保留透传。', 'warning'); }
        else this.error(doc.file, pointer, codes.reference, 'Qian 事件未登记。');
      }
      (groups as JsonValue[]).forEach((value, index) => {
        const group = obj(value), location = pointer + '/' + index;
        if (typeof group.matcher === 'string') { try { new RegExp(group.matcher); } catch { this.error(doc.file, location + '/matcher', codes.syntax, 'matcher 不是合法正则。'); } }
        (group.hooks as JsonValue[]).forEach((item, index) => {
          const action = obj(item), child = location + '/hooks/' + index;
          if (action.type === 'plugin') {
            if (['private', 'community'].includes(this.tier)) this.error(doc.file, child + '/type', codes.policy, '该信任等级只允许 HTTP hook。');
            if (!(this.options.capabilities ?? []).includes(str(action.plugin) + '/' + str(action.handler))) this.error(doc.file, child + '/handler', codes.reference, 'hook 能力或 handler 未登记。');
          }
          if (action.type === 'http') this.egress(doc.file, child + '/url', str(action.url));
          if (action.type === 'command') {
            const argv = strings(action.command), executable = path.posix.basename(argv[0].replaceAll('\\', '/')).toLowerCase();
            if (!['system', 'official'].includes(this.tier) || !this.options.allowCommandHooks || ['gateway', 'identity'].includes(this.profile) || rules.shells.includes(executable) || argv.some(arg => /[|&;<>`*?\r\n]/.test(arg) || arg.includes('$('))) this.error(doc.file, child + '/command', codes.policy, 'hook 只允许显式开启的受限 argv，不能执行 shell。');
          }
        });
      });
    }
  }
  private mcp(doc: SurfaceDocument): void {
    for (const [name, data] of Object.entries(obj(obj(doc.data).mcpServers))) {
      const server = obj(data), pointer = '/mcpServers/' + token(name), profiles = strings(server.profiles);
      if (server.type === 'stdio' && (!profiles.length || profiles.includes(this.profile)) && !['standalone', 'minimal'].includes(this.profile)) this.error(doc.file, pointer + '/type', codes.policy, '当前 profile 不允许 stdio MCP。');
      if (server.type === 'http') this.egress(doc.file, pointer + '/url', str(server.url));
    }
  }
  private egress(file: string, pointer: string, value: string): void {
    let url: URL;
    try { url = new URL(value.replaceAll('{tenant}', 'tenant')); if (!['http:', 'https:'].includes(url.protocol) || url.username || url.password) throw new Error(); }
    catch { this.error(file, pointer, codes.policy, '出网地址必须为不含凭据的 HTTP(S) URL。'); return; }
    const allowed = strings(obj(this.manifest.policy).egress).some(rule => {
      const [host, port] = rule.split(':');
      const matches = host.startsWith('*.') ? url.hostname.toLowerCase().endsWith(host.slice(1).toLowerCase()) : url.hostname.toLowerCase() === host.toLowerCase();
      return matches && (!port || Number(port) === Number(url.port || (url.protocol === 'https:' ? 443 : 80)));
    });
    if (!allowed) this.error(file, pointer, codes.policy, '出网域名或端口未在 policy.egress 中声明。');
  }
  private secrets(value: JsonValue, file: string, pointer: string, name: string): void {
    if (Array.isArray(value)) value.forEach((child, index) => this.secrets(child, file, pointer + '/' + index, ''));
    else if (typeof value === 'object' && value !== null) for (const [key, child] of Object.entries(value)) this.secrets(child, file, pointer + '/' + token(key), key);
    else if (typeof value === 'string') {
      const key = name.replaceAll('_', '').replaceAll('-', '').toLowerCase();
      const sensitive = ['authorization', 'cookie', 'setcookie', 'secret', 'password', 'token', 'apikey', 'accesskey', 'secretkey'].includes(key) || ['token', 'password', 'secret'].some(suffix => key.endsWith(suffix));
      if (sensitive && !/^(Bearer )?secretRef:\/\/(platform|tenant|dev)\/[^\s]+$/.test(value)) this.error(file, pointer, codes.secret, '敏感字段只允许 secretRef，不允许明文。');
    }
  }
}

/** 市场文件的静态验证；不代表外部来源已获取或验签。 */
export interface MarketplaceValidationResult {marketplace: JsonValue | null;diagnostics: readonly SurfaceDiagnostic[];isValid: boolean}
/** 验证市场结构及跨字段规则；具体来源仍由调用方获取、验签并复用组件校验器。 */
export function validateMarketplace(file: string, content: string, traceId = trace()): MarketplaceValidationResult {
  const parsed = parseSurface(file, content, false, traceId), diagnostics = [...parsed.diagnostics];
  const result = (marketplace: JsonValue | null): MarketplaceValidationResult => ({marketplace: diagnostics.some(item => item.severity === 'error') ? null : marketplace, diagnostics: diagnostics.sort((a,b) => a.error.pointer < b.error.pointer ? -1 : a.error.pointer > b.error.pointer ? 1 : 0), isValid: marketplace !== null && !diagnostics.some(item => item.severity === 'error')});
  if (!parsed.syntax || diagnostics.some(item => item.severity === 'error')) return result(null);
  const data = parsed.syntax.data;
  diagnostics.push(...validateSurfaceSchema('marketplace',file,data,traceId,parsed.syntax.lines));
  if (diagnostics.some(item => item.severity === 'error')) return result(data);
  const market=obj(data), names=new Set<string>(), categories=Array.isArray(market.categories)?new Set(strings(market.categories)):null;
  const safe = (value:string): boolean => !!value&&!value.startsWith('/')&&!value.includes('\\')&&!value.includes(':')&&!/[\x00-\x1f\x7f-\x9f]/.test(value)&&!value.split('/').includes('..');
  const error = (pointer:string,code:string,message:string):void => {diagnostics.push(diagnostic(file,pointer,closest(parsed.syntax!.lines,pointer),code,message,traceId));};
  (market.plugins as JsonValue[]).forEach((value,index) => {
    const entry=obj(value), pointer='/plugins/'+index, name=str(entry.name), source=obj(entry.source);
    if(names.has(name)) error(pointer+'/name',codes.name,'市场内安装名必须唯一。');names.add(name);
    if(categories&&!categories.has(str(entry.category)))error(pointer+'/category',codes.reference,'分类未在市场 categories 中声明。');
    if(source.path!==undefined&&!safe(str(source.path)))error(pointer+'/source/path',codes.path,'来源路径必须留在仓库内。');
    for(const field of ['url','repo'])if(source[field]!==undefined){
      try{const url=new URL(str(source[field]));if(url.protocol!=='https:'||url.username||url.password||url.hash||[...url.searchParams.keys()].some(key=>/token|secret|password|credential|signature|api[-_]?key|authorization/i.test(key)))throw new Error();}
      catch{error(pointer+'/source/'+field,codes.policy,'远程来源必须使用无凭据片段的 HTTPS 地址。');}
    }
  });
  strings(obj(market.owner).publicKeys).forEach((value,index)=>{
    const pointer='/owner/publicKeys/'+index;
    if(value.includes('PRIVATE KEY'))error(pointer,codes.secret,'市场只能声明公钥，不能携带私钥。');
    else if(!value.startsWith('-----BEGIN PUBLIC KEY-----')&&!safe(value))error(pointer,codes.path,'公钥路径必须留在市场仓库内。');
  });
  return result(data);
}
export {SurfaceRegistrationSession, HotReloadSurfacePlugin, readSurfaceRuntimeDiagnostics} from './runtime.js';
export type {SurfaceContext, SurfaceRegistrationHandle, SurfaceReloadStatus, SurfaceRuntimeEntry, SurfaceRuntimeReport, SurfaceDiagnosticContext} from './runtime.js';
