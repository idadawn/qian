using System.Collections.Concurrent;
using System.Globalization;
using Npgsql;
using Qian.Identity.Abstractions;
using Qian.Testing.Containers;
using static Qian.Identity.Server.Tests.AccountSupport;

namespace Qian.Identity.Server.Tests;

// 目录修订行的加锁顺序（S1-IDP-010 第一轮复核缺陷 ①）：会递增修订行的入口都先取同一把租户目录锁，再锁业务行、递增修订行。
// 曾经账号入口的「重新启用」不取目录锁、先递增修订再更新成员行，与先锁成员行再递增修订的状态变更对同一成员并发时死锁（40P01）。
public sealed class DirectoryLockingTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string Describe(Exception error)
    {
        for (var current = error; current is not null; current = current.InnerException)
            if (current is PostgresException postgresError) return "PostgresException " + postgresError.SqlState;
        return error.GetType().Name;
    }

    // 每轮先把成员锁定，再同时发起三笔写入：账号入口的状态变更（锁定 → 启用）、账号入口的重新建立（= 重新启用）、
    // 目录的档案修改（目录写入，会改成员显示名）。修复前约一成的轮次死锁，重新启用几乎每轮都因读到旧行而乐观并发冲突；
    // 修复后三笔写入在目录锁上排队，每轮都成功。只给重新启用加成员行锁、不让它取目录锁时，它会与档案修改反向加锁而死锁。
    [Fact]
    public async Task 状态变更_重新启用与档案修改对同一成员并发_不死锁且每轮都成功()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("directory-lock-order");
        var (person, _) = await CreatePerson(host); var member = await Join(host, tenant, person.Id);
        var scopeId = tenant.Id.ToString("D");
        var failures = new ConcurrentQueue<string>();
        async Task Guarded(string name, Func<Task> action)
        {
            try { await action(); }
            catch (Exception error) when (error is not OperationCanceledException) { failures.Enqueue(name + " " + Describe(error)); }
        }
        for (var round = 0; round < 60; round++)
        {
            using (host.Tenant.BeginScope(scopeId)) await host.Accounts.SetMembershipStatusAsync(member.Id, AccountStatuses.Locked, "lock_order", Ct);
            var name = "成员" + (round % 2).ToString(CultureInfo.InvariantCulture);
            await Task.WhenAll(
                Task.Run(() => Guarded("状态变更", async () => { using var scope = host.Tenant.BeginScope(scopeId); await host.Accounts.SetMembershipStatusAsync(member.Id, AccountStatuses.Active, "lock_order", Ct); }), Ct),
                Task.Run(() => Guarded("重新启用", () => Join(host, tenant, person.Id)), Ct),
                Task.Run(() => Guarded("档案修改", async () => { using var scope = host.Tenant.BeginScope(scopeId); await host.Service.SetProfileAsync(member.Id, new(name), Ct); }), Ct));
        }
        var summary = string.Join("；", failures.GroupBy(text => text, StringComparer.Ordinal).Select(group => group.Key + " ×" + group.Count().ToString(CultureInfo.InvariantCulture)));
        Assert.False(failures.Any(text => text.Contains("40P01", StringComparison.Ordinal)), "出现死锁：" + summary);
        Assert.True(failures.IsEmpty, "并发写入失败：" + summary);
        Assert.Equal("active", await host.Text("SELECT status FROM plugin_qian_identity.memberships WHERE id=@id", ("id", member.Id)));
    }

    // 重新启用先锁成员行再读：另一笔不取目录锁、直接改成员行的写入（等同 BumpIdentityRevisionAsync）持有行锁时，重新启用等它提交，
    // 读到的是提交后的新行，照常成功。先读后锁时，重新启用读到旧行，保存时乐观并发冲突（DbUpdateConcurrencyException）。
    [Fact]
    public async Task 重新启用先锁成员行再读_不取目录锁的成员行写入提交后照常成功()
    {
        await using var host = await AccountsHost.Start(postgres); var tenant = host.Tenants.Add("directory-rejoin-row-lock");
        var (person, _) = await CreatePerson(host); var member = await Join(host, tenant, person.Id);
        using (host.Tenant.BeginScope(tenant.Id.ToString("D"))) await host.Accounts.SetMembershipStatusAsync(member.Id, AccountStatuses.Locked, "row_lock", Ct);
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString); await connection.OpenAsync(Ct);
        await using var transaction = await connection.BeginTransactionAsync(Ct);
        async Task<object?> Run(string sql, params (string Name, object Value)[] args)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            foreach (var (key, value) in args) command.Parameters.AddWithValue(key, value);
            return await command.ExecuteScalarAsync(Ct);
        }
        await Run("SELECT set_config('app.current_tenant_id', @tenant, true)", ("tenant", tenant.Id.ToString("D")));
        Assert.Equal(member.Id, await Run("UPDATE plugin_qian_identity.memberships SET identity_revision = identity_revision + 1 WHERE id = @id RETURNING id", ("id", member.Id)));
        var xid = Convert.ToString(await Run("SELECT pg_current_xact_id()::text"), CultureInfo.InvariantCulture)!;
        var rejoin = Task.Run(() => Join(host, tenant, person.Id), Ct);
        // 等重新启用确实停在这笔事务持有的行锁上（pg_locks 里出现等待这个事务号的请求），再提交。
        for (var attempt = 0; !rejoin.IsCompleted &&
            await host.Text("SELECT count(*)::text FROM pg_locks WHERE locktype = 'transactionid' AND transactionid::text = @xid AND NOT granted", ("xid", xid)) == "0"; attempt++)
        {
            Assert.True(attempt < 500, "重新启用没有停在这笔事务的行锁上");
            await Task.Delay(20, Ct);
        }
        await transaction.CommitAsync(Ct);
        Assert.Equal(AccountStatuses.Active, (await rejoin).Status);
        Assert.Equal("active", await host.Text("SELECT status FROM plugin_qian_identity.memberships WHERE id=@id", ("id", member.Id)));
    }
}
