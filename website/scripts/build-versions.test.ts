import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { describe, it } from 'node:test';
import { VERSIONS_PAGE, buildVersions, readQianPackages, readVersionMatrix, readYuePackages, renderVersionsPage } from './build-versions.ts';
import { splitFrontmatter } from './repo.ts';

function write(root: string, rel: string, content: string): void {
  const target = path.join(root, rel);
  fs.mkdirSync(path.dirname(target), { recursive: true });
  fs.writeFileSync(target, content);
}

function fixtureRepo(): string {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'qian-versions-'));
  write(root, 'Directory.Packages.props', '<Project>\n  <PropertyGroup Label="Kun">\n    <KunVersion>2026.10.8</KunVersion>\n  </PropertyGroup>\n</Project>\n');
  write(root, 'Directory.Build.props', '<Project>\n  <PropertyGroup>\n    <IsPackable>false</IsPackable>\n  </PropertyGroup>\n</Project>\n');
  write(root, 'packages/Qian.Runtime/Qian.Runtime.csproj', '<Project>\n  <PropertyGroup>\n    <IsPackable>true</IsPackable>\n  </PropertyGroup>\n</Project>\n');
  write(root, 'packages/Qian.Data/Qian.Data.csproj', '<Project><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>\n');
  write(root, 'packages/Qian.Sample/Qian.Sample.csproj', '<Project><PropertyGroup><IsPackable>false</IsPackable></PropertyGroup></Project>\n');
  write(root, 'packages/Qian.Runtime.Tests/other.csproj', '<Project><PropertyGroup><IsPackable>true</IsPackable></PropertyGroup></Project>\n');
  write(root, 'global.json', '{ "sdk": { "version": "10.0.100", "rollForward": "latestFeature" } }\n');
  write(root, 'package.json', '{ "packageManager": "pnpm@12.9.1", "engines": { "node": "^20.19.0 || >=22.12.0" } }\n');
  write(root, 'pnpm-workspace.yaml', 'packages:\n  - website\n  - packages/yue/*\n');
  write(root, 'website/package.json', '{ "name": "website", "version": "0.0.0" }\n');
  write(root, 'deploy/baseline.json', '{ "k3s": "v1.35" }\n');
  return root;
}

describe('版本矩阵（docs-site.md §2b.4）', () => {
  it('从钉版文件读出矩阵：Kun、可打包的 Qian.*、.NET SDK、Node / pnpm、K3s；没有 @yue 包', () => {
    const root = fixtureRepo();
    const m = readVersionMatrix(root);
    assert.equal(m.kun, '2026.10.8');
    assert.deepEqual(m.qian, [
      { name: 'Qian.Data', version: null },
      { name: 'Qian.Runtime', version: null },
    ]);
    assert.deepEqual(m.yue, []);
    assert.equal(m.dotnetSdk, '10.0.100');
    assert.equal(m.dotnetRollForward, 'latestFeature');
    assert.equal(m.node, '^20.19.0 || >=22.12.0');
    assert.equal(m.pnpm, '12.9.1');
    assert.equal(m.k3s, 'v1.35');
    assert.equal(m.sources.kun, 'Directory.Packages.props');
  });

  it('Qian 版本取 csproj 或 Directory.Build.props；yue 包按工作区通配找（@idadawn/yue-* 与简称 @yue/* 都认）', () => {
    const root = fixtureRepo();
    write(root, 'Directory.Build.props', '<Project><PropertyGroup><VersionPrefix>0.1.0</VersionPrefix></PropertyGroup></Project>\n');
    write(root, 'packages/Qian.Data/Qian.Data.csproj', '<Project><PropertyGroup><IsPackable>true</IsPackable><Version>0.2.0</Version></PropertyGroup></Project>\n');
    assert.deepEqual(readQianPackages(root), [
      { name: 'Qian.Data', version: '0.2.0' },
      { name: 'Qian.Runtime', version: '0.1.0' },
    ]);
    write(root, 'packages/yue/core/package.json', '{ "name": "@idadawn/yue-core", "version": "0.1.0" }\n');
    write(root, 'packages/yue/tokens/package.json', '{ "name": "@yue/tokens" }\n');
    write(root, 'packages/yue/tools/package.json', '{ "name": "yue-tools", "version": "1.0.0" }\n');
    assert.deepEqual(readYuePackages(root), [
      { name: '@idadawn/yue-core', version: '0.1.0' },
      { name: '@yue/tokens', version: null },
    ]);
  });

  it('生成页：frontmatter 合规、矩阵一行、来源表、包清单；未发布与未建立写明', () => {
    const root = fixtureRepo();
    const file = buildVersions(root);
    assert.equal(file.path, VERSIONS_PAGE);
    const { frontmatter, body } = splitFrontmatter(file.content);
    assert.match(frontmatter, /^title: "版本矩阵"$/m);
    assert.match(frontmatter, /^part: start$/m);
    assert.match(frontmatter, /^project: qian$/m);
    assert.match(frontmatter, /^audience: \[developer, operator\]$/m);
    assert.match(frontmatter, /^updated: \d{4}-\d{2}-\d{2}$/m);
    assert.match(body, /^# 版本矩阵$/m);
    assert.match(body, /不要手改/);
    assert.ok(body.includes('| main（开发版） | 2026.10.8 | 未发布，开发版 main（2 个包） | 未建立（S1-YUE-003 起） | 10.0.100（rollForward latestFeature） | ^20.19.0 \\|\\| >=22.12.0 / 12.9.1 | v1.35 |'), body);
    assert.match(body, /\| Kun \| `Directory\.Packages\.props` \|/);
    assert.match(body, /- `Qian\.Runtime`（未发布）/);
    assert.match(body, /- （未建立，S1-YUE-003 起）/);
    const released = renderVersionsPage({ ...readVersionMatrix(root), qian: [{ name: 'Qian.Runtime', version: '0.1.0' }, { name: 'Qian.Data', version: '0.1.0' }], yue: [{ name: '@yue/core', version: '0.3.0' }], k3s: null }, '2026-10-09');
    assert.ok(released.includes('| 0.1.0（2 个包） | 0.3.0（1 个包） |'), released);
    assert.ok(released.includes('| 未钉版 |'), released);
  });

  it('global.json 缺 sdk.version 即失败', () => {
    const root = fixtureRepo();
    write(root, 'global.json', '{ "sdk": {} }\n');
    assert.throws(() => readVersionMatrix(root), /sdk\.version/);
  });
});
