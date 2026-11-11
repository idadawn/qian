import {readFileSync,readdirSync} from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {generateTokenCss,generateThemeCss,validateTokenRegistry,tokenContrasts,lintTokenCss,createThemeInitializationScript} from '../dist/index.js';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'../../../..'),canonical=path.join(root,'packages/yue/tokens/src/index.css'),errors=[...validateTokenRegistry()];
if(readFileSync(new URL('../src/theme.css',import.meta.url),'utf8')!==generateThemeCss())errors.push('主题映射不是目录的当前生成结果。');
if(readFileSync(canonical,'utf8')!==generateTokenCss())errors.push('令牌 CSS 不是目录的当前生成结果。');
if(readFileSync(new URL('../src/theme-init.js',import.meta.url),'utf8')!=='/* 由 theme.ts 生成的首帧脚本；勿手改。 */\n'+createThemeInitializationScript()+'\n')errors.push('首帧脚本不是当前生成结果。');
for(const pair of tokenContrasts())if(pair.ratio<pair.minimum)errors.push(`对比度不足：${pair.theme}/${pair.brand}/${pair.token} ${pair.ratio.toFixed(3)} < ${pair.minimum} (${pair.contrast})`);
// 正文字形按原型走浏览器缺省：令牌层不强制灰阶抗锯齿，应用与示例的样式表也不能再加回来（S1-YUE-019 验收发现过一次）。
const smoothing=/@apply[^;]*\bantialiased\b|-webkit-font-smoothing\s*:\s*antialiased|-moz-osx-font-smoothing\s*:\s*grayscale/;
function visit(directory,roles=true){for(const entry of readdirSync(directory,{withFileTypes:true})){if(['node_modules','dist','.turbo','test-results','playwright-report','.lighthouseci'].includes(entry.name))continue;const file=path.join(directory,entry.name);if(entry.isDirectory())visit(file,roles);else if(entry.name.endsWith('.css')){const css=readFileSync(file,'utf8');if(roles&&file!==canonical)errors.push(...lintTokenCss(css).map(error=>`${path.relative(root,file)}: ${error}`));if(smoothing.test(css.replace(/\/\*[\s\S]*?\*\//g,'')))errors.push(`${path.relative(root,file)}: 不要强制灰阶抗锯齿（antialiased / font-smoothing），正文字形用浏览器缺省。`);}}}
visit(path.join(root,'packages/yue'));visit(path.join(root,'apps'));
for(const consumer of ['packages/qian-tenancy-ui','packages/qian-tenancy-signup-ui','packages/qian-plugin-sdk','packages/qian-identity-ui','packages/qian-shell-kit','samples/qian-sample-crm-ui','website/demo'])visit(path.join(root,consumer),false);
if(errors.length){process.stderr.write(errors.join('\n')+'\n');process.exitCode=1;}
