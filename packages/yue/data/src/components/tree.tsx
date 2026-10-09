import { cn, useDirection, type TestIdProps } from '@idadawn/yue-core';
import { useCallback, useEffect, useId, useMemo, useRef, useState, type KeyboardEvent } from 'react';

// 树（自建；Base UI 与 Radix 都没有 Tree 原语）。验证门项：WAI-ARIA tree 模式的键盘导航（方向键 / Home / End / Enter / Space）、
// roving tabindex 焦点管理、aria-level / aria-setsize / aria-posinset / aria-expanded / aria-selected、RTL 下左右键互换、受控 / 非受控、SSR。
// 结构用「扁平 treeitem + aria-level」而不是嵌套 group：treeitem 的可见文字就是它的可访问名，层级关系由 aria 属性表达
// （嵌套 ul 会让父节点的可见文字包含整棵子树，触发 axe 的 label-content-name-mismatch）。
// 本版只做验证门需要的单选 + 展开折叠；拖拽、复选、异步加载、虚拟滚动由 S1-YUE-008 的 Tree 族交付。

export interface TreeNode {
  id: string;
  label: string;
  disabled?: boolean | undefined;
  children?: TreeNode[] | undefined;
}

export interface TreeProps extends TestIdProps {
  nodes: TreeNode[];
  /** 受控展开集合；不传则非受控（默认 `defaultExpandedIds`）。 */
  expandedIds?: string[] | undefined;
  defaultExpandedIds?: string[] | undefined;
  onExpandedChange?: ((ids: string[]) => void) | undefined;
  /** 受控选中；不传则非受控。 */
  selectedId?: string | null | undefined;
  defaultSelectedId?: string | null | undefined;
  onSelectedChange?: ((id: string | null) => void) | undefined;
  'aria-label'?: string | undefined;
  'aria-labelledby'?: string | undefined;
  className?: string | undefined;
}

interface FlatNode {
  node: TreeNode;
  level: number;
  parentId: string | null;
  setSize: number;
  posInSet: number;
}

/** 按展开状态把树压成可见节点的有序列表（深度优先）。 */
export function flattenTree(nodes: TreeNode[], expanded: ReadonlySet<string>, level = 1, parentId: string | null = null, out: FlatNode[] = []): FlatNode[] {
  nodes.forEach((node, index) => {
    out.push({ node, level, parentId, setSize: nodes.length, posInSet: index + 1 });
    if (node.children?.length && expanded.has(node.id)) flattenTree(node.children, expanded, level + 1, node.id, out);
  });
  return out;
}

function useControllable<T>(value: T | undefined, defaultValue: T, onChange?: ((next: T) => void) | undefined): [T, (next: T) => void] {
  const [inner, setInner] = useState(defaultValue);
  const controlled = value !== undefined;
  const current = controlled ? value : inner;
  const set = useCallback(
    (next: T) => {
      if (!controlled) setInner(next);
      onChange?.(next);
    },
    [controlled, onChange],
  );
  return [current, set];
}

export function Tree({
  nodes,
  expandedIds,
  defaultExpandedIds,
  onExpandedChange,
  selectedId,
  defaultSelectedId,
  onSelectedChange,
  className,
  testId,
  ...aria
}: TreeProps) {
  const direction = useDirection();
  const [expandedList, setExpandedList] = useControllable<string[]>(expandedIds, defaultExpandedIds ?? [], onExpandedChange);
  const [selected, setSelected] = useControllable<string | null>(selectedId, defaultSelectedId ?? null, onSelectedChange);
  const expanded = useMemo(() => new Set(expandedList), [expandedList]);
  const visible = useMemo(() => flattenTree(nodes, expanded), [nodes, expanded]);
  const [focusedId, setFocusedId] = useState<string | null>(null);
  const pendingFocus = useRef<string | null>(null);
  const rootRef = useRef<HTMLUListElement>(null);
  const baseId = useId();

  // roving tabindex：只有一个 treeitem 的 tabIndex 为 0——最近聚焦过的可见项，否则选中项，否则首个可见项。
  const isVisible = (id: string | null) => !!id && visible.some((v) => v.node.id === id);
  const tabStopId = isVisible(focusedId) ? focusedId : isVisible(selected) ? selected : (visible[0]?.node.id ?? null);

  useEffect(() => {
    if (!pendingFocus.current) return;
    const el = rootRef.current?.querySelector<HTMLElement>(`[data-node-id="${CSS.escape(pendingFocus.current)}"]`);
    pendingFocus.current = null;
    el?.focus();
  });

  const moveFocus = (id: string | null | undefined) => {
    if (!id) return;
    pendingFocus.current = id;
    setFocusedId(id);
  };
  const setExpanded = (id: string, open: boolean) => setExpandedList(open ? [...expandedList, id] : expandedList.filter((x) => x !== id));

  const onKeyDown = (event: KeyboardEvent<HTMLLIElement>, item: FlatNode, index: number) => {
    const hasChildren = !!item.node.children?.length;
    const isOpen = expanded.has(item.node.id);
    const expandKey = direction === 'rtl' ? 'ArrowLeft' : 'ArrowRight';
    const collapseKey = direction === 'rtl' ? 'ArrowRight' : 'ArrowLeft';
    let handled = true;
    switch (event.key) {
      case 'ArrowDown':
        moveFocus(visible[index + 1]?.node.id);
        break;
      case 'ArrowUp':
        moveFocus(visible[index - 1]?.node.id);
        break;
      case 'Home':
        moveFocus(visible[0]?.node.id);
        break;
      case 'End':
        moveFocus(visible[visible.length - 1]?.node.id);
        break;
      case expandKey:
        if (hasChildren && !isOpen) setExpanded(item.node.id, true);
        else if (hasChildren && isOpen) moveFocus(item.node.children?.[0]?.id);
        break;
      case collapseKey:
        if (hasChildren && isOpen) setExpanded(item.node.id, false);
        else moveFocus(item.parentId);
        break;
      case 'Enter':
      case ' ':
        if (!item.node.disabled) setSelected(item.node.id);
        break;
      default:
        handled = false;
    }
    if (handled) {
      event.preventDefault();
      event.stopPropagation();
    }
  };

  return (
    <ul
      ref={rootRef}
      role="tree"
      aria-label={aria['aria-label']}
      aria-labelledby={aria['aria-labelledby']}
      data-slot="tree"
      data-testid={testId ?? 'yue-tree'}
      className={cn('m-0 list-none p-0 text-neutral-900', className)}
    >
      {visible.map((item, index) => {
        const { node, level, setSize, posInSet } = item;
        const hasChildren = !!node.children?.length;
        const isOpen = hasChildren && expanded.has(node.id);
        return (
          <li
            key={node.id}
            id={`${baseId}-${node.id}`}
            role="treeitem"
            aria-level={level}
            aria-setsize={setSize}
            aria-posinset={posInSet}
            aria-selected={selected === node.id}
            aria-expanded={hasChildren ? isOpen : undefined}
            aria-disabled={node.disabled || undefined}
            tabIndex={tabStopId === node.id ? 0 : -1}
            data-slot="tree-item"
            data-testid="yue-tree-item"
            data-node-id={node.id}
            data-level={level}
            className={cn(
              'flex cursor-default select-none items-center gap-1 rounded-sm py-1 pe-2 text-sm outline-none focus-visible:ring-2 focus-visible:ring-neutral-900',
              selected === node.id && 'bg-neutral-100 font-medium',
              node.disabled && 'opacity-50',
            )}
            style={{ paddingInlineStart: `${(level - 1) * 16 + 8}px` }}
            onKeyDown={(event) => onKeyDown(event, item, index)}
            onFocus={() => setFocusedId(node.id)}
            onClick={() => {
              if (hasChildren) setExpanded(node.id, !isOpen);
              if (!node.disabled) setSelected(node.id);
              moveFocus(node.id);
            }}
          >
            <span data-slot="tree-item-toggle" aria-hidden className="inline-block w-4 text-neutral-500">
              {hasChildren ? (isOpen ? '▾' : '▸') : ''}
            </span>
            {node.label}
          </li>
        );
      })}
    </ul>
  );
}
