import { ESLint } from 'eslint';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

// 分层边界门禁自测（S1-YUE-003 验收第 1 条）：用根 eslint.boundaries.config.mjs 对虚拟文件路径 lint，
// 文件不必真的存在——元素分类只看路径，import 说明符解析不到的按说明符匹配。
const ROOT = path.resolve(__dirname, '../../..');
const eslint = new ESLint({ cwd: ROOT, overrideConfigFile: path.join(ROOT, 'eslint.boundaries.config.mjs') });

async function lint(file: string, code: string) {
  const [result] = await eslint.lintText(code, { filePath: path.join(ROOT, file) });
  return (result?.messages ?? []).map((m) => `${m.ruleId}: ${m.message}`);
}

describe('yue 分层边界（docs/prd/specs/yue.md §3.1）', () => {
  it('@yue/biz 引用 @yue/designer 之类的向上依赖被拒绝', async () => {
    const messages = await lint('packages/yue/biz/src/__violation__.ts', "import { Designer } from '@yue/designer';\nexport const x = Designer;\n");
    expect(messages).toHaveLength(1);
    expect(messages[0]).toMatch(/^boundaries\/dependencies: /);
    expect(messages[0]).toContain('@yue/designer');
  });

  it('已建的包之间按矩阵：core 引 data 被拒绝，biz 引 core / data / form / sdk / tokens 放行', async () => {
    expect(await lint('packages/yue/core/src/__violation__.ts', "export { Tree } from '@yue/data';\n")).toEqual([expect.stringMatching(/^boundaries\/dependencies: .*@yue\/data/)]);
    expect(
      await lint(
        'packages/yue/biz/src/__ok__.ts',
        "import { cn } from '@yue/core';\nimport { Tree } from '@yue/data';\nimport { PACKAGE_NAME as form } from '@yue/form';\nimport { PACKAGE_NAME as sdk } from '@yue/sdk';\nimport { PACKAGE_NAME as tokens } from '@yue/tokens';\nexport const all = [cn, Tree, form, sdk, tokens];\n",
      ),
    ).toEqual([]);
  });

  it('同层互不依赖：data 引 form 被拒绝；shell 引 form 是登记过的唯一例外', async () => {
    expect(await lint('packages/yue/data/src/__violation__.ts', "export { PACKAGE_NAME } from '@yue/form';\n")).toHaveLength(1);
    expect(await lint('packages/yue/shell/src/__ok__.ts', "export { PACKAGE_NAME } from '@yue/form';\n")).toEqual([]);
  });

  it('横切包只能依赖 tokens / contracts；最底层什么 @yue 包都不能引', async () => {
    expect(await lint('packages/yue/sdk/src/__ok__.ts', "export { PACKAGE_NAME } from '@yue/contracts';\n")).toEqual([]);
    expect(await lint('packages/yue/sdk/src/__violation__.ts', "export { cn } from '@yue/core';\n")).toHaveLength(1);
    expect(await lint('packages/yue/tokens/src/__violation__.ts', "export { PACKAGE_NAME } from '@yue/contracts';\n")).toHaveLength(1);
  });

  it('第三方库与包内相对引用放行；应用面可以引任何 yue 包；没登记的包名什么都不许引', async () => {
    expect(await lint('packages/yue/core/src/components/__ok__.tsx', "import { clsx } from 'clsx';\nimport { cn } from '../lib/cn';\nexport const x = [clsx, cn];\n")).toEqual([]);
    expect(await lint('apps/yue-playground/src/__ok__.tsx', "import { Tree } from '@yue/data';\nimport { cn } from '@yue/core';\nexport const x = [Tree, cn];\n")).toEqual([]);
    expect(await lint('packages/yue/unregistered/src/__violation__.ts', "export { PACKAGE_NAME } from '@yue/tokens';\n")).toHaveLength(1);
  });
});
