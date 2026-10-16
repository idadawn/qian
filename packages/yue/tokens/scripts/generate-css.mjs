import {readFileSync,writeFileSync} from 'node:fs';
import {generateTokenCss,generateThemeCss,tokenContrasts,validateTokenRegistry,createThemeInitializationScript} from '../dist/index.js';
const init='/* 由 theme.ts 生成的首帧脚本；勿手改。 */\n'+createThemeInitializationScript()+'\n';
const css=generateTokenCss(),source=new URL('../src/index.css',import.meta.url),dist=new URL('../dist/index.css',import.meta.url);
const errors=[...validateTokenRegistry(),...tokenContrasts().filter(pair=>pair.ratio<pair.minimum).map(pair=>`${pair.theme}/${pair.brand}/${pair.token}: ${pair.ratio.toFixed(3)} < ${pair.minimum}`)];
const theme=generateThemeCss(),themeSource=new URL('../src/theme.css',import.meta.url),themeDist=new URL('../dist/theme.css',import.meta.url);
if(process.argv.includes('--check')){if(readFileSync(themeSource,'utf8')!==theme)errors.push('主题映射过期；运行 tokens:generate。');if(readFileSync(source,'utf8')!==css)errors.push('CSS 过期；运行 tokens:generate。');}
else{writeFileSync(themeSource,theme);writeFileSync(themeDist,theme);writeFileSync(source,css);writeFileSync(dist,css);writeFileSync(new URL('../src/theme-init.js',import.meta.url),init);writeFileSync(new URL('../dist/theme-init.js',import.meta.url),init);}
if(errors.length){process.stderr.write(errors.join('\n')+'\n');process.exitCode=1;}
