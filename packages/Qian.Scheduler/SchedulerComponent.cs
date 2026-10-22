using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Scheduler;

/// <summary>可由 qian add 安装的调度组件唯一入口。</summary>
[QianPlugin("qian.scheduler")]
[QianConfigSchema(ComponentConfiguration.Schema)]
[DelegatePlugin(typeof(SchedulerPlugin))]
[Provides(typeof(SchedulerDefinition), nameof(SchedulerDefinition.Key))]
public sealed class SchedulerComponent : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, TenancyDefinition.Key];
    /// <inheritdoc />
    public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => new SchedulerPlugin().ApplyAsync(ctx, config, ct);
}

/// <summary>程序化装配与文件入口共用的 Provider；worker 属于 Apply 的效果栈。</summary>
public sealed class SchedulerPlugin : IQianPlugin
{
    /// <inheritdoc />
    public IReadOnlyList<ServiceKey> Inject => [DataDefinition.Key, TenancyDefinition.Key];
    /// <inheritdoc />
    public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        if (ctx.Scope != ScopeKey.Global) throw new QianErrorException(new QianError(SchedulerErrors.Misconfigured));
        var settings = SchedulerSettings.Read(config);
        var tenant = ctx.Services.GetRequiredService<ITenantContext>();
        if (tenant.TenantId is not null) throw new QianErrorException(new QianError(SchedulerErrors.Misconfigured));
        await using (var session = await ctx.Data().OpenAsync<SchedulerWorkerDbContext>(ct).ConfigureAwait(false))
        {
            await session.Context.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
            await SchedulerWorkerAccess.ValidateConnectionAsync(session.Context.Database.GetDbConnection(), settings.WorkerRole, ct).ConfigureAwait(false);
        }
        var catalog = ctx.Services.GetRequiredService<ErrorCatalog>(); ctx.Effect(catalog.Register(SchedulerErrors.Misconfigured));
        var service = new PostgresScheduler(ctx, tenant);
        var worker = new SchedulerWorker(ctx, tenant, settings);
        ctx.Provide<IScheduler>(SchedulerDefinition.Key, service);
        ctx.Effect(worker.DisposeAsync);
        worker.Start();
    }
}
internal sealed record SchedulerSettings(string WorkerRole, int LeaseSeconds, int MaxParallel)
{
    internal static SchedulerSettings Read(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object || config.EnumerateObject().GroupBy(p => p.Name, StringComparer.Ordinal).Any(g => g.Count() > 1)) throw JobSchedule.Invalid();
        var role = "qian_scheduler_worker"; var lease = 30; var parallel = 16;
        foreach (var p in config.EnumerateObject())
        {
            switch (p.Name)
            {
                case "store" when p.Value.ValueKind == JsonValueKind.String && p.Value.GetString() == "pg": break;
                case "workerRole" when p.Value.ValueKind == JsonValueKind.String: role = p.Value.GetString()!; break;
                case "leaseSeconds" when p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var n): lease = n; break;
                case "maxParallel" when p.Value.ValueKind == JsonValueKind.Number && p.Value.TryGetInt32(out var n): parallel = n; break;
                default: throw JobSchedule.Invalid();
            }
        }
        SchedulerWorkerAccess.ValidateRoleName(role);
        if (lease is < 3 or > 3600 || parallel is < 1 or > 100) throw JobSchedule.Invalid();
        return new SchedulerSettings(role, lease, parallel);
    }
}
internal static class ComponentConfiguration
{
    internal const string Schema = """
        {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false,
         "properties":{"store":{"const":"pg","default":"pg"},"workerRole":{"type":"string","pattern":"^[a-z][a-z0-9_]{0,62}$","default":"qian_scheduler_worker"},
         "leaseSeconds":{"type":"integer","minimum":3,"maximum":3600,"default":30},"maxParallel":{"type":"integer","minimum":1,"maximum":100,"default":16}}}
        """;
}
