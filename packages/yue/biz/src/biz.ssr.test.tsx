import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { renderToString } from 'react-dom/server';
import { describe, expect, it } from 'vitest';
import { createMockTransport, PlatformClient } from '@idadawn/yue-sdk';
import { ApiSelect, AuthorizationTree, DictSelect, IconPicker, OrganizeSelect, RoleSelect, UserSelect, bizLanguagePacks, createBizI18n, orgTypeDictionary } from './index';

describe('SSR', () => {
  it('七个组件可在 Node 里渲染首帧（加载态），渲染期间不发请求', () => {
    // 没有登记任何路由：渲染期间若有请求会立即失败（取数只在 effect 里经 PlatformClient 发起）。
    const client = new PlatformClient({ baseUrl: 'https://tenant.test', transport: createMockTransport([]) });
    const i18n = createBizI18n();
    const html = renderToString(<>
      <OrganizeSelect client={client} i18n={i18n} label="部门" value={null} onChange={() => {}} />
      <OrganizeSelect client={client} i18n={i18n} label="归属" multiple value={[]} onChange={() => {}} />
      <UserSelect client={client} i18n={i18n} label="成员" value={null} onChange={() => {}} />
      <RoleSelect client={client} i18n={i18n} label="角色" multiple value={[]} onChange={() => {}} />
      <DictSelect client={client} i18n={i18n} label="类型" dictionary={orgTypeDictionary} value={null} onChange={() => {}} />
      <ApiSelect<string> client={client} i18n={i18n} label="远程" value={null} onChange={() => {}} load={async () => []} getValue={(item) => item} getLabel={(item) => item} />
      <AuthorizationTree client={client} i18n={i18n} label="权限" value={[]} onChange={() => {}} />
      <IconPicker i18n={i18n} label="图标" value="building" onChange={() => {}} />
    </>);
    for (const slot of ['organize-select', 'user-select', 'role-select', 'dict-select', 'api-select', 'authorization-tree', 'icon-picker']) expect(html).toContain(`data-slot="${slot}"`);
    expect(html).toContain('data-testid="yue-user-select-trigger"');
    expect(html).toContain('加载中');
  });
});

// 静态检查：biz 的产品源码只有 PlatformClient 一条取数路径；用到的文案 key 都已登记在三种语言包里。
const root = fileURLToPath(new URL('.', import.meta.url));
const sources = (readdirSync(root, { recursive: true }) as string[])
  .filter((file) => /\.(ts|tsx)$/.test(file) && !/\.test\.tsx?$/.test(file) && !file.split(/[\\/]/).includes('test'))
  .map((file) => ({ file, text: readFileSync(join(root, file), 'utf8') }));

describe('源码约束', () => {
  it('覆盖了全部组件源码', () => {
    expect(sources.map((source) => source.file).sort()).toEqual(expect.arrayContaining(['api.ts', 'index.ts', 'remote-options.ts', 'components/organize-select.tsx', 'components/authorization-tree.tsx']));
  });

  it('不直接使用 fetch、XMLHttpRequest、WebSocket、EventSource、sendBeacon 或第三方请求库，也不写死服务地址', () => {
    const forbidden = [/\bfetch\s*\(/, /XMLHttpRequest/, /\bWebSocket\b/, /\bEventSource\b/, /sendBeacon/, /from\s+['"](axios|ky|ofetch|swr|@tanstack\/react-query)['"]/, /\bopenapi-fetch\b/];
    for (const { file, text } of sources) {
      for (const pattern of forbidden) expect(pattern.test(text), `${file} 命中 ${pattern}`).toBe(false);
      // 生成的契约类型里有错误类型 URI 的说明文字；手写源码不得出现任何服务地址。
      if (!file.endsWith('.generated.ts')) expect(/['"`]https?:\/\//.test(text), `${file} 写死了服务地址`).toBe(false);
    }
    const network = sources.filter(({ text }) => /\.api<paths>\(\)/.test(text)).map(({ file }) => file);
    expect(network).toEqual(['api.ts']);
  });

  it('源码里引用的 biz.* key 都在语言包里；按状态 / 风险拼出的 key 覆盖契约枚举中会显示的取值', () => {
    const keys = new Set(Object.keys(bizLanguagePacks['zh-CN']));
    const used = new Set(sources.flatMap(({ file, text }) => file === 'locales.ts' ? [] : [...text.matchAll(/['"`](biz\.[a-zA-Z0-9.]+)['"`]/g)].map((match) => match[1]!)));
    expect([...used].filter((key) => !keys.has(key))).toEqual([]);
    expect(used.size).toBeGreaterThan(10);
    for (const status of ['disabled', 'locked']) expect(keys.has(`biz.member.status.${status}`)).toBe(true);
    for (const risk of ['high', 'critical']) expect(keys.has(`biz.risk.${risk}`)).toBe(true);
    for (const role of ['tenant.owner', 'tenant.admin', 'tenant.developer', 'tenant.user']) expect(keys.has(`biz.role.${role}`)).toBe(true);
  });
});
