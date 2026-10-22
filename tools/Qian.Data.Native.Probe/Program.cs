using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Data.Native;
using Qian.Data.Native.Postgres;
using Qian.Runtime;
using Qian.Runtime.Errors;

try
{
    if (RuntimeFeature.IsDynamicCodeSupported || !OperatingSystem.IsLinux()) throw new InvalidOperationException("需要 Linux NativeAOT。");
    using var input = JsonDocument.Parse(await File.ReadAllTextAsync(args.Length == 1 ? args[0] : "/run/qian-native-data.json"));
    var root = input.RootElement;
    string Get(string name) => root.GetProperty(name).GetString() ?? throw new InvalidOperationException("缺少探针配置。");
    var schema = Get("schema"); var table = schema + ".items";
    var tenant = new AsyncLocalTenantContext();
    var services = new ServiceCollection(); services.AddLogging(); services.AddSingleton<ITenantContext>(tenant); services.AddSingleton(ErrorCatalog.CreateDefault());
    var tables = new[] { new NativeDataTable(schema, "items", "native_guard", Get("predicate"), Get("predicate")) };
    services.AddQianNativeData(new("tenant", Get("tenantRole"), NativeDataAccess.Tenant, _ => ValueTask.FromResult(Get("tenantConnection")), tables));
    services.AddQianNativeData(new("platform", Get("platformRole"), NativeDataAccess.Platform, _ => ValueTask.FromResult(Get("platformConnection")), tables));
    await using var provider = services.BuildServiceProvider(); await using var loader = new PluginLoader(provider);
    using var config = JsonDocument.Parse("""{"connections":["tenant","platform"]}""");
    using var empty = JsonDocument.Parse("{}");
    await loader.MountAsync([
        new(new PluginEntry("native", "qian.data.native", "1.0.0", ScopeKey.Global, 0, config.RootElement.Clone()), new NativeDataComponent(), [NativeDataDefinition.Key]),
        new(new PluginEntry("consumer", "test.consumer", "1.0.0", ScopeKey.Global, 0, empty.RootElement.Clone()), new Consumer(), [])]);
    Ensure(loader.GetState("native", ScopeKey.Global) == PluginState.Ready, "真实组件挂载");
    var caller = loader.GetContext("consumer", ScopeKey.Global); var data = caller.NativeData();
    const string a = "01900000-0000-7000-8000-000000000001";
    const string b = "01900000-0000-7000-8000-000000000002";
    using (tenant.BeginScope(a))
    {
        await using (var session = await data.OpenTenantAsync("tenant"))
        {
            Ensure((string?)await Scalar(session.Connection, "SELECT current_setting('app.current_tenant_id')") == a, "连接租户上下文");
            var transaction = await session.BeginTransactionAsync(IsolationLevel.Serializable);
            await data.ValidateTenantTransactionAsync("tenant", transaction);
            await Execute(session.Connection, $"INSERT INTO {table} VALUES ('{a}', 'aot-commit')");
            await session.CommitAsync();
        }
        await using (var session = await data.OpenTenantAsync("tenant"))
        {
            await Execute(session.Connection, "SELECT pg_advisory_lock(836493)");
            await session.BeginTransactionAsync(IsolationLevel.Serializable);
            await Execute(session.Connection, $"INSERT INTO {table} VALUES ('{a}', 'aot-rollback')");
        }
        await using (var session = await data.OpenTenantAsync("tenant"))
            Ensure((long?)await Scalar(session.Connection, $"SELECT count(*) FROM {table}") == 1, "提交与释放回滚");
    }
    using (tenant.BeginScope(b))
    {
        await using var session = await data.OpenTenantAsync("tenant");
        Ensure((long?)await Scalar(session.Connection, $"SELECT count(*) FROM {table}") == 0, "原始 SQL 跨租户隔离");
        Ensure((string?)await Scalar(session.Connection, "SELECT current_setting('app.current_tenant_id')") == b, "池复用租户更新");
    }
    await using (var session = await data.OpenPlatformAsync("platform", caller))
    {
        Ensure((long?)await Scalar(session.Connection, $"SELECT count(*) FROM {table}") == 0, "平台不见租户记录");
        await Execute(session.Connection, $"INSERT INTO {table} VALUES (NULL,'aot-platform')");
        Ensure((long?)await Scalar(session.Connection, $"SELECT count(*) FROM {table}") == 1, "独立平台受限角色读写");
    }
    var settings = new NpgsqlConnectionStringBuilder(Get("tenantConnection")) { Enlist = false };
    await using (var controlSource = new NpgsqlSlimDataSourceBuilder(settings.ConnectionString).Build())
    await using (var control = await controlSource.OpenConnectionAsync())
    {
        Ensure((bool?)await Scalar(control, "SELECT pg_try_advisory_lock(836493)") == true, "释放清除连接锁");
        await Execute(control, "SELECT pg_advisory_unlock_all()");
    }
    using (tenant.BeginScope(a))
    {
        await using var pending = await data.OpenTenantAsync("tenant"); var connection = pending.Connection;
        await pending.BeginTransactionAsync(IsolationLevel.Serializable);
        await Execute(connection, $"INSERT INTO {table} VALUES ('{a}','aot-unload')");
        await loader.UnmountAsync("native", ScopeKey.Global);
        Ensure(connection.State == ConnectionState.Closed && caller.TryInject<INativeDataService>(NativeDataDefinition.Key) is null, "卸载关闭连接并撤销服务");
    }
    await loader.DisposeAsync(); await provider.DisposeAsync();
    Console.WriteLine("NativeData Linux NativeAOT 真实数据库探针通过。");
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("NativeData 原生探针失败，错误类型：" + error.GetType().Name);
    return 1;
}

static void Ensure(bool value, string name)
{ if (!value) throw new InvalidOperationException("原生探针断言失败。"); Console.WriteLine("通过：" + name); }
static async Task<object?> Scalar(DbConnection connection, string sql)
{ await using var command = connection.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(); }
static async Task Execute(DbConnection connection, string sql)
{ await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
internal sealed class Consumer : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
}
