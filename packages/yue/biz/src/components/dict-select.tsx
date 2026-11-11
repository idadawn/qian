import type { PlatformClient } from '@idadawn/yue-sdk';
import { loadOrganizationTypes } from '../api';
import { useRemoteOptions } from '../remote-options';
import { OptionCombobox, type BizSelectBaseProps, type Selection } from './option-combobox';

export interface DictionaryItem {
  value: string;
  /** 服务端给出的显示名（租户可配字典）。 */
  label?: string | undefined;
  /** 内置字典的文案 key；同时给出时优先于 label。 */
  labelKey?: string | undefined;
  disabled?: boolean | undefined;
}

/** 字典定义：稳定 id + 经注入的 PlatformClient 取全部字典项的加载器。 */
export interface Dictionary {
  readonly id: string;
  load(client: PlatformClient, signal: AbortSignal): Promise<readonly DictionaryItem[]>;
}

export function defineDictionary(id: string, load: Dictionary['load']): Dictionary {
  return Object.freeze({ id, load });
}

/** 租户组织类型字典（GET /api/directory/org-types，PRD 7.4.3「类型为租户可配字典」）。 */
export const orgTypeDictionary = defineDictionary('qian.directory.org-type', async (client, signal) =>
  (await loadOrganizationTypes(client, signal)).map((type) => ({ value: type.code, label: type.name })));

// 字典全量缓存：按 PlatformClient 实例与字典 id 共享一次在途请求；失败即移出缓存，下次重新加载。
// 共享请求不随任一调用方中止，每个调用方只中止自己的等待。
const caches = new WeakMap<PlatformClient, Map<string, Promise<readonly DictionaryItem[]>>>();

function abortable<T>(promise: Promise<T>, signal: AbortSignal): Promise<T> {
  if (signal.aborted) return Promise.reject(signal.reason);
  return new Promise<T>((resolve, reject) => {
    const abort = () => reject(signal.reason);
    signal.addEventListener('abort', abort, { once: true });
    promise.then(resolve, reject).finally(() => signal.removeEventListener('abort', abort));
  });
}

export function loadDictionary(client: PlatformClient, dictionary: Dictionary, signal?: AbortSignal): Promise<readonly DictionaryItem[]> {
  let cache = caches.get(client);
  if (!cache) { cache = new Map(); caches.set(client, cache); }
  const store = cache;
  let pending = store.get(dictionary.id);
  if (!pending) {
    const shared = Promise.resolve().then(() => dictionary.load(client, new AbortController().signal)).then((items) => Object.freeze([...items]));
    shared.catch(() => { if (store.get(dictionary.id) === shared) store.delete(dictionary.id); });
    store.set(dictionary.id, shared);
    pending = shared;
  }
  return signal ? abortable(pending, signal) : pending;
}

/** 字典在别处被修改后清除缓存（不传 id 清除该客户端的全部字典）；已挂载的选择器下次重试或重新挂载时取新数据。 */
export function invalidateDictionary(client: PlatformClient, id?: string): void {
  const cache = caches.get(client);
  if (!cache) return;
  if (id === undefined) cache.clear();
  else cache.delete(id);
}

export type DictSelectProps = BizSelectBaseProps & Selection & {
  client: PlatformClient;
  dictionary: Dictionary;
};

/** 字典选择器：字典项全量加载并按客户端缓存，在已加载项里过滤。 */
export function DictSelect(props: DictSelectProps) {
  const { client, dictionary, i18n, label, disabled, className, testId } = props;
  const remote = useRemoteOptions({ client, load: (current, { signal }) => loadDictionary(current, dictionary, signal), reloadKey: dictionary.id });
  const options = remote.items.map((item) => ({ value: item.value, label: item.labelKey ? i18n.t(item.labelKey) : item.label ?? item.value, disabled: item.disabled }));
  return <OptionCombobox slot="dict-select" defaultTestId="yue-dict-select" selection={props} options={options}
    loading={remote.loading} error={remote.error} onRetry={remote.reload}
    i18n={i18n} label={label} disabled={disabled} className={className} testId={testId} />;
}
