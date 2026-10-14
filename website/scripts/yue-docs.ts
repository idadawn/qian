// yue 文档的源均在组件目录；使用 TypeScript checker 保留继承、泛型、回调和无注释属性。
import fs from 'node:fs';
import path from 'node:path';
import ts from 'typescript';
import { createHash } from 'node:crypto';
import { REPO_ROOT, REPO_URL, DEFAULT_BRANCH, repoFileDate, yamlString, type GeneratedFile } from './repo.ts';

export interface Scenario { id: string; component: string; state: string; theme: string; props: { disabled?: boolean; invalid?: boolean } }
export interface PropDoc { name: string; type: string; description: string; required: boolean; defaultValue: string; inherited: boolean; native: boolean }
export interface ComponentApi { name: string; props: PropDoc[] }
export interface YueDoc { name: string; label: string; description: string; source: string; parts: string[]; keyboard: string[]; scenarios: Scenario[] }
export interface YueData { docs: YueDoc[]; apis: ComponentApi[]; scenarios: Scenario[]; version: string }

const readJson = <T>(file: string): T => JSON.parse(fs.readFileSync(file, 'utf8')) as T;
// 类型中的 import() 使用包名，避免公开安装器路径，也让不同机器的属性引用保持一致。
export function publicType(type: string): string {
  return type.replace(/import\((["'])(.*?)\1\)/g, (_match, _quote: string, input: string) => {
    let specifier = input.replace(/\\/g, '/');
    const marker = specifier.lastIndexOf('node_modules/');
    if (marker >= 0) specifier = specifier.slice(marker + 'node_modules/'.length);
    specifier = specifier.replace(/^@types\/react(?=\/|$)/, 'react').replace(/\/index(?:\.d\.ts)?$/, '');
    return `import(${JSON.stringify(specifier)})`;
  });
}
const cache = new Map<string, YueData>();
export function readYueDocs(repoRoot = REPO_ROOT): YueData {
  if (cache.has(repoRoot)) return cache.get(repoRoot)!;
  const core = path.join(repoRoot, 'packages/yue/core');
  const catalog = readJson<Record<string, { label: string; description: string }>>(path.join(core, 'docs/catalog.json'));
  const manifests = readJson<Record<string, { a11y: { keyboard: string[] } }>>(path.join(core, 'src/manifest-catalog.json'));
  const scenarios = readJson<Scenario[]>(path.join(core, 'docs/scenarios.json'));
  if (JSON.stringify(Object.keys(catalog).sort()) !== JSON.stringify(Object.keys(manifests).sort())) throw new Error('组件文档与 manifest 入口不一致');
  if (scenarios.length !== 126 || new Set(scenarios.map(s => s.id)).size !== 126) throw new Error('原 126 条场景迁移清单不完整或重复');
  const config = ts.readConfigFile(path.join(core, 'tsconfig.json'), ts.sys.readFile);
  const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, core);
  const entry = path.join(core, 'src/index.ts');
  const examples = path.join(core, 'docs/examples/catalog.tsx');
  const program = ts.createProgram([entry, examples], parsed.options);
  const checker = program.getTypeChecker();
  const sourceFile = program.getSourceFile(entry)!;
  const exports = checker.getExportsOfModule(checker.getSymbolAtLocation(sourceFile)!);
  const apis: ComponentApi[] = [];
  for (const exported of exports) {
    const name = exported.getName();
    if (!/^[A-Z]/.test(name)) continue;
    const symbol = exported.flags & ts.SymbolFlags.Alias ? checker.getAliasedSymbol(exported) : exported;
    const declaration = symbol.valueDeclaration ?? symbol.declarations?.[0];
    if (!declaration) continue;
    const type = checker.getTypeOfSymbolAtLocation(symbol, declaration);
    const call = checker.getSignaturesOfType(type, ts.SignatureKind.Call)[0];
    const parameter = call?.getParameters()[0];
    if (!parameter) continue;
    const propsType = checker.getTypeOfSymbolAtLocation(parameter, declaration);
    const props = checker.getPropertiesOfType(propsType).map(prop => {
      const node = prop.valueDeclaration ?? prop.declarations?.[0] ?? declaration;
      const tags = prop.getJsDocTags(checker);
      const defaultTag = tags.find(tag => ['default', 'defaultValue'].includes(tag.name));
      let defaultValue = defaultTag ? ts.displayPartsToString(defaultTag.text) : '';
      // 默认值来自实际参数解构；未声明的默认值不猜测。
      if (ts.isFunctionDeclaration(declaration) || ts.isArrowFunction(declaration) || ts.isFunctionExpression(declaration)) {
        const binding = declaration.parameters[0]?.name;
        if (binding && ts.isObjectBindingPattern(binding)) {
          const element = binding.elements.find(e => (e.propertyName ?? e.name).getText() === prop.name);
          if (element?.initializer) defaultValue = element.initializer.getText();
        }
      }
      return { name: prop.name, type: publicType(checker.typeToString(checker.getTypeOfSymbolAtLocation(prop, node), node, ts.TypeFormatFlags.NoTruncation)), description: ts.displayPartsToString(prop.getDocumentationComment(checker)), required: !(prop.flags & ts.SymbolFlags.Optional), defaultValue, native: !['disabled', 'children', 'ref'].includes(prop.name) && (prop.declarations ?? []).length > 0 && (prop.declarations ?? []).every(d => d.getSourceFile().fileName.includes('@types/react')), inherited: (prop.declarations ?? []).every(d => d.getSourceFile().fileName.includes('node_modules')) };
    });
    apis.push({ name, props });
  }
  const byName = new Map(apis.map(api => [api.name, api]));
  for (const [name, expected] of Object.entries({ Button: ['variant', 'size', 'loading', 'disabled'], Dialog: ['open', 'defaultOpen', 'onOpenChange'] })) {
    for (const prop of expected) if (!byName.get(name)?.props.some(p => p.name === prop)) throw new Error(`Props 解析丢失 ${name}.${prop}`);
  }
  const exampleFile = program.getSourceFile(examples)!;
  const printer = ts.createPrinter({ newLine: ts.NewLineKind.LineFeed });
  const functions = new Map(exampleFile.statements.filter(ts.isFunctionDeclaration).filter(n => n.name).map(n => [n.name!.text, n]));
  const names = Object.keys(catalog);
  const docs = names.map(name => {
    if (!byName.has(name)) throw new Error(`Props 解析缺少组件 ${name}`);
    const functionName = name === 'DropdownMenu' ? 'MenuDemo' : `${name}Demo`;
    const demo = functions.get(functionName);
    if (!demo) throw new Error(`组件缺少共用示例 ${name}`);
    let source = printer.printNode(ts.EmitHint.Unspecified, demo, exampleFile).replace(/^export /, '').replace(`function ${functionName}`, 'export default function Example');
    if (['Toast', 'Notification'].includes(name)) source = `${printer.printNode(ts.EmitHint.Unspecified, functions.get('ToastButton')!, exampleFile)}\n${source}`;
    source = `import { useId, useState } from 'react';\nimport * as C from '@idadawn/yue-core';\n\ninterface DemoProps { disabled?: boolean; invalid?: boolean; rootProps?: Record<string, unknown> }\n\n${source}`;
    const parts = apis.filter(api => api.name === name || (api.name.startsWith(name) && !names.includes(api.name) && !names.some(other => other !== name && other.startsWith(name) && api.name.startsWith(other)))).map(api => api.name);
    const states = scenarios.filter(s => s.component === name);
    if (!states.some(s => s.state === 'Default') || !states.some(s => s.state === 'Dark')) throw new Error(`组件缺少明暗场景 ${name}`);
    return { name, ...catalog[name]!, source, parts, keyboard: manifests[name]!.a11y.keyboard, scenarios: states };
  });
  const data = { docs, apis, scenarios, version: readJson<{ version: string }>(path.join(core, 'package.json')).version };
  cache.set(repoRoot, data);
  return data;
}

const cell = (value: string) => value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/\{/g, '&#123;').replace(/\}/g, '&#125;').replace(/\|/g, '&#124;').replace(/`/g, '&#96;').replace(/\n/g, ' ');
export function typeCell(type: string): string {
  const ticks = '`'.repeat(Math.max(0, ...(type.match(/`+/g) ?? []).map(run => run.length)) + 1);
  return `${ticks} ${type.replace(/\|/g, '\\|').replace(/\n/g, ' ')} ${ticks}`;
}
function rows(props: PropDoc[]): string {
  return ['| 属性 | 类型 | 必填 | 默认值 | 说明 |', '|---|---|---|---|---|', ...props.map(p => `| ${cell(p.name)} | ${typeCell(p.type)} | ${p.required ? '是' : '否'} | ${cell(p.defaultValue || '未指定')} | ${cell(p.description || '以 TypeScript 类型约束为准。')} |`)].join('\n');
}
function nativeId(api: ComponentApi): string {
  return createHash('sha256').update(JSON.stringify(api.props.filter(p => p.native))).digest('hex').slice(0, 12);
}
function apiTable(api: ComponentApi): string {
  const native = api.props.filter(p => p.native);
  return `### ${api.name}\n\n${rows(api.props.filter(p => !p.native))}\n\n${native.length ? `[继承的 DOM / ARIA / 事件属性（${native.length} 项）](/components/yue/api/native-${nativeId(api)})：从相同的 TypeScript 声明提取，完整类型与说明只存一份。` : ''}\n`;
}
export function collectYuePages(repoRoot = REPO_ROOT): GeneratedFile[] {
  const data = readYueDocs(repoRoot);
  const front = (title: string, description: string, component = '@idadawn/yue-core') => `---\ntitle: ${yamlString(title)}\ndescription: ${yamlString(description)}\npart: components\nproject: yue\naudience: [developer]\nupdated: ${repoFileDate('packages/yue/core/docs/catalog.json', repoRoot)}\nversion: ${data.version}\ncomponent: ${yamlString(component)}\nsource: ${yamlString(`${REPO_URL.replace('https://github.com/', '')}@${DEFAULT_BRANCH}:${component === '@idadawn/yue-tokens' ? 'packages/yue/tokens' : 'packages/yue/core'}`)}\n---\n`;
  const files = data.docs.map(doc => ({ path: `yue/core/${doc.name.toLowerCase()}.mdx`, content: `${front(`${doc.name} ${doc.label}`, doc.description, doc.name)}
import { ComponentDemo } from '@theme';

# ${doc.name} ${doc.label}

${doc.description}

<ComponentDemo name="${doc.name}" />

## 安装

\`\`\`bash
pnpm add @idadawn/yue-core @idadawn/yue-tokens
\`\`\`

包通过 GitHub Packages 分发，配置方式见 [yue 组件库](/components/yue/)。引入令牌 CSS，并用 Tailwind v4 扫描组件包源码；React 19 为 peer 依赖。

## 用法与源码

下面代码与交互预览来自同一份组件示例；可以复制到宿主，或在上方展开编辑器修改并运行。

\`\`\`tsx
${doc.source}
\`\`\`

## 组合结构

${doc.parts.map(part => `- \`${part}\``).join('\n')}

## 状态与交互

预览工具栏可切换${doc.scenarios.map(s => ({ Default: '默认', Dark: '暗色', Disabled: '禁用', Error: '错误', Hover: '悬停' })[s.state]).join('、')}场景；切换场景会重置示例。点击、编辑或使用键盘直接操作组件，编辑代码后点击「运行」更新预览。

## API

以下属性直接取自公开 TypeScript 类型，包含继承属性和回调。默认值取源码解构或 JSDoc；「未指定」表示当前声明未提供默认值。

${doc.parts.map(part => apiTable(data.apis.find(api => api.name === part)!)).join('\n')}

## 键盘与无障碍

${doc.keyboard.map(key => `- ${key}`).join('\n') || '展示内容不接管键盘焦点。'}

为交互控件提供有意义的名称；弹层关闭后恢复触发器焦点，验证明暗两态下的键盘与读屏路径。

## 模型体验

组件清单通过 \`@idadawn/yue-core/manifest.json\` 分发；模型选择已登记属性、slots 与 events，函数回调由宿主提供。

## 已知限制与延期项

示例为本地交互，不连接业务后端。文案由消费方本地化；浏览器能力（如全屏）受宿主环境约束，失败通过组件回调处理。

## 变更

本页对应 \`@idadawn/yue-core@${data.version}\`；组件行为与示例以源码版本为准。
` }));
  const shared = data.apis.filter(api => !data.docs.some(doc => doc.parts.includes(api.name)));
  files.push({ path: 'yue/core/shared-api.mdx', content: `${front('通用上下文 API', '组件共用的上下文与公开属性。')}\n# 通用上下文 API\n\n${shared.map(apiTable).join('\n')}` });
  const nativeGroups = new Map(data.apis.filter(a => a.props.some(p => p.native)).map(a => [nativeId(a), a]));
  for (const [id, api] of nativeGroups) {
    const consumers = data.apis.filter(a => nativeId(a) === id).map(a => a.name);
    files.push({ path: `yue/api/native-${id}.mdx`, content: `${front('通用 DOM 属性 ' + api.name, '从真实 React TypeScript 声明提取的继承属性、回调与完整类型。').replace('\n---\n', '\nsidebar: false\n---\n')}\n# 通用 DOM 属性\n\n以下组件共享完全相同的继承属性与实例类型：${consumers.map(n => '`' + n + '`').join('、')}。\n\n${rows(api.props.filter(p => p.native))}\n` });
  }
  files.push({ path: 'yue/core/_meta.json', content: JSON.stringify([...data.docs.map(d => d.name.toLowerCase()), 'shared-api'], null, 2) });
  files.push({ path: 'yue/tokens.mdx', content: `${front('设计令牌', '查看 yue 三层设计令牌及明暗主题。', '@idadawn/yue-tokens')}\nimport { ComponentDemo } from '@theme';\n\n# 设计令牌\n\n三层令牌共用一个目录，切换明暗主题可查看语义色与组件令牌。\n\n<ComponentDemo name="Tokens" />\n\n## 模型体验\n\n引用语义令牌实现主题一致性。\n\n## 已知限制与延期项\n\n品牌值由宿主提供，组件不自行更换品牌。\n` });
  return files;
}
