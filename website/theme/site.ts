// 站点常量：主题（浏览器）与生成脚本（Node）共用，只放纯数据。
export const REPO_URL = 'https://github.com/idadawn/qian';
export const DEFAULT_BRANCH = 'main';
export const LICENSE_NAME = 'Apache-2.0';
export const LICENSE_URL = 'https://www.apache.org/licenses/LICENSE-2.0';

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
