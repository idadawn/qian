import { useMemo, type ReactNode } from 'react';
import { Combobox, ComboboxContent, ComboboxEmpty, ComboboxInput, ComboboxItem, ComboboxList, ComboboxTrigger, ComboboxValue, Icon, IconButton, cn, type TestIdProps } from '@idadawn/yue-core';
import type { PlatformI18n } from '@idadawn/yue-sdk';
import { SelectStatus } from './select-status';

// ApiSelect、DictSelect、UserSelect、RoleSelect、IconPicker 共用的可搜索选择器外形：core Combobox（Base UI，验证门已通过）的触发器 + 弹层内输入框，
// 不另造弹层或列表引擎。值一律是字符串标识（单选 string | null，多选 string[]），显示名由选项或调用方给出的已知名称解析。

export interface SelectOption {
  value: string;
  label: string;
  /** 次要说明（账号、编码等），显示在选项右侧并参与本地过滤。 */
  description?: string | undefined;
  disabled?: boolean | undefined;
  /** 选项前的装饰（如图标），不参与可访问名称。 */
  prefix?: ReactNode;
}

export interface SingleSelection {
  multiple?: false | undefined;
  /** 选中值的标识；未选为 null。 */
  value: string | null;
  onChange: (value: string | null) => void;
}

export interface MultipleSelection {
  /** 多选：值为标识数组，弹层里回车 / 点击切换选中，Escape 关闭。 */
  multiple: true;
  value: string[];
  onChange: (value: string[]) => void;
}

export type Selection = SingleSelection | MultipleSelection;

export interface BizSelectBaseProps extends TestIdProps {
  /** 文案实例：createBizI18n 或先 addBizLanguagePacks。 */
  i18n: PlatformI18n;
  /** 可访问名称（触发器与弹层的 aria-label）；可见标签由表单 Field 或宿主提供。 */
  label: string;
  disabled?: boolean | undefined;
  className?: string | undefined;
}

export interface OptionComboboxProps extends BizSelectBaseProps {
  slot: string;
  defaultTestId: string;
  selection: Selection;
  options: readonly SelectOption[];
  /** 已选值不在当前选项里（服务端搜索后、尚未加载）时的显示名。 */
  labels?: ReadonlyMap<string, string> | undefined;
  loading?: boolean | undefined;
  error?: unknown;
  onRetry?: (() => void) | undefined;
  notice?: string | undefined;
  /** client：在已加载选项里按名称与说明过滤；server：输入交给 onKeywordChange，由服务端过滤。 */
  search?: 'client' | 'server' | undefined;
  keyword?: string | undefined;
  onKeywordChange?: ((keyword: string) => void) | undefined;
}

export function selectedValues(selection: Selection): string[] {
  return selection.multiple ? selection.value : selection.value === null ? [] : [selection.value];
}

function contains(text: string | undefined, query: string): boolean {
  return !!text && text.toLocaleLowerCase().includes(query);
}

export function OptionCombobox({ slot, defaultTestId, selection, options, labels, loading = false, error, onRetry, notice, search = 'client', keyword, onKeywordChange, i18n, label, disabled, className, testId }: OptionComboboxProps) {
  const id = testId ?? defaultTestId;
  const byValue = useMemo(() => new Map(options.map((option) => [option.value, option])), [options]);
  const items = useMemo(() => options.map((option) => option.value), [options]);
  const labelOf = (value: string) => byValue.get(value)?.label ?? labels?.get(value) ?? value;
  const values = selectedValues(selection);
  const chosen = new Set(values);
  const summary = values.length === 0 ? i18n.t('biz.select')
    : values.length <= 2 ? new Intl.ListFormat(i18n.instance.language, { type: 'conjunction', style: 'short' }).format(values.map(labelOf))
      : i18n.t('biz.selected', { n: values.length });
  const prefix = values.length === 1 ? byValue.get(values[0]!)?.prefix : undefined;
  const filter = (value: string, query: string) => {
    const needle = query.trim().toLocaleLowerCase();
    if (!needle) return true;
    const option = byValue.get(value);
    return contains(option?.label ?? value, needle) || contains(option?.description, needle);
  };
  const clear = () => { if (selection.multiple) selection.onChange([]); else selection.onChange(null); };
  return <span data-slot={slot} data-testid={id} className={cn('inline-flex flex-wrap items-center gap-2', className)}>
    <Combobox<string, boolean>
      items={items}
      multiple={selection.multiple === true}
      value={selection.value}
      onValueChange={(next) => {
        if (selection.multiple) selection.onChange(Array.isArray(next) ? [...next] : []);
        else selection.onChange(typeof next === 'string' ? next : null);
      }}
      itemToStringLabel={labelOf}
      filter={search === 'server' ? null : filter}
      {...(search === 'server' ? { inputValue: keyword ?? '', onInputValueChange: (next: string) => onKeywordChange?.(next) } : {})}
      disabled={disabled}
    >
      <ComboboxTrigger aria-label={label} aria-busy={loading || undefined} testId={`${id}-trigger`} className="min-w-48 gap-2">
        <ComboboxValue>{() => <span className={cn('inline-flex min-w-0 items-center gap-2', values.length === 0 && 'text-muted-foreground')}>{prefix}<span className="truncate">{summary}</span></span>}</ComboboxValue>
        <Icon name="chevronsUpDown" className="text-muted-foreground" />
      </ComboboxTrigger>
      <ComboboxContent aria-label={i18n.t('biz.options', { label })} testId={`${id}-content`}>
        <ComboboxInput aria-label={i18n.t('biz.search', { label })} placeholder={i18n.t('biz.search', { label })} testId={`${id}-input`} className="mb-1" />
        <ComboboxEmpty testId={`${id}-empty`}>{loading ? i18n.t('biz.loading') : error ? i18n.t('biz.error') : options.length ? i18n.t('biz.noMatch') : i18n.t('biz.empty')}</ComboboxEmpty>
        <ComboboxList testId={`${id}-list`}>{(value: string) => {
          const option = byValue.get(value);
          return <ComboboxItem key={value} value={value} disabled={option?.disabled} testId={`${id}-option`} className="gap-2">
            {selection.multiple && <Icon name="check" className={cn(!chosen.has(value) && 'invisible')} />}
            {option?.prefix}
            <span className="truncate">{option?.label ?? value}</span>
            {option?.description && <span className="ms-auto truncate ps-3 text-xs text-muted-foreground">{option.description}</span>}
          </ComboboxItem>;
        }}</ComboboxList>
      </ComboboxContent>
    </Combobox>
    {values.length > 0 && !disabled && <IconButton variant="ghost" label={i18n.t('biz.clear', { label })} testId={`${id}-clear`} onClick={clear}><Icon name="close" /></IconButton>}
    <SelectStatus slot={slot} testId={id} i18n={i18n} loading={loading} showLoading={false} error={error} onRetry={onRetry} notice={notice} />
  </span>;
}
