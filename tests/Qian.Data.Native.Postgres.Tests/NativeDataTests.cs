using System.Data;
using System.Data.Common;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;
using Qian.Testing.NativeData;

namespace Qian.Data.Native.Postgres.Tests;

public sealed class NativeDataTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string A = "01900000-0000-7000-8000-000000000001";
    private const string B = "01900000-0000-7000-8000-000000000002";
    private static readonly string[] TenantBindings = ["tenant"];

    [Fact]
    public async Task 受限登录与原始SQL两租户隔离_保存连接后切租户仍拒绝()
    {
        await using var host = await Setup();
        object? backend = null;
        await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenTenantAsync("tenant", Ct).AsTask());
        using (host.Tenant.BeginScope(A))
        {
            await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenTenantAsync("unregistered", Ct).AsTask());
            using (var ambient = new System.Transactions.TransactionScope(System.Transactions.TransactionScopeAsyncFlowOption.Enabled))
                await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenTenantAsync("tenant", Ct).AsTask());
            await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
            backend = await Scalar(session.Connection, "SELECT pg_backend_pid()");
            Assert.Equal(A, await Scalar(session.Connection, "SELECT current_setting('app.current_tenant_id')"));
            await Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{A}', 'a')");
            Assert.Equal(1L, await Scalar(session.Connection, $"SELECT count(*) FROM {host.Table}"));
            await using var command = session.Connection.CreateCommand(); command.CommandText = $"SELECT count(*) FROM {host.Table}";
            using (host.Tenant.BeginScope(B))
                await Assert.ThrowsAsync<QianErrorException>(() => command.ExecuteScalarAsync(Ct));
        }
        using (host.Tenant.BeginScope(B))
        {
            await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
            Assert.Equal(backend, await Scalar(session.Connection, "SELECT pg_backend_pid()"));
            Assert.Equal(0L, await Scalar(session.Connection, $"SELECT count(*) FROM {host.Table}"));
            await Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{B}', 'b')");
            Assert.Equal(0, await Execute(session.Connection, $"UPDATE {host.Table} SET value='cross' WHERE tenant_id='{A}'"));
        }
        using (host.Tenant.BeginScope(A))
        {
            await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
            Assert.Equal("a", await Scalar(session.Connection, $"SELECT value FROM {host.Table}"));
            await Assert.ThrowsAsync<PostgresException>(() => Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{B}','wrong')"));
        }
        using (host.Tenant.BeginScope(A))
        {
            await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
            var denied = await Assert.ThrowsAsync<PostgresException>(() => Execute(session.Connection, $"SET ROLE {host.PlatformRole}"));
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
        }
    }

    [Fact]
    public async Task 事务本地GUC_提交与释放回滚_取消和advisory锁不污染连接池()
    {
        await using var host = await Setup();
        using (host.Tenant.BeginScope(A))
        {
            await using (var session = await host.Data.OpenTenantAsync("tenant", Ct))
            {
                await Execute(session.Connection, "SELECT pg_advisory_lock(836492)");
                await session.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
                Assert.Equal(A, await Scalar(session.Connection, "SELECT current_setting('app.current_tenant_id')"));
                await Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{A}','rollback')");
            }
            await using (var session = await host.Data.OpenTenantAsync("tenant", Ct))
            {
                Assert.Equal(0L, await Scalar(session.Connection, $"SELECT count(*) FROM {host.Table}"));
                await session.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
                await Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{A}','commit')");
                await session.CommitAsync(Ct);
            }
            await using (var session = await host.Data.OpenTenantAsync("tenant", Ct))
            {
                await session.BeginTransactionAsync(IsolationLevel.ReadCommitted, Ct);
                await Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{A}','cancel')");
                using var canceled = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));
                await using var command = session.Connection.CreateCommand(); command.CommandText = "SELECT pg_sleep(5)";
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command.ExecuteNonQueryAsync(canceled.Token));
            }
        }
        await using (var check = new NpgsqlConnection(postgres.ConnectionString))
        {
            await check.OpenAsync(Ct); Assert.Equal(true, await Scalar(check, "SELECT pg_try_advisory_lock(836492)"));
            await Execute(check, "SELECT pg_advisory_unlock_all()");
        }
        using (host.Tenant.BeginScope(B))
        {
            await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
            Assert.Equal(B, await Scalar(session.Connection, "SELECT current_setting('app.current_tenant_id')"));
            Assert.Equal(0L, await Scalar(session.Connection, $"SELECT count(*) FROM {host.Table}"));
        }
        using (host.Tenant.BeginScope(A))
        {
            await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
            Assert.Equal("commit", await Scalar(session.Connection, $"SELECT value FROM {host.Table}"));
        }
    }

    [Fact]
    public async Task 平台必须真实global调用上下文_租户entry的ScopedGlobal与卸载上下文拒绝()
    {
        await using var host = await Setup();
        await using (var platform = await host.Data.OpenPlatformAsync("platform", host.Global, Ct))
        {
            Assert.Null(platform.TenantId); Assert.Equal("", await Scalar(platform.Connection, "SELECT current_setting('app.current_tenant_id')"));
            await Execute(platform.Connection, $"INSERT INTO {host.Table} VALUES (NULL,'platform')");
        }
        using (host.Tenant.BeginScope(A))
        {
            await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenPlatformAsync("platform", host.Global, Ct).AsTask());
            await using var tenant = await host.Data.OpenTenantAsync("tenant", Ct);
            Assert.Equal(0L, await Scalar(tenant.Connection, $"SELECT count(*) FROM {host.Table}"));
        }
        var tenantContext = host.Loader.GetContext("tenant-consumer", new ScopeKey("tenant", A));
        Assert.Equal(ScopeKey.Global, tenantContext.Scoped(ScopeKey.Global).Scope);
        await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenPlatformAsync("platform", tenantContext.Scoped(ScopeKey.Global), Ct).AsTask());
        await host.Loader.MountAsync([new(new PluginEntry("invalid-owner", "qian.data.native", "1.0.0", new ScopeKey("tenant", A), 0,
            JsonSerializer.SerializeToElement(new { connections = TenantBindings })), new NativeDataComponent(), [NativeDataDefinition.Key])], Ct);
        Assert.Equal(PluginState.Failed, host.Loader.GetState("invalid-owner", new ScopeKey("tenant", A)));
        await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenPlatformAsync("tenant", host.Global, Ct).AsTask());
        await host.Loader.UnmountAsync("consumer", ScopeKey.Global, Ct);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => host.Data.OpenPlatformAsync("platform", host.Global, Ct).AsTask());
    }

    [Fact]
    public async Task 卸载撤销服务并关闭活动会话_未提交写入回滚()
    {
        await using var host = await Setup(); using var tenant = host.Tenant.BeginScope(A);
        var data = host.Data; await using var session = await data.OpenTenantAsync("tenant", Ct);
        var connection = session.Connection; await session.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
        await Execute(connection, $"INSERT INTO {host.Table} VALUES ('{A}','unloaded')");
        await host.Loader.UnmountAsync("native", ScopeKey.Global, Ct);
        Assert.Null(host.Global.TryInject<INativeDataService>(NativeDataDefinition.Key));
        Assert.Equal(ConnectionState.Closed, connection.State);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => data.OpenTenantAsync("tenant", Ct).AsTask());
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        Assert.Equal(0L, await Scalar(admin, $"SELECT count(*) FROM {host.Table}"));
    }

    [Theory]
    [InlineData("force")]
    [InlineData("policy")]
    [InlineData("true-policy")]
    [InlineData("superuser")]
    [InlineData("bypass")]
    [InlineData("membership")]
    [InlineData("truncate")]
    public async Task 安全装配漂移拒绝而非静默放行(string violation)
    {
        await using var host = await Setup();
        var sql = violation switch
        {
            "force" => $"ALTER TABLE {host.Table} NO FORCE ROW LEVEL SECURITY",
            "policy" => $"DROP POLICY native_guard ON {host.Table}",
            "true-policy" => $"ALTER POLICY native_guard ON {host.Table} USING (true) WITH CHECK (true)",
            "superuser" => $"ALTER ROLE {host.TenantRole} SUPERUSER",
            "bypass" => $"ALTER ROLE {host.TenantRole} BYPASSRLS",
            "membership" => $"GRANT {host.PlatformRole} TO {host.TenantRole}",
            "truncate" => $"GRANT TRUNCATE ON {host.Table} TO {host.TenantRole}",
            _ => throw new ArgumentException("未知反例。", nameof(violation))
        };
        await Admin(sql); using var tenant = host.Tenant.BeginScope(A);
        await Assert.ThrowsAsync<QianErrorException>(() => host.Data.OpenTenantAsync("tenant", Ct).AsTask());
        await host.Loader.UnmountAsync("native", ScopeKey.Global, Ct);
        await host.MountNative(); Assert.Equal(PluginState.Failed, host.Loader.GetState("native", ScopeKey.Global));
    }

    [Fact]
    public async Task 真实外部事务同库同角色同租户与Serializable校验_不接管提交回滚()
    {
        await using var host = await Setup(); using var tenant = host.Tenant.BeginScope(A);
        await using var external = new NpgsqlConnection(host.TenantConnection); await external.OpenAsync(Ct);
        await Execute(external, $"SELECT set_config('app.current_tenant_id','{A}',false)");
        await using (var transaction = await external.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
        {
            await host.Data.ValidateTenantTransactionAsync("tenant", transaction, Ct);
            await Execute(external, $"INSERT INTO {host.Table} VALUES ('{A}','external')");
            await host.Data.ValidateTenantTransactionAsync("tenant", transaction, Ct);
            Assert.NotNull(transaction.Connection); await transaction.RollbackAsync(Ct);
        }
        Assert.Equal(0L, await Scalar(external, $"SELECT count(*) FROM {host.Table}"));
        await using (var weak = await external.BeginTransactionAsync(IsolationLevel.ReadCommitted, Ct))
            await Assert.ThrowsAsync<QianErrorException>(() => host.Data.ValidateTenantTransactionAsync("tenant", weak, Ct).AsTask());
        await using (var transaction = await external.BeginTransactionAsync(IsolationLevel.Serializable, Ct))
        {
            await Execute(external, $"SELECT set_config('app.current_tenant_id','{B}',true)");
            await Assert.ThrowsAsync<QianErrorException>(() => host.Data.ValidateTenantTransactionAsync("tenant", transaction, Ct).AsTask());
        }
        await using (var wrongRole = new NpgsqlConnection(postgres.ConnectionString))
        {
            await wrongRole.OpenAsync(Ct); await using var transaction = await wrongRole.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
            await Assert.ThrowsAsync<QianErrorException>(() => host.Data.ValidateTenantTransactionAsync("tenant", transaction, Ct).AsTask());
        }
        await using var managed = await host.Data.OpenTenantAsync("tenant", Ct);
        var owned = await managed.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
        await host.Data.ValidateTenantTransactionAsync("tenant", owned, Ct); Assert.NotNull(owned.Connection);
        await managed.RollbackAsync(Ct);
    }

    [Fact]
    public async Task 提交开始前已取消必须封存会话_回滚后仍不能继续使用()
    {
        await using var host = await Setup(); using var tenant = host.Tenant.BeginScope(A);
        await using var session = await host.Data.OpenTenantAsync("tenant", Ct);
        await session.BeginTransactionAsync(IsolationLevel.Serializable, Ct);
        await Execute(session.Connection, $"INSERT INTO {host.Table} VALUES ('{A}','canceled-commit')");
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.CommitAsync(canceled.Token).AsTask());
        await Assert.ThrowsAsync<QianErrorException>(() => session.ValidateAsync(Ct).AsTask());
        await session.RollbackAsync(Ct);
        await Assert.ThrowsAsync<QianErrorException>(() => session.ValidateAsync(Ct).AsTask());
    }

    [Fact]
    public async Task 打开连接与卸载并发_等待交接与清理结束后没有孤立数据库会话()
    {
        await using var host = await Setup();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            using var tenant = host.Tenant.BeginScope(A); var data = host.Data;
            var first = await data.OpenTenantAsync("tenant", Ct);
            var opens = Enumerable.Range(0, 12).Select(_ => Task.Run(async () =>
            {
                try { await using var session = await data.OpenTenantAsync("tenant", Ct); }
                catch (Exception error) when (error is ObjectDisposedException or OperationCanceledException or QianErrorException) { }
            }, Ct)).ToArray();
            await Task.Yield();
            var release = first.DisposeAsync().AsTask();
            var unmount = host.Loader.UnmountAsync("native", ScopeKey.Global, Ct).AsTask();
            await Task.WhenAll(opens.Append(release).Append(unmount));
            await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
            var sql = $"SELECT count(*) FROM pg_stat_activity WHERE usename IN ('{host.TenantRole}','{host.PlatformRole}')";
            var deadline = DateTime.UtcNow.AddSeconds(2); long count;
            do { count = (long)(await Scalar(admin, sql))!; if (count != 0) await Task.Delay(10, Ct); } while (count != 0 && DateTime.UtcNow < deadline);
            if (count != 0)
            {
                var detail = await Scalar(admin, $"SELECT string_agg(usename || ':' || state || ':' || COALESCE(wait_event,'') || ':' || query, E'\\n') FROM pg_stat_activity WHERE usename IN ('{host.TenantRole}','{host.PlatformRole}')");
                Assert.Fail("卸载后仍有数据库会话；iteration=" + iteration + "；" + detail);
            }
            await host.MountNative(); Assert.Equal(PluginState.Ready, host.Loader.GetState("native", ScopeKey.Global));
        }
    }

    private async Task<Running> Setup()
    {
        var database = await NativeDataDatabase.Create(postgres, Ct);
        var schema = database.Schema; var tenantRole = database.TenantRole; var platformRole = database.PlatformRole;
        var connection = database.TenantConnection; var platform = database.PlatformConnection; var predicate = database.Predicate;
        var services = new ServiceCollection(); services.AddLogging(); var context = new AsyncLocalTenantContext();
        services.AddSingleton<ITenantContext>(context); services.AddSingleton(ErrorCatalog.CreateDefault());
        var tables = new[] { new NativeDataTable(schema, "items", "native_guard", predicate, predicate) };
        services.AddQianNativeData(new("tenant", tenantRole, NativeDataAccess.Tenant, _ => ValueTask.FromResult(connection), tables));
        services.AddQianNativeData(new("platform", platformRole, NativeDataAccess.Platform, _ => ValueTask.FromResult(platform), tables));
        var provider = services.BuildServiceProvider(); var loader = new PluginLoader(provider);
        var running = new Running(provider, loader, context, schema + ".items", tenantRole, platformRole, connection);
        await running.MountNative();
        Assert.True(loader.GetState("native", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("native", ScopeKey.Global)?.ToString());
        await loader.MountAsync([
            new(new PluginEntry("consumer", "test.consumer", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new Consumer(), []),
            new(new PluginEntry("tenant-consumer", "test.consumer", "1.0.0", new ScopeKey("tenant", A), 0, JsonSerializer.SerializeToElement(new { })), new Consumer(), [])], Ct);
        running.Global = loader.GetContext("consumer", ScopeKey.Global);
        return running;
    }
    private async Task Admin(string sql)
    { await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct); await Execute(connection, sql); }
    private static async Task<object?> Scalar(DbConnection connection, string sql)
    { await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(Ct); }
    private static async Task<int> Execute(DbConnection connection, string sql)
    { await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteNonQueryAsync(Ct); }

    private sealed class Consumer : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private sealed class Running(ServiceProvider services, PluginLoader loader, AsyncLocalTenantContext tenant, string table,
        string tenantRole, string platformRole, string tenantConnection) : IAsyncDisposable
    {
        private static readonly string[] Connections = ["tenant", "platform"];
        internal PluginLoader Loader => loader;
        internal AsyncLocalTenantContext Tenant => tenant;
        internal string Table => table;
        internal string TenantRole => tenantRole;
        internal string PlatformRole => platformRole;
        internal string TenantConnection => tenantConnection;
        internal IPluginContext Global { get; set; } = null!;
        internal INativeDataService Data => loader.GetContext("native", ScopeKey.Global).NativeData();
        internal Task MountNative() => loader.MountAsync([new(new PluginEntry("native", "qian.data.native", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { connections = Connections })), new NativeDataComponent(), [NativeDataDefinition.Key])], Ct).AsTask();
        public async ValueTask DisposeAsync() { await loader.DisposeAsync(); await services.DisposeAsync(); }
    }
}
