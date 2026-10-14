using System.Text.Json;
using Kun.Core.MultiTenancy;
using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Registry;

// 只操作平台宿主交给自己的 scope 条目；没有数据库写入口，也不加载代码程序集。
internal sealed class RegistryScopeReconciler(
    ITenantContext tenant, RegistryProfileComposer composer, PluginLoader runtime, IPluginContext owner,
    Func<ResolvedProfileEntry, PluginDefinition> resolve) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Dictionary<ScopeKey, ScopeState> _scopes = [];
    private readonly CancellationTokenSource _stop = new();
    private bool _disposed;

    internal async ValueTask ActivateAsync(string tenantId, ScopeKey scope, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (scope.Kind is not ("tenant" or "app" or "agent-session") || string.IsNullOrWhiteSpace(tenantId) ||
                (scope.Kind == "tenant" && scope.Id != tenantId)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            if (!_scopes.TryGetValue(scope, out var state)) _scopes.Add(scope, state = new ScopeState(tenantId, scope));
            if (state.TenantId != tenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            await Refresh(state, linked.Token).ConfigureAwait(false);
        }
        finally { _gate.Release(); }
    }

    internal async ValueTask<IReadOnlyList<RegistryScopeEntry>> ReadAsync(string tenantId, ScopeKey scope, ServiceKey key, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (scope.Kind != "tenant" || scope.Id != tenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            if (!_scopes.TryGetValue(scope, out var state)) _scopes.Add(scope, state = new ScopeState(tenantId, scope));
            if (state.TenantId != tenantId) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            // 与装载保持同一锁，不能在坏配置撤销局部条目的中途读到旧绑定。
            try { await Refresh(state, linked.Token).ConfigureAwait(false); }
            catch (Exception error) when (state.Blocked && !linked.IsCancellationRequested && error is QianErrorException or InvalidOperationException) { return []; }
            linked.Token.ThrowIfCancellationRequested();
            if (state.Blocked) return [];
            var live = runtime.DumpConfig().ToDictionary(entry => (entry.Scope, entry.Id));
            var result = new List<RegistryScopeEntry>();
            foreach (var registration in owner.Scoped(scope).List(key))
            {
                if (!live.TryGetValue((registration.Scope, registration.OwnerEntryId), out var entry)) continue;
                try { if (runtime.GetState(entry.Id, entry.Scope) != PluginState.Ready) continue; }
                catch (InvalidOperationException) { continue; } // 宿主可能在两个快照之间撤销全局节点。
                result.Add(new RegistryScopeEntry(entry.Id, entry.Plugin, entry.Version, entry.Scope, registration.Key, registration.Name));
            }
            return result.AsReadOnly();
        }
        finally { _gate.Release(); }
    }

    internal async ValueTask RefreshAllAsync(CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _stop.Token);
        await _gate.WaitAsync(linked.Token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            List<Exception> failures = [];
            foreach (var state in _scopes.Values)
            {
                try { await Refresh(state, linked.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (linked.IsCancellationRequested) { throw; }
                catch (Exception error) { failures.Add(error); }
            }
            if (failures.Count != 0) throw new AggregateException("部分 scope 无法重新装配。", failures);
        }
        finally { _gate.Release(); }
    }

    private async ValueTask Refresh(ScopeState state, CancellationToken ct)
    {
        try
        {
            using var current = tenant.BeginScope(state.TenantId);
            var (result, rows) = await composer.LoadWithPatchesAsync(state.Scope, ct).ConfigureAwait(false);
            var revisions = rows.ToDictionary(row => row.EntryId, row => row.Revision, StringComparer.Ordinal);
            if (!result.IsValid) throw new QianErrorException(new QianError(PlatformErrors.InputInvalid)
            {
                Errors = result.Diagnostics.Where(d => d.Severity == "error").Select(d => new QianFieldError(RegistryErrors.Invalid,
                    d.Error.Pointer ?? "registry/scope-patches.json#", new Dictionary<string, string> { ["reason"] = "scope 配置无法装配。" })).ToArray()
            });
            var name = state.Scope.Kind + ":" + state.Scope.Id;
            var entries = result.Snapshot!.Entries.Where(e => e.Scope == name).ToDictionary(e => e.Id, StringComparer.Ordinal);
            var order = result.Snapshot.MountOrder.Where(id => id.StartsWith(name + "/", StringComparison.Ordinal))
                .Select(id => id[(name.Length + 1)..]).ToArray();
            // 局部替换也隐藏原全局条目的其它贡献，避免更换 Provider 后残留旧目录项。
            var hidden = result.Snapshot.DisabledEntries.Where(e => e.Scope == name).Select(e => e.Id)
                .Concat(entries.Keys).ToHashSet(StringComparer.Ordinal);
            var changed = entries.Values.Where(entry => !state.Mounted.TryGetValue(entry.Id, out var previous) || !Same(previous, entry)).ToArray();
            var removed = state.Mounted.Keys.Where(id => !entries.ContainsKey(id)).ToArray();
            if (!state.Blocked && changed.Length == 0 && removed.Length == 0 && state.Hidden.SetEquals(hidden)) return;

            var definitions = changed.ToDictionary(entry => entry.Id, entry => Resolve(entry, state.Scope), StringComparer.Ordinal);
            foreach (var definition in definitions.Values)
                if (!state.Mounted.ContainsKey(definition.Entry.Id) && runtime.DumpConfig().Any(entry => entry.Scope == state.Scope && entry.Id == definition.Entry.Id))
                    throw new InvalidOperationException("scope 条目已由其它宿主装配者拥有。");

            var transition = hidden.Concat(state.Hidden).Concat(state.Mounted.Keys).ToHashSet(StringComparer.Ordinal);
            SetRestriction(state, transition, state.Blocked);
            foreach (var id in state.Order.Reverse().Where(id => removed.Contains(id, StringComparer.Ordinal) || definitions.ContainsKey(id)).ToArray())
                await Unmount(state, id, ct).ConfigureAwait(false);
            // 添加记录在 Mount 前完成，取消或 Apply 失败时仍能定位并清理本次拥有的条目。
            foreach (var entry in changed) state.Mounted[entry.Id] = entry;
            state.Order = order;
            if (definitions.Count != 0)
            {
                await runtime.MountAsync(order.Where(definitions.ContainsKey).Select(id => definitions[id]), ct).ConfigureAwait(false);
                if (order.FirstOrDefault(id => definitions.ContainsKey(id) && runtime.GetState(id, state.Scope) == PluginState.Failed) is { } failed)
                    throw ApplyFailed(state.Scope, entries[failed], revisions);
            }
            SetRestriction(state, hidden, false);
            state.Blocked = false;
            await runtime.WhenIdleAsync(ct).ConfigureAwait(false);
            if (state.Order.Concat(state.Mounted.Keys).FirstOrDefault(id => state.Mounted.ContainsKey(id) && runtime.GetState(id, state.Scope) == PluginState.Failed) is { } broken)
                throw ApplyFailed(state.Scope, state.Mounted[broken], revisions);
        }
        catch
        {
            if (!_stop.IsCancellationRequested)
            {
                // 已提交的禁用不能因重装失败而恢复旧权限；此 scope 先收紧，其他 scope 不受影响。
                SetRestriction(state, state.Hidden, true);
                state.Blocked = true;
                foreach (var id in state.Order.Reverse().Concat(state.Mounted.Keys).Distinct(StringComparer.Ordinal).ToArray())
                    await Unmount(state, id, _stop.Token).ConfigureAwait(false);
            }
            throw;
        }
    }

    // 管理面诊断（S1-RT-005 的 /api/lifecycle/diagnostics）需要定位到条目、组件修订与原因；异常只带标识与错误码，不带配置原文。
    private RegistryScopeApplyException ApplyFailed(ScopeKey scope, ResolvedProfileEntry entry, Dictionary<string, int> revisions)
    {
        var failure = runtime.GetFailure(entry.Id, scope);
        var reason = failure is QianErrorException qian ? qian.Error.Code.Code : PlatformErrors.Internal.Code;
        return new RegistryScopeApplyException(scope, entry.Id, entry.Plugin, revisions.TryGetValue(entry.Id, out var revision) ? revision : null, reason, failure);
    }

    private PluginDefinition Resolve(ResolvedProfileEntry entry, ScopeKey scope)
    {
        var definition = resolve(entry);
        var reference = entry.Plugin.Split('@', 2);
        if (definition.Entry.Id != entry.Id || definition.Entry.Scope != scope || definition.Entry.Plugin != reference[0] ||
            definition.Entry.Version != reference[1] || definition.Entry.Layer != entry.Layer || !JsonElement.DeepEquals(definition.Entry.Config, entry.Config))
            throw new InvalidOperationException("宿主 Provider 解析结果与已校验条目不一致。");
        if (!definition.Plugin.Inject.Select(key => key.Name).Order(StringComparer.Ordinal).SequenceEqual(entry.Inject.Order(StringComparer.Ordinal), StringComparer.Ordinal) ||
            entry.Provides.Any(key => !definition.Provides.Contains(new ServiceKey(key))))
            throw new InvalidOperationException("宿主 Provider 的依赖与服务声明不符合组件清单。");
        // 平台里的局部节点随 Registry 服务失效，不改变组件库在 minimal/standalone 下的依赖。
        return definition with { Plugin = new ScopedPlugin(definition.Plugin) };
    }

    private void SetRestriction(ScopeState state, HashSet<string> hidden, bool blockAll)
    {
        if (state.Filter is not null && state.Blocked == blockAll && state.Hidden.SetEquals(hidden)) return;
        var replacement = owner.Restrict(state.Scope, registration => !blockAll && !hidden.Contains(registration.OwnerEntryId));
        state.Filter?.Dispose(); state.Filter = replacement; state.Hidden = hidden; state.Blocked = blockAll;
    }

    private async ValueTask Unmount(ScopeState state, string id, CancellationToken ct)
    {
        if (!state.Mounted.ContainsKey(id)) return;
        if (runtime.DumpConfig().Any(entry => entry.Id == id && entry.Scope == state.Scope))
            await runtime.UnmountAsync(id, state.Scope, ct).ConfigureAwait(false);
        state.Mounted.Remove(id);
    }

    internal void Cancel() => _stop.Cancel();

    // 宿主先停止监听，再在 Runtime.Dispose 之前调用；不得从该 Runtime 的 Apply/effect 内等待此方法。
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed) return;
            _disposed = true;
            var ownerAlive = runtime.DumpConfig().Any(entry => entry.Id == owner.Entry.Id && entry.Scope == owner.Entry.Scope);
            foreach (var state in _scopes.Values)
            {
                if (ownerAlive) SetRestriction(state, state.Hidden, true);
                foreach (var id in state.Order.Reverse().Concat(state.Mounted.Keys).Distinct(StringComparer.Ordinal).ToArray())
                    await Unmount(state, id, CancellationToken.None).ConfigureAwait(false);
                state.Filter?.Dispose();
            }
            _scopes.Clear();
        }
        finally { _gate.Release(); }
    }

    private static bool Same(ResolvedProfileEntry left, ResolvedProfileEntry right) => left.Plugin == right.Plugin && left.Layer == right.Layer &&
        JsonElement.DeepEquals(left.Config, right.Config) && left.Inject.SequenceEqual(right.Inject, StringComparer.Ordinal) && left.Provides.SequenceEqual(right.Provides, StringComparer.Ordinal);

    private sealed class ScopeState(string tenantId, ScopeKey scope)
    {
        internal readonly string TenantId = tenantId;
        internal readonly ScopeKey Scope = scope;
        internal readonly Dictionary<string, ResolvedProfileEntry> Mounted = new(StringComparer.Ordinal);
        internal string[] Order = [];
        internal HashSet<string> Hidden = new(StringComparer.Ordinal);
        internal IDisposable? Filter;
        internal bool Blocked;
    }

    private sealed class ScopedPlugin(IQianPlugin inner) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject { get; } = inner.Inject.Append(RegistryDefinition.Key).Distinct().ToArray();
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => inner.ApplyAsync(ctx, config, ct);
    }
}
