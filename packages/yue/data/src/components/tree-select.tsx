import { useState } from 'react';
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
export function TreeSelect({ nodes, value, onChange, label, i18n, disabled, className, testId = 'yue-tree-select' }: TreeSelectProps) {
  const [open, setOpen] = useState(false);
  const path = value ? findTreePath(nodes, value) : [];
  return <Popover open={open} onOpenChange={setOpen}>
    <PopoverTrigger className={className} testId={testId} aria-label={label} disabled={disabled}>{path.at(-1)?.label ?? i18n.t('data.select')}</PopoverTrigger>
    <PopoverContent aria-label={label} testId={`${testId}-popup`}>
      <Tree nodes={nodes} selectedId={value ?? null} defaultExpandedIds={path.slice(0, -1).map((node) => node.id)} onSelectedChange={(id) => { onChange?.(id); setOpen(false); }} aria-label={label} />
      <Button onClick={() => { onChange?.(null); setOpen(false); }}>{i18n.t('data.clear')}</Button>
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
  const levels: TreeNode[][] = [nodes];
  let children = nodes;
  for (const id of trail) { const found = children.find((node) => node.id === id); if (!found?.children?.length) break; children = found.children; levels.push(children); }
  const selected = value.length ? findTreePath(nodes, value.at(-1)!) : [];
  return <Popover open={open} onOpenChange={(next) => { if (next) setTrail(value); setOpen(next); }}>
    <PopoverTrigger className={className} testId={testId} aria-label={label} disabled={disabled}>{selected.map((node) => node.label).join(' / ') || i18n.t('data.select')}</PopoverTrigger>
    <PopoverContent aria-label={label} className="max-w-[90vw]" testId={`${testId}-popup`}>
      <div className="flex gap-3">{levels.map((level, index) => <Tree key={index} nodes={level.map(({ children: _children, ...node }) => node)} selectedId={trail[index] ?? null} aria-label={`${label} ${index + 1}`} className="min-w-40" onSelectedChange={(id) => {
        if (!id) return;
        const next = [...trail.slice(0, index), id]; setTrail(next);
        if (!level.find((node) => node.id === id)?.children?.length) { onChange?.(next); setOpen(false); }
      }} />)}</div>
      <Button onClick={() => { onChange?.([]); setTrail([]); setOpen(false); }}>{i18n.t('data.clear')}</Button>
    </PopoverContent>
  </Popover>;
}
