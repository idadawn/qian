import { createI18n } from '@idadawn/yue-sdk';
import { vi } from 'vitest';
import { installShellLanguagePacks } from '../locales';
import type { AuthProvider, AuthSession } from '../auth';
export function makeI18n() { const i18n = createI18n(); installShellLanguagePacks(i18n); i18n.addLanguagePack('zh-CN', { 'test.home': '工作台', 'test.docs': '文档' }); return i18n; }
export const session: AuthSession = { subjectId: 'person', displayName: '用户', tenantId: 'a', tenants: [{ id: 'a', name: '租户 A' }, { id: 'b', name: '租户 B' }] };
export function makeProvider(): AuthProvider {
  return {
    signIn: vi.fn(async () => ({ kind: 'authenticated' as const, session })),
    selectTenant: vi.fn(async (id: string) => ({ ...session, tenantId: id })),
    switchTenant: vi.fn(async (id: string) => ({ ...session, tenantId: id })),
    beginSso: vi.fn(async () => '/auth/callback?code=one&state=two'),
    completeSso: vi.fn(async () => ({ kind: 'authenticated' as const, session })),
  };
}
