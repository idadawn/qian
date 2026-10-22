using System.Text.Json;
using System.Text.Json.Serialization;
using Kun.Authorization;
using Kun.Core.Cqrs;
using Kun.Core.MultiTenancy;
using Kun.Core.Security;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Scheduler.TestHost;

public sealed record HostConfig(string Application, string Worker, string WorkerRole, int LeaseSeconds = 6, IReadOnlyList<string>? WarmupTenants = null);

public sealed class ProbeHost : IAsyncDisposable
{
    private readonly IHost _host;
    public PluginLoader Runtime { get; }
    public ITenantContext Tenant { get; }
    public IPluginContext Caller => Runtime.GetContext("probe", ScopeKey.Global);
    public IServiceProvider Services => _host.Services;
    private ProbeHost(IHost host, PluginLoader runtime, ITenantContext tenant) { _host = host; Runtime = runtime; Tenant = tenant; }
    public static async Task<ProbeHost> CreateAsync(HostConfig config, bool worker, CancellationToken ct = default)
    {
        var builder = Host.CreateApplicationBuilder(); builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Error).AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Error);
        var tenant = new AsyncLocalTenantContext(); builder.Services.AddSingleton<ITenantContext>(tenant);
        builder.Services.AddSingleton(config); builder.Services.AddSingleton<ICurrentUser>(new ServiceUser());
        builder.Services.AddSingleton<IPermissionChecker>(new ProbePermissions());
        builder.Services.AddKunEventBus();
        builder.Services.AddQianData<SchedulerDbContext>((_, options) => options.UseNpgsql(config.Application).UseKunSchema(SchedulerDbContext.Schema));
        builder.Services.AddQianData<SchedulerWorkerDbContext>((_, options) => options.UseNpgsql(config.Worker).UseKunSchema(SchedulerDbContext.Schema));
        builder.Services.AddKunMediator(registry => registry.AddAuthorizedHandler<ProbeRequest, ProbeResponse, ProbeHandler>(_ => throw new QianErrorException(new QianError(PlatformErrors.AuthzDenied))));
        var host = builder.Build(); await host.StartAsync(ct);
        var runtime = new PluginLoader(host.Services, host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SchedulerProbe"));
        var result = new ProbeHost(host, runtime, tenant);
        await runtime.MountAsync([
            Define("data", "qian.data", new DataPlugin(), [DataDefinition.Key]),
            Define("tenancy", "qian.tenancy", new DirectoryPlugin(config), [TenancyDefinition.Key]),
            Define("probe", "qian.scheduler.probe", new ProbePlugin(), [])], ct);
        if (worker)
        {
            // READY 表示真实租户 EF 模型、安全检查和查询已可用。预热发生在领取循环之前，
            // 不让进程首次建模占用故障测试的 6 秒租约；不造作业、不改持久状态、不隐藏重试。
            // Kun 模型缓存按 tenant 分区，必须预热真实被测租户，随机虚构租户不能替代。
            foreach (var tenantId in config.WarmupTenants ?? [])
            {
                using var warmupTenant = tenant.BeginScope(tenantId);
                await using var session = await runtime.GetContext("data", ScopeKey.Global).Data().OpenAsync<SchedulerDbContext>(ct);
                _ = await session.Context.Definitions.AsNoTracking().AnyAsync(ct);
                _ = await session.Context.Triggers.AsNoTracking().AnyAsync(ct);
                _ = await session.Context.Runs.AsNoTracking().AnyAsync(ct);
            }
        }
        await runtime.MountAsync([
            Define("scheduler", "qian.scheduler", worker ? new SchedulerComponent() : new ProducerPlugin(), [SchedulerDefinition.Key],
                new { workerRole = config.WorkerRole, leaseSeconds = config.LeaseSeconds, maxParallel = 16 })], ct);
        if (runtime.GetState("scheduler", ScopeKey.Global) != PluginState.Ready) throw new InvalidOperationException("调度宿主未就绪", runtime.GetFailure("scheduler", ScopeKey.Global));
        if (runtime.GetState("probe", ScopeKey.Global) != PluginState.Ready) throw new InvalidOperationException("探针未就绪", runtime.GetFailure("probe", ScopeKey.Global));
        return result;
    }
    public static PluginDefinition Define(string id, string pluginId, IQianPlugin plugin, IReadOnlyList<ServiceKey> provides, object? config = null)
        => new(new PluginEntry(id, pluginId, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
    public async ValueTask DisposeAsync() { await Runtime.DisposeAsync(); await _host.StopAsync(); _host.Dispose(); }
    private sealed class ProducerPlugin : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        { ctx.Provide<IScheduler>(SchedulerDefinition.Key, new PostgresScheduler(ctx, ctx.Services.GetRequiredService<ITenantContext>())); return ValueTask.CompletedTask; }
    }
    private sealed class DirectoryPlugin(HostConfig config) : IQianPlugin, ITenantDirectory
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { ctx.Provide<ITenantDirectory>(TenancyDefinition.Key, this); return ValueTask.CompletedTask; }
        public ValueTask<TenantDescriptor?> GetAsync(string code, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public async ValueTask<TenantDescriptor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            await using var db = new NpgsqlConnection(config.Application); await db.OpenAsync(cancellationToken);
            await using var command = new NpgsqlCommand("SELECT status FROM public.sch_probe_tenants WHERE id=@id", db); command.Parameters.AddWithValue("id", id);
            var status = await command.ExecuteScalarAsync(cancellationToken) as string;
            return new TenantDescriptor(id, "fixture", "测试租户", TenantIsolation.Row, status == "suspended" ? TenantStatus.Suspended : TenantStatus.Active);
        }
    }
    private sealed class ServiceUser : ICurrentUser
    { public string UserId => "qian.scheduler"; public IReadOnlyList<string> Roles => []; public bool IsAuthenticated => true; }
    // 权限端口是调度测试依赖替身；真实能力经 Kun 的权限行为执行，不替代 Authz 的集成验收。
    private sealed class ProbePermissions : IPermissionChecker
    {
        public ValueTask<PermissionDecision> CheckAsync(PermissionRequest request, CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new PermissionDecision(request.Subject.Id == "qian.scheduler" && Guid.TryParse(request.Subject.TenantId, out _) && request.Permission == "qian.scheduler.probe:job:run", "fixture", []));
        public ValueTask<IReadOnlyList<PermissionDecision>> BatchCheckAsync(IReadOnlyList<PermissionRequest> requests, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListAllowedActionsAsync(AuthorizationSubject subject, string resourceType, string? resourceId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<IReadOnlyList<string>> ListPermissionsAsync(AuthorizationSubject subject, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask<PermissionDecision> ExplainAsync(PermissionRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}

[QianPlugin("qian.scheduler.probe")]
public sealed class ProbePlugin : IQianPlugin
{
    public IReadOnlyList<ServiceKey> Inject => [];
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    { ctx.ProvideJob<ProbeRequest, ProbeResponse>(); return ValueTask.CompletedTask; }
}

[QianJob("probe")]
[Capability("run", typeof(ProbeResponse), typeof(ProbeJson), Description = "集群调度验收探针：持久记录每次处理器进入与幂等业务效果。", SideEffect = "write", Idempotent = true, RequiredPermission = "qian.scheduler.probe:job:run")]
public sealed partial record ProbeRequest(Guid IdempotencyKey, string Mode = "normal", int DelayMs = 0);
public sealed record ProbeResponse(bool Applied);
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(ProbeRequest))][JsonSerializable(typeof(ProbeResponse))]
public sealed partial class ProbeJson : JsonSerializerContext;

public sealed class ProbeHandler(HostConfig config, ITenantContext tenant) : IRequestHandler<ProbeRequest, ProbeResponse>
{
    public async ValueTask<ProbeResponse> Handle(ProbeRequest request, CancellationToken cancellationToken)
    {
        await using var db = new NpgsqlConnection(config.Application); await db.OpenAsync(cancellationToken);
        await using var enter = new NpgsqlCommand("INSERT INTO public.sch_probe_attempts(run_id,tenant_id,process_id,started) VALUES (@run,@tenant,@pid,clock_timestamp()) RETURNING id", db);
        enter.Parameters.AddWithValue("run", request.IdempotencyKey); enter.Parameters.AddWithValue("tenant", tenant.TenantId!); enter.Parameters.AddWithValue("pid", Environment.ProcessId);
        var attempt = (long)(await enter.ExecuteScalarAsync(cancellationToken))!;
        await using var count = new NpgsqlCommand("SELECT count(*) FROM public.sch_probe_attempts WHERE run_id=@run", db); count.Parameters.AddWithValue("run", request.IdempotencyKey);
        var first = (long)(await count.ExecuteScalarAsync(cancellationToken))! == 1;
        if (request.Mode == "fail") throw new InvalidOperationException("预期失败探针");
        if (request.Mode == "hold-first" && first) await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
        if (request.DelayMs > 0) await Task.Delay(request.DelayMs, cancellationToken);
        await using var effect = new NpgsqlCommand("INSERT INTO public.sch_probe_effects(run_id,tenant_id,process_id) VALUES (@run,@tenant,@pid) ON CONFLICT (run_id) DO NOTHING", db);
        effect.Parameters.AddWithValue("run", request.IdempotencyKey); effect.Parameters.AddWithValue("tenant", tenant.TenantId!); effect.Parameters.AddWithValue("pid", Environment.ProcessId);
        var applied = await effect.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (request.Mode == "effect-then-hold" && first) await Task.Delay(TimeSpan.FromMinutes(2), cancellationToken);
        await using var finish = new NpgsqlCommand("UPDATE public.sch_probe_attempts SET finished=clock_timestamp() WHERE id=@id", db); finish.Parameters.AddWithValue("id", attempt); await finish.ExecuteNonQueryAsync(cancellationToken);
        return new ProbeResponse(applied);
    }
}
