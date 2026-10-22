using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Qian.Data;
using Qian.Scheduler.TestHost;
using Qian.Testing.Containers;

namespace Qian.Scheduler.Tests;

public sealed class SchedulerFixture : IAsyncLifetime
{
    private static readonly JsonSerializerOptions EvidenceJson = new() { WriteIndented = true };
    public PostgresFixture Postgres { get; } = new();
    public HostConfig Config { get; private set; } = null!;
    public async ValueTask InitializeAsync()
    {
        await Postgres.InitializeAsync();
        var role = "sch_worker_" + Guid.NewGuid().ToString("N")[..12]; var owner = "sch_owner";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)); var ownerPassword = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin($"""
            CREATE ROLE "{role}" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';
            CREATE ROLE "{owner}" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{ownerPassword}';
            CREATE SCHEMA plugin_qian_scheduler AUTHORIZATION "{owner}";
            GRANT CONNECT,TEMPORARY ON DATABASE qian_test TO "{role}","{owner}";
            """);
        var ownerConnection = new NpgsqlConnectionStringBuilder(Postgres.AppConnectionString) { Username = owner, Password = ownerPassword }.ConnectionString;
        var services = new ServiceCollection(); services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); services.AddKunEventBus();
        services.AddQianData<SchedulerDbContext>((_, options) => options.UseNpgsql(ownerConnection).UseKunSchema(SchedulerDbContext.Schema));
        await using (var provider = services.BuildServiceProvider()) await provider.GetRequiredService<IDataMigrator>().MigrateAsync(TestContext.Current.CancellationToken);
        await Admin(SchedulerWorkerAccess.CreateMigrationSql(role) + """
            GRANT USAGE ON SCHEMA plugin_qian_scheduler TO qian_app;
            GRANT SELECT,INSERT,UPDATE,DELETE ON ALL TABLES IN SCHEMA plugin_qian_scheduler TO qian_app;
            CREATE TABLE public.sch_probe_attempts(id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,run_id uuid NOT NULL,tenant_id text NOT NULL,process_id int NOT NULL,started timestamptz NOT NULL,finished timestamptz);
            CREATE TABLE public.sch_probe_effects(run_id uuid PRIMARY KEY,tenant_id text NOT NULL,process_id int NOT NULL);
            CREATE TABLE public.sch_probe_tenants(id uuid PRIMARY KEY,status text NOT NULL);
            GRANT SELECT,INSERT,UPDATE ON public.sch_probe_attempts,public.sch_probe_effects,public.sch_probe_tenants TO qian_app;
            GRANT USAGE ON ALL SEQUENCES IN SCHEMA public TO qian_app;
            """);
        Config = new HostConfig(Postgres.AppConnectionString, new NpgsqlConnectionStringBuilder(Postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString, role);
    }
    public async Task Reset() => await Admin("TRUNCATE plugin_qian_scheduler.job_runs,plugin_qian_scheduler.job_triggers,plugin_qian_scheduler.job_definitions,public.sch_probe_attempts,public.sch_probe_effects,public.sch_probe_tenants;");
    public async Task Admin(string sql)
    { await using var db = new NpgsqlConnection(Postgres.ConnectionString); await db.OpenAsync(TestContext.Current.CancellationToken); await using var command = new NpgsqlCommand(sql, db); await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken); }
    public async Task<T> Scalar<T>(string sql)
    { await using var db = new NpgsqlConnection(Postgres.ConnectionString); await db.OpenAsync(TestContext.Current.CancellationToken); await using var command = new NpgsqlCommand(sql, db); return (T)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!; }
    public static async Task WaitUntil(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            if (await condition()) return;
            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
        Assert.True(await condition(), "等待真实状态超时：" + timeout);
    }
    public static void Evidence(string name, object result)
    {
        if (Environment.GetEnvironmentVariable("QIAN_TEST_EVIDENCE_DIR") is not { Length: > 0 } directory) return;
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "scheduler-" + name + ".json"), JsonSerializer.Serialize(result, EvidenceJson));
    }
    internal async Task CaptureFailure(string name, IReadOnlyDictionary<string, DateTimeOffset> phases, JsonElement? claimBeforeKill, WorkerProcess[] workers)
    {
        // 失败时先保留现场，再由 await using 释放 worker；不采集作业参数、连接串或 SQL 文本。
        object database;
        try
        {
            using var document = JsonDocument.Parse(await Scalar<string>("""
                SELECT json_build_object(
                  'capturedAtUtc',clock_timestamp(),
                  'triggers',(SELECT coalesce(json_agg(t),'[]'::json) FROM (
                    SELECT id,tenant_id,state,next_run_at,available_at,lease_owner,lease_until,generation,active_run_id,run_count
                    FROM plugin_qian_scheduler.job_triggers ORDER BY id) t),
                  'runs',(SELECT coalesce(json_agg(r),'[]'::json) FROM (
                    SELECT id,trigger_id,tenant_id,scheduled_at,started_at,finished_at,status,error_class,attempts,worker,generation
                    FROM plugin_qian_scheduler.job_runs ORDER BY id) r),
                  'handlerAttempts',(SELECT coalesce(json_agg(a),'[]'::json) FROM public.sch_probe_attempts a),
                  'effects',(SELECT coalesce(json_agg(e),'[]'::json) FROM public.sch_probe_effects e),
                  'sessions',(SELECT coalesce(json_agg(s),'[]'::json) FROM (
                    SELECT pid,usename,state,wait_event_type,wait_event,backend_start,xact_start,query_start
                    FROM pg_stat_activity WHERE datname=current_database() ORDER BY pid) s),
                  'locks',(SELECT coalesce(json_agg(l),'[]'::json) FROM (
                    SELECT pid,locktype,mode,granted,relation::regclass::text AS relation
                    FROM pg_locks WHERE database=(SELECT oid FROM pg_database WHERE datname=current_database()) ORDER BY pid,locktype) l)
                )::text
                """));
            database = document.RootElement.Clone();
        }
        catch (Exception error) { database = new { captureError = error.GetType().Name }; }
        var evidence = new { phases, claimBeforeKill, database, workers = workers.Select(worker => worker.Snapshot()).ToArray() };
        // MTP/TRX 保留 TestOutputHelper 输出；DiagnosticMessage 默认不会进入失败 artifact。
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(evidence, EvidenceJson));
        try { Evidence(name + "-failure", evidence); }
        catch (Exception error) { TestContext.Current.TestOutputHelper?.WriteLine("写入失败证据文件失败：" + error.GetType().Name); }
    }
    public ValueTask DisposeAsync() => Postgres.DisposeAsync();
}

internal sealed class WorkerProcess : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task _errors;
    private readonly StringBuilder _errorLog = new();
    private readonly Lock _logGate = new();
    private readonly string[] _redactions;
    private bool _logTruncated;
    public int Id => _process.Id;
    private WorkerProcess(Process process, HostConfig config)
    {
        _process = process;
        _redactions = new[] { config.Application, config.Worker, new NpgsqlConnectionStringBuilder(config.Application).Password,
            new NpgsqlConnectionStringBuilder(config.Worker).Password }.Where(value => !string.IsNullOrEmpty(value)).Select(value => value!).Distinct(StringComparer.Ordinal).ToArray();
        _errors = CaptureErrors(process.StandardError);
    }
    private string ErrorOutput { get { lock (_logGate) return _errorLog.ToString(); } }
    private async Task CaptureErrors(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            foreach (var value in _redactions) line = line.Replace(value, "[redacted]", StringComparison.Ordinal);
            lock (_logGate)
            {
                if (_errorLog.Length + line.Length < 1_048_576) _errorLog.AppendLine(line);
                else _logTruncated = true;
            }
        }
    }
    internal object Snapshot()
    {
        var exited = _process.HasExited;
        lock (_logGate) return new { processId = Id, hasExited = exited, exitCode = exited ? (int?)_process.ExitCode : null, stderr = _errorLog.ToString(), stderrTruncated = _logTruncated };
    }
    public static async Task<WorkerProcess> Start(HostConfig config, params string[] warmupTenants)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        var executable = Path.Combine(root!.FullName, "tests/Qian.Scheduler.TestHost/bin/Release/net10.0/Qian.Scheduler.TestHost.dll");
        var start = new ProcessStartInfo("dotnet") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        start.ArgumentList.Add(executable); start.ArgumentList.Add("--worker");
        var child = new WorkerProcess(Process.Start(start)!, config);
        await child._process.StandardInput.WriteLineAsync(JsonSerializer.Serialize(config with { WarmupTenants = warmupTenants })); await child._process.StandardInput.FlushAsync();
        var line = await child._process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30));
        if (line is null || !line.StartsWith("READY ", StringComparison.Ordinal))
        { await child.DisposeAsync(); throw new InvalidOperationException("worker 未就绪：" + child.ErrorOutput); }
        return child;
    }
    public async Task Kill()
    { if (!_process.HasExited) { _process.Kill(entireProcessTree: true); await _process.WaitForExitAsync(); } }
    public async Task<string> Stop()
    {
        if (!_process.HasExited)
        {
            await _process.StandardInput.WriteLineAsync("stop"); await _process.StandardInput.FlushAsync();
            // 此入口不允许用 Kill 兜底；回归必须证明子进程正常 Dispose 并退出。
            await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
        }
        await _errors; var errors = ErrorOutput;
        Assert.Equal(0, _process.ExitCode);
        return errors;
    }
    public async ValueTask DisposeAsync()
    {
        if (!_process.HasExited)
        {
            await _process.StandardInput.WriteLineAsync("stop"); await _process.StandardInput.FlushAsync();
            try { await _process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10)); }
            catch (TimeoutException) { await Kill(); }
        }
        await _errors; var errors = ErrorOutput;
        if (errors.Length > 0) TestContext.Current.SendDiagnosticMessage(errors);
        _process.Dispose();
    }
}
