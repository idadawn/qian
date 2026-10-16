import {readFileSync,readdirSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {generateTokenCss,generateThemeCss,validateTokenRegistry,tokenContrasts,lintTokenCss,createThemeInitializationScript} from '../dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../../..'),canonical=path.join(root,'packages/yue/tokens/src/index.css'),errors=[...validateTokenRegistry()];
if(readFileSync(new URL('../src/theme.css',import.meta.url),'utf8')!==generateThemeCss())errors.push('主题映射不是目录的当前生成结果。');
if(readFileSync(canonical,'utf8')!==generateTokenCss())errors.push('令牌 CSS 不是目录的当前生成结果。');
if(readFileSync(new URL('../src/theme-init.js',import.meta.url),'utf8')!=='/* 由 theme.ts 生成的首帧脚本；勿手改。 */\n'+createThemeInitializationScript()+'\n')errors.push('首帧脚本不是当前生成结果。');
for(const pair of tokenContrasts())if(pair.ratio<pair.minimum)errors.push(`对比度不足：${pair.theme}/${pair.brand}/${pair.token} ${pair.ratio.toFixed(3)} < ${pair.minimum} (${pair.contrast})`);
function visit(directory){for(const entry of readdirSync(directory,{withFileTypes:true})){if(['node_modules','dist','.turbo','test-results','playwright-report','.lighthouseci'].includes(entry.name))continue;const file=path.join(directory,entry.name);if(entry.isDirectory())visit(file);else if(entry.name.endsWith('.css')&&file!==canonical)errors.push(...lintTokenCss(readFileSync(file,'utf8')).map(error=>`${path.relative(root,file)}: ${error}`));}}
visit(path.join(root,'packages/yue'));visit(path.join(root,'apps'));
if(errors.length){process.stderr.write(errors.join('\n')+'\n');process.exitCode=1;}
