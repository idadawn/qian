using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Scheduler.TestHost;

namespace Qian.Scheduler.Tests;

public sealed partial class SchedulerTests(SchedulerFixture fixture) : IClassFixture<SchedulerFixture>
{
    private static readonly string TenantA = Guid.NewGuid().ToString("D"), TenantB = Guid.NewGuid().ToString("D");
    private static readonly JsonElement Parameters = JsonSerializer.SerializeToElement(new { mode = "normal", delayMs = 0 });
    private const string Target = "qian.scheduler.probe/run";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task 三独立进程_两租户100分钟cron_连续两轮各恰100次且真实处理器没有重复()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        await using var a = await WorkerProcess.Start(fixture.Config, TenantA, TenantB); await using var b = await WorkerProcess.Start(fixture.Config, TenantA, TenantB); await using var c = await WorkerProcess.Start(fixture.Config, TenantA, TenantB);
        Assert.Equal(3, new[] { a.Id, b.Id, c.Id }.Distinct().Count());
        // 避免造数跨过分钟边界；保留真实分钟与默认一秒领取语义。
        if (DateTimeOffset.UtcNow.Second > 40) await Task.Delay(TimeSpan.FromSeconds(61 - DateTimeOffset.UtcNow.Second), Ct);
        for (var i = 0; i < 100; i++)
        {
            var tenant = i % 2 == 0 ? TenantA : TenantB; using var scope = producer.Tenant.BeginScope(tenant);
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", tenant));
            await caller.Scheduler().ScheduleCron(caller, "minute-" + i, "0 * * * * ?", Target, Parameters, cancellationToken: Ct);
        }
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='succeeded'") >= 200, TimeSpan.FromSeconds(135));
        Assert.Equal(200, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
        Assert.Equal(200, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
        Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM (SELECT scheduled_at FROM plugin_qian_scheduler.job_runs GROUP BY scheduled_at HAVING count(*)=100) t"));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT count(*) FROM (SELECT tenant_id,trigger_id,scheduled_at FROM plugin_qian_scheduler.job_runs GROUP BY 1,2,3 HAVING count(*)<>1) t"));
        Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM (SELECT tenant_id FROM public.sch_probe_effects GROUP BY tenant_id HAVING count(*)=100) t"));
        Assert.Equal(3, await fixture.Scalar<long>("SELECT count(DISTINCT process_id) FROM public.sch_probe_attempts"));
        using var minutes = JsonDocument.Parse(await fixture.Scalar<string>("SELECT json_agg(m)::text FROM (SELECT scheduled_at,count(*) AS runs FROM plugin_qian_scheduler.job_runs GROUP BY scheduled_at ORDER BY scheduled_at) m"));
        SchedulerFixture.Evidence("three-process-minute", new
        {
            processIds = new[] { a.Id, b.Id, c.Id },
            triggerCount = await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_triggers"),
            attempts = await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"),
            effects = await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"),
            minutes = minutes.RootElement.Clone()
        });
    }

    [Theory]
    [InlineData("hold-first", 0)]
    [InlineData("effect-then-hold", 1)]
    public async Task 真杀执行副本_到期后另一副本用同run接管_副作用幂等与执行尝试分别计数(string mode, long effectsBeforeKill)
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        await using var a = await WorkerProcess.Start(fixture.Config, TenantA); await using var b = await WorkerProcess.Start(fixture.Config, TenantA); await using var c = await WorkerProcess.Start(fixture.Config, TenantA);
        var phases = new Dictionary<string, DateTimeOffset> { ["workersReady"] = DateTimeOffset.UtcNow };
        JsonElement? claimBeforeKill = null;
        try
        {
            using (producer.Tenant.BeginScope(TenantA))
            {
                var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
                await caller.Scheduler().ScheduleOnce(caller, "crash", DateTimeOffset.UtcNow.AddSeconds(1), Target, JsonSerializer.SerializeToElement(new { mode, delayMs = 0 }), new JobOptions { Timeout = TimeSpan.FromMinutes(3), Misfire = MisfirePolicy.CatchUp }, Ct);
            }
            phases["scheduled"] = DateTimeOffset.UtcNow;
            await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts") == 1, TimeSpan.FromSeconds(10));
            phases["firstHandlerObserved"] = DateTimeOffset.UtcNow;
            await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects") == effectsBeforeKill, TimeSpan.FromSeconds(3));
            var pid = await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts LIMIT 1");
            var run = await fixture.Scalar<Guid>("SELECT run_id FROM public.sch_probe_attempts LIMIT 1");
            using var claim = JsonDocument.Parse(await fixture.Scalar<string>("SELECT to_json(t)::text FROM (SELECT lease_owner,lease_until,generation,active_run_id FROM plugin_qian_scheduler.job_triggers LIMIT 1) t"));
            claimBeforeKill = claim.RootElement.Clone();
            var firstLease = claim.RootElement.GetProperty("lease_until").GetDateTimeOffset();
            var originalOwner = claim.RootElement.GetProperty("lease_owner").GetString();
            Assert.Equal(1, claim.RootElement.GetProperty("generation").GetInt64());
            // 仅观察到 handler 的 INSERT 不足以证明仍持有租约。要求同一首次执行已真正续约，
            // 且数据库时钟下仍有活租约，才允许注入 Kill；已经自然恢复的 run 不能冒充被测故障。
            await SchedulerFixture.WaitUntil(async () =>
            {
                using var current = JsonDocument.Parse(await fixture.Scalar<string>("""
                    SELECT coalesce((SELECT to_json(s) FROM (
                      SELECT t.lease_owner,t.lease_until,t.generation,t.active_run_id,clock_timestamp() AS observed_at,r.status,r.attempts
                      FROM plugin_qian_scheduler.job_triggers t JOIN plugin_qian_scheduler.job_runs r ON r.id=t.active_run_id
                      LIMIT 1) s),'null'::json)::text
                    """));
                var row = current.RootElement;
                if (row.ValueKind == JsonValueKind.Null) return false;
                if (row.GetProperty("active_run_id").GetGuid() != run || row.GetProperty("lease_owner").GetString() != originalOwner ||
                    row.GetProperty("generation").GetInt64() != 1 || row.GetProperty("status").GetString() != "running" || row.GetProperty("attempts").GetInt32() != 1) return false;
                var until = row.GetProperty("lease_until").GetDateTimeOffset();
                if (until <= firstLease || until - row.GetProperty("observed_at").GetDateTimeOffset() < TimeSpan.FromSeconds(3)) return false;
                claimBeforeKill = row.Clone(); return true;
            }, TimeSpan.FromSeconds(10));
            phases["liveRenewedLeaseObserved"] = DateTimeOffset.UtcNow;
            var leaseUntil = claimBeforeKill.Value.GetProperty("lease_until").GetDateTimeOffset().UtcDateTime;
            phases["leaseObservedExpiresAt"] = new DateTimeOffset(leaseUntil);
            phases["beforeKill"] = DateTimeOffset.UtcNow;
            await new[] { a, b, c }.Single(worker => worker.Id == pid).Kill();
            phases["afterKill"] = DateTimeOffset.UtcNow;
            var before = leaseUntil - DateTime.UtcNow - TimeSpan.FromMilliseconds(300);
            if (before > TimeSpan.Zero) await Task.Delay(before, Ct);
            Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
            phases["beforeRecoveryWait"] = DateTimeOffset.UtcNow;
            await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='succeeded'") == 1, TimeSpan.FromSeconds(15));
            Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
            Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
            Assert.Equal(run, await fixture.Scalar<Guid>("SELECT id FROM plugin_qian_scheduler.job_runs"));
            Assert.Equal(2, await fixture.Scalar<int>("SELECT attempts FROM plugin_qian_scheduler.job_runs"));
            Assert.NotEqual(pid, await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts ORDER BY id DESC LIMIT 1"));
            SchedulerFixture.Evidence(mode, new
            {
                killedProcess = pid, recoveredProcess = await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts ORDER BY id DESC LIMIT 1"),
                runId = run, leaseExpiresAtUtc = leaseUntil, phases, claimBeforeKill,
                attempts = await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"),
                effects = await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"),
                runRows = await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs")
            });
        }
        catch
        {
            phases["failed"] = DateTimeOffset.UtcNow;
            await fixture.CaptureFailure(mode, phases, claimBeforeKill, [a, b, c]);
            throw;
        }
    }

    [Fact]
    public async Task 同业务键并发幂等_取消到点不执行_不同租户同键互不影响()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        var due = DateTimeOffset.UtcNow.AddSeconds(4);
        var ids = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = producer.Tenant.BeginScope(TenantA); var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            return (await caller.Scheduler().ScheduleOnce(caller, "same", due, Target, Parameters, cancellationToken: Ct)).Id;
        }));
        Assert.Single(ids.Distinct());
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            var conflict = await Assert.ThrowsAsync<QianErrorException>(() => caller.Scheduler().ScheduleOnce(caller, "same", due.AddMinutes(1), Target, Parameters, cancellationToken: Ct).AsTask());
            Assert.Equal("CONFLICT", conflict.Error.Code.Code);
            Assert.True(await caller.Scheduler().Cancel(caller, "same", Ct)); Assert.True(await caller.Scheduler().Cancel(caller, "same", Ct));
        }
        using (producer.Tenant.BeginScope(TenantB))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantB)); await caller.Scheduler().ScheduleOnce(caller, "same", due, Target, Parameters, cancellationToken: Ct);
        }
        await using var worker = await WorkerProcess.Start(fixture.Config, TenantA, TenantB);
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects") == 1, TimeSpan.FromSeconds(15));
        Assert.Equal(TenantB, await fixture.Scalar<string>("SELECT tenant_id FROM public.sch_probe_effects"));
        Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_definitions"));
    }

    [Fact]
    public async Task 真实目录准入与执行_拒绝任意URL_未挂载与restrict不执行缓存处理器()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        using var scope = producer.Tenant.BeginScope(TenantA); var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
        foreach (var target in new[] { "https://example.com/job", "http://localhost/run", "https://127.0.0.1/run", "file:///tmp/job", "//example.com/run", "qian.missing/run" })
            await Assert.ThrowsAsync<QianErrorException>(() => caller.Scheduler().ScheduleOnce(caller, "invalid", DateTimeOffset.UtcNow.AddMinutes(1), target, Parameters, cancellationToken: Ct).AsTask());
        var good = PostgresScheduler.Resolve(caller, Target); Assert.Equal("probe", good.Code);
        using (producer.Caller.Restrict(new ScopeKey("tenant", TenantA), registration => registration.Key != JobBindingRegistration.Key))
        {
            var error = await Assert.ThrowsAsync<QianErrorException>(() => caller.Scheduler().ScheduleOnce(caller, "hidden", DateTimeOffset.UtcNow, Target, Parameters, cancellationToken: Ct).AsTask());
            Assert.Equal("NOT_FOUND", error.Error.Code.Code);
        }
        await producer.Runtime.UnmountAsync("probe", ScopeKey.Global, Ct);
        Assert.Throws<ObjectDisposedException>(() => PostgresScheduler.Resolve(caller, Target));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_definitions"));
    }

    [Fact]
    public void 生成迁移模型与三表强制RLS一致_分钟和七段年cron不混为每秒()
    {
        using var db = new SchedulerDesignTimeFactory().CreateDbContext([]); Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript();
        foreach (var table in new[] { "job_definitions", "job_triggers", "job_runs" }) Assert.Contains("ALTER TABLE plugin_qian_scheduler." + table + " FORCE ROW LEVEL SECURITY", script, StringComparison.Ordinal);
        var now = new DateTimeOffset(2026, 10, 23, 12, 0, 12, TimeSpan.Zero);
        Assert.Equal(new DateTimeOffset(2026, 10, 23, 12, 1, 0, TimeSpan.Zero), JobSchedule.Next("0 * * * * ?", now));
        Assert.Equal(new DateTimeOffset(2027, 1, 1, 0, 0, 0, TimeSpan.Zero), JobSchedule.Next("0 0 0 1 1 ? 2027", now));
        Assert.Null(JobSchedule.Next("0 0 0 1 1 ? 2025", now));
        Assert.Throws<QianErrorException>(() => JobSchedule.Validate("* * * * *"));
        Assert.Equal(now.AddSeconds(15), SchedulerWorker.NextOccurrence("interval:50000000", now, now.AddSeconds(12)));
    }
}
