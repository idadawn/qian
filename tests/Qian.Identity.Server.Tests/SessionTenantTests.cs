using System.Net;
using Qian.Events;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;
using static Qian.Identity.Server.Tests.SessionSupport;

namespace Qian.Identity.Server.Tests;

// 租户不可用时的会话处理（R95）：tenant.suspended / tenant.expired 到达后该租户的会话立即吊销；事件丢失或迟到时，后台对账按目录状态兜底；
// 无论哪条路径，refresh 都被拒，别的租户和平台身份的会话不受牵连。
public sealed class SessionTenantTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 租户冻结_校验即时失败_对账在间隔内吊销全部会话并发出事件_别的租户与平台会话不受影响()
    {
        // 对账间隔缩到 1 秒，让测试不必等满默认的 30 秒；生产的上限是 30 秒，即冻结后会话 ≤ 60 秒失效（含旁路）。
        await using var host = await AccountsHost.Start(postgres, new IdentityAccountOptions { Session = new SessionPolicy { ReconcileInterval = TimeSpan.FromSeconds(1) } });
        var acme = host.Tenants.Add("acme"); var beta = host.Tenants.Add("beta");
        var (person, phone) = await CreatePerson(host);
        await Join(host, acme, person.Id); await Join(host, beta, person.Id);
        var (_, platformPhone) = await CreatePerson(host, "平台运营", platform: true);
        var inAcme = host.NewBrowser(); var onPhone = host.NewBrowser(); var inBeta = host.NewBrowser(); var platformBrowser = host.NewBrowser();
        var (acmeWeb, acmeTokens) = await SignIn(host, inAcme, phone, acme.Id);
        var (acmeMobile, _) = await SignIn(host, onPhone, phone, acme.Id, "mobile");
        var (betaWeb, _) = await SignIn(host, inBeta, phone, beta.Id);
        await platformBrowser.Login(platformPhone, Password);

        host.Tenants.SetStatus(acme.Id, TenantStatus.Suspended);
        // 目录状态一变，每次校验都读目录：账号接口 401、refresh 被拒，不必等对账。
        Assert.Equal(HttpStatusCode.Unauthorized, (await inAcme.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(inAcme, "web", acmeTokens.RefreshToken!));
        Assert.Null(await inAcme.Code());

        // 对账把冻结租户的会话逐个吊销（写入吊销原因），旁路随后为每个会话发一条事件，行为主体是身份服务本身。
        await Eventually(async () => await Revoked(host, acmeWeb) is not null && await Revoked(host, acmeMobile) is not null, TimeSpan.FromSeconds(15), "冻结租户的 session_revoked 事件");
        Assert.Equal(new[] { acmeWeb, acmeMobile }.Order(), (await RevokedSessions(host, person.Id)).Order());
        Assert.Equal("2", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoke_reason = 'tenant_suspended' AND revoked_by = 'service:qian.identity'"));
        var envelope = (await Revoked(host, acmeWeb))!;
        Assert.Equal(("tenant_suspended", EventActorKind.Service, "qian.identity", acme.Id), (envelope.Payload.GetProperty("reason").GetString(), envelope.Actor.Kind, envelope.Actor.Id, envelope.TenantId));

        // 别的租户的会话与平台身份的会话照常。
        Assert.Equal(HttpStatusCode.OK, (await inBeta.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Equal(HttpStatusCode.OK, (await platformBrowser.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.Null(await Revoked(host, betaWeb));

        // 租户恢复后，已吊销的会话不会复活；用户可以重新登录。
        host.Tenants.SetStatus(acme.Id, TenantStatus.Active);
        Assert.Equal(HttpStatusCode.Unauthorized, (await inAcme.Json(HttpMethod.Get, "api/identity/me")).Status);
        var (again, _) = await SignIn(host, inAcme, phone, acme.Id);
        Assert.Equal(HttpStatusCode.OK, (await inAcme.Json(HttpMethod.Get, "api/identity/me")).Status);
        Assert.DoesNotContain(again, await RevokedSessions(host, person.Id));
    }

    [Fact]
    public async Task 事件处理器立即吊销该租户的全部会话_不等对账_也不依赖目录状态()
    {
        await using var host = await AccountsHost.Start(postgres); // 对账间隔取默认 30 秒，本用例的时长内不会再对账
        var acme = host.Tenants.Add("acme"); var beta = host.Tenants.Add("beta");
        var (person, phone) = await CreatePerson(host);
        await Join(host, acme, person.Id); await Join(host, beta, person.Id);
        var inAcme = host.NewBrowser(); var onPhone = host.NewBrowser(); var inBeta = host.NewBrowser();
        var (acmeWeb, acmeTokens) = await SignIn(host, inAcme, phone, acme.Id);
        var (acmeMobile, _) = await SignIn(host, onPhone, phone, acme.Id, "mobile");
        var (betaWeb, betaTokens) = await SignIn(host, inBeta, phone, beta.Id);

        // tenant.suspended.v1：目录里仍是 Active（事件先于目录刷新到达），会话照样被吊销。
        await new TenantSuspendedSessionHandler(host.Services).HandleAsync(new TenantSuspended(acme.Id, TenantStatus.Active), default!, Ct);
        Assert.Equal(new[] { acmeWeb, acmeMobile }.Order(), (await RevokedSessions(host, person.Id)).Order());
        Assert.Equal("2", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoke_reason = 'tenant_suspended'"));
        Assert.Equal(HttpStatusCode.BadRequest, await Refresh(inAcme, "web", acmeTokens.RefreshToken!));
        Assert.Equal(HttpStatusCode.Unauthorized, (await inAcme.Json(HttpMethod.Get, "api/identity/me")).Status);
        await Eventually(async () => await Revoked(host, acmeWeb) is not null && await Revoked(host, acmeMobile) is not null, Settle, "处理器吊销后的 session_revoked 事件");
        Assert.Equal(HttpStatusCode.OK, (await inBeta.Json(HttpMethod.Get, "api/identity/me")).Status); // 另一个租户不受影响
        Assert.Null(await Revoked(host, betaWeb));
        // 重复投递（至少一次语义）是幂等的：没有新的有效会话可吊销，也不会多发事件。
        await new TenantSuspendedSessionHandler(host.Services).HandleAsync(new TenantSuspended(acme.Id, TenantStatus.Active), default!, Ct);
        await host.Service.ProcessRevocationsAsync(50, Ct);
        Assert.Equal(2, (await EventsOf(host, "identity.session_revoked.v1")).Count);

        // tenant.expired.v1 同理，原因记为 tenant_expired。
        var (renewed, _) = await SignIn(host, inAcme, phone, acme.Id);
        await new TenantExpiredSessionHandler(host.Services).HandleAsync(new TenantExpired(acme.Id, host.Clock.GetUtcNow()), default!, Ct);
        Assert.Equal("tenant_expired", await host.Text("SELECT revoke_reason FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(renewed))));
        await Eventually(async () => await Revoked(host, renewed) is not null, Settle, "tenant_expired 之后的 session_revoked 事件");
        Assert.Equal(HttpStatusCode.OK, await Refresh(inBeta, "web", betaTokens.RefreshToken!)); // 乙方的 refresh 照常
    }

    [Fact]
    public async Task 对账按目录状态判定_试用与正常保留会话_宽限_冻结_待激活与目录里已不存在的租户吊销()
    {
        await using var host = await AccountsHost.Start(postgres);
        await StopProcessor(host); // 对账与旁路都由本用例亲手驱动，不与后台处理器抢跑
        var names = new[] { "active", "trial", "grace", "suspended", "pending", "gone" };
        var tenants = new Dictionary<string, TenantDescriptor>(); var sessions = new Dictionary<string, Guid>();
        foreach (var name in names)
        {
            var tenant = host.Tenants.Add(name);
            var (person, phone) = await CreatePerson(host, name);
            await Join(host, tenant, person.Id);
            sessions[name] = (await SignIn(host, host.NewBrowser(), phone, tenant.Id)).Session;
            tenants[name] = tenant;
        }
        host.Tenants.SetStatus(tenants["trial"].Id, TenantStatus.Trial);
        host.Tenants.SetStatus(tenants["grace"].Id, TenantStatus.Grace);
        host.Tenants.SetStatus(tenants["suspended"].Id, TenantStatus.Suspended);
        host.Tenants.SetStatus(tenants["pending"].Id, TenantStatus.Pending);
        host.Tenants.Remove(tenants["gone"].Id);

        Assert.Equal(4, await host.Service.ReconcileTenantsAsync(Ct));
        foreach (var name in names)
        {
            var reason = await host.Text("SELECT coalesce(revoke_reason, '') FROM plugin_qian_identity.sessions WHERE id = @s::uuid", ("s", Id(sessions[name])));
            Assert.Equal(name is "active" or "trial" ? "" : name == "gone" ? "tenant_missing" : "tenant_" + name, reason);
        }
        Assert.Equal(0, await host.Service.ReconcileTenantsAsync(Ct)); // 幂等：没有新的有效会话需要吊销
        Assert.Equal(4, await host.Service.ProcessRevocationsAsync(50, Ct));
        // 事件落在各自的租户里。
        var revoked = names.Where(name => name is not ("active" or "trial")).Select(name => tenants[name].Id).Order();
        Assert.Equal(revoked, (await EventsOf(host, "identity.session_revoked.v1")).Select(e => e.TenantId!.Value).Order());
    }
}
