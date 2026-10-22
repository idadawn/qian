using Qian.Identity.Abstractions;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;
using static Qian.Identity.Server.Tests.SessionSupport;

namespace Qian.Identity.Server.Tests;

// 卸载与后台工作的竞争（S1-IDP-004 实现时发现的缺陷）：组件卸载紧接着会释放 Data 的会话，
// 若吊销旁路或租户对账还握着数据会话，并发释放会让正在执行的命令撞上 Npgsql 的「连接忙」，甚至让卸载一直挂住。
// 现在后台工作互斥运行，卸载时先排空（CloseAsync），关闭之后新来的后台工作直接返回。
public sealed class SessionShutdownTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 关闭之后后台工作直接返回_待处理的吊销留给下一个副本_不碰数据会话()
    {
        await using var host = await AccountsHost.Start(postgres);
        await StopProcessor(host);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        await host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct);
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, null, "admin_revoke_all", "test"), Ct);
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL"));

        await host.Service.CloseAsync();
        Assert.Equal(0, await host.Service.ProcessRevocationsAsync(50, Ct));
        Assert.Equal(0, await host.Service.ReconcileTenantsAsync(Ct));
        Assert.Equal("1", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL")); // 没有被处理，等别的副本接手
    }

    [Fact]
    public async Task 吊销积压时宿主停机不挂起_多轮()
    {
        // 缺陷是竞争型的：登录积压触发大量旁路时立刻停机才会撞上（修复前能复现卸载卡住），所以多跑几轮，每轮一个新宿主。
        for (var round = 0; round < 6; round++)
        {
            var host = await AccountsHost.Start(postgres, Policy(max: 100));
            var tenant = host.Tenants.Add("acme");
            var (person, phone) = await CreatePerson(host);
            await Join(host, tenant, person.Id);
            var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct).AsTask()));
            Assert.All(results, result => Assert.True(result.Status == SignInStatus.Authenticated, result.Error?.Code.Code));
            await host.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(60), Ct);
        }
    }
}
