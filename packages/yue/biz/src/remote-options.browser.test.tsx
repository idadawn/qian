import { act, cleanup, render, screen } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { createMockTransport, PlatformClient } from '@idadawn/yue-sdk';
import { useRemoteOptions, type RemoteLoader } from './remote-options';

// useRemoteOptions 的竞态：只采纳最新请求。加载器故意不理会 signal（宿主自己写的加载器可能如此），被中止的旧请求照样会返回或失败，
// 晚到的旧结果不得覆盖新结果，也不得让界面回到加载态。

afterEach(cleanup);

const client = new PlatformClient({ baseUrl: 'https://tenant.test', transport: createMockTransport([]) });

function deferred<T>() {
  let resolve!: (value: T) => void;
  let reject!: (reason: unknown) => void;
  const promise = new Promise<T>((done, fail) => { resolve = done; reject = fail; });
  return { promise, resolve, reject };
}

function Harness({ load, keyword, reloadKey }: { load: RemoteLoader<string>; keyword?: string; reloadKey?: string }) {
  const remote = useRemoteOptions({ client, load, keyword, reloadKey, debounceMs: 0 });
  return <output data-testid="remote" data-loading={String(remote.loading)} data-error={remote.error ? 'true' : 'false'}>{remote.items.join(',')}</output>;
}
const output = () => screen.getByTestId('remote');
async function expectSettledOn(text: string) {
  await vi.waitFor(() => expect(output().textContent).toBe(text));
  expect(output().dataset.loading).toBe('false');
  expect(output().dataset.error).toBe('false');
}

describe('useRemoteOptions 只采纳最新请求', () => {
  it('关键字变化：先发的慢请求晚于后发的快请求返回时，显示后发请求的结果', async () => {
    const slow = deferred<readonly string[]>();
    const load = vi.fn<RemoteLoader<string>>(async (_client, { keyword }) => keyword === 'slow' ? slow.promise : [`${keyword}-result`]);
    const { rerender } = render(<Harness load={load} keyword="slow" />);
    await vi.waitFor(() => expect(load).toHaveBeenCalledTimes(1));
    rerender(<Harness load={load} keyword="fast" />);
    await expectSettledOn('fast-result');
    expect(load.mock.calls[0]![1].signal.aborted).toBe(true);
    await act(async () => { slow.resolve(['slow-result']); await slow.promise; });
    await expectSettledOn('fast-result');
  });

  it('reloadKey 变化：上一个键的请求晚于新键的请求返回时，显示新键的结果', async () => {
    const slow = deferred<readonly string[]>();
    const { rerender } = render(<Harness load={() => slow.promise} reloadKey="a" />);
    rerender(<Harness load={async () => ['b-result']} reloadKey="b" />);
    await expectSettledOn('b-result');
    await act(async () => { slow.resolve(['a-result']); await slow.promise; });
    await expectSettledOn('b-result');
  });

  it('上一个键的请求晚到的失败不覆盖新键的结果，也不显示错误', async () => {
    const slow = deferred<readonly string[]>();
    const { rerender } = render(<Harness load={() => slow.promise} reloadKey="a" />);
    rerender(<Harness load={async () => ['b-result']} reloadKey="b" />);
    await expectSettledOn('b-result');
    await act(async () => { slow.reject(new Error('旧请求失败')); await slow.promise.catch(() => {}); });
    await expectSettledOn('b-result');
  });
});
