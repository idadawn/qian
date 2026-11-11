import { useMemo, useState } from 'react';
import type { PlatformClient, PlatformI18n } from '@idadawn/yue-sdk';
import { MEMBER_SEARCH_LIMIT, loadMembersByIds, searchMembers, type DirectoryMember } from '../api';
import { useRemoteResource } from '../remote-options';
import { OptionCombobox, selectedValues, type BizSelectBaseProps, type SelectOption, type Selection } from './option-combobox';

export type UserSelectProps = BizSelectBaseProps & Selection & {
  /** 宿主注入的 PlatformClient；成员经它读 GET /api/directory/members 或 /api/directory/orgs/{id}/members。 */
  client: PlatformClient;
  /** 只在该组织下选人；不传在全租户选。 */
  organizationId?: string | null | undefined;
  /** 组织范围是否含整棵子树，默认 true。 */
  descendants?: boolean | undefined;
  /**
   * 选中值取哪个标识：membership（默认）为目录成员 id，用于成员归属等目录 API；
   * person 为自然人 id（令牌 sub），授权侧 user 主体的 subjectId 用它。
   */
  valueKey?: 'membership' | 'person' | undefined;
  /** 关键字去抖毫秒数，默认 250。 */
  debounceMs?: number | undefined;
};

export function memberValue(member: DirectoryMember, valueKey: 'membership' | 'person' = 'membership'): string {
  return valueKey === 'person' ? member.membership.person_id : member.membership.id;
}

export function memberOption(member: DirectoryMember, i18n: PlatformI18n, valueKey: 'membership' | 'person' = 'membership'): SelectOption {
  const { membership } = member;
  const status = membership.status === 'active' ? undefined : i18n.t(`biz.member.status.${membership.status}`);
  return {
    value: memberValue(member, valueKey),
    label: membership.display_name,
    description: status ? `${membership.account} · ${status}` : membership.account,
  };
}

interface Resolved { requested: readonly string[]; items: readonly DirectoryMember[] }

/**
 * 用户选择器：输入去抖后交给目录接口按显示名、账号与拼音检索（可限定组织范围），每次显示第一页 50 位，后面还有时提示继续输入。
 * 已选值不在检索过的结果里时（初始值、换了组织范围）按 id 批量回查显示名，查过的值不再重复查。
 */
export function UserSelect(props: UserSelectProps) {
  const { client, organizationId, descendants = true, valueKey = 'membership', debounceMs, i18n, label, disabled, className, testId } = props;
  const [keyword, setKeyword] = useState('');
  const search = useRemoteResource({
    client, keyword, debounceMs, reloadKey: JSON.stringify([organizationId ?? null, descendants]),
    load: (current, { keyword: typed, signal }) => searchMembers(current, { organizationId, descendants, keyword: typed }, signal),
  });
  const values = selectedValues(props);
  const searched = useMemo(() => new Map(search.history.flatMap((page) => page.items.map((member) => [memberValue(member, valueKey), member] as const))), [search.history, valueKey]);
  // 只回查检索结果里没有的值，且等第一页回来再查，免得与第一页重复；之后只在待查集合变化时再请求。
  const unresolved = [...new Set(values.filter((value) => !searched.has(value)))].sort();
  const resolve = useRemoteResource<Resolved>({
    client, enabled: unresolved.length > 0 && search.data !== undefined, reloadKey: JSON.stringify([valueKey, unresolved]),
    load: async (current, { signal }) => ({ requested: unresolved, items: await loadMembersByIds(current, unresolved, valueKey, signal) }),
  });
  const labels = useMemo(() => {
    const names = new Map<string, string>();
    for (const result of resolve.history) for (const member of result.items) names.set(memberValue(member, valueKey), member.membership.display_name);
    for (const [value, member] of searched) names.set(value, member.membership.display_name);
    return names;
  }, [resolve.history, searched, valueKey]);
  const page = search.data;
  // 选项含状态文案：每次渲染按当前语言生成，宿主切换语言后重渲染即可更新。
  const options = (page?.items ?? []).map((member) => memberOption(member, i18n, valueKey));
  return <OptionCombobox slot="user-select" defaultTestId="yue-user-select" selection={props} options={options} labels={labels}
    loading={search.loading || resolve.loading} error={search.error ?? resolve.error} onRetry={() => { search.reload(); resolve.reload(); }}
    notice={page?.more ? i18n.t('biz.member.more', { n: MEMBER_SEARCH_LIMIT }) : undefined}
    search="server" keyword={keyword} onKeywordChange={setKeyword}
    i18n={i18n} label={label} disabled={disabled} className={className} testId={testId} />;
}
