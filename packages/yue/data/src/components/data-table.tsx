import { useMemo, useState, type CSSProperties, type ReactNode } from 'react';
import { Button, Checkbox, Input, useDirection } from '@idadawn/yue-core';
import type { DataColumnMeta } from '@idadawn/yue-contracts';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { columnFilteringFeature, columnPinningFeature, columnSizingFeature, columnVisibilityFeature, functionalUpdate, rowPaginationFeature, rowSelectionFeature, rowSortingFeature, tableFeatures, useTable, type ColumnDef, type RowSelectionState } from '@tanstack/react-table';

const features = tableFeatures({ columnFilteringFeature, columnPinningFeature, columnSizingFeature, columnVisibilityFeature, rowPaginationFeature, rowSelectionFeature, rowSortingFeature });
export interface DataTableQuery {
  pageIndex: number;
  pageSize: number;
  sorting: { id: string; desc: boolean }[];
  filters: Record<string, string>;
}
export interface DataColumn<T> extends DataColumnMeta {
  render?: (row: T) => ReactNode;
}
export interface DataTableProps<T extends object> {
  columns: DataColumn<T>[];
  data: T[];
  total: number;
  query: DataTableQuery;
  onQueryChange(query: DataTableQuery): void;
  getRowId(row: T): string;
  i18n: PlatformI18n;
  label: string;
  loading?: boolean;
  error?: string | null;
  onRetry?: () => void;
  selectable?: boolean;
  selectedIds?: string[];
  onSelectionChange?: (ids: string[]) => void;
  toolbar?: ReactNode;
  className?: string;
  testId?: string;
}

/** 只接收服务端当前页；分页、排序、筛选变化交给消费方通过 PlatformClient 取数。 */
export function DataTable<T extends object>({ columns, data, total, query, onQueryChange, getRowId, i18n, label, loading, error, onRetry, selectable = false, selectedIds, onSelectionChange, toolbar, className, testId = 'yue-data-table' }: DataTableProps<T>) {
  const direction = useDirection();
  const [selection, setSelection] = useState<RowSelectionState>({});
  const selected = selectedIds ? Object.fromEntries(selectedIds.map((id) => [id, true as const])) : selection;
  const defs = useMemo<ColumnDef<typeof features, T>[]>(() => columns.map((column) => ({
    id: column.field, accessorFn: (row) => (row as Record<string, unknown>)[column.field], header: column.titleKey,
    size: column.width ?? 160, enableSorting: !!column.sortable, enableColumnFilter: !!column.filterable,
  })), [columns]);
  const table = useTable({
    features, columns: defs, data, getRowId,
    manualPagination: true, manualSorting: true, manualFiltering: true, rowCount: total,
    state: { pagination: { pageIndex: query.pageIndex, pageSize: query.pageSize }, sorting: query.sorting,
      columnFilters: Object.entries(query.filters).map(([id, value]) => ({ id, value })), rowSelection: selected,
      columnPinning: { start: columns.filter((column) => column.fixed === (direction === 'rtl' ? 'right' : 'left')).map((column) => column.field), end: columns.filter((column) => column.fixed === (direction === 'rtl' ? 'left' : 'right')).map((column) => column.field) } },
    onPaginationChange: (update) => onQueryChange({ ...query, ...functionalUpdate(update, { pageIndex: query.pageIndex, pageSize: query.pageSize }) }),
    onSortingChange: (update) => onQueryChange({ ...query, pageIndex: 0, sorting: functionalUpdate(update, query.sorting) }),
    onColumnFiltersChange: (update) => onQueryChange({ ...query, pageIndex: 0, filters: Object.fromEntries(functionalUpdate(update, Object.entries(query.filters).map(([id, value]) => ({ id, value }))).map(({ id, value }) => [id, String(value)])) }),
    onRowSelectionChange: (update) => { const next = functionalUpdate(update, selected); if (!selectedIds) setSelection(next); onSelectionChange?.(Object.keys(next).filter((id) => next[id])); },
  });
  const visible = [...table.getStartVisibleLeafColumns(), ...table.getCenterVisibleLeafColumns(), ...table.getEndVisibleLeafColumns()];
  const pinned = (id: string): CSSProperties => {
    const column = table.getColumn(id)!;
    const side = column.getIsPinned();
    return { width: column.getSize(), minWidth: column.getSize(), ...(side ? { position: 'sticky', [side === 'start' ? 'insetInlineStart' : 'insetInlineEnd']: side === 'start' ? column.getStart('start') : column.getAfter('end'), zIndex: 1, background: 'var(--background)' } : {}) };
  };
  const state = loading ? 'loading' : error ? 'error' : data.length ? 'ready' : 'empty';
  return <section className={className ?? 'space-y-3'} data-slot="data-table" data-testid={testId} aria-label={label} aria-busy={!!loading}>
    <div data-slot="data-table-toolbar" className="flex flex-wrap items-center justify-between gap-3">{toolbar}
      <details><summary>{i18n.t('data.columns')}</summary><div className="flex flex-wrap gap-3">{table.getAllLeafColumns().map((column) => <label key={column.id} className="flex items-center gap-1"><Checkbox checked={column.getIsVisible()} onCheckedChange={(value) => column.toggleVisibility(!!value)} />{i18n.t(String(column.columnDef.header))}</label>)}</div></details>
    </div>
    <div className="overflow-auto rounded-lg border border-[var(--border)]" tabIndex={0} role="region" aria-label={label}>
      <table dir={direction} className="w-full border-collapse text-sm" aria-label={label}>
        <thead><tr>{selectable && <th scope="col" className="p-3"><Checkbox aria-label={i18n.t('data.selectAll')} checked={table.getIsAllPageRowsSelected()} indeterminate={table.getIsSomePageRowsSelected()} onCheckedChange={(checked) => table.toggleAllPageRowsSelected(!!checked)} /></th>}
          {visible.map((column) => <th scope="col" key={column.id} style={pinned(column.id)} className="p-3 text-start" aria-sort={column.getIsSorted() === 'asc' ? 'ascending' : column.getIsSorted() === 'desc' ? 'descending' : undefined}>
            {column.getCanSort() ? <Button variant="ghost" onClick={column.getToggleSortingHandler()}>{i18n.t(String(column.columnDef.header))}{column.getIsSorted() === 'asc' ? ' ↑' : column.getIsSorted() === 'desc' ? ' ↓' : ''}</Button> : i18n.t(String(column.columnDef.header))}
            {column.getCanFilter() && <Input aria-label={i18n.t('data.filter', { column: i18n.t(String(column.columnDef.header)) })} value={String(column.getFilterValue() ?? '')} onChange={(event) => column.setFilterValue(event.target.value)} />}
          </th>)}
        </tr></thead>
        <tbody>{state === 'ready' ? table.getRowModel().rows.map((row) => <tr key={row.id} data-slot="data-table-row" data-testid={`${testId}-row`} className="border-t border-[var(--border)]">
          {selectable && <td className="p-3"><Checkbox aria-label={i18n.t('data.selectRow', { row: row.index + 1 })} checked={row.getIsSelected()} onCheckedChange={(checked) => row.toggleSelected(!!checked)} /></td>}
          {visible.map((column) => { const definition = columns.find((item) => item.field === column.id)!; const value = row.getValue(column.id); return <td key={column.id} className="p-3" style={pinned(column.id)}>{definition.render ? definition.render(row.original) : value === null || value === undefined ? '' : String(value)}</td>; })}
        </tr>) : <tr><td colSpan={Math.max(1, visible.length + (selectable ? 1 : 0))} className="p-8 text-center"><div role={state === 'error' ? 'alert' : 'status'} data-slot={`data-table-${state}`} data-testid={`${testId}-${state}`}>{state === 'error' ? error : i18n.t(`data.${state}`)}{state === 'error' && onRetry && <Button onClick={onRetry}>{i18n.t('common.retry')}</Button>}</div></td></tr>}</tbody>
      </table>
    </div>
    <nav aria-label={i18n.t('data.page', { page: query.pageIndex + 1 })} className="flex items-center justify-between gap-3">
      <span>{i18n.t('data.total', { total })}</span><div className="flex gap-2"><Button disabled={!table.getCanPreviousPage() || loading} onClick={() => table.previousPage()}>{i18n.t('data.previous')}</Button><span>{i18n.t('data.page', { page: query.pageIndex + 1 })}</span><Button disabled={!table.getCanNextPage() || loading} onClick={() => table.nextPage()}>{i18n.t('data.next')}</Button></div>
    </nav>
  </section>;
}
