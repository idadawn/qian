// Rspress 同源 iframe 资源；它是文档站的一部分，不是独立应用或部署入口。
import fs from 'node:fs';
import path from 'node:path';
import { createRequire } from 'node:module';
import { createRsbuild } from '@rsbuild/core';
import { pluginReact } from '@rsbuild/plugin-react';
import { pluginTailwindcss } from '@rsbuild/plugin-tailwindcss';
import { readYueDocs } from './yue-docs.ts';
import { WEBSITE_ROOT } from './repo.ts';
const require = createRequire(import.meta.url);
const cache = path.join(WEBSITE_ROOT, '.cache/yue');
fs.mkdirSync(cache, { recursive: true });
const data = readYueDocs();
fs.writeFileSync(path.join(cache, 'sources.json'), JSON.stringify(Object.fromEntries(data.docs.map(d => [d.name, d.source]))));
const vendor = path.join(WEBSITE_ROOT, 'docs/public/yue-vendor');
fs.rmSync(vendor, { recursive: true, force: true });
fs.mkdirSync(vendor, { recursive: true });
fs.copyFileSync(require.resolve('@babel/standalone/babel.min.js'), path.join(vendor, 'babel.min.js'));
fs.cpSync(path.join(path.dirname(require.resolve('monaco-editor/package.json')), 'min/vs'), path.join(vendor, 'vs'), {
  recursive: true,
  // 工作区只提供中/英界面；英语内置，保留简繁中文资源，不携带其它语言包。
  filter: source => { const locale = /^nls\.messages\.(.+)\.js$/.exec(path.basename(source))?.[1]; return !locale || ['zh-cn', 'zh-tw'].includes(locale); },
});
fs.copyFileSync(require.resolve('monaco-editor/LICENSE'), path.join(vendor, 'monaco-LICENSE'));
fs.copyFileSync(require.resolve('@babel/standalone/LICENSE'), path.join(vendor, 'babel-LICENSE'));
const rsbuild = await createRsbuild({ rsbuildConfig: {
  root: WEBSITE_ROOT,
  server: { publicDir: false },
  plugins: [pluginReact(), pluginTailwindcss()],
  source: { entry: { index: path.join(WEBSITE_ROOT, 'demo/main.tsx') } },
  resolve: { alias: { 'yue-demo-sources': path.join(cache, 'sources.json') } },
  output: { distPath: { root: path.join(WEBSITE_ROOT, 'docs/public/yue-demo') }, assetPrefix: './', cleanDistPath: true, copy: [] },
  html: { title: 'yue 交互演示', template: path.join(WEBSITE_ROOT, 'demo/index.html') },
} });
await rsbuild.build();
