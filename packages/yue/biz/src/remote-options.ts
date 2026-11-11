import { useCallback, useEffect, useEffectEvent, useMemo, useState } from 'react';
import type { PlatformClient } from '@idadawn/yue-sdk';

export interface RemoteQuery {
  /** 搜索关键字（已去抖）；不做服务端搜索的加载器可以忽略。 */
  keyword: string;
  /** 参数变化、重试或卸载时中止；加载器应把它传给 PlatformClient 的请求。 */
  signal: AbortSignal;
}

/** 远程选项加载器：只能经传入的 PlatformClient 取数（ApiComponent 模式）。 */
export type RemoteLoader<T> = (client: PlatformClient, query: RemoteQuery) => Promise<readonly T[]>;

interface RemoteRequestOptions {
  /** 宿主注入的 PlatformClient；换实例即重新加载。 */
  client: PlatformClient;
  /** 当前输入的关键字；变化后去抖 debounceMs 再加载。 */
  keyword?: string | undefined;
  /** 关键字去抖毫秒数，默认 250。 */
  debounceMs?: number | undefined;
  /** false 时不发起加载，保留已有结果。默认 true。 */
  enabled?: boolean | undefined;
  /** 变化即重新加载的键（如组织范围）；load 本身不参与比较，内联函数不会造成重复请求。 */
  reloadKey?: string | undefined;
}

export interface UseRemoteOptionsOptions<T> extends RemoteRequestOptions {
  load: RemoteLoader<T>;
}

export interface RemoteOptions<T> {
  /** 最近一次成功加载的结果；重新加载期间保留旧结果。 */
  items: readonly T[];
  /** 本客户端最近加载过的结果（新的在后，最多 2000 条），供服务端搜索后仍能解析已选项的显示名。 */
  seen: readonly T[];
  loading: boolean;
  /** 最近一次加载的错误；成功后清空。 */
  error: unknown;
  reload: () => void;
}

interface RemoteResource<R> {
  data: R | undefined;
  history: readonly R[];
  loading: boolean;
  error: unknown;
  reload: () => void;
}

const HISTORY_LIMIT = 20;
const SEEN_LIMIT = 2000;
const NONE: readonly never[] = Object.freeze([]);

interface Settled<R> { key: string; client: PlatformClient; data: R | undefined; history: readonly R[]; error: unknown }

/**
 * 远程数据的通用加载：去抖、竞态（只采纳最新请求）、中止、重试，加载中保留旧结果。
 * 加载状态由「当前请求键是否已有结论」派生，不在 effect 里同步 setState。
 */
export function useRemoteResource<R>({ client, load, keyword = '', debounceMs = 250, enabled = true, reloadKey = '' }: RemoteRequestOptions & { load: (client: PlatformClient, query: RemoteQuery) => Promise<R> }): RemoteResource<R> {
  const [debounced, setDebounced] = useState(keyword);
  const [attempt, setAttempt] = useState(0);
  const [settled, setSettled] = useState<Settled<R> | null>(null);
  useEffect(() => {
    if (keyword === debounced) return;
    const timer = setTimeout(() => setDebounced(keyword), debounceMs);
    return () => clearTimeout(timer);
  }, [keyword, debounced, debounceMs]);
  const requestKey = JSON.stringify([reloadKey, debounced, attempt]);
  const run = useEffectEvent((signal: AbortSignal) => load(client, { keyword: debounced, signal }));
  useEffect(() => {
    if (!enabled) return;
    const controller = new AbortController();
    let pending: Promise<R>;
    try { pending = run(controller.signal); } catch (error) { pending = Promise.reject(error); }
    pending.then(
      (data) => { if (!controller.signal.aborted) setSettled((previous) => ({ key: requestKey, client, data, history: [...(previous?.client === client ? previous.history : []), data].slice(-HISTORY_LIMIT), error: null })); },
      (error: unknown) => { if (!controller.signal.aborted) setSettled((previous) => ({ key: requestKey, client, data: previous?.client === client ? previous.data : undefined, history: previous?.client === client ? previous.history : [], error })); },
    );
    return () => controller.abort();
  }, [client, requestKey, enabled]);
  const reload = useCallback(() => setAttempt((value) => value + 1), []);
  const own = settled !== null && settled.client === client ? settled : null;
  const current = own !== null && own.key === requestKey;
  return { data: own?.data, history: own?.history ?? [], loading: enabled && (!current || keyword !== debounced), error: current ? own.error : null, reload };
}

/**
 * 远程选项（ApiSelect 的取数核心，也可直接用于自定义选择器）。组件本身不发请求，取数全部由 load 经注入的 PlatformClient 完成。
 */
export function useRemoteOptions<T>(options: UseRemoteOptionsOptions<T>): RemoteOptions<T> {
  const { data, history, loading, error, reload } = useRemoteResource<readonly T[]>(options);
  const seen = useMemo(() => history.flat().slice(-SEEN_LIMIT), [history]);
  return { items: data ?? NONE, seen, loading, error, reload };
}
