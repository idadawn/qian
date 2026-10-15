import { readFileSync, readdirSync, existsSync, statSync, lstatSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { createHash, randomBytes } from 'node:crypto';
import { Ajv2020, type ErrorObject, type ValidateFunction } from 'ajv/dist/2020.js';
import { createScanner, parseTree, SyntaxKind, type Node as JsonTree, type ParseError } from 'jsonc-parser';
import { parseAllDocuments, isAlias, isMap, isPair, isSeq, isScalar, type CST, type Scalar } from 'yaml';

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
  catalogComplete?: boolean; traceId?: string; serviceKeys?: readonly string[];
}
const codes = { syntax: 'SURFACE_SYNTAX_INVALID', schema: 'SURFACE_SCHEMA_INVALID', name: 'SURFACE_NAME_MISMATCH', reference: 'SURFACE_REFERENCE_UNKNOWN', path: 'SURFACE_PATH_INVALID', policy: 'SURFACE_POLICY_DENIED', secret: 'SURFACE_SECRET_LITERAL', hash: 'SURFACE_HASH_MISMATCH' };
type Obj = { [key: string]: JsonValue };
const obj = (value: JsonValue | undefined): Obj => value !== null && typeof value === 'object' && !Array.isArray(value) ? value : {};
const str = (value: JsonValue | undefined): string => typeof value === 'string' ? value : '';
const strings = (value: JsonValue | undefined): string[] => Array.isArray(value) ? value.filter((v): v is string => typeof v === 'string') : [];
const token = (value: string): string => value.replaceAll('~', '~0').replaceAll('/', '~1');
const trace = (value?: string): string => value ?? randomBytes(16).toString('hex');
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
const generic = '文件语法、大小、嵌套深度或值无效。', yamlSyntax = 'YAML 语法无效或存在重复键。';
const yamlDocuments = 'YAML 只能包含一个文档。', yamlCharacters = 'YAML 不能含 NUL、单独的回车符、U+0085、U+2028 或 U+2029。';
const yamlDirectives = '不支持 YAML 指令（以 % 开头的行）。';
const forbidden = new Set([0, 0x0d, 0x85, 0x2028, 0x2029]);
const yamlTag = 'tag:yaml.org,2002:';
/** 按换行符计行号（第 1 行起）；二分查找，逐个节点取行号也不随文件变大而变慢。 */
function lineIndex(source: string): (offset: number) => number {
  const starts = [0];
  for (let index = source.indexOf('\n'); index >= 0; index = source.indexOf('\n', index + 1)) starts.push(index + 1);
  return offset => {
    let low = 0, high = starts.length;
    while (low < high) { const middle = (low + high) >> 1; if (starts[middle] <= offset) low = middle + 1; else high = middle; }
    return low;
  };
}
/** 每行行首空白里（空格之后）的第一个制表符。块标量正文里空格之后的制表符属于内容，由调用方排除。 */
function indentTabs(source: string): number[] {
  const tabs: number[] = [];
  for (let start = 0; ;) {
    let index = start;
    while (source[index] === ' ') index++;
    if (source[index] === '\t') tabs.push(index);
    const end = source.indexOf('\n', start);
    if (end < 0) return tabs;
    start = end + 1;
  }
}
/** 位置之后本行只剩空白或注释（含空行、注释行）时，顺延到其后第一个非空、非注释行的首个内容字符；都没有时为文件末尾。 */
function content(source: string, offset: number): number {
  const blank = /^[ \t]*(?:#.*)?$/;
  let end = source.indexOf('\n', offset);
  if (end < 0) end = source.length;
  if (!blank.test(source.slice(offset, end))) return offset;
  for (let start = end + 1; start < source.length; start = end + 1) {
    end = source.indexOf('\n', start);
    if (end < 0) end = source.length;
    const line = source.slice(start, end);
    if (!blank.test(line)) return start + line.length - line.replace(/^[ \t]+/, '').length;
  }
  return source.length;
}
/**
 * 同 YamlDotNet 18.1.0 对块标量开头空行的两项检查（比规范严，yaml 包不查或只在有内容行时才查），逐行模拟它读开头空行的过程：
 * 字面块标量（|）首行只有空格、且空格数不小于「显式缩进 + 2」（未写缩进指示时为 2）即报错（W9L4）；
 * 折叠块标量（>）首行是空行、而开头各空行的空格多于停下处（首个内容行的缩进，到文件末尾时为 0）即报错（S98Z）。
 * 出错位置在停下的那一行行首，没有错误时返回 -1。
 */
function blockLeadingLines(source: string, header: number, indent: number, literal: boolean): number {
  const headerEnd = source.indexOf('\n', header);
  if (headerEnd < 0) return -1;
  const digit = /^[|>](?:[+-]([1-9])|([1-9]))/.exec(source.slice(header, headerEnd));
  const increment = Number(digit?.[1] ?? digit?.[2] ?? 0), current = increment ? (indent >= 0 ? indent + increment : increment) : 0;
  let at = headerEnd + 1, column = 0, widest = 0, first = -1;
  for (let line = 0; ; line++) {
    column = 0;
    while ((current === 0 || column < current) && source[at + column] === ' ') column++;
    widest = Math.max(widest, column);
    if (source[at + column] !== '\n') {
      if (literal && line === 0) {
        let local = column;
        while (source[at + local] === ' ') local++;
        if (source[at + local] === '\n' && local > column) first = local;
      }
      break;
    }
    if (line === 0) first = column;
    at += column + 1;
  }
  if (literal ? first > 1 && current < first - 1 : widest > column && first > -1) return at;
  return -1;
}
/** 引号标量的原文是否以未转义的结束引号收尾。yaml 包遇到续行缩进不足时把标量截在上一行末尾，末尾可能是转义的引号。 */
function closed(raw: string, quote: string): boolean {
  if (raw.length < 2 || raw[raw.length - 1] !== quote) return false;
  let count = 0;
  if (quote === '"') {
    while (raw[raw.length - 2 - count] === '\\') count++;
    return count % 2 === 0;
  }
  while (count < raw.length - 1 && raw[raw.length - 1 - count] === "'") count++;
  return count % 2 === 1;
}
/** 双引号标量里转义的换行（奇数个反斜杠紧跟换行）之后是否紧跟空行。yaml 包把这种写法按普通折叠少算一个换行，与规范和 YamlDotNet 不同，两端都按语法错误拒绝。 */
function escapedBreakBeforeEmptyLine(raw: string): boolean {
  for (let index = raw.indexOf('\n'); index >= 0; index = raw.indexOf('\n', index + 1)) {
    let slashes = 0;
    while (raw[index - slashes - 1] === '\\') slashes++;
    if (slashes % 2 === 0) continue;
    let next = index + 1;
    while (raw[next] === ' ' || raw[next] === '\t') next++;
    if (raw[next] === '\n') return true;
  }
  return false;
}
/**
 * 同 SurfaceReader 的块映射条目规则，逐条看 CST，返回出错位置：
 * - 空键（冒号前没有键，含显式键「?」之后为空）两端都拒绝，报在条目开始的行：YamlDotNet 18.1.0 在嵌套块映射里会吞掉空键之后的值，流式映射不受影响；
 * - 隐式键的冒号须与键在同一行（yaml 包在第二个起的键上接受换行之后的冒号）：普通标量键与冒号之间只隔空白时报在冒号所在行（YamlDotNet 把键读成多行普通标量），
 *   隔着注释、键带引号或带 anchor、tag 时报在键开始的行（YamlDotNet 先把整个键交给解析器）；第一个条目报在冒号所在行。
 */
function blockMapEntries(map: CST.BlockMap): number[] {
  const stops: number[] = [];
  map.items.forEach((item, index) => {
    const sep = item.sep ?? [], lead = [...item.start, ...(item.key ? [item.key] : []), ...sep].find(token => !['space', 'newline', 'comment'].includes(token.type));
    if (!lead) return;
    if (!item.key) return void stops.push(lead.offset);
    const colon = sep.findIndex(token => token.type === 'map-value-ind'), before = sep.slice(0, colon);
    if (colon < 0 || item.start.some(token => token.type === 'explicit-key-ind') || !before.some(token => token.type === 'newline')) return;
    const whole = item.key.type !== 'scalar' || before.some(token => token.type === 'comment') || item.start.some(token => token.type === 'anchor' || token.type === 'tag');
    stops.push(index > 0 && whole ? lead.offset : sep[colon].offset);
  });
  return stops;
}
const keyName = (key: Scalar): string => String(key.source ?? key.value ?? '');
type Problem = { line: number; reason: string };
/**
 * 同 SurfaceReader.Prescan：逐行预检，先于解析。禁用字符；指令行（两库对指令的处理差异很大，%YAML 1.1 还会改变 yaml 包的解析规则）；
 * 第二个文档，即内容或 --- 之后又出现 ---，或 ... 之后还有内容（只有空行与注释不算）。两库对文档边界的计数不同（如多余的 ...），这里不依赖它们。
 */
function prescan(source: string): Problem | null {
  let content = false, ended = false;
  for (let start = 0, line = 1; start <= source.length; line++) {
    let end = source.indexOf('\n', start);
    if (end < 0) end = source.length;
    const text = source.slice(start, end), rest = text.replace(/^[ \t]+/, '');
    for (let index = 0; index < text.length; index++) if (forbidden.has(text.charCodeAt(index))) return { line, reason: yamlCharacters };
    if (text.startsWith('%')) return { line, reason: yamlDirectives };
    const marker = text.length === 3 || text[3] === ' ' || text[3] === '\t' ? text.slice(0, 3) : '';
    if (marker === '---') {
      if (content || ended) return { line, reason: yamlDocuments };
      content = true;
    } else if (marker === '...') ended = true;
    else if (rest && rest[0] !== '#') {
      if (ended) return { line, reason: yamlDocuments };
      content = true;
    }
    start = end + 1;
  }
  return null;
}
/**
 * 同 SurfaceReader.ScanYaml，行号约定见 README。先逐行预检；之后按文中位置，语法错误、行首缩进里的制表符、第 65 层集合、未闭合或续行缩进不足的引号标量、
 * 含未成对代理项的标量，以及两库处理不同、两端都拒绝的写法（块映射里的空键与换行之后的冒号、「-」「?」之后的制表符、紧跟冒号的 alias 键、
 * 转义换行之后紧跟空行的双引号标量）先到先报；整个流扫描无误后，再查 alias 与首个重复键。
 */
function scanYaml(source: string, line: (offset: number) => number): Problem | { contents: unknown } {
  const early = prescan(source);
  if (early) return early;
  const parsed = parseAllDocuments(source, { version: '1.2', uniqueKeys: false, keepSourceTokens: true }), documents = Array.from(parsed);
  // 没有文档时（如只有指令），错误挂在返回的空流上。
  const errors = [...documents.flatMap(document => document.errors), ...('empty' in parsed ? parsed.errors : [])];
  const stops: { offset: number; generic: boolean }[] = [];
  let alias = false;
  const quoted: Scalar[] = [], blocks: Scalar[] = [], required = new Set<number>(), keyIndent = new Map<number, number>(), collectionKeys = new Set<number>();
  const start = (node: { range?: readonly number[] | null }): number => node.range?.[0] ?? 0;
  const end = (node: { range?: readonly number[] | null }): number => node.range?.[1] ?? 0;
  // 不递归地遍历：yaml 包组装过深的集合时会耗尽调用栈（RESOURCE_EXHAUSTION），这里只需走到第 64 层。顺带记下所在块集合的缩进（列号，没有时为 -1）。
  const stack: [unknown, number, number][] = documents.map(document => [document.contents, 0, -1]);
  while (stack.length) {
    const [node, depth, indent] = stack.pop()!;
    if (isAlias(node)) alias = true;
    else if (isMap(node) || isSeq(node)) {
      if (depth >= 64) stops.push({ offset: start(node), generic: true });
      else {
        const inner = node.flow ? indent : start(node) - source.lastIndexOf('\n', start(node) - 1) - 1;
        if (isMap(node) && node.srcToken?.type === 'block-map') for (const at of blockMapEntries(node.srcToken)) stops.push({ offset: at, generic: false });
        (node.items as unknown[]).forEach((item, index, items) => {
          if (!isPair(item)) return void stack.push([item, depth + 1, inner]);
          // 块映射里第二个起的键在 YamlDotNet 看来是必须的简单键；同时记下 YamlDotNet 扫描该键时的块缩进（第一个键时映射尚未开始，取外层）。
          // 作键的流式集合见下方 IMPOSSIBLE。
          if (isScalar(item.key) && !node.flow) {
            if (index > 0) required.add(start(item.key));
            keyIndent.set(start(item.key), index > 0 ? inner : indent);
          }
          if (isMap(item.key) || isSeq(item.key)) collectionKeys.add(start(item.key));
          // YamlDotNet 18.1.0 的缺陷：流式映射里带锚点的标量值之后还有条目时（如 {x: &a 1, y: 2}），在下一个键处报语法错误。
          const following = items[index + 1];
          if (node.flow && isScalar(item.value) && item.value.anchor && isPair(following) && isScalar(following.key))
            stops.push({ offset: start(following.key), generic: false });
          stack.push([item.key, depth + 1, inner], [item.value, depth + 1, inner]);
        });
      }
    } else if (isScalar(node)) {
      if (node.type === 'QUOTE_DOUBLE' || node.type === 'QUOTE_SINGLE') {
        quoted.push(node);
        const raw = source.slice(start(node), end(node));
        if (!closed(raw, node.type === 'QUOTE_DOUBLE' ? '"' : "'") || node.type === 'QUOTE_DOUBLE' && escapedBreakBeforeEmptyLine(raw)) stops.push({ offset: start(node), generic: false });
      } else if (node.type === 'BLOCK_LITERAL' || node.type === 'BLOCK_FOLDED') {
        blocks.push(node);
        const first = blockLeadingLines(source, start(node), indent, node.type === 'BLOCK_LITERAL');
        if (first >= 0) stops.push({ offset: first, generic: false });
      }
      if (typeof node.value === 'string' && unpaired(node.value)) stops.push({ offset: start(node), generic: false });
    }
  }
  // 错误位置向 YamlDotNet 靠拢：多行隐式键报在键结束（冒号）所在行，除非 YamlDotNet 在冒号之前就结束了该键——普通标量遇到缩进不超过块缩进的续行即结束、
  // 引号键是必须简单键（块映射里第二个起的键）——这时报在键开始处；同一个键另报的 BLOCK_AS_IMPLICIT_KEY、以该键开头的行上的 BAD_INDENT（YamlDotNet 先报冒号）不计；嵌套块映射里以流式集合作键时
  // yaml 包报 IMPOSSIBLE，YamlDotNet 照常解析（键非标量，按值无效报），也不计；引号标量内部的错误报在标量开始处；停在行尾或空行、注释行上的错误
  // （yaml 包常把缺失的节点、节点前的注释算在上一行）顺延到其后第一个内容字符。
  const multiline = new Set(errors.filter(error => error.code === 'MULTILINE_IMPLICIT_KEY').map(error => error.pos[0]));
  const multilineLines = new Set([...multiline].filter(at => /^[ \t]*$/.test(source.slice(source.lastIndexOf('\n', at - 1) + 1, at))).map(at => line(at)));
  const keyStart = (at: number, colon: number): boolean => {
    const indent = keyIndent.get(at);
    if (indent === undefined) return false;
    if (source[at] === '"' || source[at] === "'") return required.has(at);
    for (let next = source.indexOf('\n', at) + 1; next > 0 && next <= colon; next = source.indexOf('\n', next) + 1) {
      let spaces = 0;
      while (source[next + spaces] === ' ') spaces++;
      if (source[next + spaces] !== '\n' && spaces <= indent) return true;
    }
    return false;
  };
  for (const error of errors) {
    if (error.code === 'BLOCK_AS_IMPLICIT_KEY' && multiline.has(error.pos[0]) || error.code === 'IMPOSSIBLE' && collectionKeys.has(error.pos[0])
      || error.code === 'BAD_INDENT' && multilineLines.has(line(error.pos[0]))) continue;
    let offset = error.code === 'MULTILINE_IMPLICIT_KEY' && !keyStart(error.pos[0], error.pos[1]) ? error.pos[1] : error.pos[0];
    const inside = quoted.find(node => start(node) < offset && offset < end(node));
    if (inside) offset = start(inside);
    stops.push({ offset: content(source, offset), generic: error.code === 'RESOURCE_EXHAUSTION' });
  }
  for (const tab of indentTabs(source))
    if (!blocks.some(node => start(node) < tab && tab < end(node) && tab > 0 && source[tab - 1] !== '\n')) stops.push({ offset: tab, generic: false });
  const first = stops.sort((a, b) => a.offset - b.offset)[0];
  if (first) return first.generic ? { line: 1, reason: generic } : { line: line(first.offset), reason: yamlSyntax };
  if (alias) return { line: 1, reason: generic };
  // 重复键按名称比较（普通标量取原文，引号与块标量取解码后的文本，同 YamlDotNet 的 Value），报文中最先出现的那个。
  let duplicate = Infinity;
  const nodes: unknown[] = [documents[0]?.contents];
  while (nodes.length) {
    const node = nodes.pop();
    if (isMap(node)) {
      const names = new Set<string>();
      for (const pair of node.items) {
        if (isScalar(pair.key)) {
          const name = keyName(pair.key);
          if (names.has(name)) duplicate = Math.min(duplicate, start(pair.key));
          names.add(name);
        }
        nodes.push(pair.key, pair.value);
      }
    } else if (isSeq(node)) for (const item of node.items) nodes.push(item);
  }
  if (duplicate < Infinity) return { line: line(duplicate), reason: yamlSyntax };
  return { contents: documents[0]?.contents ?? null };
}
/** 同 SurfaceReader.ReadYaml 的标量规则。 */
function resolveScalar(node: Scalar): JsonValue {
  const tag = node.tag ?? '', text = String(node.source ?? '');
  if (tag === '!' || tag === yamlTag + 'str' || !tag && node.type !== undefined && node.type !== 'PLAIN') return text;
  const finite = (value: number): number => { if (!Number.isFinite(value)) throw new Error('number'); return value; };
  if (/^(?:[-+]?\.(?:inf|Inf|INF)|\.nan|\.NaN|\.NAN)$/.test(text)) throw new Error('number');
  if (/^(?:0x[0-9a-fA-F]+|0o[0-7]+)$/.test(text)) return finite(Number(BigInt(text)));
  if (['null', 'Null', 'NULL', '~', ''].includes(text)) return null;
  if (['true', 'True', 'TRUE'].includes(text)) return true;
  if (['false', 'False', 'FALSE'].includes(text)) return false;
  if (/^[-+]?(?:[0-9]+|\.[0-9]+|[0-9]+\.[0-9]*)(?:[eE][-+]?[0-9]+)?$/.test(text)) return finite(Number(text));
  if ([yamlTag + 'int', yamlTag + 'float', yamlTag + 'bool', yamlTag + 'null'].includes(tag)) throw new Error('tag');
  return text;
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
      const line = lineIndex(source);
      // 不递归地逐个记号数容器层数：第 65 层容器即出错位置，jsonc-parser 只解析到它为止，深层嵌套不会耗尽调用栈。
      const scanner = createScanner(source, false);
      let depth = 0, cut = -1;
      for (let kind = scanner.scan(); kind !== SyntaxKind.EOF && cut < 0; kind = scanner.scan()) {
        if (kind === SyntaxKind.OpenBraceToken || kind === SyntaxKind.OpenBracketToken) { if (++depth > 64) cut = scanner.getTokenOffset(); }
        else if (kind === SyntaxKind.CloseBraceToken || kind === SyntaxKind.CloseBracketToken) depth--;
      }
      const errors: ParseError[] = [];
      const tree = parseTree(cut < 0 ? source : source.slice(0, cut + 1), errors, { allowTrailingComma: false, disallowComments: true });
      const syntax = errors.find(error => cut < 0 || error.offset <= cut);
      // 同 SurfaceReader：语法错误、第 65 层容器与非有限数值按文中顺序先到先报。读取器确认数值之后是分隔符（空白、逗号、右括号、/，根值还可以是文件末尾）才交出数值，
      // 故数值按末尾位置比较；分隔符本身有错（如多余的右括号、不允许的注释）时，读取器先交出数值，到下一个记号才报。
      const limit = syntax?.offset ?? Infinity;
      const fatal = (node: JsonTree | undefined, depth: number): boolean => {
        if (!node || node.offset >= limit) return false;
        if (node.type === 'property') return fatal(node.children?.[1], depth);
        if ((node.type === 'object' || node.type === 'array') && depth >= 64) return true;
        const next = source.charAt(node.offset + node.length);
        if (node.type === 'number' && !Number.isFinite(node.value) && (node.offset + node.length < limit || /^[,}\] \t\r\n/]$/.test(next) || !next && !depth)) return true;
        return (node.children ?? []).some(child => fatal(child, depth + 1));
      };
      if (fatal(tree, 0)) throw new Error('value');
      if (!tree || syntax) return fail(line(syntax?.offset ?? 0), 'JSON 语法无效。');
      // 键名与字符串值含未成对的代理项转义时记下首个所在行，走完整篇再报，同 SurfaceReader.Text。
      let surrogate = 0;
      const walk = (node: JsonTree, pointer: string): void => {
        lines[pointer] ??= line(node.offset);
        if (node.type === 'object') {
          const seen = new Set<string>();
          for (const property of node.children ?? []) {
            const [key, value] = property.children!;
            const name = String(key.value), child = pointer + '/' + token(name);
            lines[child] = line(key.offset);
            if (!surrogate && unpaired(name)) surrogate = lines[child];
            if (seen.has(name)) diagnostics.push(diagnostic(file, child, lines[child], codes.syntax, 'JSON 对象存在重复键。', traceId));
            seen.add(name); walk(value, child);
          }
        } else if (node.type === 'array') (node.children ?? []).forEach((value, index) => walk(value, pointer + '/' + index));
        else if (node.type === 'string' && !surrogate && unpaired(node.value)) surrogate = line(node.offset);
      };
      walk(tree, '');
      if (surrogate) return fail(surrogate, 'JSON 字符串或键名含未成对的代理项。');
      data = JSON.parse(source) as JsonValue;
    } else {
      // 同 SurfaceReader：末尾没有换行时补一个（front matter 截取时去掉了最后的换行，两库对文件末尾块标量是否保留结尾换行的处理也不同）。
      if (!source.endsWith('\n')) source += '\n';
      const line = lineIndex(source), scanned = scanYaml(source, line);
      if ('reason' in scanned) return fail(scanned.line + offset, scanned.reason);
      const walk = (node: unknown, pointer: string, depth: number): JsonValue => {
        if (depth > 64 || isAlias(node)) throw new Error('alias/depth');
        const tag = (node as { tag?: string } | null)?.tag;
        if (tag && tag !== '!' && !tag.startsWith(yamlTag)) throw new Error('tag');
        const range = (node as { range?: number[] } | null)?.range;
        lines[pointer] ??= (range ? line(range[0]) : 1) + offset;
        if (isMap(node)) {
          const pairs: [string, JsonValue][] = [];
          for (const pair of node.items) {
            if (!isScalar(pair.key)) throw new Error('key');
            const name = keyName(pair.key), child = pointer + '/' + token(name);
            lines[child] = line(pair.key.range?.[0] ?? 0) + offset;
            pairs.push([name, walk(pair.value, child, depth + 1)]);
          }
          return Object.fromEntries(pairs);
        }
        if (isSeq(node)) return node.items.map((value, index) => walk(value, pointer + '/' + index, depth + 1));
        if (node === null || node === undefined) return null;
        if (!isScalar(node)) throw new Error('scalar');
        return resolveScalar(node);
      };
      data = walk(scanned.contents, '', 0);
    }
    return { syntax: { data, body, lines }, diagnostics };
  } catch { return fail(1 + offset, generic); }
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
    if (['allOf', 'anyOf', 'oneOf', 'if', 'then', 'else', '$ref', 'propertyNames'].includes(error.keyword)) continue;
    let pointer = error.instancePath;
    // propertyNames 子约束的错误指向出错的属性名本身，与 .NET 侧一致。
    if (error.propertyName !== undefined) pointer += '/' + token(error.propertyName);
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
    // 宿主给出目标装配的服务键目录时，inject 必须全部有 Provider（PRD 7.2.1）；optionalInject 允许缺席。
    const provided = this.options.serviceKeys;
    if (provided) strings(this.manifest.inject).forEach((key, index) => { if (!provided.includes(key)) this.error(file, '/inject/' + index, codes.reference, 'inject 的服务键在目标装配中没有 Provider。'); });
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
