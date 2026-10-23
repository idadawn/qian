using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Audit;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Lifecycle.Tests;

// S1-RT-037 验收 1、3：租户级生命周期操作（开通、升级、卸载、清除）入该租户审计链（ADR-009 D9.8），与完成的状态迁移同一可串行化事务，审计写不进去或
// 提交失败时两者都不留；钩子重试、续跑、幂等返回不重复记；同租户链冲突换新会话重做；宿主没有审计时 fail-closed 并记宿主级告警。
// 平台级操作（安装、激活、回切、弃用、退役）入平台链：经审计端口的平台链同事务入口与状态变更同一可串行化事务（S1-AUD-009），审计写不进去或提交失败时
// 两者都不留，平台链冲突换新会话重做；平台链未配置、缺同事务函数执行权或宿主撤下审计时同样 fail-closed。真实 PostgreSQL、Registry、Audit 与 Lifecycle（LifecycleHost）。
public sealed class LifecycleAuditTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string TenantA = "01900000-0000-7000-8000-00000000000a";
    private const string TenantB = "01900000-0000-7000-8000-00000000000b";
    private const string Plugin = "acme.widget";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly SigningKey Key = new();
    private static readonly EventOrigin Operator = new("qian.ops", EventActor.User("platform-operator"));

    [Fact]
    public async Task 开通升级卸载清除各写一条租户审计链条目_主体取事件作用域否则为本组件服务主体_重复操作续跑与钩子重试不重复记()
    {
        var hooks = new FlakyHooks();
        await using var host = await Start(hooks);
        await Install(host, "1.0.0"); await Install(host, "1.0.1", activate: false);
        await host.Lifecycle.EntitleAsync(TenantA, Plugin, "operator", Ct);
        using (EventEnvelopeScope.Begin(Operator))
        {
            // 开通：钩子第一次失败停在 Provisioning，不记；续跑成功后恰一条，主体是作用域里的用户，发生时间即安装行的更新时间。
            await AssertCode(LifecycleErrors.HookFailed, () => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask());
            Assert.Empty(await Chain(host, TenantA));
            var installed = await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct);
            Assert.Equal((TenantInstallStates.Installed, "1.0.0"), (installed.State, installed.Version));
            var provisioned = Assert.Single(await Chain(host, TenantA));
            AssertRecord(provisioned, LifecycleController.ProvisionAuditCapability, "user", "platform-operator", "1.0.0", installed.UpdatedAt,
                new { pluginId = Plugin, @operator = "operator", version = "1.0.0" }, "1.0.0");
            Assert.Equal(2, hooks.Provisions);
            // 重复开通：无副作用，不记。
            await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct);
            Assert.Single(await Chain(host, TenantA));
            // 升级：一条 upgrade，资源指向目标版本，载荷带 from / to，序号接在开通条目之后。
            var upgraded = await host.Lifecycle.UpgradeAsync(TenantA, Plugin, "1.0.0", "1.0.1", "operator", Ct);
            Assert.Equal("1.0.1", upgraded.Version);
            var entries = await Chain(host, TenantA);
            Assert.Equal(2, entries.Length); Assert.Equal(entries[0].Sequence + 1, entries[1].Sequence);
            AssertRecord(entries[1], LifecycleController.UpgradeAuditCapability, "user", "platform-operator", "1.0.1", upgraded.UpdatedAt,
                new { pluginId = Plugin, @operator = "operator", fromVersion = "1.0.0", toVersion = "1.0.1" }, "1.0.0", "1.0.1");
            // 卸载：一条 uninstall；重复卸载不记。
            var uninstalled = await host.Lifecycle.UninstallAsync(TenantA, Plugin, "operator", Ct);
            Assert.Equal(TenantInstallStates.Uninstalled, uninstalled.State);
            await host.Lifecycle.UninstallAsync(TenantA, Plugin, "operator", Ct);
            entries = await Chain(host, TenantA);
            Assert.Equal(3, entries.Length);
            AssertRecord(entries[2], LifecycleController.UninstallAuditCapability, "user", "platform-operator", "1.0.1", uninstalled.UpdatedAt,
                new { pluginId = Plugin, @operator = "operator", version = "1.0.1" }, "1.0.1");
        }
        // 清除：调用方没有打开事件作用域时主体是本组件服务主体（与集成事件同一口径），操作者仍在载荷里。
        var purged = await host.Lifecycle.PurgeAsync(TenantA, Plugin, "ops-cli", Ct);
        Assert.Equal(TenantInstallStates.Purged, purged.State);
        var all = await Chain(host, TenantA);
        Assert.Equal(4, all.Length);
        AssertRecord(all[3], LifecycleController.PurgeAuditCapability, "service", "qian.lifecycle", "1.0.1", purged.UpdatedAt,
            new { pluginId = Plugin, @operator = "ops-cli", version = "1.0.1" }, "1.0.1");
        // 授权本身不入链；B 的链上没有 A 的操作。
        await host.Lifecycle.EntitleAsync(TenantB, Plugin, "operator", Ct);
        Assert.Empty(await Chain(host, TenantB));
        Assert.Equal(4, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NOT NULL", Ct));
    }

    [Fact]
    public async Task 审计与状态迁移同一事务_审计写入失败或提交失败时两者都不留且可续跑_宿主没有审计时拒绝不改状态并记宿主级告警()
    {
        var logs = new WarningLogs();
        await using var host = await Start(services: services => services.AddSingleton<ILoggerProvider>(logs));
        await Install(host, "1.0.0");
        await host.Lifecycle.EntitleAsync(TenantA, Plugin, "operator", Ct);
        // 审计条目写不进去：Provisioning → Installed 随同一事务回滚，安装行停在 Provisioning，执行记录已成功（钩子不重跑），链上没有条目。
        await LifecycleHost.Admin(postgres, "CREATE FUNCTION plugin_qian_audit.reject_lifecycle() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced audit failure'; END $$; " +
            "CREATE TRIGGER reject_lifecycle BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_lifecycle();", Ct);
        Assert.Equal("forced audit failure", Failure(await Assert.ThrowsAnyAsync<Exception>(() => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask())).MessageText);
        await LifecycleHost.Admin(postgres, "DROP TRIGGER reject_lifecycle ON plugin_qian_audit.audit_entries; DROP FUNCTION plugin_qian_audit.reject_lifecycle();", Ct);
        Assert.Equal((TenantInstallStates.Provisioning, (string?)null, (string?)"1.0.0"), Pick3((await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!));
        Assert.Equal((LifecycleRunStates.Succeeded, 1), PickRun(Assert.Single(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct))));
        Assert.Equal(0, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NOT NULL", Ct));
        // 审计已追加、迁移在提交时失败：条目随同一事务回滚，链上不留孤儿；安装行仍停在 Provisioning。
        await LifecycleHost.Admin(postgres, "CREATE FUNCTION plugin_qian_lifecycle.reject_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced commit failure'; END $$; " +
            "CREATE CONSTRAINT TRIGGER reject_commit AFTER UPDATE ON plugin_qian_lifecycle.tenant_plugin_installs DEFERRABLE INITIALLY DEFERRED FOR EACH ROW " +
            "EXECUTE FUNCTION plugin_qian_lifecycle.reject_commit();", Ct);
        Assert.Equal("forced commit failure", Failure(await Assert.ThrowsAnyAsync<Exception>(() => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask())).MessageText);
        await LifecycleHost.Admin(postgres, "DROP TRIGGER reject_commit ON plugin_qian_lifecycle.tenant_plugin_installs; DROP FUNCTION plugin_qian_lifecycle.reject_commit();", Ct);
        Assert.Equal((TenantInstallStates.Provisioning, (string?)null, (string?)"1.0.0"), Pick3((await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!));
        Assert.Empty(await Chain(host, TenantA));
        // 续跑：钩子记录已成功、不再执行，只补最终迁移与审计，链上恰一条。
        Assert.Equal((TenantInstallStates.Installed, (string?)"1.0.0", (string?)null), Pick3(await host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct)));
        Assert.Equal((LifecycleRunStates.Succeeded, 1), PickRun(Assert.Single(await host.Lifecycle.GetRunsAsync(TenantA, Plugin, Ct))));
        Assert.Equal(LifecycleController.ProvisionAuditCapability, Assert.Single(await Chain(host, TenantA)).Record.CapabilityId);
        // 宿主撤下审计：卸载按 fail-closed 以稳定错误码拒绝（503，不是 404），状态不变；宿主级告警只有操作名与租户。
        Assert.Empty(logs.AuditUnavailable);
        await host.Runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        var rejected = await AssertCode(LifecycleErrors.AuditUnavailable, () => host.Lifecycle.UninstallAsync(TenantA, Plugin, "operator", Ct).AsTask());
        Assert.Equal((503, "tenant-plugin.uninstall"), (LifecycleErrors.AuditUnavailable.Status, rejected.Args["operation"]));
        Assert.Equal((TenantInstallStates.Installed, (string?)"1.0.0", (string?)null), Pick3((await host.Lifecycle.GetInstallAsync(TenantA, Plugin, Ct))!));
        var warning = Assert.Single(logs.AuditUnavailable);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("tenant-plugin.uninstall", warning.Message, StringComparison.Ordinal); Assert.Contains(TenantA, warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Plugin, warning.Message, StringComparison.Ordinal);
        // 批准 API 缺审计时对外仍是 404（R32），但同样记告警：装配错误与无权限由日志区分。
        using (host.Tenant.BeginScope(TenantA))
            await AssertCode(PlatformErrors.NotFound, () => host.Lifecycle.ApprovePermissionsAsync(TenantA, Plugin, "1.0.0", null, new Approver("approver", TenantA), Ct).AsTask());
        Assert.Equal(2, logs.AuditUnavailable.Count);
        Assert.Contains("permissions.approve", logs.AuditUnavailable.Last().Message, StringComparison.Ordinal);
        // 重新装上审计后卸载完成并入链。
        await host.Runtime.MountAsync([host.AuditEntry()], Ct);
        Assert.Equal(TenantInstallStates.Uninstalled, (await host.Lifecycle.UninstallAsync(TenantA, Plugin, "operator", Ct)).State);
        Assert.Equal([LifecycleController.ProvisionAuditCapability, LifecycleController.UninstallAuditCapability], (await Chain(host, TenantA)).Select(entry => entry.Record.CapabilityId));
        Assert.Equal(2, logs.AuditUnavailable.Count);
    }

    [Fact]
    public async Task 同一租户两个组件同时完成开通_审计链冲突的一方换新会话重做_两条开通记录与安装行都在()
    {
        var barrier = new InstallBarrier();
        await using var host = await Start(services: services => services.ConfigureDbContext<LifecycleDbContext>((_, options) => options.AddInterceptors(barrier)));
        foreach (var plugin in new[] { "acme.first", Plugin, "acme.second" })
        {
            await Install(host, "1.0.0", plugin);
            await host.Lifecycle.EntitleAsync(TenantA, plugin, "operator", Ct);
        }
        // 链上先有一条：两次并发追加要推进同一个已存在的链头。
        await host.Lifecycle.ProvisionAsync(TenantA, "acme.first", "operator", Ct);
        // 两次开通（不同组件，安装锁不同）各自取得快照后在最终迁移的保存处会合：先提交的一方推进链头，另一方追加时撞上可串行化冲突，只能从新会话重做。
        barrier.Armed = true;
        var views = await Task.WhenAll(
            Task.Run(() => host.Lifecycle.ProvisionAsync(TenantA, Plugin, "operator", Ct).AsTask(), Ct),
            Task.Run(() => host.Lifecycle.ProvisionAsync(TenantA, "acme.second", "operator", Ct).AsTask(), Ct));
        Assert.True(barrier.Arrivals >= 3, "没有发生重做：" + barrier.Arrivals);
        Assert.All(views, view => Assert.Equal((TenantInstallStates.Installed, "1.0.0"), (view.State, view.Version)));
        var entries = await Chain(host, TenantA);
        Assert.Equal(3, entries.Length);
        Assert.Equal(["acme.first@1.0.0", "acme.second@1.0.0", "acme.widget@1.0.0"], entries.Select(entry => entry.Record.Resource!.Split('/')[^1]).Order(StringComparer.Ordinal));
        Assert.All(entries, entry => Assert.Equal(LifecycleController.ProvisionAuditCapability, entry.Record.CapabilityId));
    }

    [Fact]
    public async Task 安装激活回切弃用退役各写一条平台审计链条目_主体同租户条目口径_重复安装与幂等返回不记_租户链不受影响()
    {
        await using var host = await Start();
        PluginRevisionView first;
        using (EventEnvelopeScope.Begin(Operator))
        {
            first = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.0"), Key), "operator", Ct);
            // 同一摘要重复安装返回已有修订、已激活再激活直接返回：都不记。
            Assert.Equal(first.Revision, (await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.0"), Key), "operator", Ct)).Revision);
            first = await host.Lifecycle.ActivateAsync(Plugin, first.Revision, "operator", Ct);
            await host.Lifecycle.ActivateAsync(Plugin, first.Revision, "operator", Ct);
        }
        var second = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.1"), Key), "ops-cli", Ct);
        second = await host.Lifecycle.ActivateAsync(Plugin, second.Revision, "ops-cli", Ct);
        var rolledBack = await host.Lifecycle.RollbackAsync(Plugin, first.Revision, "ops-cli", Ct);
        Assert.Equal(PluginRevisionStates.Deprecated, (await host.Lifecycle.DeprecateAsync(Plugin, second.Revision, "ops-cli", Ct)).State);
        await host.Lifecycle.DeprecateAsync(Plugin, second.Revision, "ops-cli", Ct);
        Assert.Equal(PluginRevisionStates.Retired, (await host.Lifecycle.RetireAsync(Plugin, second.Revision, "ops-cli", Ct)).State);
        var entries = await PlatformChain(host);
        Assert.Equal(7, entries.Length);
        Assert.Equal(Enumerable.Range(1, 7), entries.Select(entry => (int)entry.Sequence));
        AssertPlatform(entries[0], LifecycleController.InstallAuditCapability, "user", "platform-operator", 1, first.InstalledAt,
            new { pluginId = Plugin, @operator = "operator", revision = 1, version = "1.0.0", artifactDigest = first.ArtifactDigest, form = first.Form, signedBy = first.SignedBy }, "1.0.0");
        AssertPlatform(entries[1], LifecycleController.ActivateAuditCapability, "user", "platform-operator", 1, first.ActivatedAt!.Value,
            new { pluginId = Plugin, @operator = "operator", revision = 1, version = "1.0.0", previousRevision = (int?)null }, "1.0.0");
        // 调用方没有打开事件作用域：主体是本组件服务主体，操作者仍在载荷里。
        AssertPlatform(entries[2], LifecycleController.InstallAuditCapability, "service", "qian.lifecycle", 2, second.InstalledAt,
            new { pluginId = Plugin, @operator = "ops-cli", revision = 2, version = "1.0.1", artifactDigest = second.ArtifactDigest, form = second.Form, signedBy = second.SignedBy }, "1.0.1");
        AssertPlatform(entries[3], LifecycleController.ActivateAuditCapability, "service", "qian.lifecycle", 2, second.ActivatedAt!.Value,
            new { pluginId = Plugin, @operator = "ops-cli", revision = 2, version = "1.0.1", previousRevision = 1 }, "1.0.1");
        AssertPlatform(entries[4], LifecycleController.RollbackAuditCapability, "service", "qian.lifecycle", 1, rolledBack.ActivatedAt!.Value,
            new { pluginId = Plugin, @operator = "ops-cli", revision = 1, version = "1.0.0", previousRevision = 2 }, "1.0.0");
        var now = new DateTimeOffset(host.Time.Now.Ticks - host.Time.Now.Ticks % 10, TimeSpan.Zero);
        AssertPlatform(entries[5], LifecycleController.DeprecateAuditCapability, "service", "qian.lifecycle", 2, now,
            new { pluginId = Plugin, @operator = "ops-cli", revision = 2, version = "1.0.1", fromState = PluginRevisionStates.Installed, toState = PluginRevisionStates.Deprecated }, "1.0.1");
        AssertPlatform(entries[6], LifecycleController.RetireAuditCapability, "service", "qian.lifecycle", 2, now,
            new { pluginId = Plugin, @operator = "ops-cli", revision = 2, version = "1.0.1", fromState = PluginRevisionStates.Deprecated, toState = PluginRevisionStates.Retired }, "1.0.1");
        // 平台条目不落在任何租户链上。
        Assert.Empty(await Chain(host, TenantA));
        Assert.Equal(7, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id IS NULL", Ct));
        Assert.Equal(7, await LifecycleHost.CountAsync(postgres, "SELECT count(*) FROM plugin_qian_audit.audit_entries", Ct));
    }

    [Fact]
    public async Task 平台链写不进去时状态不提交_状态提交失败时平台条目与状态变更都不留_平台链未配置或宿主撤下审计时以503拒绝不改状态并记告警()
    {
        var logs = new WarningLogs();
        await using var host = await Start(services: services => services.AddSingleton<ILoggerProvider>(logs));
        var revision = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.0"), Key), "operator", Ct);
        // 平台条目写不进去：激活不提交，修订仍是 Installed，链上只有安装那条。
        await LifecycleHost.Admin(postgres, "CREATE FUNCTION plugin_qian_audit.reject_platform() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.tenant_id IS NULL THEN RAISE EXCEPTION 'forced platform audit failure'; END IF; RETURN NEW; END $$; " +
            "CREATE TRIGGER reject_platform BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_platform();", Ct);
        Assert.Equal("forced platform audit failure", Failure(await Assert.ThrowsAnyAsync<Exception>(() => host.Lifecycle.ActivateAsync(Plugin, revision.Revision, "operator", Ct).AsTask())).MessageText);
        await LifecycleHost.Admin(postgres, "DROP TRIGGER reject_platform ON plugin_qian_audit.audit_entries; DROP FUNCTION plugin_qian_audit.reject_platform();", Ct);
        Assert.Equal(PluginRevisionStates.Installed, Assert.Single(await host.Lifecycle.GetRevisionsAsync(Plugin, Ct)).State);
        Assert.Equal([LifecycleController.InstallAuditCapability], (await PlatformChain(host)).Select(entry => entry.Record.CapabilityId));
        // 平台条目已挂进本事务、状态变更在提交时失败：修订仍是 Installed，平台链上也没有激活条目（同事务入口，S1-AUD-009；独立事务入口时这里会多留一条）。
        await LifecycleHost.Admin(postgres, "CREATE FUNCTION plugin_qian_lifecycle.reject_commit() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced commit failure'; END $$; " +
            "CREATE CONSTRAINT TRIGGER reject_commit AFTER UPDATE ON plugin_qian_lifecycle.plugin_revisions DEFERRABLE INITIALLY DEFERRED FOR EACH ROW EXECUTE FUNCTION plugin_qian_lifecycle.reject_commit();", Ct);
        Assert.Equal("forced commit failure", Failure(await Assert.ThrowsAnyAsync<Exception>(() => host.Lifecycle.ActivateAsync(Plugin, revision.Revision, "operator", Ct).AsTask())).MessageText);
        await LifecycleHost.Admin(postgres, "DROP TRIGGER reject_commit ON plugin_qian_lifecycle.plugin_revisions; DROP FUNCTION plugin_qian_lifecycle.reject_commit();", Ct);
        Assert.Equal(PluginRevisionStates.Installed, Assert.Single(await host.Lifecycle.GetRevisionsAsync(Plugin, Ct)).State);
        Assert.Equal([LifecycleController.InstallAuditCapability], (await PlatformChain(host)).Select(entry => entry.Record.CapabilityId));
        // 宿主没配置平台链（审计条目没有 chain.platformRole）：激活以 503 拒绝、状态不变，告警说明是平台链；租户链的告警不受影响。
        await host.Runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        await host.Runtime.MountAsync([host.AuditEntry(platformChain: false)], Ct);
        Assert.Empty(logs.PlatformAuditUnavailable);
        var rejected = await AssertCode(LifecycleErrors.AuditUnavailable, () => host.Lifecycle.ActivateAsync(Plugin, revision.Revision, "operator", Ct).AsTask());
        Assert.Equal((503, "plugin-revision.activate"), (LifecycleErrors.AuditUnavailable.Status, rejected.Args["operation"]));
        Assert.Equal(PluginRevisionStates.Installed, Assert.Single(await host.Lifecycle.GetRevisionsAsync(Plugin, Ct)).State);
        var warning = Assert.Single(logs.PlatformAuditUnavailable);
        Assert.Equal(LogLevel.Warning, warning.Level);
        Assert.Contains("plugin-revision.activate", warning.Message, StringComparison.Ordinal); Assert.Contains("chain.platformRole", warning.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(Plugin, warning.Message, StringComparison.Ordinal);
        // 宿主撤下审计：安装在提交候选制品之前就拒绝，不登记修订。
        await host.Runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        rejected = await AssertCode(LifecycleErrors.AuditUnavailable, () => host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.1"), Key), "operator", Ct).AsTask());
        Assert.Equal("plugin-revision.install", rejected.Args["operation"]);
        Assert.Single(await host.Lifecycle.GetRevisionsAsync(Plugin, Ct));
        Assert.Equal(2, logs.PlatformAuditUnavailable.Count);
        // 重新装上带平台链的审计后激活完成并入链。
        await host.Runtime.MountAsync([host.AuditEntry()], Ct);
        Assert.Equal(PluginRevisionStates.Active, (await host.Lifecycle.ActivateAsync(Plugin, revision.Revision, "operator", Ct)).State);
        Assert.Equal([LifecycleController.InstallAuditCapability, LifecycleController.ActivateAuditCapability], (await PlatformChain(host)).Select(entry => entry.Record.CapabilityId));
        Assert.Equal(2, logs.PlatformAuditUnavailable.Count); Assert.Empty(logs.AuditUnavailable);
    }

    [Fact]
    public async Task 两个组件同时激活_平台链冲突的一方换新会话重做_两条激活条目与修订状态都在()
    {
        var barrier = new ActivationBarrier();
        await using var host = await Start(services: services => services.ConfigureDbContext<LifecycleDbContext>((_, options) => options.AddInterceptors(barrier)));
        var first = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative("acme.first", "1.0.0"), Key), "operator", Ct);
        var second = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.0"), Key), "operator", Ct);
        // 两次激活（不同组件，修订锁不同）各自取得快照后在提升修订的保存处会合：先提交的一方推进平台链头，另一方追加时撞上可串行化冲突，只能从新会话重做。
        barrier.Armed = true;
        var views = await Task.WhenAll(
            Task.Run(() => host.Lifecycle.ActivateAsync("acme.first", first.Revision, "operator", Ct).AsTask(), Ct),
            Task.Run(() => host.Lifecycle.ActivateAsync(Plugin, second.Revision, "operator", Ct).AsTask(), Ct));
        Assert.True(barrier.Arrivals >= 3, "没有发生重做：" + barrier.Arrivals);
        Assert.All(views, view => Assert.Equal(PluginRevisionStates.Active, view.State));
        var entries = await PlatformChain(host);
        Assert.Equal(Enumerable.Range(1, 4), entries.Select(entry => (int)entry.Sequence));
        Assert.Equal([LifecycleController.InstallAuditCapability, LifecycleController.InstallAuditCapability], entries.Take(2).Select(entry => entry.Record.CapabilityId));
        Assert.All(entries.Skip(2), entry => Assert.Equal(LifecycleController.ActivateAuditCapability, entry.Record.CapabilityId));
        Assert.Equal(["lifecycle/revision:acme.first/1", "lifecycle/revision:acme.widget/1"], entries.Skip(2).Select(entry => entry.Record.Resource!).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task 本组件登录没有平台链同事务函数的执行权时激活以503拒绝不改状态并记告警_补上执行权后同事务入链()
    {
        var logs = new WarningLogs();
        await using var host = await Start(services: services => services.AddSingleton<ILoggerProvider>(logs));
        var revision = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(Plugin, "1.0.0"), Key), "operator", Ct);
        // 宿主只应用了一参迁移（或漏授执行权）：审计端口在调用同事务函数之前以 AUTHZ_DENIED 拒绝，Lifecycle 记事件 8 后 503，而不是把数据库权限错误原样抛出。
        const string functions = "plugin_qian_audit.platform_tip(uuid), plugin_qian_audit.platform_append(text,text,text)";
        await LifecycleHost.Admin(postgres, $"REVOKE EXECUTE ON FUNCTION {functions} FROM {PostgresFixture.AppRoleName};", Ct);
        var rejected = await AssertCode(LifecycleErrors.AuditUnavailable, () => host.Lifecycle.ActivateAsync(Plugin, revision.Revision, "operator", Ct).AsTask());
        Assert.Equal("plugin-revision.activate", rejected.Args["operation"]);
        Assert.Equal(PluginRevisionStates.Installed, Assert.Single(await host.Lifecycle.GetRevisionsAsync(Plugin, Ct)).State);
        Assert.Contains("plugin-revision.activate", Assert.Single(logs.PlatformAuditUnavailable).Message, StringComparison.Ordinal);
        Assert.Equal([LifecycleController.InstallAuditCapability], (await PlatformChain(host)).Select(entry => entry.Record.CapabilityId));
        await LifecycleHost.Admin(postgres, $"GRANT EXECUTE ON FUNCTION {functions} TO {PostgresFixture.AppRoleName};", Ct);
        Assert.Equal(PluginRevisionStates.Active, (await host.Lifecycle.ActivateAsync(Plugin, revision.Revision, "operator", Ct)).State);
        Assert.Equal([LifecycleController.InstallAuditCapability, LifecycleController.ActivateAuditCapability], (await PlatformChain(host)).Select(entry => entry.Record.CapabilityId));
        Assert.Single(logs.PlatformAuditUnavailable); Assert.Empty(logs.AuditUnavailable);
    }

    private async Task<LifecycleHost> Start(IPluginLifecycleHooks? hooks = null, Action<IServiceCollection>? services = null)
    {
        var host = await LifecycleHost.StartAsync(postgres, hooks: hooks is null ? null : (plugin, _) => plugin == Plugin ? hooks : null, services: services, ct: Ct);
        host.Trust(0, "k1", Key, host.Time.Now.AddDays(-1));
        return host;
    }

    // 不声明反向权限的声明式组件：开通不经批准，用例只看审计。
    private static async Task<PluginRevisionView> Install(LifecycleHost host, string version, string plugin = Plugin, bool activate = true)
    {
        var revision = await host.Lifecycle.InstallAsync(Artifacts.Signed(Artifacts.Declarative(plugin, version), Key), "operator", Ct);
        return activate ? await host.Lifecycle.ActivateAsync(plugin, revision.Revision, "operator", Ct) : revision;
    }

    // 该租户链上的全部条目；读之前先验整条链。
    private static async Task<AuditChainEntry[]> Chain(LifecycleHost host, string tenantId)
    {
        using var scope = host.Tenant.BeginScope(tenantId);
        var audit = host.Runtime.GetContext("audit", ScopeKey.Global).Audit();
        Assert.True((await audit.VerifyAsync(Ct)).Valid);
        return [.. await audit.ReadAsync(0, 100, cancellationToken: Ct)];
    }

    // 生命周期条目：租户链、主体与类型、无委托、能力 ID、契约 v1、通道 api、资源同批准的判定资源、结果 success、发生时间即安装行更新时间；
    // 输入哈希是 [租户, 组件, 能力, 版本…] 的 SHA-256；载荷带组件、操作者与版本字段。
    private static void AssertRecord(AuditChainEntry entry, string capability, string actorType, string actor, string version, DateTimeOffset occurredAt, object payload, params string[] versions)
    {
        var record = entry.Record;
        Assert.Equal((TenantA, actorType, actor, (string?)null, capability, "v1", "api", (string?)("lifecycle/install:" + TenantA + "/" + Plugin + "@" + version), "success", occurredAt),
            (record.TenantId, record.ActorType, record.ActorId, record.OnBehalfOf, record.CapabilityId, record.ContractVersion, record.Channel, record.Resource, record.Result, record.OccurredAt));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { TenantA, Plugin, capability }.Concat(versions))))), record.InputHash);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(payload), record.Payload), record.Payload.GetRawText());
    }

    // 平台链上的全部条目：以本组件的 global 上下文为生产方读取，读之前先验整条链；须在无租户上下文时调用。
    private static async Task<AuditChainEntry[]> PlatformChain(LifecycleHost host)
    {
        var producer = host.Runtime.GetContext("lifecycle", ScopeKey.Global);
        var audit = producer.Audit();
        Assert.True((await audit.VerifyPlatformAsync(producer, Ct)).Valid);
        return [.. await audit.ReadPlatformAsync(producer, 0, 100, cancellationToken: Ct)];
    }

    // 平台条目：无租户、主体与类型、无委托、能力 ID、契约 v1、通道 api、资源 lifecycle/revision:{plugin}/{revision}、结果 success、发生时间即修订行上的时间戳；
    // 输入哈希是 [组件, 能力, 修订号, 版本…] 的 SHA-256；载荷带组件、操作者、修订号（数字）与各操作自己的字段。
    private static void AssertPlatform(AuditChainEntry entry, string capability, string actorType, string actor, int revision, DateTimeOffset occurredAt, object payload, params string[] versions)
    {
        var record = entry.Record;
        Assert.Equal(((string?)null, actorType, actor, (string?)null, capability, "v1", "api", (string?)("lifecycle/revision:" + Plugin + "/" + revision), "success", occurredAt),
            (record.TenantId, record.ActorType, record.ActorId, record.OnBehalfOf, record.CapabilityId, record.ContractVersion, record.Channel, record.Resource, record.Result, record.OccurredAt));
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { Plugin, capability, revision.ToString(CultureInfo.InvariantCulture) }.Concat(versions))))), record.InputHash);
        Assert.True(JsonElement.DeepEquals(JsonSerializer.SerializeToElement(payload), record.Payload), record.Payload.GetRawText());
    }

    private static PostgresException Failure(Exception error)
    {
        for (var current = (Exception?)error; current is not null; current = current.InnerException)
            if (current is PostgresException postgres) return postgres;
        Assert.Fail("期望数据库异常：" + error);
        return null!;
    }

    private static (string, string?, string?) Pick3(TenantPluginInstallView view) => (view.State, view.Version, view.TargetVersion);
    private static (string, int) PickRun(LifecycleRunView run) => (run.Status, run.Attempts);

    private static async Task<QianError> AssertCode(ErrorCode code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<QianErrorException>(action);
        Assert.Equal(code.Code, error.Error.Code.Code);
        return error.Error;
    }

    // 开通钩子第一次失败、之后成功；其它钩子无操作。
    private sealed class FlakyHooks : IPluginLifecycleHooks
    {
        private int _provisions;
        internal int Provisions => Volatile.Read(ref _provisions);
        public ValueTask<JsonElement?> InvokeAsync(LifecycleHookContext context, CancellationToken cancellationToken)
        {
            if (context.Hook == LifecycleHooks.TenantProvision && Interlocked.Increment(ref _provisions) == 1) throw new QianErrorException(new QianError(PlatformErrors.Unavailable));
            return ValueTask.FromResult<JsonElement?>(null);
        }
    }

    // 武装后，前两次把安装行改为 Installed 的保存互相等待（两个事务都已取得快照），之后的保存直接通过；Arrivals 计入全部这类保存。
    private sealed class InstallBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Armed { get; set; }
        internal int Arrivals => Volatile.Read(ref _arrivals);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context?.ChangeTracker.Entries<TenantInstallRow>().Any(entry => entry.Entity.State == TenantInstallStates.Installed) == true && Interlocked.Increment(ref _arrivals) <= 2)
            {
                if (Volatile.Read(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    // 武装后，前两次把修订提升为 Active 的保存互相等待（两个事务都已取得快照），之后的保存直接通过；Arrivals 计入全部这类保存。
    private sealed class ActivationBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Armed { get; set; }
        internal int Arrivals => Volatile.Read(ref _arrivals);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed && eventData.Context?.ChangeTracker.Entries<PluginRevisionRow>().Any(entry => entry.Entity.State == PluginRevisionStates.Active) == true && Interlocked.Increment(ref _arrivals) <= 2)
            {
                if (Volatile.Read(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    // 只收 LifecycleLog 的「审计服务不可用」（EventId 7，租户链）与「平台审计链不可用」（EventId 8）告警。
    private sealed class WarningLogs : ILoggerProvider
    {
        internal ConcurrentQueue<(LogLevel Level, string Message)> AuditUnavailable { get; } = new();
        internal ConcurrentQueue<(LogLevel Level, string Message)> PlatformAuditUnavailable { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(this);
        public void Dispose() { }
        private sealed class Sink(WarningLogs owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                if (eventId.Id == 7 && message.StartsWith("审计服务不可用", StringComparison.Ordinal)) owner.AuditUnavailable.Enqueue((level, message));
                if (eventId.Id == 8 && message.StartsWith("平台审计链不可用", StringComparison.Ordinal)) owner.PlatformAuditUnavailable.Enqueue((level, message));
            }
        }
    }

    // 已由身份层校验的租户用户主体快照。
    private sealed class Approver(string subject, string tenant) : ICurrentUser
    {
        private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public Principal? Principal { get; } = new()
        {
            Issuer = "qian-lifecycle-test", Audience = ["lifecycle"], Subject = subject, Kind = PrincipalKind.User, TenantId = tenant, Roles = [], Scope = "lifecycle",
            SessionId = Guid.CreateVersion7().ToString(), IssuedAt = Now, NotBefore = Now, ExpiresAt = Now + 300, TokenId = Guid.CreateVersion7().ToString()
        };
        public string? UserId => subject;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => true;
    }
}
