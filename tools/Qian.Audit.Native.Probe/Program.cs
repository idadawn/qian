using System.Data;
using System.Data.Common;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Audit.Native;
using Qian.Data.Native;
using Qian.Data.Native.Postgres;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;

var stage = "平台检查";
try
{
    if (RuntimeFeature.IsDynamicCodeSupported || !OperatingSystem.IsLinux()) throw new InvalidOperationException("需要 Linux NativeAOT。");
    using var input = JsonDocument.Parse(await File.ReadAllTextAsync(args.Length == 1 ? args[0] : "/run/qian-native-audit.json"));
    var root = input.RootElement;
    string Get(string name) => root.GetProperty(name).GetString() ?? throw new InvalidOperationException("缺少探针配置。");
    var roles = new NativeAuditRoles(Get("tenantRole"), Get("functionOwner"), Get("platformRole"));
    var tenant = new AsyncLocalTenantContext(); var services = new ServiceCollection();
    services.AddLogging(); services.AddSingleton<ITenantContext>(tenant); services.AddSingleton(ErrorCatalog.CreateDefault());
    var guard = NativeAuditStorage.ExpectedGuard(roles);
    var tables = new[] { new NativeDataTable(NativeAuditStorage.Schema, "audit_entries", NativeAuditStorage.GuardPolicy, guard, guard),
        new NativeDataTable(NativeAuditStorage.Schema, "audit_chain_state", NativeAuditStorage.GuardPolicy, guard, guard) };
    services.AddQianNativeData(new("audit_tenant", roles.TenantLogin, NativeDataAccess.Tenant, _ => ValueTask.FromResult(Get("tenantConnection")), tables));
    services.AddQianNativeData(new("audit_platform", roles.PlatformLogin!, NativeDataAccess.Platform, _ => ValueTask.FromResult(Get("platformConnection")), tables));
    await using var provider = services.BuildServiceProvider(); await using var loader = new PluginLoader(provider);
    using var dataConfig = JsonDocument.Parse("""{"connections":["audit_tenant","audit_platform"]}""");
    using var auditConfig = JsonDocument.Parse($$"""{"tenantBinding":"audit_tenant","platformBinding":"audit_platform","tenantRole":"{{roles.TenantLogin}}","platformRole":"{{roles.PlatformLogin}}","functionOwner":"{{roles.FunctionOwner}}"}""");
    using var empty = JsonDocument.Parse("{}"); stage = "组件挂载";
    await loader.MountAsync([
        new(new PluginEntry("data", "qian.data.native", "1.0.0", ScopeKey.Global, 0, dataConfig.RootElement), new NativeDataComponent(), [NativeDataDefinition.Key]),
        new(new PluginEntry("audit", "qian.audit.native", "1.0.0", ScopeKey.Global, 0, auditConfig.RootElement), new NativeAuditComponent(), [AuditDefinition.Key, CommandPolicies.AuditKey]),
        new(new PluginEntry("consumer", "test.consumer", "1.0.0", ScopeKey.Global, 0, empty.RootElement), new Consumer(), [])]);
    Ensure(loader.GetState("audit", ScopeKey.Global) == PluginState.Ready, "真实 NativeData 与 Audit 组件挂载");
    var caller = loader.GetContext("consumer", ScopeKey.Global); var data = caller.NativeData(); var audit = caller.Audit();
    Ensure(caller.List(CommandPolicies.AuditKey).Count == 1, "真实强制审计策略登记");
    const string a = "01900000-0000-7000-8000-000000000001";
    const string b = "01900000-0000-7000-8000-000000000002";
    stage = "租户追加读取验链";
    using (tenant.BeginScope(a))
    {
        var record = Record(a); var first = await audit.AppendAsync(record);
        Ensure(first.Sequence == 1 && (await audit.AppendAsync(record)).ContentHash == first.ContentHash, "追加与同 ID 幂等");
        var conflicting = new AuditRecord(record.Id, a, "service", "conflicting", null, record.CapabilityId, record.ContractVersion,
            record.InputHash, record.Channel, record.Resource, record.Result, record.TraceId, record.OccurredAt, record.Payload);
        try { await audit.AppendAsync(conflicting); throw new InvalidOperationException("冲突未拒绝"); }
        catch (QianErrorException error) when (error.Error.Code.Code == "CONFLICT") { }
        var read = await audit.ReadAsync(0, 10);
        Ensure(read.Count == 1 && read[0].ContentHash == AuditProtocol.Hash(read[0].Record, 1, null), "全部字段可读且共享协议重放");
        Ensure((await audit.VerifyAsync()) is { Valid: true, CheckedEntries: 1 }, "租户全链验证");
        stage = "真实业务事务回滚";
        await using (var session = await data.OpenTenantAsync("audit_tenant"))
        {
            var tx = await session.BeginTransactionAsync(IsolationLevel.Serializable);
            await Execute(session.Connection, "INSERT INTO plugin_native_audit_probe.business(tenant_id,value) VALUES (current_setting('app.current_tenant_id'),'rollback')");
            Ensure((await audit.AppendEnlistedAsync(Record(a), tx)).Sequence == 2, "同一真实业务事务追加");
            await session.RollbackAsync();
        }
        await using (var session = await data.OpenTenantAsync("audit_tenant"))
            Ensure((long?)await Scalar(session.Connection, "SELECT count(*) FROM plugin_native_audit_probe.business") == 0, "外层回滚业务记录");
        Ensure((await audit.VerifyAsync()) is { Valid: true, CheckedEntries: 1 }, "外层回滚审计记录和链头");
        await using (var session = await data.OpenTenantAsync("audit_tenant"))
        {
            var tx = await session.BeginTransactionAsync(IsolationLevel.Serializable);
            await Execute(session.Connection, "INSERT INTO plugin_native_audit_probe.business(tenant_id,value) VALUES (current_setting('app.current_tenant_id'),'committed')");
            await audit.AppendEnlistedAsync(Record(a), tx); await session.CommitAsync();
        }
        Ensure((await audit.VerifyAsync()) is { Valid: true, CheckedEntries: 2 }, "同事务提交业务和审计");
    }
    using (tenant.BeginScope(b)) Ensure((await audit.ReadAsync(0, 10)).Count == 0, "跨租户隔离");
    stage = "平台独立链";
    await audit.AppendPlatformAsync(caller, Record(null));
    Ensure((await audit.ReadPlatformAsync(caller, 0, 10)).Count == 1 && (await audit.VerifyPlatformAsync(caller)).Valid, "独立平台登录追加读取验链");
    stage = "卸载";
    await loader.UnmountAsync("audit", ScopeKey.Global);
    Ensure(caller.TryInject<IAuditService>(AuditDefinition.Key) is null && caller.List(CommandPolicies.AuditKey).Count == 0, "卸载撤销服务和策略");
    try { await audit.VerifyPlatformAsync(caller); throw new InvalidOperationException("旧句柄未拒绝"); } catch (ObjectDisposedException) { }
    Console.WriteLine("NativeAudit Linux NativeAOT 真实数据库探针通过。"); return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("NativeAudit 原生探针失败，阶段：" + stage + "；错误类型：" + error.GetType().Name); return 1;
}
static AuditRecord Record(string? tenant)
{
    using var payload = JsonDocument.Parse("""{"z":1e3,"precise":9007199254740993,"a":1.00}""");
    return new(Guid.CreateVersion7(), tenant, "service", "native-probe", null, "probe.append", "1.0.0", new string('1', 64), "api", "probe:1",
        "success", new string('2', 32), DateTimeOffset.UtcNow, payload.RootElement);
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
