using System.Collections.Concurrent;
using System.Data.Common;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Scheduler;

internal sealed partial class SchedulerWorker(IPluginContext context, ITenantContext tenant, SchedulerSettings settings) : IAsyncDisposable
{
    private readonly string _id = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _stop = new();
    private readonly ConcurrentDictionary<Guid, Task> _running = new();
    private Task? _loop;
    internal void Start()
    {
        // 后台循环不继承发起挂载的请求身份或租户。
        using (ExecutionContext.SuppressFlow()) _loop = Task.Run(LoopAsync);
    }
    private async Task LoopAsync()
    {
        while (!_stop.IsCancellationRequested)
        {
            foreach (var task in _running.Where(pair => pair.Value.IsCompleted)) _running.TryRemove(task.Key, out _);
            try
            {
                var capacity = settings.MaxParallel - _running.Count;
                if (capacity > 0)
                    foreach (var claim in await ClaimAsync(capacity, _stop.Token).ConfigureAwait(false)) _running[claim.Id] = ExecuteAsync(claim);
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception error) { Log.Cycle(context.Logger, error); }
            try { await Task.Delay(TimeSpan.FromSeconds(1), _stop.Token).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }
        }
    }
    internal async Task<IReadOnlyList<JobClaim>> ClaimAsync(int count, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<SchedulerWorkerDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        await db.Database.OpenConnectionAsync(ct).ConfigureAwait(false);
        var connection = db.Database.GetDbConnection();
        await SchedulerWorkerAccess.ValidateConnectionAsync(connection, settings.WorkerRole, ct).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            WITH due AS (
              SELECT tenant_id,id FROM plugin_qian_scheduler.job_triggers
              WHERE state='ready' AND next_run_at <= now() AND available_at <= now()
                AND (lease_until IS NULL OR lease_until <= now())
              ORDER BY next_run_at,id FOR UPDATE SKIP LOCKED LIMIT @count
            )
            UPDATE plugin_qian_scheduler.job_triggers t SET lease_owner=@worker,
              lease_until=now()+@lease, generation=t.generation+1,
              active_run_id=coalesce(t.active_run_id,gen_random_uuid())
            FROM due WHERE t.tenant_id=due.tenant_id AND t.id=due.id
            RETURNING t.tenant_id,t.id,t.active_run_id,t.generation,t.next_run_at
            """;
        Add(command, "count", count); Add(command, "worker", _id); Add(command, "lease", TimeSpan.FromSeconds(settings.LeaseSeconds));
        var result = new List<JobClaim>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) result.Add(new JobClaim(reader.GetString(0), reader.GetGuid(1), reader.GetGuid(2), reader.GetInt64(3), reader.GetFieldValue<DateTimeOffset>(4), _id));
        return result;
    }
    private async Task ExecuteAsync(JobClaim claim)
    {
        using var scope = tenant.BeginScope(claim.TenantId);
        using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
        Task? heartbeat = null;
        try
        {
            var started = await BeginAsync(claim, lifetime.Token).ConfigureAwait(false);
            if (started is null) return;
            var (definition, options, attempts, now) = started.Value;
            // Begin 的建模/数据库准备可能已消耗大半初始租约。先确认仍持有有效租约并续足，
            // 再进入处理器；失败或已失去所有权时保留 run，不能复活过期租约或推进发生点。
            if (!await RenewAsync(claim, lifetime.Token).ConfigureAwait(false)) return;
            using var heartbeatStop = new CancellationTokenSource();
            heartbeat = RenewLoopAsync(claim, lifetime, heartbeatStop.Token);
            string status = "succeeded"; string? errorClass = null;
            try
            {
                if (attempts > options.MaxAttempts) { status = "failed"; errorClass = "attempts_exhausted"; }
                else if (attempts == 1 && now - claim.ScheduledAt > options.MisfireGrace && options.Misfire == MisfirePolicy.Skip) status = "skipped";
                else
                {
                    var caller = context.Scoped(new ScopeKey("tenant", claim.TenantId));
                    var currentTenant = await caller.Tenancy().GetByIdAsync(Guid.Parse(claim.TenantId), lifetime.Token).ConfigureAwait(false);
                    if (currentTenant?.Status is not (TenantStatus.Active or TenantStatus.Trial)) throw new QianErrorException(new QianError(PlatformErrors.NotFound));
                    var job = PostgresScheduler.Resolve(caller, definition.Target);
                    using var parameters = JsonDocument.Parse(definition.Parameters);
                    var input = PostgresScheduler.Input(job, parameters.RootElement, claim.RunId);
                    if (!Json.Schema.JsonSchema.FromText(job.Capability.InputSchema.ToJsonString(), new Json.Schema.BuildOptions { SchemaRegistry = new Json.Schema.SchemaRegistry { Fetch = (_, _) => null } }).Evaluate(input).IsValid) throw JobSchedule.Invalid();
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
                    timeout.CancelAfter(options.Timeout);
                    try
                    {
                        // 处理器若忽略令牌，仍保持续租和串行占用，不能假装已强制终止而并发执行下一次。
                        await caller.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, definition.Target).InvokeAsync(caller, input, timeout.Token).ConfigureAwait(false);
                        if (timeout.IsCancellationRequested) { status = "failed"; errorClass = lifetime.IsCancellationRequested ? "cancelled" : "timeout"; }
                    }
                    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
                    { status = "failed"; errorClass = lifetime.IsCancellationRequested ? "cancelled" : "timeout"; }
                }
            }
            catch (Exception error) when (error is not OutOfMemoryException)
            { status = "failed"; errorClass = error is OperationCanceledException ? "cancelled" : error.GetType().Name; }
            // 停机、续租异常或失去租约都只停止本地执行，不能把 once 当作业务 Cancel 归档。
            // 保留同一 run 供租约恢复；显式 Cancel 已在自己的事务中写终态并提升 generation。
            if (lifetime.IsCancellationRequested) return;
            // 仍持续续租直到受所有权保护的完成事务提交。
            await CompleteAsync(claim, definition.Schedule, options, status, errorClass, CancellationToken.None).ConfigureAwait(false);
            await heartbeatStop.CancelAsync().ConfigureAwait(false);
            await heartbeat.ConfigureAwait(false); heartbeat = null;
        }
        catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
        catch (Exception error) { Log.Execution(context.Logger, error, claim.RunId); }
        finally
        {
            await lifetime.CancelAsync().ConfigureAwait(false);
            if (heartbeat is not null) await heartbeat.ConfigureAwait(false);
        }
    }
    private async Task<(JobDefinitionRow Definition, JobOptions Options, int Attempts, DateTimeOffset Now)?> BeginAsync(JobClaim claim, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<SchedulerDbContext>(ct).ConfigureAwait(false);
        var db = session.Context;
        await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var trigger = await Locked(db, claim, ct).ConfigureAwait(false);
        var now = await Now(db, ct).ConfigureAwait(false);
        if (!Owned(trigger, claim, now) || trigger!.State != "ready") return null;
        var definition = await db.Definitions.SingleAsync(x => x.Id == claim.Id, ct).ConfigureAwait(false);
        var options = JsonSerializer.Deserialize<JobOptions>(definition.Options, PostgresScheduler.Json)!;
        var run = await db.Runs.SingleOrDefaultAsync(x => x.Id == claim.RunId, ct).ConfigureAwait(false);
        if (run is null)
        {
            run = new JobRunRow { TenantId = claim.TenantId, Id = claim.RunId, TriggerId = claim.Id, ScheduledAt = claim.ScheduledAt };
            db.Runs.Add(run);
        }
        run.Attempts++; run.Generation = claim.Generation; run.Worker = claim.Worker; run.StartedAt = now; run.FinishedAt = null; run.Status = "running"; run.ErrorClass = null;
        await session.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false);
        return (definition, options, run.Attempts, now);
    }
    private async Task RenewLoopAsync(JobClaim claim, CancellationTokenSource execution, CancellationToken stop)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stop, execution.Token);
        try
        {
            while (!linked.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(settings.LeaseSeconds / 3.0), linked.Token).ConfigureAwait(false);
                if (!await RenewAsync(claim, linked.Token).ConfigureAwait(false)) { await execution.CancelAsync().ConfigureAwait(false); break; }
            }
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested) { }
        catch (Exception error) { Log.Renewal(context.Logger, error, claim.RunId); await execution.CancelAsync().ConfigureAwait(false); }
    }
    private async Task<bool> RenewAsync(JobClaim claim, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<SchedulerDbContext>(ct).ConfigureAwait(false);
        await using var transaction = await session.Context.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var changed = await session.Context.Database.ExecuteSqlInterpolatedAsync($"UPDATE plugin_qian_scheduler.job_triggers SET lease_until=now()+{TimeSpan.FromSeconds(settings.LeaseSeconds)} WHERE tenant_id={claim.TenantId} AND id={claim.Id} AND lease_owner={claim.Worker} AND generation={claim.Generation} AND lease_until>now() AND state='ready'", ct).ConfigureAwait(false);
        await transaction.CommitAsync(ct).ConfigureAwait(false);
        return changed == 1;
    }
    internal async Task<bool> CompleteAsync(JobClaim claim, string schedule, JobOptions options, string status, string? error, CancellationToken ct)
    {
        await using var session = await context.Data().OpenAsync<SchedulerDbContext>(ct).ConfigureAwait(false);
        var db = session.Context; await using var transaction = await db.Database.BeginTransactionAsync(ct).ConfigureAwait(false);
        var trigger = await Locked(db, claim, ct).ConfigureAwait(false); var now = await Now(db, ct).ConfigureAwait(false);
        if (!Owned(trigger, claim, now)) return false;
        var run = await db.Runs.SingleAsync(x => x.Id == claim.RunId, ct).ConfigureAwait(false);
        if (run.Generation != claim.Generation) return false;
        var retry = status == "failed" && trigger!.State == "ready" && run.Attempts < options.MaxAttempts && error != "cancelled";
        run.Status = trigger!.State == "archived" ? "cancelled" : retry ? "retry" : status; run.ErrorClass = error; run.FinishedAt = now;
        trigger.LeaseOwner = null; trigger.LeaseUntil = null;
        if (retry) trigger.AvailableAt = now + options.RetryDelay;
        else
        {
            trigger.ActiveRunId = null; trigger.LastRunAt = claim.ScheduledAt; trigger.RunCount++;
            var from = options.Concurrency == JobConcurrency.Skip || options.Misfire != MisfirePolicy.CatchUp ? now : claim.ScheduledAt;
            var next = NextOccurrence(schedule, claim.ScheduledAt, from);
            if (next is null) trigger.State = "archived";
            else { trigger.NextRunAt = next.Value; trigger.AvailableAt = next.Value; }
        }
        await session.SaveChangesAsync(ct).ConfigureAwait(false); await transaction.CommitAsync(ct).ConfigureAwait(false); return true;
    }
    internal static DateTimeOffset? NextOccurrence(string schedule, DateTimeOffset scheduled, DateTimeOffset from)
    {
        if (schedule.StartsWith("interval:", StringComparison.Ordinal))
        {
            var interval = long.Parse(schedule.AsSpan(9), System.Globalization.CultureInfo.InvariantCulture);
            var steps = Math.Max(1, (from - scheduled).Ticks / interval + 1);
            return scheduled.AddTicks(checked(steps * interval));
        }
        return JobSchedule.Next(schedule, from);
    }
    private static Task<JobTriggerRow?> Locked(SchedulerDbContext db, JobClaim claim, CancellationToken ct) => db.Triggers
        .FromSqlInterpolated($"SELECT * FROM plugin_qian_scheduler.job_triggers WHERE tenant_id={claim.TenantId} AND id={claim.Id} FOR UPDATE").SingleOrDefaultAsync(ct);
    private static bool Owned(JobTriggerRow? trigger, JobClaim claim, DateTimeOffset now) => trigger is not null && trigger.LeaseOwner == claim.Worker && trigger.Generation == claim.Generation && trigger.ActiveRunId == claim.RunId && trigger.LeaseUntil > now;
    private static async Task<DateTimeOffset> Now(SchedulerDbContext db, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT clock_timestamp()";
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        return (DateTimeOffset)(await command.ExecuteScalarAsync(ct).ConfigureAwait(false) is DateTime value ? new DateTimeOffset(value) : throw new InvalidOperationException("数据库时间不可用。"));
    }
    private static void Add(DbCommand command, string name, object value) { var parameter = command.CreateParameter(); parameter.ParameterName = name; parameter.Value = value; command.Parameters.Add(parameter); }
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync().ConfigureAwait(false);
        if (_loop is not null) await _loop.ConfigureAwait(false);
        try { await Task.WhenAll(_running.Values).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        catch (TimeoutException) { Log.Uncooperative(context.Logger); }
        _stop.Dispose();
    }
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "调度领取失败，下一轮继续。")]
        public static partial void Cycle(ILogger logger, Exception error);
        [LoggerMessage(Level = LogLevel.Error, Message = "作业 {RunId} 收尾失败，保留租约供恢复。")]
        public static partial void Execution(ILogger logger, Exception error, Guid runId);
        [LoggerMessage(Level = LogLevel.Error, Message = "作业 {RunId} 续租失败，取消本地执行。")]
        public static partial void Renewal(ILogger logger, Exception error, Guid runId);
        [LoggerMessage(Level = LogLevel.Warning, Message = "作业未在停机期限内响应取消；租约到期后可恢复，处理器必须业务幂等。")]
        public static partial void Uncooperative(ILogger logger);
    }
}
internal sealed record JobClaim(string TenantId, Guid Id, Guid RunId, long Generation, DateTimeOffset ScheduledAt, string Worker);
