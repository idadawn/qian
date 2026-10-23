using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private readonly ConcurrentQueue<string> cleanupDiagnostics = new();
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
            var openDiagnostics = new ConcurrentQueue<Exception>();
            async Task Open()
            {
                try { await using var session = await data.OpenTenantAsync("tenant", Ct); }
                catch (Exception error) when (error is ObjectDisposedException or OperationCanceledException or QianErrorException)
                { openDiagnostics.Enqueue(error); }
            }
            var opens = Enumerable.Range(0, 12).Select(_ => Task.Run(Open, Ct)).ToArray();
            await Task.Yield();
            var release = first.DisposeAsync().AsTask();
            var unmount = host.Loader.UnmountAsync("native", ScopeKey.Global, Ct).AsTask();
            await Task.WhenAll(opens.Append(release).Append(unmount));
            var observed = Stopwatch.StartNew();
            await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
            var sql = $"SELECT count(*) FROM pg_stat_activity WHERE usename IN ('{host.TenantRole}','{host.PlatformRole}')";
            var deadline = DateTime.UtcNow.AddSeconds(2); long count;
            do { count = (long)(await Scalar(admin, sql))!; if (count != 0) await Task.Delay(10, Ct); } while (count != 0 && DateTime.UtcNow < deadline);
            if (count != 0)
            {
                var detail = await Scalar(admin, $"SELECT string_agg(usename || ':' || state || ':' || COALESCE(wait_event,'') || ':' || query, E'\\n') FROM pg_stat_activity WHERE usename IN ('{host.TenantRole}','{host.PlatformRole}')");
                var firstElapsed = observed.ElapsedMilliseconds;
                var diagnosticSql = $"SELECT string_agg(pid::text || ':' || state || ':' || COALESCE(wait_event,'') || ':backend=' || backend_start::text || ':state=' || state_change::text || ':xact=' || COALESCE(xact_start::text,'') || ':' || query, E'\\n') FROM pg_stat_activity WHERE usename IN ('{host.TenantRole}','{host.PlatformRole}')";
                var clocks = await Scalar(admin, "SELECT 'observer=' || pg_backend_pid()::text || ':transaction=' || transaction_timestamp()::text || ':clock=' || clock_timestamp()::text");
                var originalDetail = await Scalar(admin, diagnosticSql);
                await using var fresh = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Pooling = false, Enlist = false, ApplicationName = "native-unload-observer" }.ConnectionString);
                await fresh.OpenAsync(Ct);
                var freshBefore = await Scalar(fresh, diagnosticSql);
                Assert.Fail("卸载后仍有数据库会话；iteration=" + iteration + "；" + detail + "；firstElapsedMs=" + firstElapsed + "；clocks=" + clocks + "；original=" + originalDetail + "；freshBefore=" + freshBefore + "；cleanupLogs=" + string.Join("\n", cleanupDiagnostics) + "；openLogs=" + string.Join("\n", openDiagnostics.Select(error => error.GetType().FullName + "\n" + error.StackTrace)) + "；poolSnapshots=" + PoolSnapshot(data));
            }
            Assert.Empty(cleanupDiagnostics);
            await host.MountNative(); Assert.True(host.Loader.GetState("native", ScopeKey.Global) == PluginState.Ready, host.Loader.GetFailure("native", ScopeKey.Global)?.ToString());
        }
    }

    [Fact]
    public async Task 池等待在取消注册前取消_卸载不遗留连接()
    {
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        static object Field(object value, string name)
        {
            for (var type = value.GetType(); type is not null; type = type.BaseType)
                if (type.GetField(name, flags) is { } field) return field.GetValue(value)!;
            throw new InvalidOperationException("Missing diagnostic field: " + name);
        }
        await using var host = await Setup(); using var tenant = host.Tenant.BeginScope(A);
        var data = host.Data;
        var first = await data.OpenTenantAsync("tenant", Ct);
        var bindings = (System.Collections.IDictionary)Field(data, "_bindings");
        var binding = bindings["tenant"]!;
        var source = (NpgsqlDataSource)binding.GetType().GetProperty("Source")!.GetValue(binding)!;
        var reader = Field(source, "_idleConnectorReader");
        var channel = Field(reader, "_parent");
        var sync = Field(channel, "_items");
        // 本用例固定 .NET 10.0.13 / Npgsql 10.0.3 已证实的交错（dotnet/runtime#132230）。
        // 仅操作此用例拥有的池；升级依赖后若私有结构变化，应复核重现器，不能跳过回归。
        var callbackProperty = channel.GetType().GetProperty("CancellationCallbackDelegate", flags)
            ?? throw new InvalidOperationException("Channels 私有结构变化，请复核 DATA-009 取消注册回归。");
        var originalCallback = callbackProperty.GetValue(channel);
        var callbackField = channel.GetType().GetFields(flags).Single(field => field.Name.Contains("CancellationCallbackDelegate", StringComparison.Ordinal));
        var canceledCallbacks = 0;
        // 取消回调的节点移除在线程池中异步执行；将它延后至本次归还之后，固定合法调度顺序。
        Action<object?, CancellationToken> deferredRemoval = (operation, token) =>
        {
            Interlocked.Increment(ref canceledCallbacks);
            operation!.GetType().GetMethod("TrySetCanceled")!.Invoke(operation, [token]);
        };
        callbackField.SetValue(channel, deferredRemoval);
        using var cancellation = new CancellationTokenSource();
        using var threadStarted = new ManualResetEventSlim();
        Thread? openingThread = null;
        Task<Task> starting;
        var blockedInChannel = false;
        try
        {
            lock (sync)
            {
                starting = Task.Factory.StartNew(async () =>
                {
                    openingThread = Thread.CurrentThread;
                    threadStarted.Set();
                    await using var session = await data.OpenTenantAsync("tenant", cancellation.Token);
                }, Ct, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Assert.True(threadStarted.Wait(TimeSpan.FromSeconds(5), Ct), "打开线程未启动");
                Assert.True(SpinWait.SpinUntil(() => starting.IsCompleted || (openingThread!.ThreadState & System.Threading.ThreadState.WaitSleepJoin) != 0, TimeSpan.FromSeconds(5)), "打开线程未到达等待边界");
                blockedInChannel = !starting.IsCompleted;
                cancellation.Cancel();
            }
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => starting.Unwrap());
            Console.WriteLine("FIXED_BEFORE_RETURN blockedInChannel=" + blockedInChannel + ":callbacks=" + canceledCallbacks + ":" + PoolSnapshot(data));
            await first.DisposeAsync();
            Console.WriteLine("FIXED_AFTER_RETURN " + PoolSnapshot(data));
            await host.Loader.UnmountAsync("native", ScopeKey.Global, Ct);
            await using var observer = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Pooling = false, Enlist = false }.ConnectionString);
            await observer.OpenAsync(Ct);
            var sql = $"SELECT count(*) FROM pg_stat_activity WHERE usename IN ('{host.TenantRole}','{host.PlatformRole}')";
            var deadline = DateTime.UtcNow.AddSeconds(2); long count;
            do { count = (long)(await Scalar(observer, sql))!; if (count != 0) await Task.Delay(10, Ct); } while (count != 0 && DateTime.UtcNow < deadline);
            Assert.True(count == 0, "固定交错残留；blocked=" + blockedInChannel + ":callbacks=" + canceledCallbacks + ":" + PoolSnapshot(data));
            Assert.Equal(0, canceledCallbacks);
            Assert.Empty(cleanupDiagnostics);
        }
        finally
        {
            callbackField.SetValue(channel, originalCallback);
            await first.DisposeAsync();
        }
    }

    [Fact]
    public async Task 等待容量时调用者取消_重复释放不超发名额且不同绑定独立()
    {
        await using var host = await Setup();
        INativeDataSession first;
        Task<INativeDataSession> waiting;
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        using (host.Tenant.BeginScope(A))
        {
            first = await host.Data.OpenTenantAsync("tenant", Ct);
            waiting = host.Data.OpenTenantAsync("tenant", canceled.Token).AsTask();
            Assert.False(waiting.IsCompleted);
        }
        // 租户池已满不占用另一个绑定的名额。
        await using (var platform = await host.Data.OpenPlatformAsync("platform", host.Global, Ct))
            Assert.Equal(1, await Scalar(platform.Connection, "SELECT 1"));
        await canceled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5), Ct));
        await Task.WhenAll(first.DisposeAsync().AsTask(), first.DisposeAsync().AsTask());
        using (host.Tenant.BeginScope(A))
        {
            await using var next = await host.Data.OpenTenantAsync("tenant", Ct);
            using var secondCancel = CancellationTokenSource.CreateLinkedTokenSource(Ct);
            var stillLimited = host.Data.OpenTenantAsync("tenant", secondCancel.Token).AsTask();
            Assert.False(stillLimited.IsCompleted); // 重复 Dispose 不得释放第二份名额。
            await secondCancel.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stillLimited);
            Assert.Equal(1, await Scalar(next.Connection, "SELECT 1"));
        }
        Assert.Empty(cleanupDiagnostics);
    }

    [Fact]
    public async Task 卸载取消容量等待并允许重挂载_不清理其他数据源的连接池()
    {
        await using var host = await Setup(timeoutSeconds: 0);
        await using var otherSource = new NpgsqlSlimDataSourceBuilder(host.TenantConnection).Build();
        var other = await otherSource.OpenConnectionAsync(Ct);
        var otherPid = await Scalar(other, "SELECT pg_backend_pid()");
        using (host.Tenant.BeginScope(A))
        {
            var first = await host.Data.OpenTenantAsync("tenant", Ct);
            var waiting = host.Data.OpenTenantAsync("tenant", Ct).AsTask();
            Assert.False(waiting.IsCompleted);
            await host.Loader.UnmountAsync("native", ScopeKey.Global, Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            await first.DisposeAsync();
        }
        Assert.Empty(cleanupDiagnostics);
        Assert.Equal(otherPid, await Scalar(other, "SELECT pg_backend_pid()"));
        await other.DisposeAsync();
        await using var reusedOther = await otherSource.OpenConnectionAsync(Ct);
        Assert.Equal(otherPid, await Scalar(reusedOther, "SELECT pg_backend_pid()"));
        await host.MountNative();
        Assert.True(host.Loader.GetState("native", ScopeKey.Global) == PluginState.Ready, host.Loader.GetFailure("native", ScopeKey.Global)?.ToString());
        using (host.Tenant.BeginScope(B))
        {
            await using var reopened = await host.Data.OpenTenantAsync("tenant", Ct);
            Assert.Equal(B, await Scalar(reopened.Connection, "SELECT current_setting('app.current_tenant_id')"));
        }
        Assert.Empty(cleanupDiagnostics);
    }

    [Fact]
    public async Task 打开按预算超时后_卸载仍等待原始打开清理才销毁数据源()
    {
        var database = await NativeDataDatabase.Create(postgres, Ct);
        var settings = new NpgsqlConnectionStringBuilder(database.TenantConnection) { Timeout = 3, ConnectionLifetime = 1 };
        await using var proxy = new NativePoolAdmissionTests.StallSecondConnection(settings.Host ?? throw new InvalidOperationException("PostgreSQL fixture 缺少 Host。"), settings.Port, Ct);
        settings.Host = System.Net.IPAddress.Loopback.ToString(); settings.Port = proxy.Port;
        var tenantContext = new AsyncLocalTenantContext();
        await using var service = new NativeDataService(ScopeKey.Global, ScopeKey.Global, tenantContext);
        var tables = new[] { new NativeDataTable(database.Schema, "items", "native_guard", database.Predicate, database.Predicate) };
        await service.InitializeAsync([new NativeDataBinding("tenant", database.TenantRole, NativeDataAccess.Tenant,
            _ => ValueTask.FromResult(settings.ConnectionString), tables)], Ct);
        using var tenant = tenantContext.BeginScope(A);
        await using var first = await service.OpenTenantAsync("tenant", Ct);
        var elapsed = Stopwatch.StartNew();
        var opening = service.OpenTenantAsync("tenant", Ct).AsTask();
        await Task.Delay(TimeSpan.FromSeconds(2), Ct);
        await first.DisposeAsync(); // 已超过物理连接寿命，下一次租用将建立第二条 TCP 连接。
        await proxy.SecondAccepted.Task.WaitAsync(TimeSpan.FromSeconds(2), Ct);
        var error = await Assert.ThrowsAsync<NpgsqlException>(() => opening);
        Assert.IsType<TimeoutException>(error.InnerException);
        Assert.InRange(elapsed.Elapsed, TimeSpan.FromSeconds(2.5), TimeSpan.FromSeconds(4));
        var closing = service.DisposeAsync().AsTask();
        Assert.False(closing.IsCompleted);
        Assert.Contains(":disposed=0:", PoolSnapshot(service), StringComparison.Ordinal);
        proxy.ReleaseSecond.TrySetResult();
        await closing.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Contains(":disposed=1:", PoolSnapshot(service), StringComparison.Ordinal);
        await using var observer = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Pooling = false, Enlist = false }.ConnectionString);
        await observer.OpenAsync(Ct);
        var deadline = DateTime.UtcNow.AddSeconds(2); long count;
        do
        {
            count = (long)(await Scalar(observer, $"SELECT count(*) FROM pg_stat_activity WHERE usename = '{database.TenantRole}'"))!;
            if (count != 0) await Task.Delay(10, Ct);
        } while (count != 0 && DateTime.UtcNow < deadline);
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task 非池化绑定不受池容量准入限制()
    {
        await using var host = await Setup(pooling: false); using var tenant = host.Tenant.BeginScope(A);
        await using var first = await host.Data.OpenTenantAsync("tenant", Ct);
        await using var second = await host.Data.OpenTenantAsync("tenant", Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.NotEqual(await Scalar(first.Connection, "SELECT pg_backend_pid()"), await Scalar(second.Connection, "SELECT pg_backend_pid()"));
        Assert.Empty(cleanupDiagnostics);
    }

    private async Task<Running> Setup(bool pooling = true, int timeoutSeconds = 15)
    {
        var database = await NativeDataDatabase.Create(postgres, Ct);
        var schema = database.Schema; var tenantRole = database.TenantRole; var platformRole = database.PlatformRole;
        var connection = new NpgsqlConnectionStringBuilder(database.TenantConnection) { Pooling = pooling, Timeout = timeoutSeconds }.ConnectionString;
        var platform = new NpgsqlConnectionStringBuilder(database.PlatformConnection) { Pooling = pooling, Timeout = timeoutSeconds }.ConnectionString;
        var predicate = database.Predicate;
        var services = new ServiceCollection(); services.AddLogging(builder => builder.AddProvider(new DiagnosticLogs(cleanupDiagnostics))); var context = new AsyncLocalTenantContext();
        services.AddSingleton<ITenantContext>(context); services.AddSingleton(ErrorCatalog.CreateDefault());
        var tables = new[] { new NativeDataTable(schema, "items", "native_guard", predicate, predicate) };
        services.AddQianNativeData(new("tenant", tenantRole, NativeDataAccess.Tenant, _ => ValueTask.FromResult(connection), tables));
        services.AddQianNativeData(new("platform", platformRole, NativeDataAccess.Platform, _ => ValueTask.FromResult(platform), tables));
        var provider = services.BuildServiceProvider(); var loader = new PluginLoader(provider, provider.GetRequiredService<ILogger<PluginLoader>>());
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

    private static string PoolSnapshot(INativeDataService service)
    {
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;
        static object? Field(object value, string name)
        {
            for (var type = value.GetType(); type is not null; type = type.BaseType)
            { var field = type.GetField(name, Flags); if (field is not null) return field.GetValue(value); var property = type.GetProperty(name, Flags); if (property is not null) return property.GetValue(value); }
            return null;
        }
        static string Primitive(object value, string name)
        { var found = Field(value, name); return found is int or long or uint or bool or Enum ? found.ToString()! : "unavailable"; }
        try
        {
            var bindings = (System.Collections.IDictionary)Field(service, "_bindings")!;
            var lines = new List<string> { "opening=" + Primitive(service, "_opening") + ":sessions=" + Field(Field(service, "_sessions")!, "Count") };
            foreach (System.Collections.DictionaryEntry entry in bindings)
            {
                var source = (NpgsqlDataSource)entry.Value!.GetType().GetProperty("Source")!.GetValue(entry.Value)!;
                var stats = source.GetType().GetProperty("Statistics", Flags)!.GetValue(source);
                var connectors = ((Array)Field(source, "Connectors")!).Cast<object?>().Where(value => value is not null).Cast<object>().ToArray();
                var reader = Field(source, "_idleConnectorReader")!;
                var channel = Field(reader, "_parent")!;
                var queue = Field(channel, "_items");
                var queued = queue is null ? "unknown" : Field(queue, "Count");
                lines.Add(entry.Key + ":stats=" + stats + ":disposed=" + Primitive(source, "_isDisposed") + ":clear=" + Primitive(source, "_clearCounter") + ":clearing=" + Primitive(source, "_isClearing") + ":queued=" + queued);
                foreach (var connector in connectors)
                {
                    var raw = Field(connector, "Connection") as NpgsqlConnection;
                    lines.Add("connector:id=" + connector.GetType().GetProperty("Id", Flags)?.GetValue(connector) + ":state=" + connector.GetType().GetProperty("State", Flags)?.GetValue(connector) + ":clear=" + Primitive(connector, "ClearCounter") + ":rawState=" + raw?.FullState + ":rawClosing=" + (raw is null ? "null" : Primitive(raw, "_closing")) + ":rawDisposed=" + (raw is null ? "null" : Primitive(raw, "_disposed")));
                }
            }
            return string.Join(";", lines);
        }
        catch (Exception error) { return "diagnostic-error:" + error.GetType().Name; }
    }

    private sealed class DiagnosticLogs(ConcurrentQueue<string> entries) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new DiagnosticLogger(entries, categoryName);
        public void Dispose() { }
    }
    private sealed class DiagnosticLogger(ConcurrentQueue<string> entries, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => level >= LogLevel.Warning;
        public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (IsEnabled(level)) entries.Enqueue(category + ":" + eventId.Id + ":" + exception?.GetType().FullName + "\n" + exception?.StackTrace + "\ninner:" + exception?.InnerException?.GetType().FullName + "\n" + exception?.InnerException?.StackTrace);
        }
    }

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
