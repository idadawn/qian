using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Authz.Tests;

// S1-AZ-009（S1-DEC-027）：授予的增、改、撤提交后，经同进程的身份端口递增 user 主体在该租户的 identity_revision，并把 PEP 下限同步抬到新值。
// 真实 PostgreSQL、真实 Audit 与 PluginLoader；身份端口是只实现 BumpIdentityRevisionAsync 的替身组件，用来编排不存在的成员与瞬时故障。
// 真实身份组件的端到端（userinfo、refresh、PEP 401）见 Qian.Sample.Crm.Tests 的 CustomerCommandTests.IdentityRevision。
public sealed partial class SuperadminBypassTests
{
    [Fact]
    public async Task 授予变更提交后经同进程身份端口递增user主体修订并同步抬高PEP下限_非user非GUID与无成员身份跳过_瞬时故障退避重试_持续失败不回滚_角色级变更不扇出()
    {
        var tenant = new AsyncLocalTenantContext(); using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var data = host.Services.GetRequiredService<IDataService>(); var catalog = Catalog();
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        using var active = host.Services.GetRequiredService<IPermissionRegistry>().Register(catalog.Version, catalog.Permissions);
        using var metrics = new PropagationMetrics();
        var identity = new IdentityDouble();
        await using var runtime = new PluginLoader(host.Services); var observer = new DecisionObserver();
        await runtime.MountAsync([Define("data", new DataPlugin(), [DataDefinition.Key]), Define("audit", new AuditPlugin(), [AuditDefinition.Key], new { chain = new { store = "pg" } }),
            Define("authz", new AuthzPlugin(), AuthzKeys), Define("identity", identity, [IdentityDefinition.AccountsKey]), Define("observer", observer, [])], Ct);
        var authz = runtime.GetContext("authz", ScopeKey.Global); var enforcement = observer.Context!.Enforcement();
        var grants = authz.Inject<ITenantRoleGrant>(TenantRoleGrant.Key); var store = AuthzPlugin.RoleStore(authz, tenant);
        string alice = PersonId(), carol = PersonId(), dave = PersonId(), erin = PersonId(), frank = PersonId();
        identity.Missing.Add(Guid.Parse(carol)); identity.Transient[Guid.Parse(dave)] = 2; identity.Broken.Add(Guid.Parse(erin));
        Task<AuthorizationDecision> Enforce(string subject, long revision) => enforcement.EnforceAsync(Request(new EnforcementSubject(subject, A, "user", revision), Read), Ct).AsTask();
        using var scope = tenant.BeginScope(A);

        // 角色级变更（建内置角色、组件默认授权安装）只经租户授权修订生效，不扇出到成员（Q3）。
        await store.EnsureBuiltinsAsync("operator", Ct); await store.InstallDefaultsAsync(catalog.PluginId, catalog.Version, null, "operator", Ct);
        Assert.Empty(identity.Calls);

        // 授予（生产路径的授予端口）→ 提交后递增 alice 在 A 的修订并抬高下限：修订 0 的旧令牌即刻 identity_revision_stale，修订 1 放行。
        await grants.AssignAsync("tenant.user", "user", alice, "operator", Ct);
        Assert.Equal([(A, Guid.Parse(alice), "authz.grant.assigned")], identity.Calls);
        Assert.Equal(1L, await Floor(data, alice));
        Assert.Equal(AuthzEnforcement.StaleIdentityRevision, (await Enforce(alice, 0)).Reason);
        Assert.True((await Enforce(alice, 1)).Allow);
        // 重复授予没有变更，不递增；改有效期与撤销各递增一次，下限随之抬高。
        await grants.AssignAsync("tenant.user", "user", alice, "operator", Ct);
        await grants.BindAsync(new RoleBinding("tenant.user", "user", alice, "operator", ValidTo: DateTimeOffset.UtcNow.AddDays(1)), Ct);
        Assert.True(await store.RevokeAsync("tenant.user", "user", alice, "operator", Ct));
        Assert.Equal(["authz.grant.assigned", "authz.grant.updated", "authz.grant.revoked"], identity.Calls.Select(call => call.Reason));
        Assert.Equal(3L, await Floor(data, alice));
        Assert.Equal(AuthzEnforcement.StaleIdentityRevision, (await Enforce(alice, 2)).Reason);
        Assert.Equal("no_matching_grant", (await Enforce(alice, 3)).Reason);

        // 非 user 主体与不是 Person 的 user 主体不调身份端口；该租户没有成员身份（NOT_FOUND）调一次即跳过、不重试；授予照常提交。
        identity.Calls.Clear();
        await grants.AssignAsync("tenant.user", "service", "crm-reader", "operator", Ct);
        await grants.AssignAsync("tenant.user", "organization", "org-1", "operator", Ct);
        await grants.AssignAsync("tenant.user", "user", "bob", "operator", Ct);
        Assert.Empty(identity.Calls);
        await grants.AssignAsync("tenant.user", "user", carol, "operator", Ct);
        Assert.Equal([(A, Guid.Parse(carol), "authz.grant.assigned")], identity.Calls);
        Assert.Null(await Floor(data, carol));

        // 瞬时故障退避重试：前两次失败、第三次成功。
        identity.Calls.Clear();
        await grants.AssignAsync("tenant.user", "user", dave, "operator", Ct);
        Assert.Equal(3, identity.Calls.Count(call => call.Person == Guid.Parse(dave)));
        Assert.Equal(1L, await Floor(data, dave));
        // 下限只增不减：令牌已把 dave 的下限抬到 10，身份端口这次返回 2，下限仍是 10。
        Assert.True((await Enforce(dave, 10)).Allow);
        await grants.BindAsync(new RoleBinding("tenant.user", "user", dave, "operator", ValidTo: DateTimeOffset.UtcNow.AddDays(1)), Ct);
        Assert.Equal(10L, await Floor(data, dave));

        // 持续失败：预算用尽即放弃并告警计数，授予已提交、不回滚，也不向调用方抛错。
        var quick = new PostgresTenantRoleStore(data, tenant, new IdentityRevisionPropagation(authz, data) { Budget = TimeSpan.FromMilliseconds(400) });
        identity.Calls.Clear(); var elapsed = Stopwatch.StartNew();
        await quick.AssignAsync("tenant.user", "user", erin, "operator", Ct);
        Assert.True(elapsed.Elapsed < TimeSpan.FromSeconds(2), $"持续失败的递增耗时 {elapsed.Elapsed.TotalMilliseconds:F0} ms，没有按预算放弃。");
        Assert.True(identity.Calls.Count >= 2, "瞬时故障没有重试。");
        Assert.Contains(await store.ListMembersAsync(Ct), member => member.SubjectId == erin);
        Assert.Null(await Floor(data, erin));
        Assert.Contains(("failed", "bump"), metrics.Outcomes);
        Assert.Contains(("skipped", "NOT_FOUND"), metrics.Outcomes);
        Assert.Contains(("bumped", null), metrics.Outcomes);

        // 身份组件不在本进程（已卸载）：跳过，授予照常提交。
        await runtime.UnmountAsync("identity", ScopeKey.Global, Ct);
        identity.Calls.Clear();
        await grants.AssignAsync("tenant.user", "user", frank, "operator", Ct);
        Assert.Empty(identity.Calls);
        Assert.Null(await Floor(data, frank));
        Assert.Contains(await store.ListMembersAsync(Ct), member => member.SubjectId == frank);
        Assert.Contains(("skipped", "identity_unavailable"), metrics.Outcomes);
    }

    private static string PersonId() => Guid.CreateVersion7().ToString("D");

    private static async Task<long?> Floor(IDataService data, string subject)
    {
        await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
        return await session.Context.SubjectRevisions.AsNoTracking().Where(row => row.TenantId == A && row.SubjectType == "user" && row.SubjectId == subject)
            .Select(row => (long?)row.IdentityRevision).SingleOrDefaultAsync(Ct);
    }

    // 只收本计数器的 (outcome, reason)；同一进程里别的用例也可能计数，断言一律用 Contains。
    private sealed class PropagationMetrics : IDisposable
    {
        private readonly MeterListener _listener = new();
        public ConcurrentQueue<(string? Outcome, string? Reason)> Outcomes { get; } = new();
        public PropagationMetrics()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == AuthzMetrics.MeterName && instrument.Name == "qian.authz.identity_revision.propagations") listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
            {
                string? outcome = null, reason = null;
                foreach (var tag in tags) { if (tag.Key == "outcome") outcome = tag.Value as string; else if (tag.Key == "reason") reason = tag.Value as string; }
                Outcomes.Enqueue((outcome, reason));
            });
            _listener.Start();
        }
        public void Dispose() => _listener.Dispose();
    }

    // 身份组件的替身：只提供账号端口的递增，按 (租户, Person) 计修订；可预置不存在的成员（NOT_FOUND）、前几次瞬时故障或持续故障。
    private sealed class IdentityDouble : IQianPlugin
    {
        private readonly ConcurrentDictionary<(string?, Guid), long> _revisions = new();
        private Kun.Core.MultiTenancy.ITenantContext? _tenant;
        public ConcurrentQueue<(string? Tenant, Guid Person, string Reason)> Calls { get; } = new();
        public HashSet<Guid> Missing { get; } = [];
        public HashSet<Guid> Broken { get; } = [];
        public ConcurrentDictionary<Guid, int> Transient { get; } = new();
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            _tenant = ctx.Services.GetRequiredService<Kun.Core.MultiTenancy.ITenantContext>();
            ctx.Provide<IIdentityAccounts>(IdentityDefinition.AccountsKey, new Accounts(this));
            return ValueTask.CompletedTask;
        }

        private ValueTask<long> Bump(Guid person, string reason)
        {
            var tenant = _tenant!.TenantId; Calls.Enqueue((tenant, person, reason));
            if (Missing.Contains(person)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
            if (Broken.Contains(person)) throw new TimeoutException("身份库不可用");
            if (Transient.TryGetValue(person, out var left) && left > 0) { Transient[person] = left - 1; throw new TimeoutException("瞬时故障"); }
            return ValueTask.FromResult(_revisions.AddOrUpdate((tenant, person), 1, (_, value) => value + 1));
        }

        private sealed class Accounts(IdentityDouble owner) : IIdentityAccounts
        {
            public ValueTask<long> BumpIdentityRevisionAsync(Guid personId, string reason, CancellationToken cancellationToken = default) => owner.Bump(personId, reason);
            public ValueTask<Person> CreatePersonAsync(CreatePerson request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Person?> FindPersonAsync(string identifier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Membership> CreateMembershipAsync(CreateMembership request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Person> BindIdentifierAsync(Guid personId, IdentifierInput identifier, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Person> UnbindIdentifierAsync(Guid personId, IdentifierKind kind, string value, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Person> ReplaceIdentifierAsync(Guid personId, IdentifierKind kind, string oldValue, IdentifierInput replacement, CancellationToken cancellationToken = default) =>
                throw new NotSupportedException();
            public ValueTask ChangePasswordAsync(Guid personId, PasswordChange change, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask ResetPasswordAsync(Guid personId, PasswordReset reset, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Person> SetPersonStatusAsync(Guid personId, string status, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask<Membership> SetMembershipStatusAsync(Guid membershipId, string status, string reason, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask LinkExternalIdentityAsync(ExternalIdentityLink link, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public ValueTask UnlinkExternalIdentityAsync(Guid membershipId, string connectorId, string subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }
    }
}
