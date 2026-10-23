// yue 六层分层的依赖矩阵与 ESLint 边界规则（docs/prd/specs/yue.md §3.1；ADR-013 D13.2）。
// 唯一真相源：改分层先改这里，再改规格表；tests/gates/yue 的自测与 package.json 的依赖校验都读这个文件。
import boundaries from 'eslint-plugin-boundaries';
import tseslint from 'typescript-eslint';

/** 最底层横切包：任何包都可以依赖（它们自己不依赖任何 @yue 包）。 */
export const BOTTOM_LAYERS = ['tokens', 'contracts'];

/** 每个包允许直接依赖的 @yue 包（不含 BOTTOM_LAYERS，会自动补上）。键即 packages/yue/<目录> 与 @idadawn/yue-<目录>。 */
const DIRECT = {
  tokens: [],
  contracts: [],
  core: [],
  sdk: [],
  shell: ['core', 'sdk', 'form'], // shell → form 的登录表单依赖是同层唯一例外（规格 §3.1，经 ADR 记录）
  data: ['core', 'sdk'],
  form: ['core', 'sdk'],
  charts: ['core'],
  'code-editor': ['core'],
  biz: ['core', 'data', 'form', 'sdk'],
  widgets: ['core', 'data', 'form', 'biz', 'sdk'],
  runtime: ['widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  designer: ['runtime', 'widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  'designer-form': ['designer', 'runtime', 'widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  'designer-list': ['designer', 'runtime', 'widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  'designer-flow': ['designer', 'runtime', 'widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  'designer-portal': ['designer', 'runtime', 'widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  mobile: ['runtime', 'widgets', 'biz', 'shell', 'data', 'form', 'charts', 'code-editor', 'core', 'sdk'],
  agent: ['core', 'sdk'],
};

/** 全部已知的 @yue 包名（目录名）。 */
export const YUE_PACKAGES = Object.keys(DIRECT);

/** 某包允许依赖的 @yue 包目录名（含最底层横切包，不含自己）。未知包名 → 空数组（什么都不许依赖，先来这里登记）。 */
export function allowedDependencies(pkg) {
  const direct = DIRECT[pkg];
  if (!direct) return [];
  if (BOTTOM_LAYERS.includes(pkg)) return []; // tokens / contracts 不依赖任何 @yue 包，彼此也不
  return [...new Set([...direct, ...BOTTOM_LAYERS])].filter((d) => d !== pkg).sort();
}

const policies = [
  // 同一个包内部的相对引用随便引。
  { from: { element: { type: 'yue' } }, allow: { to: { element: { type: 'yue', captured: { pkg: '{{ from.captured.pkg }}' } } } } },
  // 每个包按矩阵引其它 @yue 包：解析到工作区源码的按元素匹配，没解析到的（包还没建）按 import 说明符匹配。
  ...YUE_PACKAGES.map((pkg) => {
    const allowed = allowedDependencies(pkg);
    return {
      from: { element: { type: 'yue', captured: { pkg } } },
      allow: {
        to: [
          ...(allowed.length ? [{ element: { type: 'yue', captured: { pkg: allowed } } }] : []),
          { module: { source: allowed.map((d) => `@idadawn/yue-${d}`) } },
        ],
      },
    };
  }),
  // 第三方库与 Node 内置模块对所有 yue 包放行（@idadawn/yue-* 不在此列，只走矩阵）。
  // Qian 专有的外壳接线（@qian/shell-kit）同样在 yue 之上：yue 包不得反向引用。
  { from: { element: { type: 'yue' } }, allow: { to: { module: { source: '!{@idadawn/yue-**,@idadawn/qian-plugin-sdk{,/**},@qian/plugin-sdk{,/**},@idadawn/qian-runtime{,/**},@qian/runtime{,/**},@qian/shell-kit{,/**}}' } } } },
  // Qian 的前端 SDK 在 yue 之上，只消费 sdk/底层契约与 TS Runtime，不能反向进入 UI 控件或应用。
  { from: { element: { type: 'qian-ui-sdk' } }, allow: { to: [{ element: { type: 'qian-ui-sdk' } }, { element: { type: 'qian-runtime' } },
    { element: { type: 'yue', captured: { pkg: ['sdk', 'tokens', 'contracts'] } } },
    { module: { source: ['@idadawn/yue-sdk{,/**}', '@idadawn/yue-tokens{,/**}', '@idadawn/yue-contracts{,/**}', '@idadawn/qian-runtime', '@qian/runtime'] } },
    { module: { source: '!{@idadawn/yue-**,@idadawn/qian-plugin-sdk{,/**},@qian/plugin-sdk{,/**},@qian/shell-kit{,/**}}' } }] } },
  // 应用面（apps/*、可部署的前端包、外壳接线 @qian/shell-kit 与参考应用的远程模块）可以引任何 yue 包、彼此与第三方库。
  { from: { element: { type: 'app' } }, allow: { to: [{ element: { type: 'yue' } }, { element: { type: 'app' } }, { element: { type: 'qian-ui-sdk' } }, { element: { type: 'qian-runtime' } }, { module: { source: ['!@idadawn/yue-**'] } }] } },
];

/** 只含边界规则的 ESLint 配置片段（根 eslint.config.mjs 与 eslint.boundaries.config.mjs 共用）。 */
export const yueBoundariesConfig = [
  {
    files: ['packages/yue/**/*.{ts,tsx,mts,mjs}', 'apps/**/*.{ts,tsx,mts,mjs}', 'packages/qian-tenancy-ui/**/*.{ts,tsx,mts,mjs}', 'packages/qian-plugin-sdk/**/*.{ts,tsx,mts,mjs}', 'packages/qian-identity-ui/**/*.{ts,tsx,mts,mjs}',
      'packages/qian-shell-kit/**/*.{ts,tsx,mts,mjs}', 'samples/qian-sample-crm-ui/**/*.{ts,tsx,mts,mjs}'],
    languageOptions: { parser: tseslint.parser, ecmaVersion: 'latest', sourceType: 'module' },
    plugins: { boundaries },
    settings: {
      'boundaries/elements': [
        { type: 'yue', pattern: 'packages/yue/*', capture: ['pkg'] },
        { type: 'app', pattern: 'apps/*', capture: ['app'] },
        { type: 'app', pattern: 'packages/qian-tenancy-ui' },
        { type: 'app', pattern: 'packages/qian-identity-ui' },
        { type: 'app', pattern: 'packages/qian-shell-kit' },
        { type: 'app', pattern: 'samples/qian-sample-crm-ui' },
        { type: 'qian-ui-sdk', pattern: 'packages/qian-plugin-sdk' },
        { type: 'qian-runtime', pattern: 'packages/qian-runtime' },
      ],
      'boundaries/dependency-nodes': ['import', 'dynamic-import', 'export'],
      'boundaries/ignore': ['**/node_modules/**', '**/dist/**'],
      'import/resolver': {
        typescript: { project: ['packages/yue/*/tsconfig.json', 'apps/*/tsconfig.json', 'packages/qian-tenancy-ui/tsconfig.json', 'packages/qian-plugin-sdk/tsconfig.json', 'packages/qian-identity-ui/tsconfig.json',
          'packages/qian-shell-kit/tsconfig.json', 'samples/qian-sample-crm-ui/tsconfig.json'] },
      },
    },
    rules: {
      'boundaries/dependencies': [
        'error',
        {
          default: 'disallow',
          checkAllOrigins: true,
          checkUnknownLocals: true,
          message: 'yue 分层违规：不得依赖 {{ dependency.source }}（矩阵在 packages/yue/boundaries.config.mjs，规格 docs/prd/specs/yue.md §3.1）',
          policies,
        },
      ],
      'boundaries/no-unknown-files': 'off',
    },
  },
];
