using System.Text.Json;
using Npgsql;
using Qian.Data;
using Qian.Runtime;
using Qian.Scheduler.TestHost;

namespace Qian.Scheduler.Tests;

public sealed partial class SchedulerTests
{
    [Theory]
    [InlineData("hold-first", 0)]
    [InlineData("effect-then-hold", 1)]
    public async Task 正常Stop无需异常收尾_保留同run供另一真实worker接管(string mode, long effectsBeforeStop)
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        await using var first = await WorkerProcess.Start(fixture.Config);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            await caller.Scheduler().ScheduleOnce(caller, "normal-stop", DateTimeOffset.UtcNow, Target,
                JsonSerializer.SerializeToElement(new { mode, delayMs = 0 }), new JobOptions { Timeout = TimeSpan.FromMinutes(3) }, Ct);
        }
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts") == 1, TimeSpan.FromSeconds(10));
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects") == effectsBeforeStop, TimeSpan.FromSeconds(3));
        var run = await fixture.Scalar<Guid>("SELECT active_run_id FROM plugin_qian_scheduler.job_triggers");
        var owner = await fixture.Scalar<string>("SELECT lease_owner FROM plugin_qian_scheduler.job_triggers");
        var generation = await fixture.Scalar<long>("SELECT generation FROM plugin_qian_scheduler.job_triggers");
        var stoppedProcess = first.Id;
        var errors = await first.Stop();
        Assert.DoesNotContain("ObjectDisposedException", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("收尾失败", errors, StringComparison.Ordinal);
        Assert.Equal("ready", await fixture.Scalar<string>("SELECT state FROM plugin_qian_scheduler.job_triggers"));
        Assert.Equal(run, await fixture.Scalar<Guid>("SELECT active_run_id FROM plugin_qian_scheduler.job_triggers"));
        Assert.Equal(owner, await fixture.Scalar<string>("SELECT lease_owner FROM plugin_qian_scheduler.job_triggers"));
        Assert.Equal(generation, await fixture.Scalar<long>("SELECT generation FROM plugin_qian_scheduler.job_triggers"));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT run_count FROM plugin_qian_scheduler.job_triggers"));
        Assert.Equal("running", await fixture.Scalar<string>("SELECT status FROM plugin_qian_scheduler.job_runs"));
        var leaseUntil = await fixture.Scalar<DateTime>("SELECT lease_until FROM plugin_qian_scheduler.job_triggers");
        await using var second = await WorkerProcess.Start(fixture.Config);
        var beforeExpiry = leaseUntil - DateTime.UtcNow - TimeSpan.FromMilliseconds(300);
        if (beforeExpiry > TimeSpan.Zero) await Task.Delay(beforeExpiry, Ct);
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='succeeded'") == 1, TimeSpan.FromSeconds(15));
        Assert.Equal(2, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
        Assert.Equal(run, await fixture.Scalar<Guid>("SELECT id FROM plugin_qian_scheduler.job_runs"));
        Assert.Equal(2, await fixture.Scalar<int>("SELECT attempts FROM plugin_qian_scheduler.job_runs"));
        Assert.Equal(second.Id, await fixture.Scalar<int>("SELECT process_id FROM public.sch_probe_attempts ORDER BY id DESC LIMIT 1"));
        SchedulerFixture.Evidence("normal-stop-" + mode, new { stoppedProcess, recoveredProcess = second.Id, runId = run, leaseExpiresAtUtc = leaseUntil, attempts = 2, effects = 1, shutdownErrors = errors });
    }

    [Fact]
    public async Task 过期租约拒绝旧执行者完成_新owner代次也拒绝旧续租和推进()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            await caller.Scheduler().ScheduleOnce(caller, "fencing", DateTimeOffset.UtcNow, Target, Parameters, cancellationToken: Ct);
        }
        var settings = new SchedulerSettings(fixture.Config.WorkerRole, 3, 1);
        await using var first = new SchedulerWorker(producer.Caller, producer.Tenant, settings);
        await using var second = new SchedulerWorker(producer.Caller, producer.Tenant, settings);
        var old = Assert.Single(await first.ClaimAsync(1, Ct));
        Assert.Empty(await second.ClaimAsync(1, Ct));
        await Task.Delay(TimeSpan.FromMilliseconds(3200), Ct);
        using (producer.Tenant.BeginScope(TenantA)) Assert.False(await first.CompleteAsync(old, "once", new JobOptions(), "succeeded", null, Ct));
        var current = Assert.Single(await second.ClaimAsync(1, Ct));
        Assert.Equal(old.RunId, current.RunId); Assert.Equal(old.Generation + 1, current.Generation); Assert.NotEqual(old.Worker, current.Worker);
        using (producer.Tenant.BeginScope(TenantA))
        {
            Assert.False(await first.CompleteAsync(old, "once", new JobOptions(), "failed", "old", Ct));
            await using var db = new NpgsqlConnection(fixture.Config.Application); await db.OpenAsync(Ct); await using var transaction = await db.BeginTransactionAsync(Ct);
            await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id',@tenant,true)", db, transaction)) { set.Parameters.AddWithValue("tenant", TenantA); await set.ExecuteScalarAsync(Ct); }
            await using var renew = new NpgsqlCommand("UPDATE plugin_qian_scheduler.job_triggers SET lease_until=now()+interval '30 seconds' WHERE tenant_id=@tenant AND id=@id AND generation=@generation AND lease_owner=@owner AND lease_until>now()", db, transaction);
            renew.Parameters.AddWithValue("tenant", TenantA); renew.Parameters.AddWithValue("id", old.Id); renew.Parameters.AddWithValue("generation", old.Generation); renew.Parameters.AddWithValue("owner", old.Worker);
            Assert.Equal(0, await renew.ExecuteNonQueryAsync(Ct));
        }
        Assert.Equal(current.Worker, await fixture.Scalar<string>("SELECT lease_owner FROM plugin_qian_scheduler.job_triggers"));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT run_count FROM plugin_qian_scheduler.job_triggers"));
    }

    [Fact]
    public async Task 普通应用原始SQL不能读改他租户三表_触发器组合外键拒绝跨租户伪造()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        Guid b;
        using (producer.Tenant.BeginScope(TenantB))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantB));
            b = (await caller.Scheduler().ScheduleOnce(caller, "b", DateTimeOffset.UtcNow.AddMinutes(1), Target, Parameters, cancellationToken: Ct)).Id;
        }
        // 三张表都有 B 的真实行；不能以空运行表的零行结果冒充隔离证据。
        await fixture.Admin($"INSERT INTO plugin_qian_scheduler.job_runs(tenant_id,id,trigger_id,scheduled_at,started_at,status,attempts,worker,generation) VALUES ('{TenantB}','{Guid.NewGuid()}','{b}',now(),now(),'running',1,'fixture',1);");
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs"));
        await using var db = new NpgsqlConnection(fixture.Config.Application); await db.OpenAsync(Ct); await using var transaction = await db.BeginTransactionAsync(Ct);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id',@tenant,true)", db, transaction)) { set.Parameters.AddWithValue("tenant", TenantA); await set.ExecuteScalarAsync(Ct); }
        foreach (var table in new[] { "job_definitions", "job_triggers", "job_runs" })
        {
            await using var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_scheduler." + table, db, transaction);
            Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
        }
        foreach (var statement in new[] { "UPDATE plugin_qian_scheduler.job_definitions SET business_key='hijacked'", "UPDATE plugin_qian_scheduler.job_triggers SET state='archived'", "UPDATE plugin_qian_scheduler.job_runs SET status='failed'" })
        {
            await using var update = new NpgsqlCommand(statement + " WHERE tenant_id=@other", db, transaction); update.Parameters.AddWithValue("other", TenantB);
            Assert.Equal(0, await update.ExecuteNonQueryAsync(Ct));
        }
        await using (var alter = new NpgsqlCommand("UPDATE plugin_qian_scheduler.job_triggers SET state='archived' WHERE id=@id", db, transaction))
        { alter.Parameters.AddWithValue("id", b); Assert.Equal(0, await alter.ExecuteNonQueryAsync(Ct)); }
        await using var forge = new NpgsqlCommand("INSERT INTO plugin_qian_scheduler.job_triggers(tenant_id,id,state,next_run_at,available_at,run_count,generation) VALUES (@tenant,@id,'ready',now(),now(),0,0)", db, transaction);
        forge.Parameters.AddWithValue("tenant", TenantA); forge.Parameters.AddWithValue("id", b);
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, (await Assert.ThrowsAsync<PostgresException>(() => forge.ExecuteNonQueryAsync(Ct))).SqlState);
        await transaction.RollbackAsync(Ct);
        await using var crossTenant = new NpgsqlCommand("INSERT INTO plugin_qian_scheduler.job_triggers(tenant_id,id,state,next_run_at,available_at,run_count,generation) VALUES (@tenant,@id,'ready',now(),now(),0,0)", db);
        crossTenant.Parameters.AddWithValue("tenant", TenantB); crossTenant.Parameters.AddWithValue("id", Guid.NewGuid());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => crossTenant.ExecuteNonQueryAsync(Ct))).SqlState);
    }

    [Fact]
    public async Task 专用worker只有领取列权限_SET_ROLE及成员关系不能冒充全局登录()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            await caller.Scheduler().ScheduleOnce(caller, "permission", DateTimeOffset.UtcNow.AddMinutes(1), Target, Parameters, cancellationToken: Ct);
        }
        await using (var worker = new NpgsqlConnection(fixture.Config.Worker))
        {
            await worker.OpenAsync(Ct); await SchedulerWorkerAccess.ValidateConnectionAsync(worker, fixture.Config.WorkerRole, Ct);
            foreach (var sql in new[] { "SELECT * FROM plugin_qian_scheduler.job_definitions", "SELECT * FROM plugin_qian_scheduler.job_runs", "UPDATE plugin_qian_scheduler.job_triggers SET tenant_id='other'", "DELETE FROM plugin_qian_scheduler.job_triggers" })
            { await using var command = new NpgsqlCommand(sql, worker); Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct))).SqlState); }
        }
        foreach (var permission in new[] { "UPDATE (tenant_id) ON plugin_qian_scheduler.job_triggers", "UPDATE (next_run_at) ON plugin_qian_scheduler.job_triggers", "INSERT ON plugin_qian_scheduler.job_definitions", "UPDATE (status) ON plugin_qian_scheduler.job_runs" })
        {
            await fixture.Admin($"GRANT {permission} TO \"{fixture.Config.WorkerRole}\";");
            try
            {
                await using var expanded = new NpgsqlConnection(fixture.Config.Worker); await expanded.OpenAsync(Ct);
                await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => SchedulerWorkerAccess.ValidateConnectionAsync(expanded, fixture.Config.WorkerRole, Ct));
            }
            finally { await fixture.Admin($"REVOKE {permission} FROM \"{fixture.Config.WorkerRole}\";"); }
        }
        await fixture.Admin($"GRANT \"{fixture.Config.WorkerRole}\" TO qian_app;");
        try
        {
            await using var db = new NpgsqlConnection(fixture.Config.Application); await db.OpenAsync(Ct);
            await using (var set = new NpgsqlCommand($"SET ROLE \"{fixture.Config.WorkerRole}\"", db)) await set.ExecuteNonQueryAsync(Ct);
            await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => SchedulerWorkerAccess.ValidateConnectionAsync(db, fixture.Config.WorkerRole, Ct));
            await using var read = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_scheduler.job_triggers", db); Assert.Equal(0L, await read.ExecuteScalarAsync(Ct));
            await using var realWorker = new NpgsqlConnection(fixture.Config.Worker); await realWorker.OpenAsync(Ct);
            await Assert.ThrowsAsync<QianDataMisconfiguredException>(() => SchedulerWorkerAccess.ValidateConnectionAsync(realWorker, fixture.Config.WorkerRole, Ct));
        }
        finally { await fixture.Admin($"REVOKE \"{fixture.Config.WorkerRole}\" FROM qian_app;"); }
        var nonlogin = "sch_nonlogin";
        await fixture.Admin($"CREATE ROLE {nonlogin} NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS;");
        await Assert.ThrowsAsync<PostgresException>(() => fixture.Admin(SchedulerWorkerAccess.CreateMigrationSql(nonlogin)));
    }

    [Fact]
    public async Task 创建时有效但到期前被冻结的租户不执行处理器()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            await caller.Scheduler().ScheduleOnce(caller, "tenant-frozen", DateTimeOffset.UtcNow.AddSeconds(2), Target, Parameters, new JobOptions { MaxAttempts = 1 }, Ct);
        }
        await fixture.Admin($"INSERT INTO public.sch_probe_tenants(id,status) VALUES ('{TenantA}','suspended');");
        await using var worker = await WorkerProcess.Start(fixture.Config);
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='failed'") == 1, TimeSpan.FromSeconds(10));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts"));
    }

    [Fact]
    public async Task 超时重试保留runId_重试上限和取消终态不会被旧执行者覆盖()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        await using var worker = await WorkerProcess.Start(fixture.Config);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA));
            await caller.Scheduler().ScheduleOnce(caller, "timeout", DateTimeOffset.UtcNow, Target, JsonSerializer.SerializeToElement(new { mode = "normal", delayMs = 5000 }),
                new JobOptions { Timeout = TimeSpan.FromMilliseconds(200), RetryDelay = TimeSpan.Zero, MaxAttempts = 2 }, Ct);
        }
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='failed' AND error_class='timeout' AND attempts=2") == 1, TimeSpan.FromSeconds(12));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(DISTINCT run_id) FROM public.sch_probe_attempts"));
        using (producer.Tenant.BeginScope(TenantB))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantB));
            await caller.Scheduler().ScheduleOnce(caller, "cancel-running", DateTimeOffset.UtcNow, Target, JsonSerializer.SerializeToElement(new { mode = "normal", delayMs = 15000 }), cancellationToken: Ct);
            await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>($"SELECT count(*) FROM public.sch_probe_attempts WHERE tenant_id='{TenantB}'") == 1, TimeSpan.FromSeconds(10));
            Assert.True(await caller.Scheduler().Cancel(caller, "cancel-running", Ct));
        }
        await Task.Delay(TimeSpan.FromSeconds(3), Ct);
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='cancelled'"));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_effects"));
    }

    [Fact]
    public async Task misfire跳过不调用_补一次合并积压_补齐按计划发生点且同定义不重叠()
    {
        await fixture.Reset(); await using var producer = await ProbeHost.CreateAsync(fixture.Config, false, Ct);
        using (producer.Tenant.BeginScope(TenantA))
        {
            var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA)); var past = DateTimeOffset.UtcNow.AddSeconds(-5);
            await caller.Scheduler().ScheduleOnce(caller, "skip", past, Target, Parameters, new JobOptions { Misfire = MisfirePolicy.Skip, MisfireGrace = TimeSpan.Zero }, Ct);
            await caller.Scheduler().ScheduleInterval(caller, "fire-once", past, TimeSpan.FromHours(1), Target, Parameters, new JobOptions { Misfire = MisfirePolicy.FireOnce }, Ct);
            await caller.Scheduler().ScheduleInterval(caller, "catch-up", past, TimeSpan.FromSeconds(1), Target, JsonSerializer.SerializeToElement(new { mode = "normal", delayMs = 50 }), new JobOptions { Misfire = MisfirePolicy.CatchUp }, Ct);
        }
        await using var a = await WorkerProcess.Start(fixture.Config); await using var b = await WorkerProcess.Start(fixture.Config);
        await SchedulerFixture.WaitUntil(async () => await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs r JOIN plugin_qian_scheduler.job_definitions d ON r.tenant_id=d.tenant_id AND r.trigger_id=d.id WHERE d.business_key='catch-up' AND r.status='succeeded'") >= 4, TimeSpan.FromSeconds(15));
        using (producer.Tenant.BeginScope(TenantA))
        { var caller = producer.Caller.Scoped(new ScopeKey("tenant", TenantA)); await caller.Scheduler().Cancel(caller, "catch-up", Ct); }
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs WHERE status='skipped'"));
        Assert.Equal(1, await fixture.Scalar<long>("SELECT count(*) FROM plugin_qian_scheduler.job_runs r JOIN plugin_qian_scheduler.job_definitions d ON r.tenant_id=d.tenant_id AND r.trigger_id=d.id WHERE d.business_key='fire-once'"));
        Assert.Equal(0, await fixture.Scalar<long>("SELECT count(*) FROM public.sch_probe_attempts a JOIN public.sch_probe_attempts b ON a.id<b.id JOIN plugin_qian_scheduler.job_runs ra ON ra.id=a.run_id JOIN plugin_qian_scheduler.job_runs rb ON rb.id=b.run_id WHERE ra.trigger_id=rb.trigger_id AND a.finished>b.started"));
    }
}
