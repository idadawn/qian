using System.Text.Json;
using Qian.Runtime;
using Qian.Scheduler.TestHost;
using static Qian.Scheduler.Tests.SchedulerScenario;

namespace Qian.Scheduler.Tests;

// 分钟验收：三个独立 worker 进程、两个租户、100 个真实每分钟 cron，连续两个真实分钟各恰 100 次。
// 时长由分钟边界决定（CI 上 90–143 秒），是本程序集的长杆，所以独占一个类与一个容器，和其他主题类并行（S1-QA-022）。
public sealed class SchedulerMinuteTests(SchedulerFixture fixture) : IClassFixture<SchedulerFixture>
{
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
}
