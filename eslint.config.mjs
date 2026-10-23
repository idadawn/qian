// 前端工作区（packages/yue/*、apps/*）的 ESLint 配置：代码规则 + yue 分层边界。
// 只跑边界门禁用 eslint.boundaries.config.mjs；文档站 website/ 不在此列。
import js from '@eslint/js';
import reactHooks from 'eslint-plugin-react-hooks';
import globals from 'globals';
import tseslint from 'typescript-eslint';
import { yueBoundariesConfig } from './packages/yue/boundaries.config.mjs';
import { noHardcodedText } from './packages/yue/sdk/eslint-i18n.mjs';

export default tseslint.config(
  {
    ignores: ['**/node_modules/**', '**/dist/**', 'website/**', '**/.turbo/**', '**/playwright-report/**', '**/test-results/**', '**/.lighthouseci/**', 'tests/gates/yue/fixtures/**'],
  },
  {
    files: ['packages/yue/**/*.{ts,tsx,mts}', 'apps/**/*.{ts,tsx,mts}', 'packages/qian-tenancy-ui/**/*.{ts,tsx,mts}', 'packages/qian-plugin-sdk/**/*.{ts,tsx,mts}', 'packages/qian-identity-ui/**/*.{ts,tsx,mts}'],
    extends: [js.configs.recommended, ...tseslint.configs.recommended, reactHooks.configs.flat.recommended],
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
    rules: {
      '@typescript-eslint/consistent-type-imports': ['error', { fixStyle: 'inline-type-imports' }],
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_', varsIgnorePattern: '^_' }],
      '@typescript-eslint/no-explicit-any': 'error',
      'no-console': ['error', { allow: ['warn', 'error'] }],
    },
  },
  {
    files: ['packages/yue/**/*.mjs', 'apps/**/*.mjs', 'packages/qian-tenancy-ui/**/*.mjs', 'packages/qian-plugin-sdk/**/*.mjs', 'packages/qian-identity-ui/**/*.mjs', '**/*.cjs'],
    extends: [js.configs.recommended],
    languageOptions: { globals: { ...globals.node } },
  },
  ...yueBoundariesConfig,
  {
    files: ['packages/yue/{sdk,shell,data,form,biz}/src/**/*.{ts,tsx}', 'packages/qian-tenancy-ui/src/**/*.{ts,tsx}', 'packages/qian-identity-ui/src/**/*.{ts,tsx}'],
    ignores: ['**/*.test.ts', '**/*.test.tsx', '**/test/**', '**/*.generated.ts', '**/i18n.ts', '**/locales.ts'],
    plugins: { 'yue-i18n': { rules: { 'no-hardcoded-text': noHardcodedText } } },
    rules: { 'yue-i18n/no-hardcoded-text': 'error' },
  },
);
