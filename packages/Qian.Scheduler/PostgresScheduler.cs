using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Scheduler;

internal sealed class PostgresScheduler(IPluginContext owner, ITenantContext tenant) : IScheduler
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    internal IDataService Data => owner.Data();
    public ValueTask<ScheduledJob> ScheduleOnce(IPluginContext caller, string businessKey, DateTimeOffset runAt, string target, JsonElement parameters, JobOptions? options = null, CancellationToken cancellationToken = default)
        => Schedule(caller, businessKey, "once", runAt, target, parameters, options, cancellationToken);
    public ValueTask<ScheduledJob> ScheduleCron(IPluginContext caller, string businessKey, string cron, string target, JsonElement parameters, JobOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cron); JobSchedule.Validate(cron);
        return Schedule(caller, businessKey, cron, JobSchedule.Next(cron, DateTimeOffset.UtcNow) ?? throw JobSchedule.Invalid(), target, parameters, options, cancellationToken);
    }
    public ValueTask<ScheduledJob> ScheduleInterval(IPluginContext caller, string businessKey, DateTimeOffset firstRunAt, TimeSpan interval, string target, JsonElement parameters, JobOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (interval < TimeSpan.FromSeconds(1) || interval > TimeSpan.FromDays(365)) throw JobSchedule.Invalid();
        return Schedule(caller, businessKey, "interval:" + interval.Ticks.ToString(CultureInfo.InvariantCulture), firstRunAt, target, parameters, options, cancellationToken);
    }
    private async ValueTask<ScheduledJob> Schedule(IPluginContext caller, string key, string schedule, DateTimeOffset first, string target, JsonElement parameters, JobOptions? options, CancellationToken ct)
    {
        var tenantId = CheckCaller(caller); ValidateKey(key); options ??= new JobOptions(); ValidateOptions(options);
        var descriptor = await caller.Tenancy().GetByIdAsync(Guid.Parse(tenantId), ct).ConfigureAwait(false);
        if (descriptor?.Status is not (TenantStatus.Active or TenantStatus.Trial)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        var job = Resolve(caller, target);
        var input = Input(job, parameters, Guid.NewGuid());
        if (!JsonSchema.FromText(job.Capability.InputSchema.ToJsonString(), new BuildOptions { SchemaRegistry = new SchemaRegistry { Fetch = (_, _) => null } }).Evaluate(input).IsValid) throw JobSchedule.Invalid();
        first = Microseconds(first);
        var optionsJson = JsonSerializer.Serialize(options, Json);
        // cron 的首个发生点由首次登记决定，重试同一业务键不能因当前时间改变而产生冲突。
        var fingerprint = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(schedule + "\n" + (schedule.Contains(' ', StringComparison.Ordinal) ? "" : first.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)) + "\n" + target + "\n" + Canonical(parameters) + "\n" + optionsJson)));
        await using var session = await Data.OpenAsync<SchedulerDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        var lockKey = "scheduler:" + tenantId + ":" + key;
        await session.Context.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({lockKey},0))", ct).ConfigureAwait(false);
        var existing = await session.Context.Definitions.SingleOrDefaultAsync(x => x.BusinessKey == key, ct).ConfigureAwait(false);
        if (existing is not null)
        {
            if (existing.Fingerprint != fingerprint) throw new QianErrorException(new QianError(PlatformErrors.Conflict));
            return new ScheduledJob(existing.Id, key);
        }
        var id = Guid.CreateVersion7(); first = Microseconds(first);
        session.Context.Definitions.Add(new JobDefinitionRow { TenantId = tenantId, Id = id, BusinessKey = key, Target = target,
            Parameters = parameters.GetRawText(), Options = optionsJson, Schedule = schedule, Fingerprint = fingerprint, CreatedBy = caller.Entry.Plugin });
        session.Context.Triggers.Add(new JobTriggerRow { TenantId = tenantId, Id = id, NextRunAt = first, AvailableAt = first });
        await session.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false);
        return new ScheduledJob(id, key);
    }
    public async ValueTask<bool> Cancel(IPluginContext caller, string businessKey, CancellationToken cancellationToken = default)
    {
        _ = CheckCaller(caller); ValidateKey(businessKey);
        await using var session = await Data.OpenAsync<SchedulerDbContext>(cancellationToken).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var id = await session.Context.Definitions.Where(x => x.BusinessKey == businessKey).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (id is null) return false;
        await session.Context.Triggers.Where(x => x.Id == id).ExecuteUpdateAsync(set => set.SetProperty(x => x.State, "archived").SetProperty(x => x.Generation, x => x.Generation + 1)
            .SetProperty(x => x.LeaseOwner, (string?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), cancellationToken).ConfigureAwait(false);
        var finished = DateTimeOffset.UtcNow;
        await session.Context.Runs.Where(x => x.TriggerId == id && (x.Status == "running" || x.Status == "retry"))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.Status, "cancelled").SetProperty(x => x.ErrorClass, "cancelled").SetProperty(x => x.FinishedAt, finished), cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false); return true;
    }
    private string CheckCaller(IPluginContext caller)
    {
        ArgumentNullException.ThrowIfNull(caller);
        if (!Guid.TryParseExact(tenant.TenantId, "D", out _) || caller.Scope != new ScopeKey("tenant", tenant.TenantId) ||
            !ReferenceEquals(caller.TryInject<IScheduler>(SchedulerDefinition.Key), this)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return tenant.TenantId;
    }
    internal static JobContract Resolve(IPluginContext caller, string target)
    {
        if (string.IsNullOrWhiteSpace(target) || target.Length > 300 || target.Contains(':', StringComparison.Ordinal) || target.Contains('\\', StringComparison.Ordinal) || target.Count(c => c == '/') != 1) throw JobSchedule.Invalid();
        var job = caller.List(JobBindingRegistration.Key).SingleOrDefault(x => x.Name == target)?.Value as JobContract;
        var binding = caller.List(CapabilityBindingRegistration.Key).SingleOrDefault(x => x.Name == target)?.Value as IExecutableCapability;
        if (job is null || binding is null || !ReferenceEquals(job.Capability, binding.Contract)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
        return job;
    }
    internal static JsonElement Input(JobContract job, JsonElement parameters, Guid runId)
    {
        if (parameters.ValueKind != JsonValueKind.Object || parameters.GetRawText().Length > 65536 || parameters.EnumerateObject().GroupBy(x => x.Name, StringComparer.Ordinal).Any(x => x.Count() > 1)) throw JobSchedule.Invalid();
        var node = JsonNode.Parse(parameters.GetRawText())!.AsObject();
        if (job.Capability.Metadata.GetProperty("sideEffect").GetString() != "read") node["idempotencyKey"] = runId;
        return JsonSerializer.SerializeToElement(node);
    }
    private static string Canonical(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Object => "{" + string.Join(',', value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => JsonSerializer.Serialize(p.Name) + ":" + Canonical(p.Value))) + "}",
        JsonValueKind.Array => "[" + string.Join(',', value.EnumerateArray().Select(Canonical)) + "]",
        _ => value.GetRawText()
    };
    internal static DateTimeOffset Microseconds(DateTimeOffset value) => value.ToUniversalTime().AddTicks(-(value.Ticks % 10));
    private static void ValidateKey(string key) { if (string.IsNullOrWhiteSpace(key) || key.Length > 200 || key.Any(char.IsControl)) throw JobSchedule.Invalid(); }
    internal static void ValidateOptions(JobOptions options)
    {
        if (options.Timeout < TimeSpan.FromMilliseconds(100) || options.Timeout > TimeSpan.FromDays(1) || options.MisfireGrace < TimeSpan.Zero || options.MisfireGrace > TimeSpan.FromDays(1) ||
            options.MaxAttempts is < 1 or > 100 || options.RetryDelay < TimeSpan.Zero || options.RetryDelay > TimeSpan.FromDays(1) || !Enum.IsDefined(options.Misfire) || !Enum.IsDefined(options.Concurrency)) throw JobSchedule.Invalid();
    }
}
