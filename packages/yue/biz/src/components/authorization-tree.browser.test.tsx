import { cleanup, render, screen } from '@testing-library/react';
import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { userEvent } from 'vitest/browser';
import { blockingViolations, describeViolations, runAxe } from '../test/axe';
import { CATALOG, createFixture } from '../test/fixtures';
import { createBizI18n } from '../locales';
import { AuthorizationTree, buildAuthorizationTree, groupPermissionCatalog, linkAuthorization } from './authorization-tree';

afterEach(cleanup);
const i18n = createBizI18n();
const READ = 'qian.sample.crm:customer:read';
const WRITE = 'qian.sample.crm:customer:write';
const EXPORT = 'qian.sample.crm:customer:export';

describe('菜单 / 按钮授权树的数据口径', () => {
  it('按资源分菜单、read 置顶，按钮文案走 i18n 并标出高风险与二次验证；没有名称的资源用全名', () => {
    const nodes = buildAuthorizationTree(CATALOG, i18n);
    expect(nodes.map((node) => [node.id, node.label])).toEqual([['menu:qian.sample.crm:customer', '客户'], ['menu:qian.authz:role', '角色'], ['menu:qian.audit:log', 'qian.audit:log']]);
    expect(nodes[0]!.children!.map((node) => node.label)).toEqual(['查看', '写入', '导出 · 高风险']);
    expect(nodes[1]!.children!.map((node) => node.label)).toEqual(['查看', '分配 · 关键操作 · 需二次验证']);
    expect(nodes[0]!.children!.map((node) => node.id)).toEqual([READ, WRITE, EXPORT]);
  });

  it('勾选按钮带上同菜单的 read，取消 read 连带取消按钮，分组 id 不进入值，目录外的已授予项保留', () => {
    const menus = groupPermissionCatalog(CATALOG);
    expect(linkAuthorization(menus, [], [EXPORT])).toEqual([EXPORT, READ]);
    expect(linkAuthorization(menus, [READ, EXPORT], [EXPORT])).toEqual([]);
    expect(linkAuthorization(menus, [READ, EXPORT], [READ])).toEqual([READ]);
    expect(linkAuthorization(menus, ['legacy:thing:read'], ['legacy:thing:read', 'menu:qian.authz:role', 'qian.authz:role:read', 'qian.authz:role:assign'])).toEqual(['legacy:thing:read', 'qian.authz:role:read', 'qian.authz:role:assign']);
    expect(linkAuthorization(menus, [], ['qian.audit:log:export'])).toEqual(['qian.audit:log:export']);
  });
});

describe('AuthorizationTree', () => {
  function Example({ initial = [] as string[] }) {
    const [{ client }] = useState(() => createFixture());
    const [granted, setGranted] = useState<string[]>(initial);
    return <><AuthorizationTree client={client} i18n={i18n} label="角色权限" value={granted} onChange={setGranted} /><output data-testid="value">{[...granted].sort().join(',')}</output></>;
  }
  const item = (name: string) => screen.getByRole('treeitem', { name });
  const value = () => screen.getByTestId('value').textContent;

  it('经 PlatformClient 读权限点目录，菜单默认展开；键盘勾选按钮联动菜单，分支状态随之变为半选 / 全选', async () => {
    render(<Example />);
    const tree = await screen.findByRole('tree', { name: '角色权限' });
    expect(tree.getAttribute('aria-multiselectable')).toBe('true');
    expect(item('导出 · 高风险')).toBeTruthy();
    item('导出 · 高风险').focus();
    await userEvent.keyboard(' ');
    expect(value()).toBe([EXPORT, READ].sort().join(','));
    expect(item('客户').getAttribute('aria-checked')).toBe('mixed');
    await userEvent.keyboard('{ArrowUp}');
    expect(document.activeElement).toBe(item('写入'));
    await userEvent.keyboard('{Enter}');
    expect(item('客户').getAttribute('aria-checked')).toBe('true');
    await userEvent.keyboard('{ArrowUp}{ArrowUp}');
    expect(document.activeElement).toBe(item('客户'));
    await userEvent.keyboard(' ');
    expect(value()).toBe('');
    expect(item('客户').getAttribute('aria-checked')).toBe('false');
    const violations = blockingViolations(await runAxe());
    expect(violations, describeViolations(violations)).toHaveLength(0);
  });

  it('取消「查看」连带取消同菜单的按钮；勾选菜单授予其全部权限点', async () => {
    render(<Example initial={[READ, WRITE]} />);
    await screen.findByRole('tree', { name: '角色权限' });
    expect(item('客户').getAttribute('aria-checked')).toBe('mixed');
    await userEvent.click(screen.getAllByRole('treeitem', { name: '查看' })[0]!);
    expect(value()).toBe('');
    await userEvent.click(item('角色'));
    expect(value()).toBe(['qian.authz:role:read', 'qian.authz:role:assign'].sort().join(','));
  });

  it('目录为空显示空态；加载失败给出重试', async () => {
    const empty = createFixture({ routes: [{ method: 'GET', path: '/api/authz/permissions', respond: () => Response.json({ items: [], resources: [] }) }] });
    render(<AuthorizationTree client={empty.client} i18n={i18n} label="角色权限" value={[]} onChange={() => {}} />);
    expect(await screen.findByText('暂无数据')).toBeTruthy();
    cleanup();
    const failing = createFixture({ failOnce: ['/api/authz/permissions'] });
    render(<AuthorizationTree client={failing.client} i18n={i18n} label="角色权限" value={[]} onChange={() => {}} />);
    await userEvent.click(await screen.findByRole('button', { name: '重试' }));
    await vi.waitFor(() => expect(screen.getByRole('tree', { name: '角色权限' })).toBeTruthy());
  });
});
