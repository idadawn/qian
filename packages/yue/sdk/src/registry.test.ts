import { describe, expect, it } from 'vitest';
import { createRegistries, type WidgetMeta } from './registry';

const widget: WidgetMeta = { type: 'qian.input', version: '0.1.0', name: 'widget.input', category: 'basic', props: { type: 'object' }, valueType: 'string', events: ['change'], devices: ['pc', 'h5'], capabilities: { bindsField: true, canBeColumn: true, canBeSearch: true, canBeGroup: false, canBeInSubtable: true, supportsReadonly: true, participatesInLinkage: true }, defaultOperators: ['eq'], authAware: true, i18nKeys: ['widget.input'], a11y: { role: 'textbox', labelledBy: 'label' }, tokens: [] };

describe('注册表', () => {
  it('八类注册表导出纯 JSON，运行时代码不进入目录', () => {
    const registry = createRegistries();
    for (const key of ['navigation', 'settings', 'commands', 'shortcuts', 'previewers', 'menuTypes'] as const) registry[key].register({ meta: { id: key, title: `registry.${key}` }, implementation: () => undefined });
    registry.clientActions.register({ meta: { id: 'open', title: 'action.open', description: '打开记录', permission: 'record:read', parameters: { type: 'object' } }, implementation: () => undefined });
    registry.registerWidget({ meta: widget, Runtime: () => null });
    const json = JSON.parse(JSON.stringify(registry));
    expect(Object.keys(json)).toHaveLength(8);
    expect(json.widgets[0].type).toBe('qian.input');
    expect(json.clientActions[0].parameters).toEqual({ type: 'object' });
    expect(JSON.stringify(json)).not.toContain('implementation');
    expect(registry.listWidgets({ device: 'miniapp' })).toEqual([]);
    expect(registry.listWidgets({ category: 'basic', device: 'pc' })).toHaveLength(1);
  });

  it('拒绝重复注册，释放幂等；旧释放函数不删除后来注册的条目', () => {
    const { navigation } = createRegistries();
    const definition = { meta: { id: 'home', title: 'nav.home' } };
    const dispose = navigation.register(definition);
    expect(() => navigation.register(definition)).toThrow('sdk.error.registryConflict');
    dispose();
    navigation.register(definition);
    dispose();
    expect(navigation.list()).toHaveLength(1);
  });

  it('每个宿主隔离，目录快照修改不会污染注册表', () => {
    const first = createRegistries();
    const second = createRegistries();
    first.navigation.register({ meta: { id: 'home', title: 'nav.home' } });
    first.navigation.list()[0]!.title = 'changed';
    first.navigation.get('home')!.meta.title = 'changed';
    expect(first.navigation.list()[0]!.title).toBe('nav.home');
    expect(second.navigation.list()).toEqual([]);
  });

  it('目录元数据拒绝不可序列化值', () => {
    const { navigation } = createRegistries();
    expect(() => navigation.register({ meta: { id: 'bad', title: 'title', order: Number.NaN } })).toThrow('sdk.error.registryMetadata');
  });
});
