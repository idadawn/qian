// 把生成器接进 Rspress：config 钩子在扫描路由之前执行（dev 与 build 都是），生成的页面因此能被路由与侧栏收录。
import type { RspressPlugin } from '@rspress/core';
import { apiReferenceFiles, buildKunApi } from './build-kun-api.ts';
import { collectComponents } from './collect-components.ts';
import { openKun, readKunVersion } from './kun.ts';
import { SITE_DOCS, writeGenerated, type GeneratedPart, type WriteResult } from './repo.ts';
import { KUN_HANDWRITTEN, syncKunDocs } from './sync-kun-docs.ts';
import { syncCli, syncDesign } from './sync-specs.ts';

export interface GenerateReport {
  results: Record<GeneratedPart, WriteResult>;
  /** kun 文档的上游问题（链接越界、表格少列等），已按规则处理，供维护者到 kun 仓库修。 */
  warnings: string[];
  kun: string;
}

export function generateAll(siteDocs: string = SITE_DOCS): GenerateReport {
  // Kun 小节：版本缺失、tag 不存在都在这里失败（docs-site.md §2b.1）
  const kun = openKun(readKunVersion());
  const api = buildKunApi(kun);
  const docs = syncKunDocs(kun, api.packages.length);
  return {
    results: {
      components: writeGenerated('components', collectComponents(), siteDocs),
      cli: writeGenerated('cli', syncCli(), siteDocs),
      design: writeGenerated('design', syncDesign(), siteDocs),
      'sdk/kun': writeGenerated('sdk/kun', docs.files, siteDocs, KUN_HANDWRITTEN),
      'sdk/api': writeGenerated('sdk/api', apiReferenceFiles(kun, api), siteDocs),
    },
    warnings: docs.warnings,
    kun: `Kun ${kun.tag}：文档 ${docs.pages.length + 1} 页，API ${api.packages.length} 个包 ${api.namespaces} 个命名空间 ${api.types} 个类型`,
  };
}

export function summarize(report: GenerateReport): string {
  const parts = Object.entries(report.results)
    .map(([part, r]) => `${part}/ 写入 ${r.written.length}、删除 ${r.removed.length}、未变 ${r.unchanged}`)
    .join('；');
  const warnings = report.warnings.map((w) => `\n  [kun] ${w}`).join('');
  return `${parts}\n${report.kun}${warnings}`;
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
