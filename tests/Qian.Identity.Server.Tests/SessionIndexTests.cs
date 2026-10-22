using Microsoft.Extensions.Logging;
using Qian.Identity.Abstractions;
using Qian.Testing.Containers;
using StackExchange.Redis;
using static Qian.Identity.Server.Tests.AccountSupport;
using static Qian.Identity.Server.Tests.SessionSupport;

namespace Qian.Identity.Server.Tests;

// 会话中心的 Redis 索引（sessions:{person} 与吊销墓碑）：登录登记、吊销移出并写墓碑、索引缺失或被污染时以数据库为准修复。
// 索引只是派生数据——PostgreSQL 的 sessions 表是唯一事实来源，Redis 不可用时一切退回数据库。
public sealed class SessionIndexTests(PostgresFixture postgres, RedisFixture redis) : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    private static string[] Members(IDatabase db, Guid person) => [.. db.SortedSetRangeByRank(RedisSessionIndex.PersonKey(person)).Select(member => member.ToString()).Order(StringComparer.Ordinal)];

    private static string[] Keys(params Guid[] sessions) => [.. sessions.Select(session => session.ToString("N")).Order(StringComparer.Ordinal)];

    private static IdentityAccountOptions Capped(int sessions) => new() { Login = Policy(max: 100).Login, Session = new SessionPolicy { MaxConcurrentSessions = sessions } };

    private static async Task<(Guid Session, DateTimeOffset ExpiresAt)> Login(AccountsHost.Running host, string phone, Guid tenant, string client = "web")
    {
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var result = await host.SignIn.SignInAsync(Input(phone, Password, client, tenant), Net, Ct);
        Assert.Equal(SignInStatus.Authenticated, result.Status);
        return (result.Session!.SessionId, result.Session.ExpiresAt);
    }

    [Fact]
    public async Task 登录登记到索引_吊销后移出并写墓碑_墓碑活到会话自然到期_并发上限踢掉的会话同样处理()
    {
        await using var host = await AccountsHost.Start(postgres, Capped(2), redis: redis.ConnectionString);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        var db = host.Redis!.GetDatabase();
        var (first, firstExpires) = await Login(host, phone, tenant.Id);
        var (second, _) = await Login(host, phone, tenant.Id, "mobile");

        // 登录登记：成员是 N 格式的会话标识，分值是会话绝对到期的 Unix 秒，键的寿命覆盖最晚到期的会话。
        Assert.Equal(Keys(first, second), Members(db, person.Id));
        Assert.Equal(firstExpires.ToUnixTimeSeconds(), (long)db.SortedSetScore(RedisSessionIndex.PersonKey(person.Id), first.ToString("N"))!.Value);
        Assert.InRange(db.KeyTimeToLive(RedisSessionIndex.PersonKey(person.Id))!.Value, TimeSpan.FromDays(29), TimeSpan.FromDays(31));
        Assert.False(db.KeyExists(RedisSessionIndex.TombstoneKey(first)));

        // 第三次登录超过上限 2：最早的会话被踢；旁路处理后它移出索引并留下墓碑，墓碑活到该会话本来的到期时刻。
        var (third, _) = await Login(host, phone, tenant.Id);
        await Eventually(() => Task.FromResult(db.KeyExists(RedisSessionIndex.TombstoneKey(first))), Settle, "被踢会话的墓碑");
        Assert.Equal(Keys(second, third), Members(db, person.Id));
        Assert.InRange(db.KeyTimeToLive(RedisSessionIndex.TombstoneKey(first))!.Value, TimeSpan.FromDays(29), TimeSpan.FromDays(31));
        Assert.False(db.KeyExists(RedisSessionIndex.TombstoneKey(second)));
        Assert.Null(await host.Service.ValidateAsync(first, person.Id, member.Id, Ct));
        Assert.NotNull(await host.Service.ValidateAsync(third, person.Id, member.Id, Ct));

        // 管理侧吊销同样走这一条路径。
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, second, "admin_revoke", "test"), Ct);
        await Eventually(() => Task.FromResult(db.KeyExists(RedisSessionIndex.TombstoneKey(second))), Settle, "管理员吊销会话的墓碑");
        Assert.Equal(Keys(third), Members(db, person.Id));
    }

    [Fact]
    public async Task 墓碑先于数据库被检查_命中即失效_没有墓碑不代表有效_仍以数据库为准()
    {
        await using var host = await AccountsHost.Start(postgres, redis: redis.ConnectionString);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        var db = host.Redis!.GetDatabase();
        var (session, _) = await Login(host, phone, tenant.Id);
        Assert.NotNull(await host.Service.ValidateAsync(session, person.Id, member.Id, Ct));

        // 只在 PostgreSQL 里吊销（模拟旁路尚未写墓碑）：校验仍然失效——索引从不放行任何会话。
        await host.Exec("UPDATE plugin_qian_identity.sessions SET revoked_at = now(), revoke_reason = 'manual', revocation_processed_at = now() WHERE id = @s::uuid", ("s", Id(session)));
        Assert.False(db.KeyExists(RedisSessionIndex.TombstoneKey(session)));
        Assert.Null(await host.Service.ValidateAsync(session, person.Id, member.Id, Ct));

        // 墓碑命中：不用查库就判为失效（墓碑只在库里吊销提交之后才写，所以「有墓碑 ⇒ 已吊销」恒成立）。
        var (live, _) = await Login(host, phone, tenant.Id);
        Assert.NotNull(await host.Service.ValidateAsync(live, person.Id, member.Id, Ct));
        db.StringSet(RedisSessionIndex.TombstoneKey(live), "1", TimeSpan.FromMinutes(1));
        Assert.Null(await host.Service.ValidateAsync(live, person.Id, member.Id, Ct));
        db.KeyDelete(RedisSessionIndex.TombstoneKey(live));
        Assert.NotNull(await host.Service.ValidateAsync(live, person.Id, member.Id, Ct));
    }

    [Fact]
    public async Task 索引被清空或被污染时列表以数据库为准并顺带修复()
    {
        await using var host = await AccountsHost.Start(postgres, redis: redis.ConnectionString);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        await Join(host, tenant, person.Id);
        var db = host.Redis!.GetDatabase();
        var key = RedisSessionIndex.PersonKey(person.Id);
        var (first, _) = await Login(host, phone, tenant.Id); var (second, _) = await Login(host, phone, tenant.Id, "mobile");

        // 索引丢了（Redis 重启、被驱逐）：列表照样给出数据库里的有效会话，并把索引补回来。
        Assert.True(db.KeyDelete(key));
        Assert.Equal(Keys(first, second), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id.ToString("N")).Order(StringComparer.Ordinal));
        Assert.Equal(Keys(first, second), Members(db, person.Id));

        // 索引里多出库里不存在的会话（脏数据）：不会出现在列表里，修复时被去掉；已过期的成员不算有效。
        var ghost = Guid.NewGuid();
        db.SortedSetAdd(key, ghost.ToString("N"), host.Clock.GetUtcNow().AddDays(1).ToUnixTimeSeconds());
        db.SortedSetAdd(key, Guid.NewGuid().ToString("N"), host.Clock.GetUtcNow().AddDays(-1).ToUnixTimeSeconds());
        Assert.Equal(Keys(first, second), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id.ToString("N")).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(ghost.ToString("N"), Members(db, person.Id));
        Assert.Equal(3, Members(db, person.Id).Length); // 两个真会话加那个早已过期、不影响判断的成员
    }
}

// Redis 不可用时的降级：登录、吊销、列表、旁路与事件全部照常，只是少了索引与墓碑这层加速；降级告警限频，且不带会话标识。
public sealed class SessionIndexOutageTests(PostgresFixture postgres, RedisFixture redis) : IClassFixture<PostgresFixture>, IClassFixture<RedisFixture>
{
    [Fact]
    public async Task Redis从一开始就连不上_登录吊销列表与事件照常_只有限频的降级告警()
    {
        await using var host = await AccountsHost.Start(postgres, redis: "127.0.0.1:1");
        await Exercise(host);
    }

    [Fact]
    public async Task Redis运行中途宕机_已有会话照常校验_新登录与吊销照常()
    {
        await using var host = await AccountsHost.Start(postgres, redis: redis.ConnectionString);
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var before = (await host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct)).Session!;
        Assert.True(host.Redis!.GetDatabase().KeyExists(RedisSessionIndex.PersonKey(person.Id)));

        await redis.Container.StopAsync(Ct);
        Assert.NotNull(await host.Service.ValidateAsync(before.SessionId, person.Id, member.Id, Ct));
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var after = (await host.SignIn.SignInAsync(Input(phone, Password, "mobile", tenant.Id), Net, Ct)).Session!;
        Assert.Equal(new[] { before.SessionId, after.SessionId }.Order(), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id).Order());
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, before.SessionId, "admin_revoke", "test"), Ct);
        Assert.Null(await host.Service.ValidateAsync(before.SessionId, person.Id, member.Id, Ct)); // 没有墓碑，数据库照样拒绝
        await Eventually(async () => await Revoked(host, before.SessionId) is not null, Settle, "Redis 宕机时的 session_revoked 事件");
        Assert.Contains(host.Logs.Entries, entry => entry.Level == LogLevel.Warning && entry.Message.Contains("会话索引（Redis）不可用", StringComparison.Ordinal));
    }

    private static async Task Exercise(AccountsHost.Running host)
    {
        var tenant = host.Tenants.Add("acme");
        var (person, phone) = await CreatePerson(host);
        var member = await Join(host, tenant, person.Id);
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var first = (await host.SignIn.SignInAsync(Input(phone, Password, "web", tenant.Id), Net, Ct)).Session!;
        host.Clock.Advance(TimeSpan.FromSeconds(1));
        var second = (await host.SignIn.SignInAsync(Input(phone, Password, "mobile", tenant.Id), Net, Ct)).Session!;
        Assert.Equal(new[] { first.SessionId, second.SessionId }.Order(), (await host.Service.ListSessionsAsync(person.Id, Ct)).Select(s => s.Id).Order());
        Assert.NotNull(await host.Service.ValidateAsync(first.SessionId, person.Id, member.Id, Ct));
        await host.Service.RevokeSessionsAsync(new SessionRevocation(person.Id, first.SessionId, "admin_revoke", "test"), Ct);
        Assert.Null(await host.Service.ValidateAsync(first.SessionId, person.Id, member.Id, Ct));
        await Eventually(async () => await Revoked(host, first.SessionId) is not null, Settle, "没有 Redis 时的 session_revoked 事件");
        Assert.Equal("0", await host.Text("SELECT count(*) FROM plugin_qian_identity.sessions WHERE revoked_at IS NOT NULL AND revocation_processed_at IS NULL"));

        // 降级告警限频（熔断 5 秒）：不会每个请求一条；日志里没有会话标识。
        var warnings = host.Logs.Entries.Where(entry => entry.Message.Contains("会话索引（Redis）不可用", StringComparison.Ordinal)).ToArray();
        Assert.InRange(warnings.Length, 1, 4);
        Assert.All(host.Logs.Entries, entry =>
        {
            Assert.DoesNotContain(first.SessionId.ToString(), entry.Message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(first.SessionId.ToString("N"), entry.Message, StringComparison.OrdinalIgnoreCase);
        });
    }
}
