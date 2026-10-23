import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { SLOT_IDS, validateManifest } from '../src/index';
import { uiSchema } from '../src/ui-schema.generated';

interface UiCase { name: string; uiFile?: string; uiRuntimeValid?: boolean; files: Record<string, string> }
const vectors = JSON.parse(readFileSync(new URL('../../../tck/surfaces/cases.json', import.meta.url), 'utf8')) as UiCase[];

describe('同源 ui/v2 与运行态支持边界', () => {
  it('生成副本、插槽词表与公共真相源一致', () => {
    expect(uiSchema).toEqual(JSON.parse(readFileSync(new URL('../../../schemas/v2/ui.schema.json', import.meta.url), 'utf8')));
    expect(uiSchema.$defs.slotId.enum).toEqual(SLOT_IDS);
  });
  for (const vector of vectors.filter((item) => item.uiFile)) {
    it(vector.name, () => {
      const value = JSON.parse(vector.files[vector.uiFile!]!);
      if (vector.uiRuntimeValid) expect(() => validateManifest(value)).not.toThrow();
      else expect(() => validateManifest(value)).toThrow();
    });
  }
  it('静态描述缺 bridge、widgets 和 settingsCards 不会在加载时被静默忽略', () => {
    const source = JSON.parse(vectors.find((item) => item.name === 'ui-bridge-contributions')!.files['ui/manifest.json']!);
    delete source.contributions;
    const missing = structuredClone(source); delete missing.bridge;
    expect(() => validateManifest(missing)).toThrow(expect.objectContaining({ pointer: '/bridge' }));
    expect(() => validateManifest({ ...source, widgets: [{ type: 'qian.widget', expose: 'Widget' }] })).toThrow(expect.objectContaining({ pointer: '/widgets' }));
    expect(() => validateManifest({ ...source, settingsCards: [{ expose: 'Settings', scope: 'tenant', permission: 'qian.test:settings:read' }] })).toThrow(expect.objectContaining({ pointer: '/settingsCards' }));
  });
});
