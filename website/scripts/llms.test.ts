import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { renderLlmsTxt } from './llms.ts';

describe('llms.txt 按 project 分节（ADR-021 D21.5）', () => {
  it('先 Qian 再 Kun 再 yue，每个项目下按部分分节；没写 project 的页归 Qian', () => {
    const txt = renderLlmsTxt({
      title: 'Qian 文档',
      description: '一句话。',
      sections: [
        {
          title: '开始',
          pages: [
            { routePath: '/start/', link: '/start/index.md', title: '开始', description: '  两行\n摘要 ', frontmatter: { project: 'qian' } },
            { routePath: '/start/hosts', link: '/start/hosts.md', title: '宿主', frontmatter: {} },
          ],
        },
        {
          title: '组件目录',
          pages: [{ routePath: '/components/yue/', link: '/components/yue/index.md', title: 'yue 组件库', frontmatter: { project: 'yue' } }],
        },
        {
          title: 'SDK',
          pages: [
            { routePath: '/sdk/kun/', link: '/sdk/kun/index.md', title: 'Kun 框架', description: '镜像', frontmatter: { project: 'kun' } },
            { routePath: '/sdk/dotnet/', link: '/sdk/dotnet/index.md', title: '后端 SDK', frontmatter: { project: 'qian' } },
            { routePath: '/sdk/x', link: '/sdk/x.md', frontmatter: { project: 'unknown' } },
          ],
        },
        { title: '空的部分', pages: [] },
      ],
    });
    assert.equal(
      txt,
      [
        '# Qian 文档',
        '',
        '> 一句话。',
        '',
        '## Qian · 开始',
        '',
        '- [开始](/start/index.md): 两行 摘要',
        '- [宿主](/start/hosts.md)',
        '',
        '## Qian · SDK',
        '',
        '- [后端 SDK](/sdk/dotnet/index.md)',
        '- [/sdk/x](/sdk/x.md)',
        '',
        '## Kun · SDK',
        '',
        '- [Kun 框架](/sdk/kun/index.md): 镜像',
        '',
        '## yue · 组件目录',
        '',
        '- [yue 组件库](/components/yue/index.md)',
        '',
      ].join('\n'),
    );
  });

  it('没有标题与描述时从第一节开始', () => {
    assert.equal(renderLlmsTxt({ sections: [{ title: 'A', pages: [{ routePath: '/a', link: '/a.md', title: 'a' }] }] }), '## Qian · A\n\n- [a](/a.md)\n');
  });
});
