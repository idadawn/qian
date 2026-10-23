import { describe, expect, it, vi } from 'vitest';
import { readFileSync } from 'node:fs';
import { Ajv2020 } from 'ajv/dist/2020.js';
import { PlatformClient, ShellHost, SLOT_IDS, slotContracts, validateSlot, type RemoteBridge, type RemoteModule, type RemoteRegistration, type ShellContext, type SlotDescriptor } from '../src/index';

const nav: SlotDescriptor = { slot: 'qian.shell.nav/v1', id: 'tenants', title: '租户', description: '管理当前租户的配置', permission: 'tenant:read', route: '/tenants', expose: 'Page' };
const action: SlotDescriptor = { slot: 'qian.shell.client-action/v1', id: 'refresh', title: '刷新', description: '刷新当前列表中的租户记录', permission: 'tenant:read', expose: 'Page', parameters: { type: 'object', additionalProperties: false, required: ['force'], properties: { force: { type: 'boolean' } } } };
function registration(scope: RemoteRegistration['scope'] = 'global', id = 'qian.test'): RemoteRegistration {
  return { id, version: '1.0.0', scope, manifest: { $schema: 'https://qian.dev/schemas/ui/v2', federation: { manifest: '/remote/mf-manifest.json', exposes: ['Page'] }, engines: { yue: '^0.1.0' }, contributions: [nav, action], bridge: { slots: [...SLOT_IDS], navigate: ['/tenants'], notify: true, capabilities: [{ id: 'qian.test/save', permission: 'tenant:write', sideEffect: 'write' }] } } };
}
async function setup(module: RemoteModule = { default: 'Page' }) {
  let context: ShellContext = { tenant: { id: 'a', name: '甲' }, membership: { id: 'member-a', permissions: ['tenant:read', 'tenant:write'] }, app: { id: 'app', name: '应用' }, locale: 'zh-CN', theme: 'light' };
  const requests: Request[] = [];
  const transport = vi.fn(async (request: Request) => { requests.push(request); return Response.json({ saved: true }); });
  const client = new PlatformClient({ baseUrl: 'https://shell.test', getTenantId: () => context.tenant?.id, transport });
  const federation = { registerRemotes: vi.fn(), loadRemote: vi.fn(async <T>() => module as T), preloadRemote: vi.fn(async () => {}), removeRemote: vi.fn() };
  const navigate = vi.fn(), notify = vi.fn(), audit = vi.fn(async () => {});
  const isMounted = vi.fn(() => true), isEntitled = vi.fn(() => true);
  const host = new ShellHost({ yueVersion: '0.1.0', baseUrl: 'https://shell.test', client, federation, getContext: () => context, isMounted, isEntitled,
    hasPermission: (permission) => context.membership?.permissions.includes(permission) ?? false, navigate, notify, audit });
  await host.start();
  return { host, federation, navigate, notify, audit, requests, transport, isMounted, isEntitled, context, setContext: (value: ShellContext) => { context = value; } };
}
describe('真实 Qian 插件树与 MF2 加载边界', () => {
  it('六个插槽各有带版本的 Schema，拒绝未声明的字段与坏类型', () => {
    expect(Object.keys(slotContracts)).toEqual([...SLOT_IDS]);
    for (const slot of SLOT_IDS) {
      expect(slotContracts[slot]?.$id).toBe(`https://qian.dev/schemas/${slot}`);
      expect(() => validateSlot({ ...nav, slot, parameters: action.parameters })).not.toThrow();
      expect(() => validateSlot({ ...nav, slot, permission: 1 })).toThrow();
      expect(() => validateSlot({ ...nav, slot, hidden: true })).toThrow();
    }
  });
  it('导航目录不发请求，按需加载使用真实 Apply/provide/unmount，深只读快照隔离宿主', async () => {
    const t = await setup(); t.host.registerRemote(registration('tenant:a'));
    expect(t.host.catalog(nav.slot)).toHaveLength(1); expect(t.federation.loadRemote).not.toHaveBeenCalled();
    await t.host.loadSlot(nav.slot, nav.id);
    const item = t.host.list(nav.slot)[0]!;
    expect(t.host.dumpConfig().map(({ plugin }) => plugin)).toEqual(['qian.shell', 'qian.test']);
    const snapshot = item.bridge.getContext();
    expect(Object.isFrozen(snapshot)).toBe(true); expect(Object.isFrozen(snapshot.membership?.permissions)).toBe(true);
    expect(Reflect.set(snapshot.tenant!, 'id', 'evil')).toBe(false);
    t.context.tenant!.name = '已更新'; expect(snapshot.tenant!.name).toBe('甲');
    await t.host.unloadRemote('qian.test', 'tenant:a');
    expect(t.host.list(nav.slot)).toEqual([]); expect(t.host.dumpConfig()).toHaveLength(1);
    expect(() => item.bridge.getContext()).toThrow('NOT_FOUND'); await t.host.dispose();
  });
  it('global 与 tenant 同名贡献遮蔽；卸载租户贡献恢复全局，跨租户不见本地贡献', async () => {
    const t = await setup(); t.host.registerRemote(registration());
    await t.host.loadSlot(nav.slot);
    t.host.registerRemote(registration('tenant:a', 'qian.custom'));
    await t.host.loadSlot(nav.slot);
    expect(t.host.list(nav.slot).map(({ remoteId }) => remoteId)).toEqual(['qian.custom']);
    t.setContext({ ...t.context, tenant: { id: 'b', name: '乙' } });
    expect(t.host.list(nav.slot).map(({ remoteId }) => remoteId)).toEqual(['qian.test']);
    t.setContext(t.context);
    await t.host.unloadRemote('qian.custom', 'tenant:a');
    expect(t.host.list(nav.slot).map(({ remoteId }) => remoteId)).toEqual(['qian.test']); await t.host.dispose();
  });
  it('预加载与目录采用相同遮蔽：不请求被遮蔽全局，租户权限撤销不回落到全局绕过', async () => {
    const t = await setup(); t.host.registerRemote(registration()); await t.host.loadSlot(nav.slot);
    const tenant = registration('tenant:a', 'qian.custom'); tenant.manifest.contributions![0] = { ...nav, permission: 'tenant:custom' };
    t.context.membership!.permissions.push('tenant:custom'); t.host.registerRemote(tenant);
    t.federation.preloadRemote.mockClear(); await t.host.preloadSlot(nav.slot);
    expect(t.federation.preloadRemote).toHaveBeenCalledOnce();
    const registered = t.federation.registerRemotes.mock.calls.at(-1)![0]![0]!;
    expect(t.federation.preloadRemote).toHaveBeenCalledWith([{ nameOrAlias: registered.name, exposes: ['Page'] }]);
    await t.host.loadSlot(nav.slot);
    t.context.membership!.permissions = ['tenant:read'];
    expect(t.host.catalog(nav.slot)).toEqual([]); expect(t.host.list(nav.slot)).toEqual([]);
    t.federation.preloadRemote.mockClear(); await t.host.preloadSlot(nav.slot); expect(t.federation.preloadRemote).not.toHaveBeenCalled();
    await t.host.unloadRemote('qian.custom', 'tenant:a'); expect(t.host.catalog(nav.slot).map((item) => item.remoteId)).toEqual(['qian.test']); expect(t.host.list(nav.slot).map((item) => item.remoteId)).toEqual(['qian.test']); await t.host.dispose();
  });
  it.each(['isMounted', 'isEntitled'] as const)('%s 为假，load/preload 连 manifest 都不请求', async (check) => {
    const t = await setup(); t[check].mockReturnValue(false); t.host.registerRemote(registration());
    await t.host.loadSlot(nav.slot); await t.host.preloadSlot(nav.slot);
    expect(t.federation.registerRemotes).not.toHaveBeenCalled(); expect(t.federation.loadRemote).not.toHaveBeenCalled(); expect(t.federation.preloadRemote).not.toHaveBeenCalled(); await t.host.dispose();
  });
  it('不满足 engines.yue 在任何网络请求前留下结构化控制台与必需审计记录', async () => {
    const t = await setup(); const remote = registration(); remote.manifest.engines.yue = '^9.0.0'; t.host.registerRemote(remote);
    const consoleError = vi.spyOn(console, 'error').mockImplementation(() => {});
    try {
      await expect(t.host.loadSlot(nav.slot)).rejects.toMatchObject({ code: 'INPUT_INVALID', pointer: '/engines/yue' });
      expect(consoleError).toHaveBeenCalledOnce(); expect(t.audit).toHaveBeenCalledWith(expect.objectContaining({ remoteId: 'qian.test', reason: 'engines.yue', code: 'INPUT_INVALID' }));
      expect(t.federation.registerRemotes).not.toHaveBeenCalled();
      t.audit.mockRejectedValueOnce(new Error('审计失败'));
      await expect(t.host.preloadSlot(nav.slot)).rejects.toThrow('审计失败'); expect(t.federation.preloadRemote).not.toHaveBeenCalled();
    } finally { consoleError.mockRestore(); await t.host.dispose(); }
  });
  it('等待网络期间切换租户，不把已下载模块挂入新的租户', async () => {
    const t = await setup(); t.host.registerRemote(registration('tenant:a'));
    let resolve!: (module: RemoteModule) => void;
    t.federation.loadRemote.mockImplementationOnce(() => new Promise((done) => { resolve = done; }) as never);
    const loaded = t.host.loadSlot(nav.slot);
    await vi.waitFor(() => expect(t.federation.loadRemote).toHaveBeenCalledOnce());
    t.setContext({ ...t.context, tenant: { id: 'b', name: '乙' } }); resolve({ default: 'Page' });
    await loaded; expect(t.host.dumpConfig()).toHaveLength(1); expect(t.host.list(nav.slot)).toEqual([]); await t.host.dispose();
  });
  it('严格校验 manifest，不接受可执行 URL、跨源、重复和未声明 expose', async () => {
    const t = await setup();
    for (const url of ['javascript:alert(1)', 'https://evil.test/mf-manifest.json', 'https://user:secret@shell.test/mf-manifest.json']) {
      const remote = registration(); remote.manifest.federation.manifest = url; expect(() => t.host.registerRemote(remote)).toThrow('INPUT_INVALID');
    }
    const remote = registration(); remote.manifest.contributions!.push(nav); expect(() => t.host.registerRemote(remote)).toThrow('INPUT_INVALID');
    await t.host.dispose();
  });
});
describe('桥接身份、scope、权限与客户端动作', () => {
  it('伪造注册远程 ID 的对象仍被拒绝；公开入口不暴露注册方法', async () => {
    const t = await setup(); t.host.registerRemote(registration()); await t.host.loadSlot(nav.slot);
    expect(() => t.host.bridge.getContext({ remoteId: 'qian.test', scope: 'global' })).toThrow('NOT_FOUND');
    expect(Object.keys(t.host.bridge)).toEqual(['getContext', 'navigate', 'notify', 'invoke', 'registerClientAction']); await t.host.dispose();
  });
  it('导航、通知和命令只能在清单授权内；permission 撤销立即生效', async () => {
    const t = await setup(); t.host.registerRemote(registration('tenant:a')); await t.host.loadSlot(nav.slot); const bridge = t.host.list(nav.slot)[0]!.bridge;
    expect(() => bridge.navigate('https://evil.test')).toThrow('INSUFFICIENT_SCOPE'); expect(() => bridge.notify('info', '')).toThrow('INPUT_INVALID');
    bridge.navigate('/tenants'); expect(t.navigate).toHaveBeenCalledWith('/tenants');
    bridge.navigate('qian:a:qian.test:page:%2Ftenants'); expect(t.navigate).toHaveBeenLastCalledWith('/tenants');
    expect(() => bridge.navigate('qian:b:qian.test:page:%2Ftenants')).toThrow('NOT_FOUND');
    await expect(bridge.invoke('qian.other/save', {})).rejects.toThrow('NOT_FOUND');
    t.context.membership!.permissions = ['tenant:read']; await expect(bridge.invoke('qian.test/save', {})).rejects.toThrow('NOT_FOUND'); expect(t.requests).toHaveLength(0);
    t.setContext({ ...t.context, tenant: { id: 'b', name: '乙' } }); expect(() => bridge.getContext()).toThrow('NOT_FOUND'); await t.host.dispose();
  });
  it('没有声明 command scope 的远程即使知道能力 ID 也无法调用', async () => {
    const t = await setup(); const remote = registration(); remote.manifest.bridge.slots = ['qian.shell.nav/v1', 'qian.shell.client-action/v1']; t.host.registerRemote(remote); await t.host.loadSlot(nav.slot);
    await expect(t.host.list(nav.slot)[0]!.bridge.invoke('qian.test/save', {})).rejects.toThrow('INSUFFICIENT_SCOPE'); expect(t.requests).toHaveLength(0); await t.host.dispose();
  });
  it('invoke 使用同一个 PlatformClient 与拦截器、租户头及指定幂等键', async () => {
    const t = await setup(); t.host.registerRemote(registration()); await t.host.loadSlot(nav.slot);
    const key = '11111111-1111-4111-8111-111111111111';
    expect(await t.host.list(nav.slot)[0]!.bridge.invoke('qian.test/save', { name: '保存' }, { idempotencyKey: key })).toEqual({ saved: true });
    expect(t.requests[0]!.url).toBe('https://shell.test/api/capabilities/qian.test/save/invoke');
    expect(t.requests[0]!.headers.get('Idempotency-Key')).toBe(key); expect(t.requests[0]!.headers.get('X-Tenant-Id')).toBe('a'); await t.host.dispose();
  });
  it('实际冻结写能力 inputSchema 接受自动幂等键，header/body 唯一一致且冲突不发请求', async () => {
    const fixture = JSON.parse(readFileSync(new URL('../../../contracts/v0/fixtures/capability-entry.valid.json', import.meta.url), 'utf8'))[0].data;
    const ajv = new Ajv2020(); ajv.addFormat('uuid', /^[0-9a-f]{8}-[0-9a-f]{4}-[1-8][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i);
    const validate = ajv.compile(fixture.inputSchema);
    const t = await setup(); t.context.membership!.permissions.push(fixture.requiredPermission);
    const remote = registration(); remote.manifest.bridge.capabilities = [{ id: fixture.id, permission: fixture.requiredPermission, sideEffect: fixture.sideEffect }];
    t.transport.mockImplementation(async (request) => {
      t.requests.push(request); const body = await request.json();
      expect(validate(body)).toBe(true); expect(body.idempotencyKey).toBe(request.headers.get('Idempotency-Key'));
      return Response.json({ fileId: 'accepted' });
    });
    t.host.registerRemote(remote); await t.host.loadSlot(nav.slot); const bridge = t.host.list(nav.slot)[0]!.bridge;
    const input = { content: 'aGVsbG8=', fileName: 'hello.txt', purpose: 'attachment' };
    await expect(bridge.invoke(fixture.id, input)).resolves.toEqual({ fileId: 'accepted' }); expect(input).not.toHaveProperty('idempotencyKey');
    const key = '11111111-1111-4111-8111-111111111111';
    await bridge.invoke(fixture.id, { ...input, idempotencyKey: key });
    await expect(bridge.invoke(fixture.id, { ...input, idempotencyKey: key }, { idempotencyKey: '22222222-2222-4222-8222-222222222222' })).rejects.toThrow('INPUT_INVALID');
    expect(t.requests).toHaveLength(2); await t.host.dispose();
  });
  it('客户端动作验证 JSON Schema、导出名称/权限/描述，卸载同时撤销动作和旧执行引用', async () => {
    const executed = vi.fn(); let bridge!: RemoteBridge;
    const t = await setup({ default: 'Page', activate: (value) => { bridge = value; return bridge.registerClientAction({ id: action.id, title: action.title, description: action.description, permission: action.permission, parameters: action.parameters! }, executed); } });
    t.host.registerRemote(registration()); await t.host.loadSlot(nav.slot);
    const catalog = t.host.exportClientActions(); expect(JSON.parse(JSON.stringify(catalog))[0]).toMatchObject({ id: 'refresh', permission: 'tenant:read', parameters: action.parameters });
    const execute = t.host.list<(input: unknown) => unknown>('qian.shell.client-action/v1')[0]!.implementation;
    expect(() => execute({ force: 'true' })).toThrow('INPUT_INVALID'); execute({ force: true }); expect(executed).toHaveBeenCalledWith({ force: true });
    await t.host.unloadRemote('qian.test', 'global'); expect(t.host.exportClientActions()).toEqual([]); expect(() => execute({ force: true })).toThrow('NOT_FOUND'); expect(() => bridge.getContext()).toThrow('NOT_FOUND'); await t.host.dispose();
  });
  it('Apply 失败撤销已注册贡献与桥接句柄，允许后续干净重试', async () => {
    let bridge!: RemoteBridge; const t = await setup({ default: 'Page', activate: (value) => { bridge = value; throw new Error('初始化失败'); } });
    t.host.registerRemote(registration()); await expect(t.host.loadSlot(nav.slot)).rejects.toThrow('初始化失败');
    expect(t.host.list(nav.slot)).toEqual([]); expect(t.host.dumpConfig()).toHaveLength(1); expect(() => bridge.getContext()).toThrow('NOT_FOUND'); await t.host.dispose();
  });
});
