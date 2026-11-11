import type { PlatformClient, PlatformI18n } from '@idadawn/yue-sdk';
import { loadRoles, type AuthzRole } from '../api';
import { useRemoteResource } from '../remote-options';
import { OptionCombobox, type BizSelectBaseProps, type SelectOption, type Selection } from './option-combobox';

/** 角色显示名：内置角色按 code 取文案 biz.role.<code>，其余用服务端名称，缺省为 code。 */
export function roleLabel(role: AuthzRole, i18n: PlatformI18n): string {
  const key = `biz.role.${role.code}`;
  return role.builtin && i18n.instance.exists(key) ? i18n.t(key) : role.name ?? role.code;
}

export function roleOption(role: AuthzRole, i18n: PlatformI18n): SelectOption {
  return { value: role.code, label: roleLabel(role, i18n), description: role.builtin ? i18n.t('biz.role.builtin') : role.code };
}

export type RoleSelectProps = BizSelectBaseProps & Selection & {
  /** 宿主注入的 PlatformClient；角色目录经它读 GET /api/authz/roles。选中值是角色 code（授予与撤销沿用 roleCode）。 */
  client: PlatformClient;
};

/** 角色选择器：列出本租户内置与自定义角色，按名称与 code 在本地过滤。 */
export function RoleSelect(props: RoleSelectProps) {
  const { client, i18n, label, disabled, className, testId } = props;
  const remote = useRemoteResource({ client, load: (current, { signal }) => loadRoles(current, signal) });
  const options = (remote.data ?? []).map((role) => roleOption(role, i18n));
  return <OptionCombobox slot="role-select" defaultTestId="yue-role-select" selection={props} options={options}
    loading={remote.loading} error={remote.error} onRetry={remote.reload}
    i18n={i18n} label={label} disabled={disabled} className={className} testId={testId} />;
}
