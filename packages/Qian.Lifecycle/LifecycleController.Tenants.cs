using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using Qian.Data;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Lifecycle;

// 租户级状态机：Entitle / Provision / Upgrade / Uninstall / Purge。钩子不在数据库事务内执行：先在锁内认领执行记录（租约），
// 钩子返回后再在锁内写结论，执行记录的 Attempts 即认领凭据。
internal sealed partial class LifecycleController
{
    public async ValueTask<TenantPluginInstallView> EntitleAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId); CheckActor(actor);
        using var scope = tenant.BeginScope(tenantId);
        return await Locked(["install", tenantId, pluginId], async session =>
        {
            var row = await session.Context.Installs.SingleOrDefaultAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false);
            if (row is not null) return row.View();
            row = new TenantInstallRow { TenantId = tenantId, PluginId = pluginId, State = TenantInstallStates.Entitled };
            Touch(row, actor);
            session.Context.Installs.Add(row);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return row.View();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TenantPluginInstallView> ProvisionAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId); CheckActor(actor);
        using var scope = tenant.BeginScope(tenantId);
        using var origin = Origin();
        var plan = await Locked(["install", tenantId, pluginId], async session =>
        {
            var db = session.Context;
            var install = await db.Installs.SingleOrDefaultAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false);
            // 已开通：重复开通直接返回，不认领执行记录、不调用钩子、不写任何记录。
            if (install is { State: TenantInstallStates.Installed }) return Plan.Done(install.View());
            if (install is { State: TenantInstallStates.Upgrading }) throw new QianErrorException(new QianError(LifecycleErrors.InProgress));
            string version;
            if (install is { State: TenantInstallStates.Provisioning, TargetVersion: { } target })
            {
                // 续跑沿用原目标版本，同样先过第二道门：两次尝试之间批准可能已被整份替换为更小的内容。未批准时停在 Provisioning，不认领执行记录。
                var resumed = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == pluginId && row.Version == target, cancellationToken).ConfigureAwait(false)
                    ?? throw Conflict("续跑的目标版本没有已安装的修订。");
                await RequireApproval(db, resumed, cancellationToken).ConfigureAwait(false);
                version = target;
            }
            else
            {
                var active = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == pluginId && row.State == PluginRevisionStates.Active, cancellationToken).ConfigureAwait(false)
                    ?? throw Conflict("组件没有激活的修订，不能开通。");
                if (active.Form == "service" && active.Health != DeploymentHealthStates.Healthy) throw Conflict("激活的 service 修订尚未健康。");
                if (install is null)
                {
                    // policy.required 的平台组件恒视为已授权；其余组件须先经 qian.entitlement 授权。
                    if (!active.Required) throw new QianErrorException(new QianError(LifecycleErrors.NotEntitled));
                    install = new TenantInstallRow { TenantId = tenantId, PluginId = pluginId };
                    db.Installs.Add(install);
                }
                // 第二道门：权益（买没买）之后核对反向权限批准（同不同意）；未批准时不写安装行、不认领执行记录。
                await RequireApproval(db, active, cancellationToken).ConfigureAwait(false);
                // 清除后重新开通：旧执行记录随组件数据一起作废，钩子重新执行。
                if (install.State == TenantInstallStates.Purged) db.Runs.RemoveRange(await db.Runs.Where(run => run.PluginId == pluginId).ToListAsync(cancellationToken).ConfigureAwait(false));
                version = active.Version;
            }
            install.State = TenantInstallStates.Provisioning; install.TargetVersion = version; Touch(install, actor);
            var claim = await Claim(db, tenantId, pluginId, LifecycleHooks.TenantProvision, null, version, cancellationToken).ConfigureAwait(false);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return new Plan(install.View(), version, claim);
        }, cancellationToken).ConfigureAwait(false);
        if (plan.Finished) return plan.View;
        if (plan.Attempt is { } attempt) await Execute(tenantId, pluginId, LifecycleHooks.TenantProvision, null, plan.Version!, attempt, cancellationToken).ConfigureAwait(false);
        return await Locked(["install", tenantId, pluginId], async session =>
        {
            var install = await session.Context.Installs.SingleAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false);
            if (install.State != TenantInstallStates.Provisioning || install.TargetVersion != plan.Version) return install.View();
            install.State = TenantInstallStates.Installed; install.Version = plan.Version; install.TargetVersion = null; Touch(install, actor);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return install.View();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TenantPluginInstallView> UpgradeAsync(string tenantId, string pluginId, string fromVersion, string toVersion, string actor, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId); CheckActor(actor); CheckVersion(fromVersion, "#/fromVersion"); CheckVersion(toVersion, "#/toVersion");
        if (fromVersion == toVersion) throw LifecycleArtifact.Invalid("#/toVersion", "目标版本与当前版本相同。");
        using var scope = tenant.BeginScope(tenantId);
        using var origin = Origin();
        var plan = await Locked(["install", tenantId, pluginId], async session =>
        {
            var db = session.Context;
            var target = await db.Revisions.AsNoTracking().SingleOrDefaultAsync(row => row.PluginId == pluginId && row.Version == toVersion, cancellationToken).ConfigureAwait(false)
                ?? throw Conflict("目标版本没有已安装的修订。");
            var install = await db.Installs.SingleOrDefaultAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false);
            if (install is { State: TenantInstallStates.Installed } && install.Version == toVersion) return Plan.Done(install.View());
            var resuming = install is { State: TenantInstallStates.Upgrading } && install.Version == fromVersion && install.TargetVersion == toVersion;
            if (!resuming && !(install is { State: TenantInstallStates.Installed } && install.Version == fromVersion)) throw Conflict("租户当前不是该版本的已开通状态。");
            if (target.State == PluginRevisionStates.Retired) throw Conflict("目标修订已退役。");
            if (target.Form == "service" && target.Health != DeploymentHealthStates.Healthy) throw Conflict("目标 service 修订尚未健康。");
            // 升级新增的反向权限须重批（D7）；未批准时停在 Installed(from)，不认领执行记录。
            await RequireApproval(db, target, cancellationToken).ConfigureAwait(false);
            install!.State = TenantInstallStates.Upgrading; install.TargetVersion = toVersion; Touch(install, actor);
            var claim = await Claim(db, tenantId, pluginId, LifecycleHooks.TenantUpgrade, fromVersion, toVersion, cancellationToken).ConfigureAwait(false);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return new Plan(install.View(), toVersion, claim);
        }, cancellationToken).ConfigureAwait(false);
        if (plan.Finished) return plan.View;
        try
        {
            if (plan.Attempt is { } attempt) await Execute(tenantId, pluginId, LifecycleHooks.TenantUpgrade, fromVersion, toVersion, attempt, cancellationToken).ConfigureAwait(false);
        }
        catch (QianErrorException)
        {
            await Settle(tenantId, pluginId, fromVersion, toVersion, actor, false, [], cancellationToken).ConfigureAwait(false);
            throw;
        }
        // 改写本租户 scope 中引用 from 的条目并合入；任何条目装配失败都整体撤回，租户继续由 from 处理。
        var scopeKey = new ScopeKey("tenant", tenantId);
        var registry = context.Registry();
        var rewritten = new List<(string EntryId, JsonElement Previous, int Revision)>();
        RegistryScopeDiagnostic? failure = null;
        string? current = null;
        try
        {
            foreach (var patch in await registry.GetScopePatchesAsync(scopeKey, cancellationToken).ConfigureAwait(false))
            {
                if (!patch.Patch.TryGetProperty("plugin", out var reference) || reference.GetString() != pluginId + "@" + fromVersion) continue;
                var next = JsonNode.Parse(patch.Patch.GetRawText())!.AsObject();
                next["plugin"] = pluginId + "@" + toVersion;
                current = patch.EntryId;
                var saved = await registry.MountAsync(scopeKey, JsonSerializer.SerializeToElement(next), patch.Revision, actor, cancellationToken).ConfigureAwait(false);
                rewritten.Add((patch.EntryId, patch.Patch, saved.Revision));
            }
            failure = await Synchronize(registry, scopeKey, cancellationToken).ConfigureAwait(false);
        }
        catch (QianErrorException error)
        {
            // 写入本身被拒（如新版本的配置 Schema 不接受原配置）：同样整体撤回。
            failure = new RegistryScopeDiagnostic(Name(scopeKey), current, pluginId + "@" + toVersion, null, error.Error.Code.Code, Now());
        }
        if (failure is not null)
        {
            foreach (var item in rewritten.AsEnumerable().Reverse())
                await registry.RestoreScopePatchAsync(scopeKey, item.EntryId, item.Previous, item.Revision, actor, cancellationToken).ConfigureAwait(false);
            await Synchronize(registry, scopeKey, cancellationToken).ConfigureAwait(false);
            var entryId = failure.EntryId ?? current ?? pluginId;
            int? revision = failure.Revision ?? (rewritten.Any(item => item.EntryId == entryId) ? rewritten.First(item => item.EntryId == entryId).Revision : null);
            await RecordFailure(scopeKey, entryId, failure.Plugin ?? pluginId + "@" + toVersion, revision, failure.ReasonCode, cancellationToken).ConfigureAwait(false);
            await Settle(tenantId, pluginId, fromVersion, toVersion, actor, false, [], cancellationToken).ConfigureAwait(false);
            throw new QianErrorException(new QianError(LifecycleErrors.MountFailed, new Dictionary<string, string> { ["entryId"] = entryId, ["reasonCode"] = failure.ReasonCode }));
        }
        return await Settle(tenantId, pluginId, fromVersion, toVersion, actor, true, rewritten.Select(item => item.EntryId).ToArray(), cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TenantPluginInstallView> UninstallAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId); CheckActor(actor);
        using var scope = tenant.BeginScope(tenantId);
        using var origin = Origin();
        var plan = await Locked(["install", tenantId, pluginId], async session =>
        {
            var install = await session.Context.Installs.SingleOrDefaultAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
            if (install.State == TenantInstallStates.Uninstalled) return Plan.Done(install.View());
            if (install.State != TenantInstallStates.Installed) throw Conflict("只有已开通的组件可以卸载。");
            var claim = await Claim(session.Context, tenantId, pluginId, LifecycleHooks.TenantUninstall, install.Version, install.Version!, cancellationToken).ConfigureAwait(false);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return new Plan(install.View(), install.Version, claim);
        }, cancellationToken).ConfigureAwait(false);
        if (plan.Finished) return plan.View;
        if (plan.Attempt is { } attempt) await Execute(tenantId, pluginId, LifecycleHooks.TenantUninstall, plan.Version, plan.Version!, attempt, cancellationToken).ConfigureAwait(false);
        // 撤下本租户 scope 中引用该组件任一版本的条目（同事务发 unmounted 事件）；业务数据保留。
        var scopeKey = new ScopeKey("tenant", tenantId);
        var registry = context.Registry();
        var removed = new List<string>();
        foreach (var patch in await registry.GetScopePatchesAsync(scopeKey, cancellationToken).ConfigureAwait(false))
        {
            if (!patch.Patch.TryGetProperty("plugin", out var reference) || reference.GetString()?.StartsWith(pluginId + "@", StringComparison.Ordinal) != true) continue;
            await registry.UnmountAsync(scopeKey, patch.EntryId, patch.Revision, actor, cancellationToken).ConfigureAwait(false);
            removed.Add(patch.EntryId);
        }
        await Synchronize(registry, scopeKey, cancellationToken).ConfigureAwait(false);
        await ClearFailures(scopeKey, removed, cancellationToken).ConfigureAwait(false);
        return await Locked(["install", tenantId, pluginId], async session =>
        {
            var install = await session.Context.Installs.SingleAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false);
            if (install.State != TenantInstallStates.Installed) return install.View();
            install.State = TenantInstallStates.Uninstalled; Touch(install, actor);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return install.View();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TenantPluginInstallView> PurgeAsync(string tenantId, string pluginId, string actor, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId); CheckActor(actor);
        using var scope = tenant.BeginScope(tenantId);
        using var origin = Origin();
        var plan = await Locked(["install", tenantId, pluginId], async session =>
        {
            var install = await session.Context.Installs.SingleOrDefaultAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false) ?? throw NotFound();
            if (install.State == TenantInstallStates.Purged) return Plan.Done(install.View());
            if (install.State != TenantInstallStates.Uninstalled) throw Conflict("只有已卸载的组件可以清除。");
            var claim = await Claim(session.Context, tenantId, pluginId, LifecycleHooks.TenantPurge, install.Version, install.Version!, cancellationToken).ConfigureAwait(false);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return new Plan(install.View(), install.Version, claim);
        }, cancellationToken).ConfigureAwait(false);
        if (plan.Finished) return plan.View;
        if (plan.Attempt is { } attempt) await Execute(tenantId, pluginId, LifecycleHooks.TenantPurge, plan.Version, plan.Version!, attempt, cancellationToken).ConfigureAwait(false);
        return await Locked(["install", tenantId, pluginId], async session =>
        {
            var install = await session.Context.Installs.SingleAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false);
            if (install.State != TenantInstallStates.Uninstalled) return install.View();
            install.State = TenantInstallStates.Purged; Touch(install, actor);
            await Save(session, null, cancellationToken).ConfigureAwait(false);
            return install.View();
        }, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask<TenantPluginInstallView?> GetInstallAsync(string tenantId, string pluginId, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId);
        using var scope = tenant.BeginScope(tenantId);
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(cancellationToken).ConfigureAwait(false);
        return (await session.Context.Installs.AsNoTracking().SingleOrDefaultAsync(item => item.PluginId == pluginId, cancellationToken).ConfigureAwait(false))?.View();
    }

    public async ValueTask<IReadOnlyList<LifecycleRunView>> GetRunsAsync(string tenantId, string pluginId, CancellationToken cancellationToken = default)
    {
        CheckTenant(tenantId); CheckPlugin(pluginId);
        using var scope = tenant.BeginScope(tenantId);
        await using var session = await context.Data().OpenAsync<LifecycleDbContext>(cancellationToken).ConfigureAwait(false);
        var rows = await session.Context.Runs.AsNoTracking().Where(run => run.PluginId == pluginId).ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.OrderBy(run => run.StartedAt).ThenBy(run => run.Hook, StringComparer.Ordinal).Select(run => run.View()).ToArray();
    }

    // 升级收尾：成功时 Installed(to) 并随事务发 upgraded 事件、清掉这些条目的旧诊断；失败时回到 Installed(from)。
    private Task<TenantPluginInstallView> Settle(string tenantId, string pluginId, string fromVersion, string toVersion, string actor, bool succeeded,
        IReadOnlyList<string> entries, CancellationToken ct) => Locked(["install", tenantId, pluginId], async session =>
    {
        var install = await session.Context.Installs.SingleAsync(item => item.PluginId == pluginId, ct).ConfigureAwait(false);
        if (install.State != TenantInstallStates.Upgrading || install.Version != fromVersion || install.TargetVersion != toVersion) return install.View();
        install.State = TenantInstallStates.Installed; install.TargetVersion = null; Touch(install, actor);
        LifecycleChange? change = null;
        if (succeeded)
        {
            install.Version = toVersion;
            install.Raise(change = new TenantPluginUpgraded(pluginId, fromVersion, toVersion));
            var scope = Name(new ScopeKey("tenant", tenantId));
            session.Context.MountFailures.RemoveRange(await session.Context.MountFailures.Where(row => row.Scope == scope && entries.Contains(row.EntryId)).ToListAsync(ct).ConfigureAwait(false));
        }
        await Save(session, change, ct).ConfigureAwait(false);
        return install.View();
    }, ct);

    // 认领执行记录：已成功返回 null（不再执行）；他人持有未过期的租约时报告进行中；失败或租约过期的记录由本次接管。
    private async Task<int?> Claim(LifecycleDbContext db, string tenantId, string pluginId, string hook, string? fromVersion, string toVersion, CancellationToken ct)
    {
        var now = Now();
        var run = await db.Runs.SingleOrDefaultAsync(item => item.PluginId == pluginId && item.Hook == hook && item.ToVersion == toVersion, ct).ConfigureAwait(false);
        if (run is { Status: LifecycleRunStates.Succeeded }) return null;
        if (run is { Status: LifecycleRunStates.Running } && run.StartedAt + settings.RunLease > now) throw new QianErrorException(new QianError(LifecycleErrors.InProgress));
        if (run is null)
        {
            run = new LifecycleRunRow { TenantId = tenantId, PluginId = pluginId, Hook = hook, ToVersion = toVersion, IdempotencyKey = IdempotencyKey(tenantId, pluginId, hook, toVersion) };
            db.Runs.Add(run);
        }
        run.FromVersion = fromVersion; run.Status = LifecycleRunStates.Running; run.Attempts++; run.ReasonCode = null; run.Result = null; run.StartedAt = now; run.FinishedAt = null;
        return run.Attempts;
    }

    // 执行一次钩子并写结论。钩子在该租户的数据上下文与该组件的事件来源下运行；没有实现的钩子记为成功、无操作。
    private async Task Execute(string tenantId, string pluginId, string hook, string? fromVersion, string toVersion, int attempt, CancellationToken ct)
    {
        string? result = null, reason = null;
        try
        {
            if (options.Hooks(pluginId, toVersion) is { } hooks)
            {
                using var producer = Origin(pluginId);
                var value = await hooks.InvokeAsync(new LifecycleHookContext
                {
                    Hook = hook, TenantId = tenantId, PluginId = pluginId, FromVersion = fromVersion, ToVersion = toVersion,
                    IdempotencyKey = IdempotencyKey(tenantId, pluginId, hook, toVersion), Context = context.Scoped(new ScopeKey("tenant", tenantId))
                }, ct).ConfigureAwait(false);
                // 结果只留 64 KiB 以内的 JSON；更大的结果不保存，不影响结论。
                if (value is { ValueKind: not JsonValueKind.Undefined } json && json.GetRawText() is { Length: <= 65536 } raw) result = raw;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            reason = error is QianErrorException qian ? qian.Error.Code.Code : PlatformErrors.Internal.Code;
            LifecycleLog.HookFailed(context.Logger, error, hook, pluginId, toVersion);
        }
        await Locked(["install", tenantId, pluginId], async session =>
        {
            var run = await session.Context.Runs.SingleAsync(item => item.PluginId == pluginId && item.Hook == hook && item.ToVersion == toVersion, ct).ConfigureAwait(false);
            // 租约已被重试接管时不覆盖它的结论。
            if (run.Attempts != attempt || run.Status != LifecycleRunStates.Running) return 0;
            run.Status = reason is null ? LifecycleRunStates.Succeeded : LifecycleRunStates.Failed; run.ReasonCode = reason; run.Result = result; run.FinishedAt = Now();
            await Save(session, null, ct).ConfigureAwait(false);
            return 0;
        }, ct).ConfigureAwait(false);
        if (reason is not null) throw new QianErrorException(new QianError(LifecycleErrors.HookFailed, new Dictionary<string, string> { ["hook"] = hook, ["reasonCode"] = reason }));
    }

    private void Touch(TenantInstallRow row, string actor)
    {
        row.UpdatedBy = actor; row.UpdatedAt = Now(); row.RowVersion++;
    }

    private static string Name(ScopeKey scope) => scope.Kind + ":" + scope.Id;

    // 一次状态机步骤的计划：Finished 表示无事可做（幂等返回）；Attempt 为本次认领的执行记录凭据，null 表示钩子已成功、不再执行。
    private sealed record Plan(TenantPluginInstallView View, string? Version, int? Attempt, bool Finished = false)
    {
        internal static Plan Done(TenantPluginInstallView view) => new(view, null, null, true);
    }
}
