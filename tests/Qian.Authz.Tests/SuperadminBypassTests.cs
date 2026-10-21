using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Authorization;
using Kun.Caching.Abstractions;
using Kun.Caching.DependencyInjection;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Events;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Testing.Containers;

namespace Qian.Authz.Tests;

// S1-AZ-003：平台超管显式旁路与审计、直接主体授予 / 撤销的失效传播、经 ctx.Authz() 的完整解释。真实 PostgreSQL（受限应用角色 + 专用平台登录）、真实 Audit 插件与审计链、
// 真实 Redis；没有替身判定器、替身审计或测试快照。
public sealed partial class SuperadminBypassTests(PostgresFixture postgres, RedisFixture redis) : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private const string A = "7a1c0e52-3b6d-4f1e-9c2a-00000000000a";
    private const string B = "7a1c0e52-3b6d-4f1e-9c2a-00000000000b";
    private const string Read = "qian.sample.crm:customer:read";
    private static readonly ServiceKey[] AuthzKeys = [AuthzDefinition.Key, AuthzDefinition.PermissionsKey, AuthzDefinition.ResourceTypesKey, AuthzDefinition.SuperadminBypassKey];
    private static readonly AuthorizationSubject Root = new("root", null, "user");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 平台超管经显式旁路访问租户资源_放行并记入该租户审计链bypass为superadmin_不在租户成员列表_普通Check仍拒绝()
    {
        var role = Login("qian_authz_platform_"); var platform = await CreateLogin(role);
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant, platform); await host.StartAsync(Ct); await Prepare(host, role);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog();
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        using var active = host.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg" } }),
            Define("authz", new AuthzPlugin(), AuthzKeys, new { platformRole = role }), Define("consumer", consumer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        var ctx = consumer.Context!; var bypass = ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey);
        var roles = new PostgresTenantRoleStore(data, tenant); var platformRoles = new PostgresPlatformRoleStore(data, tenant, role);
        using (tenant.BeginScope(A))
        {
            await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner-a", "operator", Ct);
            await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
            // 平台角色只能在无租户上下文的平台运营流程里授予，不能从某个租户的请求里顺手提升。
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(() => platformRoles.AssignSuperadminAsync("root", "platform-operator", Ct))).Error.Code.Code);
        }
        await platformRoles.AssignSuperadminAsync("root", "platform-operator", Ct);
        using (tenant.BeginScope(A))
        {
            var allowed = await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, A, "customer/42", "api"), Ct);
            Assert.True(allowed.Allow); Assert.Equal("superadmin_bypass", allowed.Reason); Assert.Equal(["role:platform.superadmin", "bypass:superadmin"], allowed.MatchedPolicies);
            var record = Assert.Single(await ctx.Audit().ReadAsync(0, 10, cancellationToken: Ct)).Record;
            Assert.Equal((A, "user", "root", "api", "customer/42", "allowed", "qian.authz/superadmin.bypass"),
                (record.TenantId, record.ActorType, record.ActorId, record.Channel, record.Resource, record.Result, record.CapabilityId));
            Assert.Equal("superadmin", record.Payload.GetProperty("bypass").GetString()); Assert.Equal(Read, record.Payload.GetProperty("permission").GetString());
            // 超管对租户不可见（ADR-006 D6.11）：Authz 的租户成员视图只有本租户的角色绑定；超管在租户内也没有任何角色。
            var members = await roles.ListMembersAsync(Ct);
            Assert.Equal([("user", "owner-a")], members.Select(member => (member.SubjectType, member.SubjectId)));
            Assert.DoesNotContain(members, member => member.SubjectId == "root");
            // 普通 Check 不认平台主体，同名租户主体也没有授权。
            Assert.Equal("platform_subject_requires_bypass", (await ctx.Authz().CheckAsync(new PermissionRequest(Root, Read), Ct)).Reason);
            Assert.Equal("no_matching_grant", (await ctx.Authz().CheckAsync(new PermissionRequest(new AuthorizationSubject("root", A, "user"), Read), Ct)).Reason);
            // 拒绝路径只给稳定码，一律不写审计。
            Assert.Equal("not_superadmin", (await bypass.AuthorizeAsync(new SuperadminAccess(new AuthorizationSubject("intruder", null, "user"), Read, A, null, "api"), Ct)).Reason);
            Assert.Equal("tenant_context_mismatch", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, B, null, "api"), Ct)).Reason);
            Assert.Equal("tenant_context_mismatch", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, null, null, "api"), Ct)).Reason);
            Assert.Equal("permission_not_registered", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, "qian.sample.crm:customer:delete", A, null, "api"), Ct)).Reason);
            Assert.Equal("invalid_subject", (await bypass.AuthorizeAsync(new SuperadminAccess(new AuthorizationSubject("root", A, "user"), Read, A, null, "api"), Ct)).Reason);
            Assert.Equal("invalid_subject", (await bypass.AuthorizeAsync(new SuperadminAccess(new AuthorizationSubject("root", null, "service"), Read, A, null, "api"), Ct)).Reason);
            Assert.Equal("invalid_channel", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, A, null, "web"), Ct)).Reason);
            Assert.Single(await ctx.Audit().ReadAsync(0, 10, cancellationToken: Ct));
        }
        // 组件策略没有装进租户 B，B 里不存在可旁路的资源。
        using (tenant.BeginScope(B))
            Assert.Equal("permission_not_registered", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, B, null, "api"), Ct)).Reason);
        Assert.True(await platformRoles.RevokeSuperadminAsync("root", "platform-operator", Ct));
        using (tenant.BeginScope(A)) Assert.Equal("not_superadmin", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, A, null, "api"), Ct)).Reason);
        // 审计不可用即拒绝：卸载 Audit 后旁路不放行，租户链没有新记录。
        await platformRoles.AssignSuperadminAsync("root", "platform-operator", Ct);
        await runtime.UnmountAsync("audit", ScopeKey.Global, Ct);
        using (tenant.BeginScope(A)) Assert.Equal("audit_unavailable", (await bypass.AuthorizeAsync(new SuperadminAccess(Root, Read, A, null, "api"), Ct)).Reason);
        Assert.Equal(1L, await AdminScalar($"SELECT count(*) FROM plugin_qian_audit.audit_entries WHERE tenant_id = '{A}'"));
    }

    [Fact]
    public async Task 平台资源经旁路记入平台审计链_须无租户上下文_租户凭据看不到也写不进平台角色行_平台登录读不到租户行与授权表()
    {
        var role = Login("qian_authz_platform_"); var platform = await CreateLogin(role);
        var auditRole = Login("qian_audit_platform_"); var auditPlatform = await CreateLogin(auditRole);
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant, platform, auditPlatform); await host.StartAsync(Ct); await Prepare(host, role, auditRole);
        var data = host.Services.GetRequiredService<IDataService>();
        var tenancy = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.tenancy", "1.0.0",
            JsonSerializer.Deserialize<JsonElement>("""[{"resource":"tenant","actions":["create"],"risk":{"create":"critical"}}]"""), JsonSerializer.Deserialize<JsonElement>("[]"));
        await new PostgresPermissionCatalogStore(data).PutAsync(tenancy, Ct);
        var active = host.Services.GetRequiredService<IPermissionRegistry>().Register(tenancy.Version, tenancy.Permissions);
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]),
            Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg", platformRole = auditRole } }),
            Define("authz", new AuthzPlugin(), AuthzKeys, new { platformRole = role }), Define("consumer", consumer, [])], Ct);
        var ctx = consumer.Context!; var bypass = ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey);
        using (tenant.BeginScope(A))
        {
            var roles = new PostgresTenantRoleStore(data, tenant); await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner-a", "operator", Ct);
        }
        await new PostgresPlatformRoleStore(data, tenant, role).AssignSuperadminAsync("root", "platform-operator", Ct);
        var access = new SuperadminAccess(Root, "qian.tenancy:tenant:create", null, "tenancy/tenants", "api");
        var allowed = await bypass.AuthorizeAsync(access, Ct); Assert.True(allowed.Allow); Assert.Equal("superadmin_bypass", allowed.Reason);
        var record = Assert.Single(await ctx.Audit().ReadPlatformAsync(ctx, 0, 10, cancellationToken: Ct)).Record;
        Assert.Null(record.TenantId); Assert.Equal(("root", "tenancy/tenants"), (record.ActorId, record.Resource));
        Assert.Equal("superadmin", record.Payload.GetProperty("bypass").GetString());
        // 平台资源必须在无租户上下文中判定；活动登记释放后目录行仍在，但不能再旁路。
        using (tenant.BeginScope(A)) Assert.Equal("tenant_context_mismatch", (await bypass.AuthorizeAsync(access, Ct)).Reason);
        active.Dispose();
        Assert.Equal("permission_not_registered", (await bypass.AuthorizeAsync(access, Ct)).Reason);
        Assert.Single(await ctx.Audit().ReadPlatformAsync(ctx, 0, 10, cancellationToken: Ct));

        // 数据库层：普通应用角色看不到也写不进平台行；平台登录看不到租户行，也没有授权表的任何权限。
        await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
        Assert.Equal(0L, await Scalar(app, "SELECT count(*) FROM plugin_qian_authz.role_assignments"));
        await Scalar(app, $"SELECT set_config('app.current_tenant_id', '{A}', false)");
        Assert.Equal(1L, await Scalar(app, "SELECT count(*) FROM plugin_qian_authz.role_assignments"));
        var forged = await Assert.ThrowsAsync<PostgresException>(() => Scalar(app,
            "INSERT INTO plugin_qian_authz.roles(id, tenant_id, code, builtin) VALUES (gen_random_uuid(), NULL, 'platform.superadmin', true) RETURNING 1"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, forged.SqlState);
        await using var login = new NpgsqlConnection(platform); await login.OpenAsync(Ct);
        Assert.Equal(1L, await Scalar(login, "SELECT count(*) FROM plugin_qian_authz.role_assignments"));
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => Scalar(login, "SELECT count(*) FROM plugin_qian_authz.grants"))).SqlState);
        await Scalar(login, $"SELECT set_config('app.current_tenant_id', '{A}', false)");
        Assert.Equal(0L, await Scalar(login, "SELECT count(*) FROM plugin_qian_authz.role_assignments"));
    }

    [Theory]
    [InlineData("redis")]
    [InlineData("memory")]
    public async Task 授予直接主体后另一宿主三秒内获得权限_撤销后三秒内失效_快照按租户授权修订缓存(string backend)
    {
        var firstTenant = new AsyncLocalTenantContext(); var secondTenant = new AsyncLocalTenantContext();
        using var first = Host(firstTenant); using var second = Host(secondTenant); await first.StartAsync(Ct); await second.StartAsync(Ct); await Prepare(first);
        var catalog = Catalog(); await new PostgresPermissionCatalogStore(first.Services.GetRequiredService<IDataService>()).PutAsync(catalog, Ct);
        // redis：两宿主共用同一 Redis 前缀；memory：各自进程内缓存。两种后端都靠修订号失效，不依赖 TTL。
        var prefix = "az003-" + Guid.NewGuid().ToString("N")[..8] + ":";
        await using var oneServices = CacheServices(backend, prefix); await using var twoServices = CacheServices(backend, prefix);
        var oneCache = new CountingCache(oneServices.GetRequiredService<IKunCache>()); var twoCache = new CountingCache(twoServices.GetRequiredService<IKunCache>());
        var oneRegistry = new PermissionRegistry(); using var oneLease = oneRegistry.Register(catalog.Version, catalog.Permissions);
        var twoRegistry = new PermissionRegistry(); using var twoLease = twoRegistry.Register(catalog.Version, catalog.Permissions);
        var oneData = first.Services.GetRequiredService<IDataService>(); var twoData = second.Services.GetRequiredService<IDataService>();
        var one = new RbacPermissionChecker(oneData, firstTenant, oneRegistry, cache: oneCache);
        var two = new RbacPermissionChecker(twoData, secondTenant, twoRegistry, cache: twoCache);
        var oneRoles = new PostgresTenantRoleStore(oneData, firstTenant); var twoRoles = new PostgresTenantRoleStore(twoData, secondTenant);
        var alice = new PermissionRequest(new AuthorizationSubject("alice", A, "user"), Read);
        using var firstScope = firstTenant.BeginScope(A); using var secondScope = secondTenant.BeginScope(A);
        await oneRoles.EnsureBuiltinsAsync("operator", Ct); await oneRoles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);

        // 未授予：宿主二两次判定都拒绝，第二次直接命中缓存快照。
        Assert.Equal("no_matching_grant", (await two.CheckAsync(alice, Ct)).Reason);
        Assert.Equal("no_matching_grant", (await two.CheckAsync(alice, Ct)).Reason);
        Assert.Equal(1, twoCache.Hits); Assert.Single(twoCache.Writes);
        Assert.False((await one.CheckAsync(alice, Ct)).Allow);
        Assert.Equal(backend == "redis" ? 1 : 0, oneCache.Hits);

        // 宿主一授予并提交；宿主二已缓存的旧快照不再被读取，3 秒内放行。
        await oneRoles.AssignAsync("tenant.user", "user", "alice", "operator", Ct);
        var granted = await Within(TimeSpan.FromSeconds(3), async () => (await two.CheckAsync(alice, Ct)).Allow);
        Assert.True((await one.CheckAsync(alice, Ct)).Allow);
        // 宿主二撤销并提交；宿主一 3 秒内拒绝。
        Assert.True(await twoRoles.RevokeAsync("tenant.user", "user", "alice", "operator", Ct));
        var revoked = await Within(TimeSpan.FromSeconds(3), async () => !(await one.CheckAsync(alice, Ct)).Allow);
        Assert.Equal("no_matching_grant", (await two.CheckAsync(alice, Ct)).Reason);
        TestContext.Current.TestOutputHelper?.WriteLine($"{backend}: 授予生效 {granted.TotalMilliseconds:F0} ms，撤销失效 {revoked.TotalMilliseconds:F0} ms");

        // 撤销不存在的绑定不递增修订，已缓存的快照继续命中；每次真实变更都产生带新修订的键。
        var hits = twoCache.Hits;
        Assert.False(await twoRoles.RevokeAsync("tenant.user", "user", "alice", "operator", Ct));
        Assert.False((await two.CheckAsync(alice, Ct)).Allow); Assert.Equal(hits + 1, twoCache.Hits);
        Assert.Equal(3, oneCache.Writes.Concat(twoCache.Writes).Distinct(StringComparer.Ordinal).Count());
        Assert.All(oneCache.Writes.Concat(twoCache.Writes), key => Assert.StartsWith("qian.authz/rbac/v1:[\"" + A + "\",\"user\",\"alice\",", key, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Authz插件提供判定与登记端口_未配置平台登录时旁路一律拒绝_数据与字段端口拒绝_错误配置装载失败()
    {
        // PlatformAuthzDbContext 故意绑到普通应用凭据：配置 platformRole 时必须装载失败。
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant, postgres.AppConnectionString); await host.StartAsync(Ct); await Prepare(host);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog(); await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var registry = host.Services.GetRequiredService<IPermissionRegistry>(); using var lease = registry.Register(catalog.Version, catalog.Permissions);
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("authz", new AuthzPlugin(), AuthzKeys, new { advancedMode = false }),
            Define("consumer", consumer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        var ctx = consumer.Context!;
        Assert.Same(registry, ctx.Inject<IPermissionRegistry>(AuthzDefinition.PermissionsKey)); Assert.Same(registry, ctx.Inject<IResourceTypeRegistry>(AuthzDefinition.ResourceTypesKey));
        using (tenant.BeginScope(A))
        {
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner-a", "operator", Ct);
            await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
            var owner = new PermissionRequest(new AuthorizationSubject("owner-a", A, "user"), "qian.sample.crm:customer:write");
            Assert.True((await ctx.Authz().CheckAsync(owner, Ct)).Allow);
            Assert.Equal(["read", "write"], await ctx.Authz().ListAllowedActionsAsync(owner.Subject, "qian.sample.crm:customer", cancellationToken: Ct));
            Assert.Equal("platform_role_not_configured", (await ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey)
                .AuthorizeAsync(new SuperadminAccess(Root, Read, A, null, "api"), Ct)).Reason);
            var expression = await ctx.Authz().DataPermissions.GetExpressionAsync<string>(owner, Ct);
            Assert.False(expression.Compile()("any-row"));
            Assert.Equal("AUTHZ_DENIED", (await Assert.ThrowsAsync<QianErrorException>(async () => await ctx.Authz().DataPermissions.GetPredicateAsync(owner, Ct))).Error.Code.Code);
            var fields = await ctx.Authz().Fields.EvaluateAsync(owner, ["name", "phone"], Ct);
            Assert.Empty(fields.Readable); Assert.Empty(fields.Writable); Assert.Empty(fields.Masked);
        }
        await runtime.UnmountAsync("authz", ScopeKey.Global, Ct);
        foreach (var (id, scope, config) in new (string, ScopeKey, object)[]
        {
            ("advanced", ScopeKey.Global, new { advancedMode = true }), ("bad-role", ScopeKey.Global, new { platformRole = "Bad-Role" }),
            ("unknown", ScopeKey.Global, new { cache = "redis" }), ("tenant", new ScopeKey("tenant", A), new { })
        })
        {
            await runtime.MountAsync([new PluginDefinition(new PluginEntry(id, "qian.authz", "1.0.0", scope, 0, JsonSerializer.SerializeToElement(config)), new AuthzPlugin(), AuthzKeys)], Ct);
            Assert.Equal(PluginState.Failed, runtime.GetState(id, scope));
            Assert.Equal("INPUT_INVALID", Assert.IsType<QianErrorException>(runtime.GetFailure(id, scope)).Error.Code.Code);
            await runtime.UnmountAsync(id, scope, Ct);
        }
        await runtime.MountAsync([Define("shared-login", new AuthzPlugin(), AuthzKeys, new { platformRole = "qian_app" })], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("shared-login", ScopeKey.Global));
        Assert.IsType<QianDataMisconfiguredException>(runtime.GetFailure("shared-login", ScopeKey.Global));
    }

    [Fact]
    public async Task 经ctx_Authz取完整解释_拒绝给出缺少的权限点与命中角色_放行与stepup拒绝给出命中链路_跨租户不泄露()
    {
        // 消费方只依赖 Qian.Authz.Abstractions：完整解释必须经组件端口拿到，不能靠 Provider 的 RbacPermissionChecker。
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.sample.crm", "1.0.0",
            JsonSerializer.Deserialize<JsonElement>("""[{"resource":"customer","actions":["read","write","export","review"],"stepUp":["export"],"risk":{"export":"high"}}]"""),
            JsonSerializer.Deserialize<JsonElement>("""[{"role":"tenant.owner","grant":["qian.sample.crm:customer:read","qian.sample.crm:customer:write","qian.sample.crm:customer:export"]},{"role":"tenant.user","grant":["qian.sample.crm:customer:read"]}]"""));
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var registry = host.Services.GetRequiredService<IPermissionRegistry>(); using var lease = registry.Register(catalog.Version, catalog.Permissions);
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("authz", new AuthzPlugin(), AuthzKeys, new { advancedMode = false }),
            Define("consumer", consumer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        var authz = consumer.Context!.Authz();
        const string Write = "qian.sample.crm:customer:write", Export = "qian.sample.crm:customer:export";
        AuthorizationSubject owner = new("owner-a", A, "user"), reader = new("reader-a", A, "user");
        using (tenant.BeginScope(A))
        {
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.EnsureBuiltinsAsync("operator", Ct); await roles.AssignAsync("tenant.owner", "user", "owner-a", "operator", Ct);
            await roles.AssignAsync("tenant.user", "user", "owner-a", "operator", Ct);
            await roles.AssignAsync("tenant.user", "user", "reader-a", "operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
            var allowed = await authz.ExplainDetailedAsync(new PermissionRequest(owner, Write), Ct);
            Assert.True(allowed.Decision.Allow); Assert.Equal("role_grant", allowed.Decision.Reason); Assert.Equal(["tenant.owner"], allowed.MatchedRoles);
            await using (var session = await data.OpenAsync<AuthzDbContext>(Ct))
            {
                var ids = await session.Context.Grants.AsNoTracking().Where(grant => grant.PermissionCode == Write).Select(grant => grant.Id).ToArrayAsync(Ct);
                Assert.Equal(ids.Select(id => "grant:" + id.ToString("D")).Order(StringComparer.Ordinal), allowed.MatchedGrants);
            }
            Assert.Single(allowed.MatchedGrants); Assert.Empty(allowed.Missing);
            const string Review = "qian.sample.crm:customer:review";
            var noGrant = await authz.ExplainDetailedAsync(new PermissionRequest(owner, Review), Ct);
            Assert.False(noGrant.Decision.Allow); Assert.Equal("no_matching_grant", noGrant.Decision.Reason);
            Assert.Equal(["tenant.owner", "tenant.user"], noGrant.MatchedRoles); Assert.Empty(noGrant.MatchedGrants); Assert.Equal([Review], noGrant.Missing);
            var denied = await authz.ExplainDetailedAsync(new PermissionRequest(reader, Write), Ct);
            Assert.False(denied.Decision.Allow); Assert.Equal("no_matching_grant", denied.Decision.Reason);
            Assert.Equal(["tenant.user"], denied.MatchedRoles); Assert.Empty(denied.MatchedGrants); Assert.Equal([Write], denied.Missing);
            // RBAC 已命中、只缺 step-up 证明的拒绝也带命中链路（宿主没有注册执行约束）。
            var stepUp = await authz.ExplainDetailedAsync(new PermissionRequest(owner, Export), Ct);
            Assert.False(stepUp.Decision.Allow); Assert.Equal("step_up_required", stepUp.Decision.Reason); Assert.Equal(["tenant.owner"], stepUp.MatchedRoles);
            Assert.Single(stepUp.MatchedGrants); Assert.Empty(stepUp.Missing);
            // 继承自 Kun 的 ExplainAsync 只给判定，与 Check 和完整解释的结论一致。
            foreach (var (subject, permission, explanation) in new[] { (owner, Write, allowed), (reader, Write, denied), (owner, Export, stepUp), (owner, Review, noGrant) })
            {
                var decision = await authz.ExplainAsync(new PermissionRequest(subject, permission), Ct);
                Assert.Equal((explanation.Decision.Allow, explanation.Decision.Reason), (decision.Allow, decision.Reason));
                var checkedDecision = await authz.CheckAsync(new PermissionRequest(subject, permission), Ct);
                Assert.Equal((explanation.Decision.Allow, explanation.Decision.Reason), (checkedDecision.Allow, checkedDecision.Reason));
            }
        }
        using (tenant.BeginScope(B))
        {
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.EnsureBuiltinsAsync("operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
            var foreign = await authz.ExplainDetailedAsync(new PermissionRequest(new AuthorizationSubject(owner.Id, B, "user"), Write), Ct);
            Assert.False(foreign.Decision.Allow); Assert.Equal("no_matching_grant", foreign.Decision.Reason);
            Assert.Empty(foreign.MatchedRoles); Assert.Empty(foreign.MatchedGrants); Assert.Equal([Write], foreign.Missing);
        }
    }

    private IHost Host(AsyncLocalTenantContext tenant, string? authzPlatform = null, string? auditPlatform = null, IKunCache? cache = null, bool authzEvents = true)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunEventBus(registrar => registrar.RegisterAuthzEvents());
        // 与生产宿主相同：授权变更的领域事件处理器 + 写 v1 信封的 Outbox 端口；authzEvents=false 复现漏装处理器的宿主。
        builder.Services.AddKunMediator(mediator => { if (authzEvents) mediator.AddQianAuthzEvents(); }); builder.Services.AddQianEvents();
        builder.Services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
        if (cache is not null) builder.Services.AddSingleton(cache);
        builder.Services.AddQianData<AuthzDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuthzDbContext.Schema));
        builder.Services.AddQianData<AuditDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuditDbContext.Schema));
        if (authzPlatform is not null)
            builder.Services.AddQianData<PlatformAuthzDbContext>((_, options) => options.UseNpgsql(authzPlatform).UseKunSchema(AuthzDbContext.Schema));
        if (auditPlatform is not null)
            builder.Services.AddQianData<PlatformAuditDbContext>((_, options) => options.UseNpgsql(auditPlatform).UseKunSchema(AuditDbContext.Schema));
        return builder.Build();
    }

    private async Task Prepare(IHost host, string? authzRole = null, string? auditRole = null)
    {
        using var scope = host.Services.CreateScope();
        var sql = "DROP SCHEMA IF EXISTS plugin_qian_authz CASCADE;\nDROP SCHEMA IF EXISTS plugin_qian_audit CASCADE;\n" +
            CreateScript(scope.ServiceProvider.GetRequiredService<AuthzDbContext>()) + "\n" + CreateScript(scope.ServiceProvider.GetRequiredService<AuditDbContext>()) +
            "\nGRANT USAGE ON SCHEMA plugin_qian_authz TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_authz TO qian_app;" +
            "\nGRANT USAGE ON SCHEMA plugin_qian_audit TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_audit TO qian_app;";
        await AdminScalar(sql + "\nSELECT 1");
        if (authzRole is not null) await AdminScalar("BEGIN;" + AuthzPlatformAccess.CreateMigrationSql(authzRole) + "COMMIT; SELECT 1");
        if (auditRole is not null) await AdminScalar("BEGIN;" + AuditPlatformAccess.CreateMigrationSql(auditRole) + "COMMIT; SELECT 1");
    }

    private static string CreateScript(DbContext db)
    {
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        return string.Join('\n', db.GetService<IMigrationsSqlGenerator>().Generate(operations, model).Select(command => command.CommandText));
    }

    private async Task<string> CreateLogin(string role)
    {
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await AdminScalar($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'; SELECT 1");
        return new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
    }

    private async Task<object?> AdminScalar(string sql)
    {
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        return await Scalar(admin, sql);
    }

    private static async Task<object?> Scalar(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection); return await command.ExecuteScalarAsync(Ct);
    }

    private ServiceProvider CacheServices(string backend, string prefix)
    {
        var services = new ServiceCollection(); services.AddLogging();
        var builder = services.AddKunCaching(options => options.KeyPrefix = prefix);
        if (backend == "redis") builder.UseRedis(redis.ConnectionString); else builder.UseMemory();
        return services.BuildServiceProvider();
    }

    private static async Task<TimeSpan> Within(TimeSpan limit, Func<Task<bool>> ready)
    {
        var elapsed = Stopwatch.StartNew();
        while (!await ready())
        {
            Assert.True(elapsed.Elapsed <= limit, $"超过 {limit.TotalSeconds:F0} 秒仍未生效。");
            await Task.Delay(50, Ct);
        }
        Assert.True(elapsed.Elapsed <= limit, $"生效耗时 {elapsed.Elapsed.TotalMilliseconds:F0} ms，超过 {limit.TotalSeconds:F0} 秒。");
        return elapsed.Elapsed;
    }

    private static string Login(string prefix) => prefix + Guid.NewGuid().ToString("N")[..8];

    private static PermissionCatalog Catalog()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var directory = Path.Combine(root!.FullName, "samples/Qian.Sample.Crm/metadata");
        return new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.sample.crm", "1.0.0",
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "permissions.json"))),
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(directory, "roles.json"))));
    }

    private static PluginDefinition Define(string id, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides, object? config = null) => new(
        new PluginEntry(id, "qian." + id, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);

    private sealed class Consumer : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [AuthzDefinition.Key, AuthzDefinition.SuperadminBypassKey];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }

    // 只统计字节路径：命中次数与写入的键；判定器只走 GetBytesAsync / SetBytesAsync。
    private sealed class CountingCache(IKunCache inner) : IKunCache
    {
        private int _hits;
        public int Hits => Volatile.Read(ref _hits);
        public ConcurrentQueue<string> Writes { get; } = new();
        [RequiresUnreferencedCode("测试包装，委托给内部缓存。")]
        [RequiresDynamicCode("测试包装，委托给内部缓存。")]
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => inner.GetAsync<T>(key, cancellationToken);
        [RequiresUnreferencedCode("测试包装，委托给内部缓存。")]
        [RequiresDynamicCode("测试包装，委托给内部缓存。")]
        public Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default) => inner.SetAsync(key, value, ttl, cancellationToken);
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => inner.RemoveAsync(key, cancellationToken);
        public async Task<byte[]?> GetBytesAsync(string key, CancellationToken cancellationToken = default)
        {
            var value = await inner.GetBytesAsync(key, cancellationToken);
            if (value is not null) Interlocked.Increment(ref _hits);
            return value;
        }
        public Task SetBytesAsync(string key, byte[] value, TimeSpan? ttl = null, CancellationToken cancellationToken = default)
        {
            Assert.Equal(TimeSpan.FromMinutes(10), ttl); Writes.Enqueue(key);
            return inner.SetBytesAsync(key, value, ttl, cancellationToken);
        }
        [RequiresUnreferencedCode("测试包装，委托给内部缓存。")]
        [RequiresDynamicCode("测试包装，委托给内部缓存。")]
        public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? ttl = null, CancellationToken cancellationToken = default) =>
            inner.GetOrSetAsync(key, factory, ttl, cancellationToken);
    }
}
