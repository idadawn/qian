using System.Text.Json;
using Qian.Runtime;
using Qian.Scheduler.TestHost;
using static Qian.Scheduler.Tests.SchedulerScenario;

namespace Qian.Scheduler.Tests;

// 租约交接：真杀执行副本后到期接管、正常 Stop 后另一真实 worker 接管同一 run。
public sealed class SchedulerTakeoverTests(SchedulerFixture fixture) : IClassFixture<SchedulerFixture>
{
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
            claimBeforeKill = await fixture.RenewedLease(run);
            var originalOwner = claimBeforeKill.Value.GetProperty("lease_owner").GetString();
            phases["liveRenewedLeaseObserved"] = DateTimeOffset.UtcNow;
            phases["beforeKill"] = DateTimeOffset.UtcNow;
            await new[] { a, b, c }.Single(worker => worker.Id == pid).Kill();
            phases["afterKill"] = DateTimeOffset.UtcNow;
            // 原 owner 已退出，续租不再变化；立即从同一数据库快照证明还未接管并冻结实际到期点。
            var frozenLease = await fixture.LeaseSnapshot(run);
            var leaseUntil = SchedulerFixture.AssertLiveFirstLease(frozenLease, run, originalOwner).UtcDateTime;
            phases["leaseObservedExpiresAt"] = new DateTimeOffset(leaseUntil);
            phases["beforeRecoveryWait"] = DateTimeOffset.UtcNow;
            await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='succeeded'") == 1, TimeSpan.FromSeconds(15));
            Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
            Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
            Assert.Equal(run, await fixture.Scalar<Guid>("SELECT id FROM plugin_qian_scheduler.job_runs"));
            Assert.Equal(2, await fixture.Scalar<int>("SELECT attempts FROM plugin_qian_scheduler.job_runs"));
            Assert.NotEqual(pid, await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts ORDER BY id DESC LIMIT 1"));
            var recoveredStartedAt = await fixture.Scalar<DateTime>("SELECT started_at FROM plugin_qian_scheduler.job_runs");
            Assert.True(recoveredStartedAt >= leaseUntil, "接管的持久 StartedAt 不得早于冻结的租约到期点。");
            SchedulerFixture.Evidence(mode, new
            {
                killedProcess = pid, recoveredProcess = await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts ORDER BY id DESC LIMIT 1"),
                runId = run, leaseExpiresAtUtc = leaseUntil, recoveredStartedAt, phases, claimBeforeKill, frozenLease,
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

    [Theory]
    [InlineData("hold-first", 0)]
    [InlineData("effect-then-hold", 1)]
    public async Task 正常Stop无需异常收尾_保留同run供另一真实worker接管(string mode, long effectsBeforeStop)
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        await using var first = await WorkerProcess.Start(fixture.Config, TenantA);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            await caller.Scheduler().ScheduleOnce(caller, "normal-stop", DateTimeOffset.UtcNow, Target,
                JsonSerializer.SerializeToElement(new { mode, delayMs = 0 }), new JobOptions { Timeout = TimeSpan.FromMinutes(3) }, Ct);
        }
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts") == 1, TimeSpan.FromSeconds(10));
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects") == effectsBeforeStop, TimeSpan.FromSeconds(3));
        var run = await fixture.Scalar<Guid>("SELECT active_run_id FROM plugin_qian_scheduler.job_triggers");
        // 第二副本先完成真实预热，不能把它的启动时间算进“到期前尚未接管”观测。
        await using var second = await WorkerProcess.Start(fixture.Config, TenantA);
        var beforeStop = await fixture.RenewedLease(run);
        var owner = beforeStop.GetProperty("lease_owner").GetString();
        var stoppedProcess = first.Id;
        var errors = await first.Stop();
        Assert.DoesNotContain("ObjectDisposedException", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("收尾失败", errors, StringComparison.Ordinal);
        var frozenLease = await fixture.LeaseSnapshot(run);
        var leaseUntil = SchedulerFixture.AssertLiveFirstLease(frozenLease, run, owner).UtcDateTime;
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='succeeded'") == 1, TimeSpan.FromSeconds(15));
        Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
        Assert.Equal(run, await fixture.Scalar<Guid>("SELECT id FROM plugin_qian_scheduler.job_runs"));
        Assert.Equal(2, await fixture.Scalar<int>("SELECT attempts FROM plugin_qian_scheduler.job_runs"));
        Assert.Equal(second.Id, await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts ORDER BY id DESC LIMIT 1"));
        var recoveredStartedAt = await fixture.Scalar<DateTime>("SELECT started_at FROM plugin_qian_scheduler.job_runs");
        Assert.True(recoveredStartedAt >= leaseUntil, "接管的持久 StartedAt 不得早于冻结的租约到期点。");
        SchedulerFixture.Evidence("normal-stop-" + mode, new { stoppedProcess, recoveredProcess = second.Id, runId = run, leaseExpiresAtUtc = leaseUntil, recoveredStartedAt, beforeStop, frozenLease, attempts = 2, effects = 1, shutdownErrors = errors });
    }
}
