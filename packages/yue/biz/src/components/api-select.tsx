import { useMemo, useState } from 'react';
import type { PlatformClient } from '@idadawn/yue-sdk';
import { useRemoteOptions, type RemoteLoader } from '../remote-options';
import { OptionCombobox, type BizSelectBaseProps, type Selection } from './option-combobox';

export type ApiSelectProps<T> = BizSelectBaseProps & Selection & {
  /** 宿主注入的 PlatformClient；load 只能经它取数。 */
  client: PlatformClient;
  /** 远程选项加载器，收到 client 与 { keyword, signal }。 */
  load: RemoteLoader<T>;
  getValue: (item: T) => string;
  getLabel: (item: T) => string;
  getDescription?: ((item: T) => string | undefined) | undefined;
  isItemDisabled?: ((item: T) => boolean) | undefined;
  /** server（默认）：输入去抖后作为 keyword 交给 load；client：只加载一次，在已加载项里过滤。 */
  search?: 'server' | 'client' | undefined;
  /** 变化即重新加载（如上级筛选条件）。 */
  reloadKey?: string | undefined;
  /** 服务端搜索的去抖毫秒数，默认 250。 */
  debounceMs?: number | undefined;
};

/** 远程选项选择器（ApiComponent 模式）：任意接口的选项都经 load + 注入的 PlatformClient 取得，组件不直接发请求。 */
export function ApiSelect<T>(props: ApiSelectProps<T>) {
  const { client, load, getValue, getLabel, getDescription, isItemDisabled, search = 'server', reloadKey, debounceMs, i18n, label, disabled, className, testId } = props;
  const [keyword, setKeyword] = useState('');
  const remote = useRemoteOptions({ client, load, keyword: search === 'server' ? keyword : '', debounceMs, reloadKey });
  const options = useMemo(() => remote.items.map((item) => ({ value: getValue(item), label: getLabel(item), description: getDescription?.(item), disabled: isItemDisabled?.(item) })), [remote.items, getValue, getLabel, getDescription, isItemDisabled]);
  const labels = useMemo(() => new Map(remote.seen.map((item) => [getValue(item), getLabel(item)])), [remote.seen, getValue, getLabel]);
  return <OptionCombobox slot="api-select" defaultTestId="yue-api-select" selection={props} options={options} labels={labels}
    loading={remote.loading} error={remote.error} onRetry={remote.reload} search={search} keyword={keyword} onKeywordChange={setKeyword}
    i18n={i18n} label={label} disabled={disabled} className={className} testId={testId} />;
}
