import fs from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import { YUE_PACKAGES, allowedDependencies } from '../../../packages/yue/boundaries.config.mjs';

// 包形状门禁：每个 @yue 包的 package.json 与矩阵、catalog、发布形状一致（docs/prd/specs/yue.md §3.1、§4.1；PRD §07 7.15.2）。
const ROOT = path.resolve(__dirname, '../../..');
const YUE_DIR = path.join(ROOT, 'packages', 'yue');

interface Pkg {
  name: string;
  version: string;
  private?: boolean;
  type?: string;
  exports?: Record<string, unknown>;
  publishConfig?: { access?: string; exports?: Record<string, unknown> };
  dependencies?: Record<string, string>;
  devDependencies?: Record<string, string>;
  peerDependencies?: Record<string, string>;
  scripts?: Record<string, string>;
  files?: string[];
}

const dirs = fs
  .readdirSync(YUE_DIR, { withFileTypes: true })
  .filter((d) => d.isDirectory() && fs.existsSync(path.join(YUE_DIR, d.name, 'package.json')))
  .map((d) => d.name);
const packages = dirs.map((dir) => ({ dir, pkg: JSON.parse(fs.readFileSync(path.join(YUE_DIR, dir, 'package.json'), 'utf8')) as Pkg }));

describe('packages/yue/* 的 package.json', () => {
  it('至少建立了 S1 的包，目录名与包名一致且都登记在分层矩阵里', () => {
    expect(dirs).toEqual(expect.arrayContaining(['tokens', 'contracts', 'core', 'sdk', 'shell', 'data', 'form', 'biz']));
    for (const { dir, pkg } of packages) {
      expect(pkg.name).toBe(`@idadawn/yue-${dir}`);
      expect(YUE_PACKAGES).toContain(dir);
    }
  });

  it('dependencies 里的 @yue 包不超出矩阵允许范围，且用 workspace 协议', () => {
    for (const { dir, pkg } of packages) {
      const allowed = allowedDependencies(dir);
      for (const [name, range] of Object.entries(pkg.dependencies ?? {})) {
        if (!name.startsWith('@idadawn/yue-')) continue;
        expect(allowed, `${pkg.name} 不得依赖 ${name}`).toContain(name.slice('@idadawn/yue-'.length));
        expect(range).toMatch(/^workspace:/);
      }
    }
  });

  it('所有 @yue 包同一版本、ESM、开发期 exports 指源码、publishConfig 指 dist、第三方依赖走 catalog', () => {
    const versions = new Set(packages.map((p) => p.pkg.version));
    expect(versions.size).toBe(1);
    for (const { pkg } of packages) {
      expect(pkg.private).toBeUndefined();
      expect(pkg.type).toBe('module');
      expect(pkg.exports?.['.']).toBe('./src/index.ts');
      expect(pkg.publishConfig?.access).toBe('restricted');
      expect(pkg.publishConfig?.exports?.['.']).toEqual({ types: './dist/index.d.ts', default: './dist/index.js' });
      expect(pkg.files).toContain('dist');
      expect(pkg.scripts?.build).toBe('rslib build');
      expect(pkg.scripts?.publint).toMatch(/^publint/);
      expect(pkg.peerDependencies?.react).toBeDefined();
      for (const [name, range] of Object.entries({ ...pkg.dependencies, ...pkg.devDependencies })) {
        if (name.startsWith('@idadawn/yue-')) continue;
        expect(range, `${pkg.name} 的 ${name} 要写 catalog:`).toBe('catalog:');
      }
    }
  });
});
