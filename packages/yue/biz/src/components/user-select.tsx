import type { PlatformClient, PlatformI18n } from '@idadawn/yue-sdk';
import { loadMembers, type DirectoryMember } from '../api';
import { useRemoteResource } from '../remote-options';
import { OptionCombobox, type BizSelectBaseProps, type SelectOption, type Selection } from './option-combobox';

export type UserSelectProps = BizSelectBaseProps & Selection & {
  /** 宿主注入的 PlatformClient；成员经它读 GET /api/directory/members 或 /api/directory/orgs/{id}/members。 */
  client: PlatformClient;
  /** 只列该组织下的成员；不传列全租户。 */
  organizationId?: string | null | undefined;
  /** 组织范围是否含整棵子树，默认 true。 */
  descendants?: boolean | undefined;
  /**
   * 选中值取哪个标识：membership（默认）为目录成员 id，用于成员归属等目录 API；
   * person 为自然人 id（令牌 sub），授权侧 user 主体的 subjectId 用它。
   */
  valueKey?: 'membership' | 'person' | undefined;
  /** 最多加载的成员数，默认 1000；超出时提示按组织缩小范围。 */
  max?: number | undefined;
};

export function memberOption(member: DirectoryMember, i18n: PlatformI18n, valueKey: 'membership' | 'person' = 'membership'): SelectOption {
  const { membership } = member;
  const status = membership.status === 'active' ? undefined : i18n.t(`biz.member.status.${membership.status}`);
  return {
    value: valueKey === 'person' ? membership.person_id : membership.id,
    label: membership.display_name,
    description: status ? `${membership.account} · ${status}` : membership.account,
  };
}

/** 用户选择器：按页读取本租户目录成员（可限定组织范围），按姓名与账号在本地过滤。 */
export function UserSelect(props: UserSelectProps) {
  const { client, organizationId, descendants = true, valueKey = 'membership', max = 1000, i18n, label, disabled, className, testId } = props;
  const remote = useRemoteResource({
    client, reloadKey: JSON.stringify([organizationId ?? null, descendants, max]),
    load: (current, { signal }) => loadMembers(current, { organizationId, descendants, max }, signal),
  });
  const page = remote.data;
  // 选项含状态文案：每次渲染按当前语言生成，宿主切换语言后重渲染即可更新。
  const options = (page?.items ?? []).map((member) => memberOption(member, i18n, valueKey));
  return <OptionCombobox slot="user-select" defaultTestId="yue-user-select" selection={props} options={options}
    loading={remote.loading} error={remote.error} onRetry={remote.reload} notice={page?.truncated ? i18n.t('biz.member.truncated', { n: page.items.length }) : undefined}
    i18n={i18n} label={label} disabled={disabled} className={className} testId={testId} />;
}
