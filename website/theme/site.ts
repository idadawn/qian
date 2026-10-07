// 站点常量：主题（浏览器）与生成脚本（Node）共用，只放纯数据。
export const REPO_URL = 'https://github.com/idadawn/qian';
export const DEFAULT_BRANCH = 'main';

/** 产品族仓库（ADR-021 D21.2，所有者统一为 idadawn），首页页脚按此顺序列出；没有 url 的显示为「待建」。 */
export const FAMILY_REPOS: readonly { name: string; url?: string; note?: string }[] = [
  { name: 'Qian', url: REPO_URL },
  { name: 'Kun', url: 'https://github.com/idadawn/kun' },
  { name: 'yue', url: REPO_URL, note: 'Qian 仓库内' },
  { name: 'qian-plugins' }, // 官方市场仓库 idadawn/qian-plugins，建仓后补 url
];

/** 许可证（docs-site.md §2a.1）：Qian（含 yue）Apache-2.0，Kun MIT。 */
export const LICENSES = [
  { project: 'Qian', name: 'Apache-2.0', url: 'https://www.apache.org/licenses/LICENSE-2.0' },
  { project: 'Kun', name: 'MIT', url: 'https://opensource.org/license/mit' },
] as const;

/** 第 2、3、4 部分随 Qian 包族的 SemVer 多版本发布，其余部分只有当前版（docs-site.md §5）。 */
export const VERSIONED_PARTS = [
  { prefix: '/components/', name: '组件目录' },
  { prefix: '/sdk/', name: 'SDK' },
  { prefix: '/cli/', name: 'CLI 与规格' },
] as const;

/** 七个部分（docs-site.md §2）：导航、站点地图按此顺序。 */
export const PARTS = [
  { prefix: '/start/', name: '开始' },
  { prefix: '/components/', name: '组件目录' },
  { prefix: '/sdk/', name: 'SDK' },
  { prefix: '/cli/', name: 'CLI 与规格' },
  { prefix: '/ops/', name: '运维' },
  { prefix: '/marketplace/', name: '市场与 ISV' },
  { prefix: '/design/', name: '设计与路线' },
] as const;
