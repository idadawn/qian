import { useMemo, useState, type CSSProperties, type ReactNode } from 'react';
import { Button, Checkbox, Input, Select, SelectTrigger, SelectContent, SelectItem, Pagination, Popover, PopoverTrigger, PopoverContent, cn, useDirection } from '@idadawn/yue-core';
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
    return { width: side || columns.find(item => item.field === id)?.width ? column.getSize() : undefined, minWidth: column.getSize(), ...(side ? { position: 'sticky', [side === 'start' ? 'insetInlineStart' : 'insetInlineEnd']: side === 'start' ? column.getStart('start') : column.getAfter('end'), zIndex: 1 } : {}) };
  };
  const state = loading ? 'loading' : error ? 'error' : data.length ? 'ready' : 'empty';
  const filters = table.getAllLeafColumns().filter(column => column.getCanFilter());
  const pageSizes = [...new Set([query.pageSize, 10, 20, 50, 100])].sort((a, b) => a - b);
  const cell = 'p-2 align-middle text-sm [[data-yue-density=compact]_&]:py-1';
  return <section className={cn('space-y-4', className)} data-slot="data-table" data-testid={testId} aria-label={label} aria-busy={!!loading}>
    {filters.length > 0 && <div data-slot="data-table-filters" role="search" aria-label={i18n.t('data.filters')} className="flex flex-wrap items-end gap-3 rounded-lg bg-muted/30 p-3">
      {filters.map(column => <label key={column.id} className="grid min-w-40 flex-1 gap-2 text-sm font-medium sm:max-w-64">
        <span>{i18n.t(String(column.columnDef.header))}</span>
        <Input aria-label={i18n.t('data.filter', { column: i18n.t(String(column.columnDef.header)) })} value={String(column.getFilterValue() ?? '')} onChange={event => column.setFilterValue(event.target.value)} />
      </label>)}
      <Button variant="outline" onClick={() => onQueryChange({ ...query, pageIndex: 0, filters: {} })}>{i18n.t('data.reset')}</Button>
    </div>}
    <div data-slot="data-table-toolbar" className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-2">{toolbar}</div>
      <Popover><PopoverTrigger render={<Button variant="outline" size="sm" />}>{i18n.t('data.columns')}</PopoverTrigger>
        <PopoverContent align="end" aria-label={i18n.t('data.columns')} className="min-w-40 space-y-2 p-3">{table.getAllLeafColumns().map(column => <label key={column.id} className="flex items-center gap-2 text-sm"><Checkbox checked={column.getIsVisible()} onCheckedChange={value => column.toggleVisibility(!!value)} />{i18n.t(String(column.columnDef.header))}</label>)}</PopoverContent>
      </Popover>
    </div>
    <div className="overflow-auto outline-none focus-visible:ring-[3px] focus-visible:ring-ring/50" tabIndex={0} role="region" aria-label={label}>
      <table dir={direction} className="w-full caption-bottom border-collapse text-sm" aria-label={label}>
        <thead className="bg-table-header"><tr className="h-10 border-b border-border text-muted-foreground">{selectable && <th scope="col" className={cn(cell, 'h-10 w-10 py-0')}><Checkbox aria-label={i18n.t('data.selectAll')} checked={table.getIsAllPageRowsSelected()} indeterminate={table.getIsSomePageRowsSelected()} onCheckedChange={checked => table.toggleAllPageRowsSelected(!!checked)} /></th>}
          {visible.map(column => <th scope="col" key={column.id} style={pinned(column.id)} className={cn(cell, 'h-10 py-0 text-start font-medium', column.getIsPinned() && 'bg-table-header')} aria-sort={column.getIsSorted() === 'asc' ? 'ascending' : column.getIsSorted() === 'desc' ? 'descending' : undefined}>
            {column.getCanSort() ? <Button variant="ghost" size="sm" className="-ml-2 h-8 text-muted-foreground" onClick={column.getToggleSortingHandler()}>{i18n.t(String(column.columnDef.header))}{column.getIsSorted() === 'asc' ? ' ↑' : column.getIsSorted() === 'desc' ? ' ↓' : ''}</Button> : i18n.t(String(column.columnDef.header))}
          </th>)}
        </tr></thead>
        <tbody>{state === 'ready' ? table.getRowModel().rows.map(row => <tr key={row.id} data-slot="data-table-row" data-testid={`${testId}-row`} data-state={row.getIsSelected() ? 'selected' : undefined} className="group h-(--yue-table-row-height) border-b border-border transition-colors hover:bg-muted/50 data-[state=selected]:bg-muted">
          {selectable && <td className={cn(cell, 'w-10')}><Checkbox aria-label={i18n.t('data.selectRow', { row: row.index + 1 })} checked={row.getIsSelected()} onCheckedChange={checked => row.toggleSelected(!!checked)} /></td>}
          {visible.map(column => { const definition = columns.find(item => item.field === column.id)!; const value = row.getValue(column.id); return <td key={column.id} className={cn(cell, column.getIsPinned() && 'bg-background group-hover:bg-linear-to-b group-hover:from-muted/50 group-hover:to-muted/50 group-data-[state=selected]:bg-muted')} style={pinned(column.id)}>{definition.render ? definition.render(row.original) : value === null || value === undefined ? '' : String(value)}</td>; })}
        </tr>) : <tr><td colSpan={Math.max(1, visible.length + (selectable ? 1 : 0))} className="h-28 p-8 text-center text-muted-foreground"><div role={state === 'error' ? 'alert' : 'status'} data-slot={`data-table-${state}`} data-testid={`${testId}-${state}`}>{state === 'error' ? error : i18n.t(`data.${state}`)}{state === 'error' && onRetry && <Button variant="outline" className="ml-3" onClick={onRetry}>{i18n.t('common.retry')}</Button>}</div></td></tr>}</tbody>
      </table>
    </div>
    <div data-slot="data-table-pagination" className="flex flex-wrap items-center justify-between gap-3">
      <div className="flex flex-wrap items-center gap-4 text-sm text-muted-foreground"><span>{i18n.t('data.total', { total })}</span>
        <label className="flex items-center gap-2">{i18n.t('data.pageSize')}<Select value={query.pageSize} items={pageSizes.map(size => ({ value: size, label: String(size) }))} disabled={loading} onValueChange={size => { if (size !== null) onQueryChange({ ...query, pageIndex: 0, pageSize: size }); }}>
          <SelectTrigger aria-label={i18n.t('data.pageSize')} className="h-8 min-w-20 px-2" /><SelectContent className="min-w-20">{pageSizes.map(size => <SelectItem key={size} value={size}>{size}</SelectItem>)}</SelectContent>
        </Select></label>
      </div>
      <div className="flex items-center gap-3"><span className="text-sm text-muted-foreground">{i18n.t('data.page', { page: query.pageIndex + 1 })}</span>
        <Pagination page={query.pageIndex + 1} totalPages={Math.max(1, Math.ceil(total / query.pageSize))} onPageChange={page => table.setPageIndex(page - 1)} disabled={loading} label={i18n.t('data.pagination')} previousLabel={i18n.t('data.previous')} nextLabel={i18n.t('data.next')} getPageLabel={page => i18n.t('data.page', { page })} />
      </div>
    </div>
  </section>;
}
