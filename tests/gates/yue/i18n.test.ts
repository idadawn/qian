import { Linter } from 'eslint';
import tseslint from 'typescript-eslint';
import { expect, it } from 'vitest';
import { noHardcodedText } from '../../../packages/yue/sdk/eslint-i18n.mjs';

it('文案门禁拒绝 JSX 文字、可访问名称与中文常量，允许 key 和注入的 label', () => {
  const linter = new Linter();
  const check = (code: string) => linter.verify(code, [{ files: ['**/*.tsx'], languageOptions: { parser: tseslint.parser, parserOptions: { ecmaFeatures: { jsx: true } } }, plugins: { local: { rules: { i18n: noHardcodedText } } }, rules: { 'local/i18n': 'error' } }], { filename: 'component.tsx' });
  expect(check('const x = <button>Save</button>')).not.toHaveLength(0);
  expect(check('const x = <input aria-label="Name" />')).not.toHaveLength(0);
  expect(check('const label = "保存"')).not.toHaveLength(0);
  expect(check('const x = <button aria-label={label}>{t("common.save")}</button>')).toEqual([]);
  expect(check('const x = <span aria-hidden>⌄</span>')).toEqual([]);
});
