// 循环依赖门禁（pnpm yue:circular）：packages/yue/* 与 apps/* 的 import 图不得有环、不得引到解析不到的模块。
// 分层方向由 eslint-plugin-boundaries 管（packages/yue/boundaries.config.mjs），这里不重复。
/** @type {import('dependency-cruiser').IConfiguration} */
module.exports = {
  forbidden: [
    {
      name: 'no-circular',
      severity: 'error',
      comment: '模块之间不得有循环依赖（docs/prd/specs/yue.md §8「工程化」、PRD §14 C14）',
      from: {},
      to: { circular: true },
    },
    {
      name: 'not-to-unresolvable',
      severity: 'error',
      comment: 'import 的模块必须能解析到（包没装、路径写错、还没建的 @yue 包）',
      from: {},
      to: { couldNotResolve: true },
    },
  ],
  options: {
    includeOnly: '^(packages/yue|packages/qian-tenancy-ui|packages/qian-identity-ui|apps)/',
    exclude: { path: ['node_modules', '/dist/', '\\.d\\.ts$'] },
    doNotFollow: { path: ['node_modules'] },
    tsPreCompilationDeps: true,
    enhancedResolveOptions: {
      exportsFields: ['exports'],
      conditionNames: ['import', 'module', 'default', 'types'],
      mainFields: ['module', 'main', 'types'],
      extensions: ['.ts', '.tsx', '.mts', '.js', '.mjs', '.cjs', '.json', '.css'],
    },
    reporterOptions: { text: { highlightFocused: true } },
  },
};
