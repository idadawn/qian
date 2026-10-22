using System.Security.Cryptography;
using System.Text.Json;
using Kun.Authorization;
using Kun.Caching.Abstractions;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Events;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz.Tests;

// S1-AZ-004：三处 PEP 共用的执行端口（ctx.Enforcement()）、决策审计、identity_revision 服务端比对、授权变更事件与两条守卫。
// 真实 PostgreSQL（受限应用角色）、真实 Audit 组件与审计链、真实 PluginLoader；判定仍只来自 Check / BatchCheck / ListAllowedActions / Explain。
public sealed partial class SuperadminBypassTests
{
    private const string CustomerWrite = "qian.sample.crm:customer:write";
    private const string CommandTarget = "qian.sample.crm/customer.create";

    [Fact]
    public async Task 执行端口经唯一判定点判定_放行与拒绝都写决策审计并可解释_主体与租户边界fail_closed_采样只省放行_审计不可用即拒绝()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog();
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        using var active = host.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        await using var runtime = new PluginLoader(host.Services); var observer = new DecisionObserver();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg" } }),
            Define("authz", new AuthzPlugin(), AuthzKeys), Define("observer", observer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        var ctx = observer.Context!; var enforcement = ctx.Enforcement(); var authz = ctx.Authz();
        var roles = new PostgresTenantRoleStore(data, tenant);
        var owner = new EnforcementSubject("owner-a", A, "user", IdentityRevision: 7); var reader = new EnforcementSubject("reader-a", A, "user");
        using (tenant.BeginScope(A))
        {
            await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner-a", "operator", Ct);
            await roles.AssignAsync("tenant.user", "user", "reader-a", "operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);

            var allowed = await enforcement.EnforceAsync(Request(owner, CustomerWrite), Ct);
            Assert.True(allowed.Allow); Assert.Equal("role_grant", allowed.Reason); Assert.Equal(["tenant.owner"], allowed.Explanation.MatchedRoles);
            Assert.True(allowed.Logged); Assert.False(allowed.Bypass); Assert.NotEqual(Guid.Empty, allowed.Id);
            var denied = await enforcement.EnforceAsync(Request(reader, CustomerWrite), Ct);
            Assert.False(denied.Allow); Assert.Equal("no_matching_grant", denied.Reason); Assert.True(denied.Logged);
            Assert.Equal(["tenant.user"], denied.Explanation.MatchedRoles); Assert.Equal([CustomerWrite], denied.Explanation.Missing);
            // 判定与唯一判定点的完整解释同源：同一主体、同一权限点，结论与命中链路一致。
            var explained = await authz.ExplainDetailedAsync(new PermissionRequest(new AuthorizationSubject("reader-a", A, "user"), CustomerWrite), Ct);
            Assert.Equal((denied.Allow, denied.Reason), (explained.Decision.Allow, explained.Decision.Reason));
            Assert.Equal(explained.MatchedRoles, denied.Explanation.MatchedRoles); Assert.Equal(explained.Missing, denied.Explanation.Missing);

            // 决策审计进该租户的哈希链：放行（采样 1.0）与拒绝各一条，payload 与解释同源，不含输入原文；输入哈希只盖权限点/入口/目标/通道。
            var records = (await ctx.Audit().ReadAsync(0, 10, cancellationToken: Ct)).Select(entry => entry.Record).ToArray();
            Assert.Equal([allowed.Id, denied.Id], records.Select(record => record.Id));
            Assert.All(records, record => Assert.Equal((A, "api", AuthzEnforcement.DecisionCapabilityId, "1.0.0", (string?)null), (record.TenantId, record.Channel, record.CapabilityId, record.ContractVersion, record.Resource)));
            Assert.Equal(("user", "owner-a", "allowed"), (records[0].ActorType, records[0].ActorId, records[0].Result));
            Assert.Equal(("allow", "role_grant", AuthzEnforcement.CommandEntryPoint, CommandTarget, 7L, false), Decision(records[0].Payload));
            Assert.Equal(["tenant.owner"], Strings(records[0].Payload, "matchedRoles")); Assert.Empty(Strings(records[0].Payload, "missing"));
            Assert.Equal(("user", "reader-a", "denied"), (records[1].ActorType, records[1].ActorId, records[1].Result));
            Assert.Equal(("deny", "no_matching_grant", AuthzEnforcement.CommandEntryPoint, CommandTarget, (long?)null, false), Decision(records[1].Payload));
            Assert.Equal([CustomerWrite], Strings(records[1].Payload, "missing"));
            var input = JsonSerializer.SerializeToUtf8Bytes(new[] { CustomerWrite, "", AuthzEnforcement.CommandEntryPoint, CommandTarget, "api" });
            Assert.All(records, record => Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(input)), record.InputHash));
            Assert.True((await ctx.Audit().VerifyAsync(Ct)).Valid);
            Assert.Equal([allowed.Id, denied.Id], observer.Decided.Select(decision => decision.Id));

            // 主体、租户与权限点的边界一律拒绝并记录：缺省拒绝、非法权限点、委托 / Agent、租户不一致、平台主体落在租户上下文里、非法主体。
            foreach (var (request, reason) in new (EnforcementRequest Request, string Reason)[]
            {
                (Request(reader, null), "permission_not_declared"), (Request(reader, "not a permission"), "invalid_permission"),
                (Request(new EnforcementSubject("owner-a", A, "user", Delegated: true), CustomerWrite), "delegation_not_supported"),
                (Request(new EnforcementSubject("agent-a", A, "agent"), CustomerWrite), "delegation_not_supported"),
                (Request(new EnforcementSubject("owner-a", B, "user"), CustomerWrite), "subject_tenant_mismatch"),
                (Request(new EnforcementSubject("root", null, "user"), CustomerWrite), "tenant_context_mismatch"),
            })
            {
                var decision = await enforcement.EnforceAsync(request, Ct);
                Assert.False(decision.Allow); Assert.Equal(reason, decision.Reason); Assert.True(decision.Logged); Assert.Empty(decision.Explanation.MatchedRoles);
            }
            // 非法主体没有可写进审计的 actor：拒绝，但只能记日志（Logged=false）。
            var invalid = await enforcement.EnforceAsync(Request(new EnforcementSubject(" ", A, "user"), CustomerWrite), Ct);
            Assert.False(invalid.Allow); Assert.Equal("invalid_subject", invalid.Reason); Assert.False(invalid.Logged);
            var platformInTenant = (await ctx.Audit().ReadAsync(0, 20, cancellationToken: Ct)).Select(entry => entry.Record).Single(record => record.ActorId == "root");
            Assert.Equal((A, "denied", "tenant_context_mismatch"), (platformInTenant.TenantId, platformInTenant.Result, platformInTenant.Payload.GetProperty("reason").GetString()));
            Assert.Equal(8, (await ctx.Audit().ReadAsync(0, 20, cancellationToken: Ct)).Count);
            // 通道 / 入口 / 目标不在枚举内是调用方编程错误，不是判定。
            await Assert.ThrowsAsync<ArgumentException>(async () => await enforcement.EnforceAsync(new EnforcementRequest(owner, CustomerWrite, null, "web", AuthzEnforcement.HttpEntryPoint, "GET /x"), Ct));
            await Assert.ThrowsAsync<ArgumentException>(async () => await enforcement.EnforceAsync(new EnforcementRequest(owner, CustomerWrite, null, "api", "mcp", "tool"), Ct));
        }
        // 已认证主体带租户、宿主却没有为本次调用建立租户上下文：抛错而不是拒绝（PEP 放错了位置）。
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await enforcement.EnforceAsync(Request(owner, CustomerWrite), Ct));
        // 平台主体（tenant_id=null）在无租户上下文里合法，只走显式旁路；没配 platformRole 时旁路一律拒绝；平台链未配置写不进审计，拒绝原因保留。
        var platform = await enforcement.EnforceAsync(Request(new EnforcementSubject("root", null, "user"), CustomerWrite), Ct);
        Assert.False(platform.Allow); Assert.Equal("platform_role_not_configured", platform.Reason); Assert.False(platform.Logged); Assert.True(platform.Bypass);

        // 审计不可用即拒绝：卸载 Audit 后 RBAC 仍命中，但放行无据可查，判定变为拒绝；拒绝保留原因但记不下来。
        await runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        using (tenant.BeginScope(A))
        {
            var unavailable = await enforcement.EnforceAsync(Request(owner, CustomerWrite), Ct);
            Assert.False(unavailable.Allow); Assert.Equal("audit_unavailable", unavailable.Reason); Assert.False(unavailable.Logged);
            Assert.Equal(["tenant.owner"], unavailable.Explanation.MatchedRoles); Assert.Equal([CustomerWrite], unavailable.Explanation.Missing);
            var unlogged = await enforcement.EnforceAsync(Request(reader, CustomerWrite), Ct);
            Assert.False(unlogged.Allow); Assert.Equal("no_matching_grant", unlogged.Reason); Assert.False(unlogged.Logged);
        }
        await runtime.MountAsync([Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg" } })], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("audit", ScopeKey.Global));

        // 采样 0：放行不记录（Logged=false），拒绝必记。旁观者依赖 Authz：先卸旁观者再卸 authz，重装时一起挂回。
        await runtime.UnmountAsync("observer", ScopeKey.Global, Ct); await runtime.UnmountAsync("authz", ScopeKey.Global, Ct);
        observer = new DecisionObserver();
        await runtime.MountAsync([Define("authz", new AuthzPlugin(), AuthzKeys, new { decisionLogSampling = 0 }), Define("observer", observer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        ctx = observer.Context!; enforcement = ctx.Enforcement();
        using (tenant.BeginScope(A))
        {
            var before = (await ctx.Audit().ReadAsync(0, 50, cancellationToken: Ct)).Count;
            var sampledOut = await enforcement.EnforceAsync(Request(owner, CustomerWrite), Ct);
            Assert.True(sampledOut.Allow); Assert.False(sampledOut.Logged);
            var stillLogged = await enforcement.EnforceAsync(Request(reader, CustomerWrite), Ct);
            Assert.False(stillLogged.Allow); Assert.True(stillLogged.Logged);
            Assert.Equal(before + 1, (await ctx.Audit().ReadAsync(0, 50, cancellationToken: Ct)).Count);
            Assert.True((await ctx.Audit().VerifyAsync(Ct)).Valid);
        }
    }

    [Fact]
    public async Task 令牌identity_revision低于服务端已见最高值即旧令牌_下限持久化后任一宿主立即拒绝_修订并入判定快照键()
    {
        var firstTenant = new AsyncLocalTenantContext(); var secondTenant = new AsyncLocalTenantContext();
        var prefix = "az004-" + Guid.NewGuid().ToString("N")[..8] + ":";
        // 两宿主各自进程内缓存：下限不靠缓存共享，靠本组件表；快照缓存键带 identity_revision。
        await using var oneServices = CacheServices("memory", prefix); await using var twoServices = CacheServices("memory", prefix);
        var oneCache = new CountingCache(oneServices.GetRequiredService<IKunCache>()); var twoCache = new CountingCache(twoServices.GetRequiredService<IKunCache>());
        using var first = Host(firstTenant, cache: oneCache); using var second = Host(secondTenant, cache: twoCache);
        await first.StartAsync(Ct); await second.StartAsync(Ct); await Prepare(first);
        var catalog = Catalog(); await new PostgresPermissionCatalogStore(first.Services.GetRequiredService<IDataService>()).PutAsync(catalog, Ct);
        using var oneLease = first.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        using var twoLease = second.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        await using var oneRuntime = new PluginLoader(first.Services); await using var twoRuntime = new PluginLoader(second.Services);
        var oneObserver = new DecisionObserver(); var twoObserver = new DecisionObserver();
        foreach (var (runtime, observer) in new[] { (oneRuntime, oneObserver), (twoRuntime, twoObserver) })
        {
            await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg" } }),
                Define("authz", new AuthzPlugin(), AuthzKeys), Define("observer", observer, [])], Ct);
            Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        }
        var one = oneObserver.Context!.Enforcement(); var two = twoObserver.Context!.Enforcement();
        using var firstScope = firstTenant.BeginScope(A); using var secondScope = secondTenant.BeginScope(A);
        var roles = new PostgresTenantRoleStore(first.Services.GetRequiredService<IDataService>(), firstTenant);
        await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "alice", "operator", Ct);
        await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
        static EnforcementSubject Alice(long? revision) => new("alice", A, "user", revision);

        // 未携带修订的令牌不比对（S2S 令牌）；首次见到修订 1 即记为下限。
        Assert.True((await one.EnforceAsync(Request(Alice(null), CustomerWrite), Ct)).Allow);
        Assert.True((await one.EnforceAsync(Request(Alice(1), CustomerWrite), Ct)).Allow);
        // 宿主二见到修订 2：下限抬到 2，宿主一随即拒绝修订 1 的旧令牌（不等 TTL，不靠共享缓存）。
        Assert.True((await two.EnforceAsync(Request(Alice(2), CustomerWrite), Ct)).Allow);
        var stale = await one.EnforceAsync(Request(Alice(1), CustomerWrite), Ct);
        Assert.False(stale.Allow); Assert.Equal("identity_revision_stale", stale.Reason); Assert.True(stale.Logged); Assert.Equal([CustomerWrite], stale.Explanation.Missing);
        Assert.True((await one.EnforceAsync(Request(Alice(2), CustomerWrite), Ct)).Allow);
        Assert.Equal("identity_revision_stale", (await two.EnforceAsync(Request(Alice(1), CustomerWrite), Ct)).Reason);
        Assert.Equal("identity_revision_stale", (await two.EnforceAsync(Request(Alice(-1), CustomerWrite), Ct)).Reason);
        // 拒绝原因进决策日志，带令牌修订。
        var record = (await oneObserver.Context!.Audit().ReadAsync(0, 20, cancellationToken: Ct)).Select(entry => entry.Record).First(r => r.Result == "denied" && r.ActorId == "alice");
        Assert.Equal(("deny", "identity_revision_stale", AuthzEnforcement.CommandEntryPoint, CommandTarget, 1L, false), Decision(record.Payload));
        // 快照键第四段是令牌修订：同一主体不同修订各自一份快照，键里带租户授权修订。
        var keys = oneCache.Writes.Concat(twoCache.Writes).Where(key => key.StartsWith("qian.authz/rbac/v1:", StringComparison.Ordinal)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Assert.Equal(["qian.authz/rbac/v1:[\"" + A + "\",\"user\",\"alice\",\"\",\"3\"]", "qian.authz/rbac/v1:[\"" + A + "\",\"user\",\"alice\",\"1\",\"3\"]", "qian.authz/rbac/v1:[\"" + A + "\",\"user\",\"alice\",\"2\",\"3\"]"], keys);
        // 撤销角色：租户授权修订递增，新令牌 3 秒内在另一宿主拒绝（no_matching_grant），旧令牌仍按旧修订拒绝。
        await roles.AssignAsync("tenant.admin", "user", "admin-a", "operator", Ct);
        Assert.True(await roles.RevokeAsync("tenant.owner", "user", "alice", "operator", Ct));
        await Within(TimeSpan.FromSeconds(3), async () => (await two.EnforceAsync(Request(Alice(2), CustomerWrite), Ct)).Reason == "no_matching_grant");
        Assert.Equal("identity_revision_stale", (await two.EnforceAsync(Request(Alice(1), CustomerWrite), Ct)).Reason);
    }

    [Fact]
    public async Task 授权变更与租户授权修订同事务发出authz事件_信封带修订号与代操作者_无变更不发事件_宿主漏装处理器时写入回滚()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog(); await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var roles = new PostgresTenantRoleStore(data, tenant);
        using (tenant.BeginScope(A))
        {
            await roles.EnsureBuiltinsAsync("operator", Ct);
            await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
            await roles.AssignAsync("tenant.user", "user", "alice", "operator", Ct);
            Assert.True(await roles.RevokeAsync("tenant.user", "user", "alice", "operator", Ct));
            Assert.False(await roles.RevokeAsync("tenant.user", "user", "alice", "operator", Ct));
            Assert.Equal(4L, await Revision(data, A));
        }
        var envelopes = (await Outbox(data, tenant, A)).Select(message => (message.Type, Envelope: QianEventEnvelope.Parse(message.Payload))).ToArray();
        Assert.All(envelopes, item => Assert.Equal((item.Type, Guid.Parse(A), "qian.authz", EventActorKind.Service, "qian.authz", "operator"),
            (item.Envelope.Type, item.Envelope.TenantId, item.Envelope.Producer, item.Envelope.Actor.Kind, item.Envelope.Actor.Id, item.Envelope.Actor.OnBehalfOf)));
        var changes = envelopes.Select(item => (item.Type, item.Envelope.Payload.GetProperty("authzRevision").GetInt64(), item.Envelope.Payload.GetProperty("change").GetString(),
            item.Envelope.Payload.TryGetProperty("roleCode", out var role) ? role.GetString() : null, item.Envelope.Payload.TryGetProperty("subjectId", out var subject) ? subject.GetString() : null))
            .Order().ToArray();
        Assert.Equal(
        [
            (AuthzEventIds.GrantChanged, 3L, "assigned", "tenant.user", "alice"), (AuthzEventIds.GrantChanged, 4L, "revoked", "tenant.user", "alice"),
            (AuthzEventIds.RoleChanged, 1L, "created", "tenant.admin", null), (AuthzEventIds.RoleChanged, 1L, "created", "tenant.developer", null),
            (AuthzEventIds.RoleChanged, 1L, "created", "tenant.owner", null), (AuthzEventIds.RoleChanged, 1L, "created", "tenant.user", null),
            (AuthzEventIds.RoleChanged, 2L, "grants_installed", "tenant.owner", null), (AuthzEventIds.RoleChanged, 2L, "grants_installed", "tenant.user", null),
        ], changes);
        var installed = envelopes.Where(item => item.Envelope.Payload.GetProperty("change").GetString() == "grants_installed").Select(item => item.Envelope.Payload);
        Assert.All(installed, payload => Assert.Equal(("qian.sample.crm", "1.0.0"), (payload.GetProperty("pluginId").GetString(), payload.GetProperty("pluginVersion").GetString())));

        // 漏装 AddQianAuthzEvents 的宿主：事件进不了 Outbox，整笔写入（含修订递增）回滚。
        var bareTenant = new AsyncLocalTenantContext(); using var bare = Host(bareTenant, authzEvents: false); await bare.StartAsync(Ct);
        var bareData = bare.Services.GetRequiredService<IDataService>();
        using (bareTenant.BeginScope(A))
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => new PostgresTenantRoleStore(bareData, bareTenant).AssignAsync("tenant.user", "user", "bob", "operator", Ct));
            Assert.Contains("AddQianAuthzEvents", error.Message, StringComparison.Ordinal);
            Assert.Equal(4L, await Revision(bareData, A));
            Assert.DoesNotContain(await new PostgresTenantRoleStore(bareData, bareTenant).ListMembersAsync(Ct), member => member.SubjectId == "bob");
        }
        Assert.Equal(8, (await Outbox(data, tenant, A)).Count);
    }

    [Fact]
    public async Task 租户专属权限点不可绑定到子作用域_最后一个成员管理绑定不可移除或降级_经组件端口返回结构化错误且不落库()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog(); await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        using var active = host.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        await using var runtime = new PluginLoader(host.Services); var observer = new DecisionObserver();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("authz", new AuthzPlugin(), AuthzKeys), Define("observer", observer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        var grant = observer.Context!.Inject<ITenantRoleGrant>(TenantRoleGrant.Key); var roles = new PostgresTenantRoleStore(data, tenant);
        var errors = host.Services.GetRequiredService<ErrorCatalog>();
        Assert.True(errors.Contains(AuthzErrors.ScopeMismatch)); Assert.True(errors.Contains(AuthzErrors.LastAdministratorLockout));
        using var scope = tenant.BeginScope(A);
        await roles.EnsureBuiltinsAsync("operator", Ct); await grant.AssignAsync("tenant.owner", "user", "owner-a", "operator", Ct);
        var revision = await Revision(data, A);

        // 守卫一：S1 的权限点全部租户专属，绑定到租户之下的子作用域（应用、Agent 会话）或别的租户一律 422，pointer 指向 scope，不递增修订。
        foreach (var bound in new[] { new ScopeKey("app", "crm"), new ScopeKey("agent-session", "s-1"), new ScopeKey("tenant", B) })
        {
            var error = await Assert.ThrowsAsync<QianErrorException>(async () => await grant.BindAsync(new RoleBinding("tenant.user", "user", "alice", "operator", bound), Ct));
            Assert.Equal(("AUTHZ_SCOPE_MISMATCH", 422, "#/scope", bound.Kind + ":" + bound.Id), (error.Error.Code.Code, error.Error.Code.Status, error.Error.Pointer, error.Error.Args["scope"]));
        }
        Assert.DoesNotContain(await roles.ListMembersAsync(Ct), member => member.SubjectId == "alice"); Assert.Equal(revision, await Revision(data, A));
        await grant.BindAsync(new RoleBinding("tenant.user", "user", "alice", "operator", new ScopeKey("tenant", A)), Ct);
        Assert.Contains(await roles.ListMembersAsync(Ct), member => member.SubjectId == "alice");

        // 守卫二：唯一持有成员管理权限的绑定不可移除，也不可用有效期上限降级；加入第二个管理员后才能移除，之后那个管理员又成为最后一个。
        var lockout = await Assert.ThrowsAsync<QianErrorException>(() => roles.RevokeAsync("tenant.owner", "user", "owner-a", "operator", Ct));
        Assert.Equal(("AUTHZ_LAST_ADMIN_LOCKOUT", 409, "tenant.owner"), (lockout.Error.Code.Code, lockout.Error.Code.Status, lockout.Error.Args["role"]));
        var downgrade = await Assert.ThrowsAsync<QianErrorException>(async () => await grant.BindAsync(new RoleBinding("tenant.owner", "user", "owner-a", "operator", ValidTo: DateTimeOffset.UtcNow.AddHours(1)), Ct));
        Assert.Equal("AUTHZ_LAST_ADMIN_LOCKOUT", downgrade.Error.Code.Code);
        Assert.Contains(await roles.ListMembersAsync(Ct), member => member.SubjectId == "owner-a" && member.Roles.Contains("tenant.owner"));
        // 已过期的管理员绑定不算在内；普通角色随便撤。
        await grant.BindAsync(new RoleBinding("tenant.admin", "user", "expired-admin", "operator", ValidFrom: DateTimeOffset.UtcNow.AddHours(-2), ValidTo: DateTimeOffset.UtcNow.AddHours(-1)), Ct);
        Assert.Equal("AUTHZ_LAST_ADMIN_LOCKOUT", (await Assert.ThrowsAsync<QianErrorException>(() => roles.RevokeAsync("tenant.owner", "user", "owner-a", "operator", Ct))).Error.Code.Code);
        Assert.True(await roles.RevokeAsync("tenant.user", "user", "alice", "operator", Ct));
        await grant.AssignAsync("tenant.admin", "user", "admin-a", "operator", Ct);
        Assert.True(await roles.RevokeAsync("tenant.owner", "user", "owner-a", "operator", Ct));
        await grant.BindAsync(new RoleBinding("tenant.admin", "user", "admin-a", "operator", ValidFrom: DateTimeOffset.UtcNow.AddMinutes(-1)), Ct);
        Assert.Equal("AUTHZ_LAST_ADMIN_LOCKOUT", (await Assert.ThrowsAsync<QianErrorException>(() => roles.RevokeAsync("tenant.admin", "user", "admin-a", "operator", Ct))).Error.Code.Code);
        Assert.Equal(["admin-a", "expired-admin"], (await roles.ListMembersAsync(Ct)).Select(member => member.SubjectId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task 卸载authz组件时waterfall监听器与执行端口按登记逆序撤销_无残留_重装后只有一个命令PEP监听器()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var catalog = Catalog(); await new PostgresPermissionCatalogStore(host.Services.GetRequiredService<IDataService>()).PutAsync(catalog, Ct);
        var errors = host.Services.GetRequiredService<ErrorCatalog>();
        var registered = new List<string>(); var revoked = new List<string>(); var codesAtListenerRevocation = new List<bool>();
        var recorder = new RecordingPlugin(new AuthzPlugin(), registered, revoked, label => { if (label == "on:" + CommandEvents.Dispatching.Name) codesAtListenerRevocation.Add(errors.Contains(AuthzErrors.ScopeMismatch)); });
        await using var runtime = new PluginLoader(host.Services); var observer = new DispatchObserver();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("observer", observer, []), Define("authz", recorder, [.. AuthzKeys, CommandPolicies.AuthorizationKey])], Ct);
        Assert.True(runtime.GetState("authz", ScopeKey.Global) == PluginState.Ready, runtime.GetFailure("authz", ScopeKey.Global)?.ToString());
        var ctx = observer.Context!;
        // 命令 PEP 是 command/dispatching 上的 waterfall 监听器；执行端口、判定服务与角色授予端口都在同一效果栈上。
        Assert.Contains("on:" + CommandEvents.Dispatching.Name, registered);
        Assert.Contains("provide:" + AuthzEnforcement.Key.Name, registered); Assert.Contains("provide:" + CommandPolicies.AuthorizationKey.Name, registered);
        Assert.Equal(["authz", "observer"], DispatchListeners(ctx));
        Assert.NotNull(ctx.TryInject<IAuthzEnforcement>(AuthzEnforcement.Key)); Assert.True(errors.Contains(AuthzErrors.ScopeMismatch));
        var enforcement = ctx.Enforcement();

        await runtime.UnmountAsync("authz", ScopeKey.Global, Ct);

        // 撤销严格逆序；监听器撤下时错误码还在，错误码最后撤销。
        Assert.Equal(Enumerable.Reverse(registered), revoked);
        Assert.Equal([true], codesAtListenerRevocation);
        Assert.False(errors.Contains(AuthzErrors.ScopeMismatch));
        Assert.Equal(["observer"], DispatchListeners(ctx));
        Assert.Null(ctx.TryInject<IAuthzEnforcement>(AuthzEnforcement.Key)); Assert.Null(ctx.TryInject<IAuthzService>(AuthzDefinition.Key));
        Assert.Empty(ctx.List(CommandPolicies.AuthorizationKey));
        using (tenant.BeginScope(A))
            await Assert.ThrowsAnyAsync<Exception>(async () => await enforcement.EnforceAsync(Request(new EnforcementSubject("owner-a", A, "user"), CustomerWrite), Ct));

        // 重装得到新的单个监听器，而不是在旧登记上叠加。
        await runtime.MountAsync([Define("authz", new AuthzPlugin(), AuthzKeys)], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        Assert.Equal(["authz", "observer"], DispatchListeners(ctx)); Assert.True(errors.Contains(AuthzErrors.ScopeMismatch));
    }

    private static EnforcementRequest Request(EnforcementSubject subject, string? permission) =>
        new(subject, permission, null, "api", AuthzEnforcement.CommandEntryPoint, CommandTarget);

    private static (string Decision, string Reason, string EntryPoint, string Target, long? IdentityRevision, bool Bypass) Decision(JsonElement payload) =>
        (payload.GetProperty("decision").GetString()!, payload.GetProperty("reason").GetString()!, payload.GetProperty("entryPoint").GetString()!, payload.GetProperty("target").GetString()!,
            payload.GetProperty("identityRevision").ValueKind == JsonValueKind.Null ? null : payload.GetProperty("identityRevision").GetInt64(), payload.GetProperty("bypass").GetBoolean());

    private static string[] Strings(JsonElement payload, string property) => [.. payload.GetProperty(property).EnumerateArray().Select(item => item.GetString()!)];

    private static string[] DispatchListeners(IPluginContext ctx) =>
        ctx.List(new ServiceKey(CommandEvents.Dispatching.Name)).Select(registration => registration.OwnerEntryId).Order(StringComparer.Ordinal).ToArray();

    private static async Task<long> Revision(IDataService data, string tenantId)
    {
        await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
        return await session.Context.Revisions.AsNoTracking().Where(row => row.TenantId == tenantId).Select(row => row.Revision).SingleOrDefaultAsync(Ct);
    }

    // Outbox 是服务级传输表（不按租户过滤），按行的路由租户筛出某租户的事件。
    private static async Task<IReadOnlyList<(string Type, string Payload)>> Outbox(IDataService data, AsyncLocalTenantContext tenant, string tenantId)
    {
        using var current = tenant.BeginScope(tenantId);
        await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
        return (await session.Context.OutboxMessages.AsNoTracking().Where(row => row.TenantId == tenantId).OrderBy(row => row.OccurredAt).ToListAsync(Ct))
            .Select(row => (row.EventTypeId, row.Payload)).ToArray();
    }

    // 链上的另一个组件：透传 command/dispatching，并收集 authz/decided，用来证明卸载 authz 只撤下它自己的监听器。
    private sealed class DecisionObserver : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public List<AuthorizationDecision> Decided { get; } = [];
        // AuthzEnforcement 与 Authz 由同一 Provider 提供；清单 provides 只声明 Authz，执行端口按服务键运行期取用（README「组件入口」）。
        public IReadOnlyList<ServiceKey> Inject => [AuthzDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            Context = ctx;
            ctx.On(CommandEvents.Dispatching, DispatchMode.Waterfall, (Func<CommandInvocation, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next()));
            ctx.On(AuthzRuntimeEvents.Decided, DispatchMode.Emit, (Func<AuthorizationDecision, ValueTask>)(decision => { Decided.Add(decision); return ValueTask.CompletedTask; }));
            return ValueTask.CompletedTask;
        }
    }

    // 不依赖 authz 的旁观者：卸载 authz 时它不会被级联卸载，用它观察链上残留。
    private sealed class DispatchObserver : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            Context = ctx;
            ctx.On(CommandEvents.Dispatching, DispatchMode.Waterfall, (Func<CommandInvocation, Func<ValueTask<object?>>, ValueTask<object?>>)((_, next) => next()));
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingPlugin(IQianPlugin inner, List<string> registered, List<string> revoked, Action<string> onRevoke) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inner.Inject;
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => inner.ApplyAsync(new RecordingContext(ctx, registered, revoked, onRevoke), config, ct);
    }

    // 原样转发，不改变任何解析、登记或分发结果；Effect 包装后记录实际执行，Provide / On / Restrict 之后压入标记——效果栈后进先出，标记序列即撤销顺序。
    private sealed class RecordingContext(IPluginContext inner, List<string> registered, List<string> revoked, Action<string> onRevoke) : IPluginContext
    {
        private int _effects;
        public PluginEntry Entry => inner.Entry;
        public ScopeKey Scope => inner.Scope;
        public IServiceProvider Services => inner.Services;
        public ILogger Logger => inner.Logger;
        public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, TService instance) where TService : class => Mark("provide:" + key.Name, inner.Provide(key, instance));
        public IRegistrationHandle<TService> Provide<TService>(ServiceKey key, string name, TService instance) where TService : class => Mark("provide:" + key.Name + ":" + name, inner.Provide(key, name, instance));
        public TService Inject<TService>(ServiceKey key) where TService : class => inner.Inject<TService>(key);
        public TService Inject<TService>(ServiceKey key, string name) where TService : class => inner.Inject<TService>(key, name);
        public TService? TryInject<TService>(ServiceKey key) where TService : class => inner.TryInject<TService>(key);
        public ValueTask<TService> WhenReadyAsync<TService>(ServiceKey key, CancellationToken ct) where TService : class => inner.WhenReadyAsync<TService>(key, ct);
        public void Effect(IDisposable disposer) { var label = Next(); inner.Effect(() => { Revoked(label); disposer.Dispose(); }); }
        public void Effect(Action dispose) { var label = Next(); inner.Effect(() => { Revoked(label); dispose(); }); }
        public void Effect(Func<ValueTask> disposeAsync) { var label = Next(); inner.Effect(() => { Revoked(label); return disposeAsync(); }); }
        public IDisposable On<TPayload, TResult>(QianEvent<TPayload, TResult> evt, DispatchMode mode, Delegate handler, int priority) => Mark("on:" + evt.Name, inner.On(evt, mode, handler, priority));
        public IDisposable On<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, DispatchMode mode, Delegate handler, int priority) => Mark("on:" + evt.Name, inner.On(evt, mode, handler, priority));
        public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, CancellationToken ct) => inner.DispatchAsync(evt, payload, ct);
        public ValueTask<IReadOnlyList<TResult>> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, IReadOnlyList<TResult>> evt, TPayload payload, CancellationToken ct) => inner.DispatchAsync(evt, payload, ct);
        public ValueTask<TResult> DispatchAsync<TPayload, TResult>(QianEvent<TPayload, TResult> evt, TPayload payload, Func<ValueTask<TResult>> terminal, CancellationToken ct) => inner.DispatchAsync(evt, payload, terminal, ct);
        public IPluginContext Scoped(ScopeKey scope) => inner.Scoped(scope);
        public IDisposable Restrict(ScopeKey scope, Func<Registration, bool> keep) => Mark("restrict:" + scope.Kind, inner.Restrict(scope, keep));
        public IReadOnlyList<Registration> List(ServiceKey key) => inner.List(key);

        private string Next() { var label = "effect#" + ++_effects; registered.Add(label); return label; }
        private T Mark<T>(string label, T registration)
        {
            registered.Add(label);
            inner.Effect(() => Revoked(label));
            return registration;
        }
        private void Revoked(string label) { revoked.Add(label); onRevoke(label); }
    }
}
