import { useId, useState } from 'react';
import { Popover, PopoverContent, PopoverTrigger, Button } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { Tree, type TreeNode } from './tree';

export interface TreeSelectProps {
  nodes: TreeNode[];
  value?: string | null;
  onChange?: (id: string | null) => void;
  label: string;
  i18n: PlatformI18n;
  disabled?: boolean;
  className?: string;
  testId?: string;
}
export function findTreePath(nodes: TreeNode[], id: string): TreeNode[] {
  for (const node of nodes) {
    if (node.id === id) return [node];
    const child = findTreePath(node.children ?? [], id);
    if (child.length) return [node, ...child];
  }
  return [];
}
// 触发器是按钮，可访问名称取「标签 + 当前值」（aria-labelledby 指向隐藏的标签与可见的值），可见文字始终包含在名称里（WCAG 2.5.3）。
function useTriggerName() {
  const labelId = useId();
  const valueId = useId();
  return { labelId, valueId, labelledBy: `${labelId} ${valueId}` };
}
export function TreeSelect({ nodes, value, onChange, label, i18n, disabled, className, testId = 'yue-tree-select' }: TreeSelectProps) {
  const [open, setOpen] = useState(false);
  const name = useTriggerName();
  const path = value ? findTreePath(nodes, value) : [];
  return <Popover open={open} onOpenChange={setOpen}>
    <PopoverTrigger render={<Button variant="outline" className="border-input" />} className={className} testId={testId} aria-labelledby={name.labelledBy} disabled={disabled}><span id={name.valueId}>{path.at(-1)?.label ?? i18n.t('data.select')}</span></PopoverTrigger>
    <span id={name.labelId} hidden>{label}</span>
    <PopoverContent aria-label={label} testId={`${testId}-popup`}>
      <Tree nodes={nodes} selectedId={value ?? null} defaultExpandedIds={path.slice(0, -1).map((node) => node.id)} onSelectedChange={(id) => { onChange?.(id); setOpen(false); }} aria-label={label} />
      <Button variant="ghost" size="sm" className="mt-3" onClick={() => { onChange?.(null); setOpen(false); }}>{i18n.t('data.clear')}</Button>
    </PopoverContent>
  </Popover>;
}

export interface CascaderProps extends Omit<TreeSelectProps, 'value' | 'onChange'> {
  value?: string[];
  onChange?: (path: string[]) => void;
}
export function Cascader({ nodes, value = [], onChange, label, i18n, disabled, className, testId = 'yue-cascader' }: CascaderProps) {
  const [open, setOpen] = useState(false);
  const [trail, setTrail] = useState<string[]>(value);
  const name = useTriggerName();
  const levels: TreeNode[][] = [nodes];
  let children = nodes;
  for (const id of trail) { const found = children.find((node) => node.id === id); if (!found?.children?.length) break; children = found.children; levels.push(children); }
  const selected = value.length ? findTreePath(nodes, value.at(-1)!) : [];
  return <Popover open={open} onOpenChange={(next) => { if (next) setTrail(value); setOpen(next); }}>
    <PopoverTrigger render={<Button variant="outline" className="border-input" />} className={className} testId={testId} aria-labelledby={name.labelledBy} disabled={disabled}><span id={name.valueId}>{selected.map((node) => node.label).join(' / ') || i18n.t('data.select')}</span></PopoverTrigger>
    <span id={name.labelId} hidden>{label}</span>
    <PopoverContent aria-label={label} className="max-w-[90vw]" testId={`${testId}-popup`}>
      <div className="flex gap-3">{levels.map((level, index) => <Tree key={index} nodes={level.map(({ children: _children, ...node }) => node)} selectedId={trail[index] ?? null} aria-label={`${label} ${index + 1}`} className="min-w-40" onSelectedChange={(id) => {
        if (!id) return;
        const next = [...trail.slice(0, index), id]; setTrail(next);
        if (!level.find((node) => node.id === id)?.children?.length) { onChange?.(next); setOpen(false); }
      }} />)}</div>
      <Button variant="ghost" size="sm" className="mt-3" onClick={() => { onChange?.([]); setTrail([]); setOpen(false); }}>{i18n.t('data.clear')}</Button>
    </PopoverContent>
  </Popover>;
}
