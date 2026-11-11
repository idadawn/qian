import { useId, useMemo, useState } from 'react';
import { Button, Icon, Popover, PopoverContent, PopoverTrigger, cn } from '@idadawn/yue-core';
import { Tree, TreeSelect, findTreePath, type TreeNode } from '@idadawn/yue-data';
import type { PlatformClient } from '@idadawn/yue-sdk';
import { loadOrganizations, type DirectoryOrganization } from '../api';
import { useRemoteResource } from '../remote-options';
import { selectedValues, type BizSelectBaseProps, type Selection } from './option-combobox';
import { SelectStatus } from './select-status';

/** 把目录接口的扁平组织列表（按 path 排序、以 parent_id 关联）组装成 Tree 节点；找不到上级的组织作为根，兄弟顺序沿用接口顺序。 */
export function buildOrganizationTree(organizations: readonly DirectoryOrganization[]): TreeNode[] {
  const nodes = new Map<string, TreeNode & { children: TreeNode[] }>();
  for (const organization of organizations) nodes.set(organization.id, { id: organization.id, label: organization.name, children: [] });
  const roots: TreeNode[] = [];
  for (const organization of organizations) {
    const node = nodes.get(organization.id)!;
    const parent = organization.parent_id && organization.parent_id !== organization.id ? nodes.get(organization.parent_id) : undefined;
    (parent ? parent.children : roots).push(node);
  }
  return roots;
}

export type OrganizeSelectProps = BizSelectBaseProps & Selection & {
  /** 宿主注入的 PlatformClient；组织树经它读 GET /api/directory/orgs/tree。 */
  client: PlatformClient;
};

/** 组织选择器：单选复用 @yue/data 的 TreeSelect，多选在同一 Tree 上做不级联的复选（成员可归属多个组织）。 */
export function OrganizeSelect(props: OrganizeSelectProps) {
  const { client, i18n, label, disabled, className, testId } = props;
  const id = testId ?? 'yue-organize-select';
  const remote = useRemoteResource({ client, load: (current, { signal }) => loadOrganizations(current, signal) });
  const organizations = remote.data;
  const nodes = useMemo(() => buildOrganizationTree(organizations ?? []), [organizations]);
  const [open, setOpen] = useState(false);
  const labelId = useId();
  const valueId = useId();
  const values = selectedValues(props);
  const names = new Map((organizations ?? []).map((organization) => [organization.id, organization.name]));
  const summary = values.length === 0 ? i18n.t('biz.select')
    : values.length <= 2 ? new Intl.ListFormat(i18n.instance.language, { type: 'conjunction', style: 'short' }).format(values.map((value) => names.get(value) ?? value))
      : i18n.t('biz.selected', { n: values.length });
  const expanded = [...new Set(values.flatMap((value) => findTreePath(nodes, value).slice(0, -1).map((node) => node.id)))];
  return <span data-slot="organize-select" data-testid={id} className={cn('inline-flex flex-wrap items-center gap-2', className)}>
    {props.multiple ? <Popover open={open} onOpenChange={setOpen}>
      {/* 与 TreeSelect 相同：按钮名称取「标签 + 当前值」，可见文字包含在名称里（WCAG 2.5.3）。 */}
      <PopoverTrigger render={<Button variant="outline" className="min-w-48 justify-between border-input" />} aria-labelledby={`${labelId} ${valueId}`} aria-busy={remote.loading || undefined} testId={`${id}-trigger`} disabled={disabled}>
        <span id={valueId} className={cn('truncate', values.length === 0 && 'text-muted-foreground')}>{summary}</span><Icon name="chevronsUpDown" className="text-muted-foreground" />
      </PopoverTrigger>
      <span id={labelId} hidden>{label}</span>
      {/* 与 TreeSelect 同宽：组织多时 Tree 虚拟化，绝对定位的行撑不开浮层宽度，浮层给定宽度。 */}
      <PopoverContent aria-label={label} className="w-72 max-w-[90vw]" testId={`${id}-trigger-popup`}>
        <Tree nodes={nodes} checkedIds={props.value} onCheckedChange={props.onChange} cascade={false} defaultExpandedIds={expanded} aria-label={label} testId={`${id}-tree`} />
        <Button variant="ghost" size="sm" className="mt-3" disabled={values.length === 0} onClick={() => props.onChange([])}>{i18n.t('data.clear')}</Button>
      </PopoverContent>
    </Popover>
      : <TreeSelect nodes={nodes} value={props.value} onChange={props.onChange} label={label} i18n={i18n} disabled={disabled} testId={`${id}-trigger`} />}
    <SelectStatus slot="organize-select" testId={id} i18n={i18n} loading={remote.loading && organizations === undefined} error={remote.error} onRetry={remote.reload} />
  </span>;
}
