import {existsSync,lstatSync,mkdirSync,writeFileSync,unlinkSync} from 'node:fs';
import path from 'node:path';
import {renderDocumentationTemplates} from './documentation-templates.js';

/** skill-only 骨架实际消费同一组文档模板；代码组件模板由完整 init 任务接入。 */
export function initializeSkillOnly(root:string,id:string,name:string):string[]{
 const documents=renderDocumentationTemplates(id,name);
 const manifest={id,version:'0.1.0',description:`${name} 的声明式技能组件；具体能力与限制以 README 为准。`,author:{name:'组件作者'},license:'Apache-2.0',interface:{displayName:name,category:'operations'},inject:[],provides:[],policy:{tier:'private',sandbox:'none',authentication:'ON_INSTALL'},scopes:['global','tenant']};
 if(name.length>40||/[\r\n]/.test(name))throw new Error('组件展示名须为不超过 40 字的单行文本。');
 const files:Record<string,string>={...documents,'.qian-plugin/plugin.json':JSON.stringify(manifest,null,2)+'\n','.qian/config.yml':'defaultProfile: minimal\n','profiles/minimal/qian.profile.yml':'name: minimal\nbundles: []\npatch: ./qian.patch.yml\n','profiles/minimal/qian.patch.yml':'[]\n','skills/guide/SKILL.md':`---\nname: guide\ndescription: ${JSON.stringify(name+' 的只读使用指引，包含适用范围与已知限制。')}\n---\n使用前阅读组件 README，按其中的契约与限制提供指引。\n`};
 for(const file of Object.keys(files)){
  const target=path.join(root,file);if(existsSync(target))throw new Error('目标文件已存在；init 不覆盖已有内容。');
  let current=root;for(const part of file.split('/')){current=path.join(current,part);if(existsSync(current)&&lstatSync(current).isSymbolicLink())throw new Error('模板输出路径不能经过符号链接。');}
 }
 const created:string[]=[];
 try{for(const[file,content]of Object.entries(files)){const target=path.join(root,file);mkdirSync(path.dirname(target),{recursive:true});writeFileSync(target,content,{flag:'wx'});created.push(target);}}
 catch(error){for(const file of created.reverse())unlinkSync(file);throw error;}
 return Object.keys(files);
}
