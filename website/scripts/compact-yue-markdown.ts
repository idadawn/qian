// SSG-MD 会按最长类型为整张表补空格；去掉对齐填充，保留每个单元格与代码块。
import fs from 'node:fs';
import path from 'node:path';
import { listFiles, WEBSITE_ROOT } from './repo.ts';
export function compactTables(markdown: string): string {
  let fence = '';
  return markdown.split('\n').map(line => {
    const marker = /^\s*(`{3,}|~{3,})/.exec(line)?.[1];
    if (marker) { if (!fence) fence = marker; else if (marker[0] === fence[0] && marker.length >= fence.length) fence = ''; return line; }
    if (fence || !line.startsWith('|') || !line.trimEnd().endsWith('|')) return line;
    const cells = line.trimEnd().split(/(?<!\\)\|/);
    return `| ${cells.slice(1, -1).map(cell => cell.trim()).join(' | ')} |`;
  }).join('\n');
}
export function compactYueMarkdown(root = path.join(WEBSITE_ROOT, 'doc_build')) {
  const yue = path.join(root, 'components/yue');
  for (const rel of listFiles(yue).filter(file => file.endsWith('.md'))) {
    const file = path.join(yue, rel); fs.writeFileSync(file, compactTables(fs.readFileSync(file, 'utf8')));
  }
  const full = path.join(root, 'llms-full.txt');
  fs.writeFileSync(full, compactTables(fs.readFileSync(full, 'utf8')));
}
if (process.argv[1]?.endsWith('compact-yue-markdown.ts')) compactYueMarkdown();
