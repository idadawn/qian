using System.Text.Json;
using Qian.Runtime;
using Qian.Scheduler.TestHost;

namespace Qian.Scheduler.Tests;

public sealed partial class SchedulerTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("expired")]
    [InlineData("initial-error")]
    public async Task 首次建run准备后初始续租_有效续足_过期和异常保留run(string mode)
    {
        await fixture.Reset();
        await fixture.Admin($"""
            CREATE TABLE public.sch_prepare_probe(run_id uuid PRIMARY KEY,lease_until timestamptz NOT NULL,preparing_at timestamptz NOT NULL,prepared_at timestamptz);
            GRANT SELECT,INSERT,UPDATE ON public.sch_prepare_probe TO qian_app;
            CREATE SEQUENCE plugin_qian_scheduler.initial_renew_calls;
            GRANT USAGE ON SEQUENCE plugin_qian_scheduler.initial_renew_calls TO qian_app;
            CREATE FUNCTION plugin_qian_scheduler.delay_first_prepare() RETURNS trigger LANGUAGE plpgsql AS $$
            DECLARE deadline timestamptz;
            BEGIN
              INSERT INTO public.sch_prepare_probe(run_id,lease_until,preparing_at)
                SELECT NEW.id,lease_until,clock_timestamp() FROM plugin_qian_scheduler.job_triggers WHERE id=NEW.trigger_id AND tenant_id=NEW.tenant_id;
              SELECT lease_until INTO deadline FROM public.sch_prepare_probe WHERE run_id=NEW.id;
              -- 以真实数据库租约作参照：valid 留 1.5 秒（小于首次周期的 2 秒），expired 越过到期点。
              -- 不修改租约或时钟，也不把进程其它准备耗时再叠加一段固定 sleep。
              IF '{mode}'='valid' THEN PERFORM pg_sleep(greatest(0,extract(epoch FROM (deadline-clock_timestamp()))-1.5));
              ELSIF '{mode}'='expired' THEN PERFORM pg_sleep(greatest(0,extract(epoch FROM (deadline-clock_timestamp()))+0.2));
              END IF;
              UPDATE public.sch_prepare_probe SET prepared_at=clock_timestamp() WHERE run_id=NEW.id;
              RETURN NEW;
            END $$;
            CREATE TRIGGER delay_first_prepare BEFORE INSERT ON plugin_qian_scheduler.job_runs
              FOR EACH ROW EXECUTE FUNCTION plugin_qian_scheduler.delay_first_prepare();
            CREATE FUNCTION plugin_qian_scheduler.fail_initial_renew() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF '{mode}'='initial-error' AND NEW.generation=OLD.generation AND NEW.lease_owner=OLD.lease_owner
                AND NEW.lease_until>OLD.lease_until AND NOT EXISTS(SELECT 1 FROM public.sch_probe_attempts WHERE run_id=NEW.active_run_id)
              THEN
                IF nextval('plugin_qian_scheduler.initial_renew_calls')=1
                THEN RAISE EXCEPTION 'expected initial renewal failure' USING ERRCODE='40001'; END IF;
              END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER fail_initial_renew BEFORE UPDATE OF lease_until ON plugin_qian_scheduler.job_triggers
              FOR EACH ROW EXECUTE FUNCTION plugin_qian_scheduler.fail_initial_renew();
            """);
        try
        {
            await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
            await using var worker = await WorkerProcess.Start(fixture.Config, TenantA);
            var phases = new Dictionary<string, DateTimeOffset> { ["workerReady"] = DateTimeOffset.UtcNow };
            try
            {
                using (producer.Tenant.BeginScope(TenantA))
                {
                    var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
                    await caller.Scheduler().ScheduleOnce(caller, "prepare-delay", DateTimeOffset.UtcNow, Target,
                        JsonSerializer.SerializeToElement(new { mode = "normal", delayMs = 3500 }), new JobOptions { Timeout = TimeSpan.FromMinutes(1) }, Ct);
                }
                await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='succeeded'") == 1, TimeSpan.FromSeconds(15));
                using var preparation = JsonDocument.Parse(await fixture.Scalar<string>("SELECT to_json(p)::text FROM public.sch_prepare_probe p"));
                var row = preparation.RootElement;
                var preparedAt = row.GetProperty("prepared_at").GetDateTimeOffset();
                var originalExpiry = row.GetProperty("lease_until").GetDateTimeOffset();
                var attempts = await fixture.Scalar<int>("SELECT attempts FROM plugin_qian_scheduler.job_runs");
                var handlers = await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts");
                var effects = await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects");
                var initialRenewCalls = await fixture.Scalar<long>("SELECT CASE WHEN is_called THEN last_value ELSE 0 END FROM plugin_qian_scheduler.initial_renew_calls");
                var handlerStarted = await fixture.Scalar<DateTime>("SELECT started FROM public.sch_probe_attempts ORDER BY id LIMIT 1");
                SchedulerFixture.Evidence("prepare-" + mode, new { preparation = row.Clone(), attempts, handlers, effects, handlerStarted, initialRenewCalls });
                if (mode == "expired")
                {
                    Assert.True(preparedAt >= originalExpiry);
                    Assert.True(handlerStarted >= preparedAt.UtcDateTime, "初始租约失效时不能进入第一轮处理器。");
                }
                else Assert.True(preparedAt < originalExpiry, "有效/异常边界在准备完成时必须仍有初始租约。");
                if (mode == "valid") Assert.True(originalExpiry - preparedAt < TimeSpan.FromSeconds(2));
                if (mode == "initial-error") Assert.Equal(2, initialRenewCalls);
                Assert.Equal(1, effects);
                Assert.Equal(mode == "valid" ? 1 : 2, attempts);
                Assert.Equal(1, handlers);
                Assert.Equal(row.GetProperty("run_id").GetGuid(), await fixture.Scalar<Guid>("SELECT id FROM plugin_qian_scheduler.job_runs"));
                Assert.Equal(1, await fixture.Scalar<long>("SELECT run_count FROM plugin_qian_scheduler.job_triggers"));
            }
            catch
            {
                phases["failed"] = DateTimeOffset.UtcNow;
                await fixture.CaptureFailure("prepare-" + mode, phases, null, [worker]);
                throw;
            }
        }
        finally
        {
            await fixture.Admin("DROP TRIGGER delay_first_prepare ON plugin_qian_scheduler.job_runs; DROP FUNCTION plugin_qian_scheduler.delay_first_prepare(); DROP TRIGGER fail_initial_renew ON plugin_qian_scheduler.job_triggers; DROP FUNCTION plugin_qian_scheduler.fail_initial_renew(); DROP SEQUENCE plugin_qian_scheduler.initial_renew_calls; DROP TABLE public.sch_prepare_probe;");
        }
    }
}
