import { readFile, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import ts from 'typescript';
import { updatePropsMetadata } from './manifest-metadata.mjs';

// 机器契约的属性形状仍由目录维护；标题和说明只从属性名与真实 TypeScript 注释派生。
const root = fileURLToPath(new URL('../', import.meta.url));
const source = new URL('../src/manifest-catalog.json', import.meta.url);
const original = await readFile(source, 'utf8');
const catalog = JSON.parse(original);
const config = ts.readConfigFile(path.join(root, 'tsconfig.json'), ts.sys.readFile);
const parsed = ts.parseJsonConfigFileContent(config.config, ts.sys, root);
const entry = path.join(root, 'src/index.ts');
const program = ts.createProgram([entry], parsed.options);
const checker = program.getTypeChecker();
const exports = checker.getExportsOfModule(checker.getSymbolAtLocation(program.getSourceFile(entry)));
for (const [name, item] of Object.entries(catalog)) {
  const exported = exports.find(symbol => symbol.name === name);
  if (!exported) throw new Error(`未导出组件 ${name}`);
  const symbol = exported.flags & ts.SymbolFlags.Alias ? checker.getAliasedSymbol(exported) : exported;
  const declaration = symbol.valueDeclaration ?? symbol.declarations?.[0];
  const call = checker.getSignaturesOfType(checker.getTypeOfSymbolAtLocation(symbol, declaration), ts.SignatureKind.Call)[0];
  const parameter = call?.getParameters()[0];
  if (!parameter) throw new Error(`无法解析 ${name} 的 props`);
  const props = checker.getTypeOfSymbolAtLocation(parameter, declaration);
  updatePropsMetadata(item.props, props, checker, name);
}
const generated = JSON.stringify(catalog, null, 2) + '\n';
if (process.argv.includes('--generate')) {
  await writeFile(source, generated);
} else {
  if (original !== generated) throw new Error('manifest 属性注释已过期，请运行 pnpm --filter @idadawn/yue-core manifest:generate');
  if (!process.argv.includes('--check')) {
    const { name, version } = JSON.parse(await readFile(new URL('../package.json', import.meta.url), 'utf8'));
    await writeFile(new URL('../dist/yue-manifest.json', import.meta.url), JSON.stringify(Object.values(catalog).map(item => ({ ...item, package: name, version })), null, 2) + '\n');
  }
}
