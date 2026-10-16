using Kun.Authorization;
using Kun.Caching.Abstractions;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Runtime;

namespace Qian.Authz.Tests;

public sealed partial class SuperadminBypassTests
{
    [Theory]
    [InlineData("memory")]
    [InlineData("redis")]
    [InlineData("none")]
    public async Task 真实AuthzPlugin从宿主取得缓存_第二次判定命中_授予撤销下一次即生效_缺缓存也正常(string backend)
    {
        await using var cacheHost = backend == "none" ? null : CacheServices(backend, "az008-" + Guid.NewGuid().ToString("N") + ":");
        var cache = cacheHost is null ? null : new CountingCache(cacheHost.GetRequiredService<IKunCache>());
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant, cache: cache); await host.StartAsync(Ct); await Prepare(host);
        var catalog = Catalog(); var data = host.Services.GetRequiredService<IDataService>();
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("authz", new AuthzPlugin([catalog]), AuthzKeys),
            Define("consumer", consumer, [])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("authz", ScopeKey.Global));
        var authz = consumer.Context!.Authz(); var roles = new PostgresTenantRoleStore(data, tenant);
        using var scope = tenant.BeginScope(A);
        await roles.EnsureBuiltinsAsync("operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
        var request = new PermissionRequest(new AuthorizationSubject("alice", A, "user"), Read);
        Assert.False((await authz.CheckAsync(request, Ct)).Allow); Assert.False((await authz.CheckAsync(request, Ct)).Allow);
        if (cache is not null) { Assert.Equal(1, cache.Hits); Assert.Single(cache.Writes); }
        await roles.AssignAsync("tenant.user", "user", "alice", "operator", Ct);
        Assert.True((await authz.CheckAsync(request, Ct)).Allow);
        if (cache is not null) { Assert.Equal(1, cache.Hits); Assert.Equal(2, cache.Writes.Count); }
        Assert.True((await authz.CheckAsync(request, Ct)).Allow);
        if (cache is not null) Assert.Equal(2, cache.Hits);
        Assert.True(await roles.RevokeAsync("tenant.user", "user", "alice", "operator", Ct));
        var revoked = await authz.CheckAsync(request, Ct); Assert.False(revoked.Allow); Assert.Equal("no_matching_grant", revoked.Reason);
        if (cache is not null) { Assert.Equal(2, cache.Hits); Assert.Equal(3, cache.Writes.Distinct(StringComparer.Ordinal).Count()); }
    }

    [Fact]
    public async Task 超管旁路审计真实数据库追加抛错时拒绝_事务不留半条链_恢复后同一请求放行()
    {
        var role = Login("qian_authz_platform_"); var platform = await CreateLogin(role);
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant, platform); await host.StartAsync(Ct); await Prepare(host, role);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog();
        await using var runtime = new PluginLoader(host.Services); var consumer = new Consumer();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key]),
            Define("authz", new AuthzPlugin([catalog]), AuthzKeys, new { platformRole = role }), Define("consumer", consumer, [])], Ct);
        var ctx = consumer.Context!; var roles = new PostgresTenantRoleStore(data, tenant);
        using (tenant.BeginScope(A))
        { await roles.EnsureBuiltinsAsync("operator", Ct); await roles.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct); }
        await new PostgresPlatformRoleStore(data, tenant, role).AssignSuperadminAsync("root", "platform-operator", Ct);
        await AdminScalar("""
            CREATE FUNCTION plugin_qian_audit.reject_bypass() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION 'audit write failure'; END $$;
            CREATE TRIGGER reject_bypass BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW
              EXECUTE FUNCTION plugin_qian_audit.reject_bypass(); SELECT 1;
            """);
        var bypass = ctx.Inject<ISuperadminBypass>(AuthzDefinition.SuperadminBypassKey);
        var access = new SuperadminAccess(Root, Read, A, "customer/42", "api");
        using (tenant.BeginScope(A))
        {
            var denied = await bypass.AuthorizeAsync(access, Ct);
            Assert.False(denied.Allow); Assert.Equal("audit_failed", denied.Reason);
            Assert.Empty(await ctx.Audit().ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal(0L, await AdminScalar("SELECT count(*) FROM plugin_qian_audit.audit_entries"));
            Assert.Equal(0L, await AdminScalar("SELECT count(*) FROM plugin_qian_audit.audit_chain_state"));
        }
        await AdminScalar("DROP TRIGGER reject_bypass ON plugin_qian_audit.audit_entries; DROP FUNCTION plugin_qian_audit.reject_bypass(); SELECT 1;");
        using (tenant.BeginScope(A))
        {
            var allowed = await bypass.AuthorizeAsync(access, Ct); Assert.True(allowed.Allow);
            var entry = Assert.Single(await ctx.Audit().ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal(1, entry.Sequence); Assert.Equal(A, entry.Record.TenantId);
            Assert.Equal("superadmin", entry.Record.Payload.GetProperty("bypass").GetString());
            Assert.True((await ctx.Audit().VerifyAsync(Ct)).Valid);
        }
    }
}
