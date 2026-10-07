import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { apiReferenceFiles, convertDocfx, docfxAnchor, htmlToMarkdown, kunPackages, parseDocfxPages, type DocfxOutput } from './build-kun-api.ts';
import { checkKunPages } from './check-site.ts';
import type { KunCheckout } from './kun.ts';
import { splitFrontmatter } from './repo.ts';

const kun: KunCheckout = { dir: '/tmp/kun', version: '2026.9.13', tag: 'v2026.9.13', commit: 'abc', date: '2026-09-13' };

// DocFX 2.81 `outputFormat: markdown` 的样例（结构照真实输出缩写）
const NAMESPACE = `# <a id="Kun_Core_Results"></a> Namespace Kun.Core.Results

### Classes

 [Error](Kun.Core.Results.Error.md)

强类型错误。

### Structs

 [Result<T\\>](Kun.Core.Results.Result\\-1.md)

带返回值的 Result。
`;

const RESULT = `# <a id="Kun_Core_Results_Result_1"></a> Struct Result<T\\>

Namespace: [Kun.Core.Results](Kun.Core.Results.md)
Assembly: Kun.Core.dll

带返回值的 Result。失败时携带 <xref href="Kun.Core.Results.Result%601.Error" data-throw-if-not-resolved="false"></xref>，
见 <xref href="Kun.Core.Results.Error" data-throw-if-not-resolved="false"></xref> 与 <xref href="System.Threading.Tasks.Task%601" data-throw-if-not-resolved="false"></xref>。

<p>
<b>注意</b>：<code>Result&lt;T&gt;</code> 不抛异常，<see cref="O:DbContext.SaveChanges"></see> 之外。
</p>

<ul><li>一 <i>甲</i></li><li>二 <a href="https://example.com">链接</a></li></ul>

<pre><code class="lang-csharp">var r = Result&lt;int&gt;.Ok(1);</code></pre>

\`\`\`csharp
public readonly struct Result<T> : IEquatable<Result<T>>
\`\`\`

## Properties

### <a id="Kun_Core_Results_Result_1_Error"></a> Error

见 [Error](Kun.Core.Results.Error.md\\#Kun\\_Core\\_Results\\_Error\\_None) 与 [Program](Program.md)。
`;

const ERROR = `# <a id="Kun_Core_Results_Error"></a> Class Error

Namespace: [Kun.Core.Results](Kun.Core.Results.md)
Assembly: Kun.Core.dll

强类型错误。

### <a id="Kun_Core_Results_Error_None"></a> None
`;

const PROGRAM = `# <a id="Program"></a> Class Program

Namespace:
Assembly: Kun.Gateway.dll

Auto-generated.
`;

const output: DocfxOutput = {
  pages: {
    'Kun.Core.Results': NAMESPACE,
    'Kun.Core.Results.Result-1': RESULT,
    'Kun.Core.Results.Error': ERROR,
    Program: PROGRAM,
  },
  descriptions: { 'Kun.Core': 'Core abstractions | Result' },
  warnings: 3,
};

describe('Kun API 参考', () => {
  it('包清单取 tag 上 src/<名>/<名>.csproj', () => {
    assert.deepEqual(
      kunPackages(['src/Kun.Core/Kun.Core.csproj', 'src/Kun.Core/README.md', 'src/Kun.Mediator/Kun.Mediator.csproj', 'tests/Kun.Core.Tests/Kun.Core.Tests.csproj', 'src/x/y.csproj']),
      ['Kun.Core', 'Kun.Mediator'],
    );
  });

  it('解析 DocFX 页面：命名空间页与类型页的路径，全局命名空间不进站', () => {
    const pages = parseDocfxPages(output.pages);
    assert.deepEqual(
      pages.map((p) => [p.stem, p.uid, p.path, p.route]).sort(),
      [
        ['Kun.Core.Results', 'Kun.Core.Results', 'kun-core-results/index.md', '/sdk/api/kun/kun-core-results/'],
        ['Kun.Core.Results.Error', 'Kun.Core.Results.Error', 'kun-core-results/error.md', '/sdk/api/kun/kun-core-results/error'],
        ['Kun.Core.Results.Result-1', 'Kun.Core.Results.Result`1', 'kun-core-results/result-1.md', '/sdk/api/kun/kun-core-results/result-1'],
      ],
    );
  });

  it('成员锚点与 DocFX 一致', () => {
    assert.equal(
      docfxAnchor('Kun.Core.Results.ResultExtensions.Bind``2(Kun.Core.Results.Result{``0},System.Func{``0,Kun.Core.Results.Result{``1}})'),
      'Kun_Core_Results_ResultExtensions_Bind__2_Kun_Core_Results_Result___0__System_Func___0_Kun_Core_Results_Result___1___',
    );
  });

  it('HTML 转 Markdown：段落、列表、强调、代码、see、a', () => {
    const md = htmlToMarkdown('<p>\n<b>B</b> <i>I</i> <code>List&lt;int&gt;</code> <see cref="T:X.Y"></see>\n</p><ul><li>x</li><li>y</li></ul><pre><code class="lang-csharp">f&lt;int&gt;()</code></pre>', () => '');
    assert.match(md, /\*\*B\*\* \*I\* `List<int>` `X\.Y`/);
    assert.match(md, /\n- x\n- y\n/);
    assert.match(md, /```csharp\nf<int>\(\)\n```/);
    assert.doesNotMatch(md, /<\/?(p|ul|li|b|i|code|pre|see)\b/);
  });

  it('转换类型页：xref、页面链接、锚点、横幅与 frontmatter', () => {
    const result = convertDocfx(kun, output, ['Kun.Core', 'Kun.Gateway']);
    const page = result.files.find((f) => f.path === 'kun-core-results/result-1.md')?.content ?? '';
    const { frontmatter, body } = splitFrontmatter(page);
    assert.match(frontmatter, /^title: "Result<T>（Struct）"$/m);
    assert.match(frontmatter, /^description: "带返回值的 Result。失败时携带 Result<T>\.Error，见 Error 与 Task。"$/m);
    assert.match(frontmatter, /^project: kun$/m);
    assert.match(frontmatter, /^source: "idadawn\/kun@v2026\.9\.13:src\/Kun\.Core"$/m);
    assert.match(body, /^# Struct Result<T\\>\n\n:::info 生成页\n/m);
    assert.match(body, /（`v2026\.9\.13`）/);
    assert.match(body, /\[Result<T\\>\.Error\]\(\/sdk\/api\/kun\/kun-core-results\/result-1#Kun_Core_Results_Result_1_Error\)/);
    assert.match(body, /\[Error\]\(\/sdk\/api\/kun\/kun-core-results\/error\)/);
    assert.match(body, /\[`Task`\]\(https:\/\/learn\.microsoft\.com\/dotnet\/api\/system\.threading\.tasks\.task-1\)/);
    assert.match(body, /\[Error\]\(\/sdk\/api\/kun\/kun-core-results\/error#Kun_Core_Results_Error_None\)/);
    assert.match(body, /^### Error \{#Kun_Core_Results_Result_1_Error\}$/m);
    assert.match(body, /与 Program。/);
    assert.match(body, /\[链接\]\(https:\/\/example\.com\)/);
    assert.doesNotMatch(body, /<xref|<see |<a id=|<\/?(p|ul|li|b|i)>/);
    assert.match(body, /```csharp\npublic readonly struct Result<T> : IEquatable<Result<T>>\n```/);
  });

  it('命名空间页与概览：包表格（说明里的竖线转义）、无侧栏文件', () => {
    const result = convertDocfx(kun, output, ['Kun.Core', 'Kun.Gateway']);
    const ns = result.files.find((f) => f.path === 'kun-core-results/index.md')?.content ?? '';
    assert.match(ns, /^description: "命名空间 Kun\.Core\.Results：2 个公开类型（Kun\.Core）"$/m);
    assert.match(ns, /\[Result<T\\>\]\(\/sdk\/api\/kun\/kun-core-results\/result-1\)/);
    const index = result.files.find((f) => f.path === 'index.md')?.content ?? '';
    assert.match(index, /\| \[`Kun\.Core`\]\(https:\/\/www\.nuget\.org\/packages\/Kun\.Core\/2026\.9\.13\) \| Core abstractions \\\| Result \| \[Kun\.Core\.Results\]\(\/sdk\/api\/kun\/kun-core-results\/\) \|/);
    assert.match(index, /\| \[`Kun\.Gateway`\]\(https:\/\/www\.nuget\.org\/packages\/Kun\.Gateway\/2026\.9\.13\) \| — \| — \|/);
    assert.equal(result.files.filter((f) => f.path.endsWith('_meta.json')).length, 0);
    assert.equal(result.types, 2);
    assert.equal(result.namespaces, 1);
  });

  it('sdk/api/ 落地页列出 Kun，Qian.* 与前端标注随 S1-DOCS-002', () => {
    const files = apiReferenceFiles(kun, convertDocfx(kun, output, ['Kun.Core']));
    const landing = files.find((f) => f.path === 'index.md')?.content ?? '';
    assert.match(landing, /^project: qian$/m);
    assert.match(landing, /\[Kun API 参考\]\(\/sdk\/api\/kun\/\)/);
    assert.match(landing, /S1-DOCS-002/);
    assert.ok(files.some((f) => f.path === 'kun/kun-core-results/result-1.md'));
  });
});

describe('构建产物里的 Kun 版本检查', () => {
  const banner = (tag: string) => `<p>本页镜像自 kun 仓库 <a><code>docs/x.md</code></a>（<code>${tag}</code>）。</p>`;
  const files = ['sdk/kun/index.html', 'sdk/kun/with-qian.html', 'sdk/api/kun/index.html', 'start/index.html'];

  it('横幅里的 tag 与 KunVersion 一致时通过，手写页不查', () => {
    const html: Record<string, string> = { 'sdk/kun/index.html': banner('v2026.9.13'), 'sdk/api/kun/index.html': banner('v2026.9.13') };
    assert.deepEqual(checkKunPages(files, (rel) => html[rel] ?? '', '2026.9.13'), []);
  });

  it('tag 不一致、或页面不是镜像页即失败', () => {
    const html: Record<string, string> = { 'sdk/kun/index.html': banner('v2026.6.12'), 'sdk/api/kun/index.html': '<p>手加的页面</p>' };
    const errors = checkKunPages(files, (rel) => html[rel] ?? '', '2026.9.13');
    assert.equal(errors.length, 2, errors.join('\n'));
    assert.match(errors[0] ?? '', /v2026\.6\.12 与 KunVersion 2026\.9\.13 不一致/);
    assert.match(errors[1] ?? '', /不是镜像页也不是手写页/);
  });
});
