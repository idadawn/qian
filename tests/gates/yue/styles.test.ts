import { readFileSync, readdirSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';
import manifest from '../../../packages/yue/core/src/manifest-catalog.json';
import reference from '../../../packages/yue/core/docs/visual-reference.json';

const root = path.resolve(import.meta.dirname, '../../..');
const prohibited = (source: string): string[] => source.match(/[\w:-]+-\[(?:oklch\()?var\(--[^\]]+\]|\b(?:hover:)?brightness-\d+/g) ?? [];
function sources(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap(entry => {
    if (['node_modules', 'dist', 'test-results', '.turbo'].includes(entry.name)) return [];
    const file = path.join(directory, entry.name);
    return entry.isDirectory() ? sources(file) : /\.[cm]?[jt]sx?$/.test(entry.name) && !entry.name.includes('.test.') ? [file] : [];
  });
}
describe('yue 视觉配方门禁', () => {
  it('拒绝绕过语义映射的变量颜色与 brightness 悬停；允许语义透明度、尺寸变量和固定焦点宽度', () => {
    expect(prohibited('hover:bg-[var(--primary)] border-[oklch(var(--yue-border))] hover:brightness-95')).toHaveLength(3);
    expect(prohibited('hover:bg-primary/90 ring-ring/50 h-(--yue-button-height) focus-visible:ring-[3px]')).toEqual([]);
  });
  it('生产源码与共用示例均使用语义类', () => {
    const violations = ['packages/yue', 'apps/yue-playground'].flatMap(directory => sources(path.join(root, directory))).flatMap(file => prohibited(readFileSync(file, 'utf8')).map(value => `${path.relative(root, file)}: ${value}`));
    expect(violations).toEqual([]);
  });
  it('51 个入口均登记官方基准的对应组件与有意偏离', () => {
    expect(Object.keys(reference.components).sort()).toEqual(Object.keys(manifest).sort());
    expect(reference.style).toBe('base-nova');
    for (const sources of Object.values(reference.components)) for (const source of sources) expect(reference.sources[source as keyof typeof reference.sources]).toMatch(/^[a-f0-9]{64}$/);
  });
});
