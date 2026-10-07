// 把生成器接进 Rspress：config 钩子在扫描路由之前执行（dev 与 build 都是），生成的页面因此能被路由与侧栏收录。
import type { RspressPlugin } from '@rspress/core';
import { collectComponents } from './collect-components.ts';
import { SITE_DOCS, writeGenerated, type GeneratedPart, type WriteResult } from './repo.ts';
import { syncCli, syncDesign } from './sync-specs.ts';

export function generateAll(siteDocs: string = SITE_DOCS): Record<GeneratedPart, WriteResult> {
  return {
    components: writeGenerated('components', collectComponents(), siteDocs),
    cli: writeGenerated('cli', syncCli(), siteDocs),
    design: writeGenerated('design', syncDesign(), siteDocs),
  };
}

export function summarize(results: Record<GeneratedPart, WriteResult>): string {
  return Object.entries(results)
    .map(([part, r]) => `${part}/ 写入 ${r.written.length}、删除 ${r.removed.length}、未变 ${r.unchanged}`)
    .join('；');
}

export function qianDocsSources(): RspressPlugin {
  return {
    name: 'qian-docs-sources',
    config(config) {
      console.log(`[qian-docs] 生成目录：${summarize(generateAll())}`);
      return config;
    },
  };
}
