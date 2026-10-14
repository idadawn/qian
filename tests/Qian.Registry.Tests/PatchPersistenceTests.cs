using System.Data.Common;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Qian.Capabilities;
using Qian.Data;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Testing.Containers;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static JsonElement Patch(string json) => JsonSerializer.Deserialize<JsonElement>(json);
    private static readonly ScopeKey TenantA = new("tenant", "a");
    private static readonly ScopeKey TenantB = new("tenant", "b");

    [Fact]
    public async Task 真实受限应用角色_租户读写隔离并与文件patch保留相同字段语义()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        var repository = Repository(host, tenant);
        await AssertCode(PlatformErrors.Unauthenticated, () => repository.ListAsync(TenantA, Ct));
        using (tenant.BeginScope("a"))
        {
            var first = await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","plugin":"qian.files@1.0.0","config":{"nested":{"value":1}},"disabled":false,"x-origin":{"note":"opaque"}}"""), 0, "operator-a", Ct);
            Assert.Equal(1, first.Revision);
            Assert.False(first.Patch.GetProperty("disabled").GetBoolean());
            Assert.Equal("opaque", first.Patch.GetProperty("x-origin").GetProperty("note").GetString());
            await AssertCode(PlatformErrors.NotFound, () => repository.ListAsync(TenantB, Ct));
        }
        using (tenant.BeginScope("b"))
        {
            Assert.Empty(await repository.ListAsync(TenantB, Ct));
            await repository.PutAsync(TenantB, "files", Patch("""{"id":"files","config":{},"note":"b-only"}"""), 0, "operator-b", Ct);
        }
        using (tenant.BeginScope("a"))
        {
            var row = Assert.Single(await repository.ListAsync(TenantA, Ct));
            Assert.Equal("a", row.TenantId); Assert.Equal("operator-a", row.UpdatedBy);
            var omitted = await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{}}"""), 1, "operator-a", Ct);
            Assert.False(omitted.Patch.TryGetProperty("disabled", out _));
            Assert.False(omitted.Patch.TryGetProperty("x-origin", out _));
        }
        await using var app = new NpgsqlConnection(postgres.AppConnectionString); await app.OpenAsync(Ct);
        await using (var flags = new NpgsqlCommand("SELECT rolsuper OR rolbypassrls FROM pg_roles WHERE rolname = current_user", app)) Assert.Equal(false, await flags.ExecuteScalarAsync(Ct));
        await using (var none = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_registry.scope_patches", app)) Assert.Equal(0L, await none.ExecuteScalarAsync(Ct));
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', 'a', false)", app)) await set.ExecuteNonQueryAsync(Ct);
        await using (var own = new NpgsqlCommand("SELECT tenant_id FROM plugin_qian_registry.scope_patches", app)) Assert.Equal("a", await own.ExecuteScalarAsync(Ct));
        await using var other = new NpgsqlCommand("UPDATE plugin_qian_registry.scope_patches SET tenant_id = 'b' WHERE tenant_id = 'a'", app);
        var denied = await Assert.ThrowsAsync<PostgresException>(() => other.ExecuteNonQueryAsync(Ct)); Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, denied.SqlState);
    }

    [Fact]
    public async Task 撤销后重建修订继续增长_旧客户端不能覆盖新条目_重复相同内容无额外写入()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var current = tenant.BeginScope("a"); var repository = Repository(host, tenant);
        var initial = Patch("""{"id":"files","config":{"value":1},"note":"not-for-logs"}""");
        var first = await repository.PutAsync(TenantA, "files", initial, 0, "actor", Ct);
        var repeated = await repository.PutAsync(TenantA, "files", initial, first.Revision, "actor", Ct);
        Assert.Equal(first.Revision, repeated.Revision); Assert.Equal(first.UpdatedAt, repeated.UpdatedAt);
        Assert.DoesNotContain("not-for-logs", first.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, await repository.RemoveAsync(TenantA, "files", 1, "actor", Ct));
        Assert.Null(await repository.GetAsync(TenantA, "files", Ct)); Assert.Empty(await repository.ListAsync(TenantA, Ct));
        await AssertCode(RegistryErrors.RevisionConflict, () => repository.PutAsync(TenantA, "files", initial, 1, "actor", Ct));
        var recreated = await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{"value":3}}"""), 0, "actor", Ct);
        Assert.Equal(3, recreated.Revision);
        await AssertCode(RegistryErrors.RevisionConflict, () => repository.RemoveAsync(TenantA, "files", 1, "actor", Ct));
        Assert.Equal(3, (await repository.GetAsync(TenantA, "files", Ct))!.Patch.GetProperty("config").GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task 两个独立工作单元同时提交_数据库只接受一个预期修订(bool existing)
    {
        var tenant = new AsyncLocalTenantContext(); var barrier = new ReadBarrier();
        using var host = Host(tenant, barrier); await host.StartAsync(Ct); await Prepare(host);
        using var current = tenant.BeginScope("a");
        var firstRepository = Repository(host, tenant); var secondRepository = Repository(host, tenant);
        if (existing) await firstRepository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{}}"""), 0, "initial", Ct);
        barrier.Enabled = true;
        var writes = await Task.WhenAll(Write(firstRepository, 1), Write(secondRepository, 2));
        Assert.Equal(2, barrier.Reads); barrier.Enabled = false;
        Assert.Single(writes, value => value is null); Assert.Single(writes, value => value == "REGISTRY_REVISION_CONFLICT");
        var saved = Assert.Single(await firstRepository.ListAsync(TenantA, Ct)); Assert.Equal(existing ? 2 : 1, saved.Revision);

        async Task<string?> Write(PostgresPatchRepository repository, int value)
        {
            try
            {
                await repository.PutAsync(TenantA, "files", JsonSerializer.SerializeToElement(new { id = "files", config = new { value } }), existing ? 1 : 0, "actor", Ct);
                return null;
            }
            catch (QianErrorException error) { return error.Error.Code.Code; }
        }
    }

    [Fact]
    public async Task patch语法和路径身份被共用校验拒绝_错误不包含输入值()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var current = tenant.BeginScope("a"); var repository = Repository(host, tenant);
        foreach (var invalid in new[]
        {
            """{"id":"files","config":{},"tenantId":"DO-NOT-LEAK"}""",
            """{"id":"other","config":{}}""",
            """{"id":"files","scope":"tenant:b","config":{}}""",
            """{"id":"files","config":{"value":1,"value":2}}""",
            """{"id":"files","disabled":"DO-NOT-LEAK"}"""
        })
        {
            var error = await Assert.ThrowsAsync<QianErrorException>(() => repository.PutAsync(TenantA, "files", Patch(invalid), 0, "actor", Ct));
            Assert.Equal("INPUT_INVALID", error.Error.Code.Code); Assert.NotEmpty(error.Error.Errors);
            Assert.All(error.Error.Errors, field => Assert.StartsWith("registry/scope-patches.json#", field.Pointer, StringComparison.Ordinal));
            Assert.DoesNotContain("DO-NOT-LEAK", error.ToString(), StringComparison.Ordinal);
        }
        Assert.Empty(await repository.ListAsync(TenantA, Ct));
    }

    [Fact]
    public async Task 提交后真实PG通知只含修订定位信息_通知失败时整笔写入回滚()
    {
        var tenant = new AsyncLocalTenantContext(); var failure = new NotifyFailure();
        using var host = Host(tenant, failure); await host.StartAsync(Ct); await Prepare(host);
        using var current = tenant.BeginScope("a"); var repository = Repository(host, tenant);
        await using var listener = new NpgsqlConnection(postgres.AppConnectionString); await listener.OpenAsync(Ct);
        await using (var listen = new NpgsqlCommand("LISTEN " + PostgresPatchRepository.ChangeChannel, listener)) await listen.ExecuteNonQueryAsync(Ct);
        var received = new List<string>(); listener.Notification += (_, message) => received.Add(message.Payload);
        await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{"value":"DO-NOT-BROADCAST"},"note":"PRIVATE-NOTE"}"""), 0, "actor", Ct);
        Assert.True(await listener.WaitAsync(5000, Ct));
        var payload = Assert.Single(received); Assert.DoesNotContain("DO-NOT-BROADCAST", payload, StringComparison.Ordinal); Assert.DoesNotContain("PRIVATE-NOTE", payload, StringComparison.Ordinal);
        var change = Patch(payload); Assert.Equal(1, change.GetProperty("revision").GetInt32()); Assert.False(change.GetProperty("removed").GetBoolean());
        received.Clear(); failure.Enabled = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{"value":"rollback"}}"""), 1, "actor", Ct));
        failure.Enabled = false;
        Assert.False(await listener.WaitAsync(300, Ct)); Assert.Empty(received);
        var kept = await repository.GetAsync(TenantA, "files", Ct); Assert.Equal(1, kept!.Revision); Assert.Equal("DO-NOT-BROADCAST", kept.Patch.GetProperty("config").GetProperty("value").GetString());
        Assert.Equal(2, await repository.RemoveAsync(TenantA, "files", 1, "actor", Ct)); Assert.True(await listener.WaitAsync(5000, Ct));
        Assert.True(Patch(Assert.Single(received)).GetProperty("removed").GetBoolean());
    }

    [Fact]
    public async Task 数据库patch作为最后一层_租户A整体替换配置_租户B与文件原件不变()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var profile = new TestProfile();
        var repository = Repository(host, tenant);
        var composer = new RegistryProfileComposer(repository, new ProfileLoader(new SurfaceValidator()), profile.Options);
        using (tenant.BeginScope("a"))
        {
            await repository.PutAsync(TenantA, "settings", Patch("""{"id":"settings","config":{"port":23}}"""), 0, "operator-a", Ct);
            var result = await composer.LoadAsync(TenantA, Ct); Assert.True(result.IsValid);
            var local = Assert.Single(result.Snapshot!.Entries, e => e.Id == "settings" && e.Scope == "tenant:a");
            Assert.Equal(23, local.Config.GetProperty("port").GetInt32());
            Assert.Equal("default", local.Config.GetProperty("marker").GetString());
            Assert.False(local.Config.TryGetProperty("obsolete", out _));
            Assert.Equal("scope-patch", result.Snapshot.Layers[local.Layer].Kind);
            Assert.Equal(3, Assert.Single(result.Snapshot.Entries, e => e.Id == "settings" && e.Scope == "global").Config.GetProperty("port").GetInt32());
        }
        using (tenant.BeginScope("b"))
        {
            var result = await composer.LoadAsync(TenantB, Ct); Assert.True(result.IsValid);
            Assert.DoesNotContain(result.Snapshot!.Entries, e => e.Scope == "tenant:a");
            Assert.Equal(3, Assert.Single(result.Snapshot.Entries, e => e.Id == "settings").Config.GetProperty("port").GetInt32());
        }
        Assert.Equal(profile.OriginalPatch, File.ReadAllText(Path.Combine(profile.Directory, "profiles/test/qian.patch.yml")));
    }

    [Fact]
    public async Task 禁用与撤销的真库层保留隐藏定位_无效组件配置不能产出可运行快照()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var profile = new TestProfile(); using var current = tenant.BeginScope("a");
        var repository = Repository(host, tenant);
        var composer = new RegistryProfileComposer(repository, new ProfileLoader(new SurfaceValidator()), profile.Options);
        await repository.PutAsync(TenantA, "settings", Patch("""{"id":"settings","disabled":true}"""), 0, "actor", Ct);
        var disabled = await composer.LoadAsync(TenantA, Ct); Assert.True(disabled.IsValid);
        var hidden = Assert.Single(disabled.Snapshot!.DisabledEntries);
        Assert.Equal("tenant:a", hidden.Scope); Assert.Equal("settings", hidden.Id);
        Assert.Equal("registry/scope-patches.json#/0", hidden.SourceLocation);
        Assert.Single(disabled.Snapshot.Entries, e => e.Id == "settings" && e.Scope == "global");
        await repository.RemoveAsync(TenantA, "settings", 1, "actor", Ct);
        var restored = await composer.LoadAsync(TenantA, Ct); Assert.True(restored.IsValid); Assert.Empty(restored.Snapshot!.DisabledEntries);
        // 内部仓储只检查 patch 格式；服务准入必须在写入前调用同一个装配器。
        await repository.PutAsync(TenantA, "settings", Patch("""{"id":"settings","config":{"port":0}}"""), 0, "actor", Ct);
        var invalid = await composer.LoadAsync(TenantA, Ct); Assert.False(invalid.IsValid); Assert.Null(invalid.Snapshot);
        Assert.Contains(invalid.Diagnostics, d => d.Error.Pointer == "registry/scope-patches.json#/0/config/port");
    }

    [Fact]
    public async Task 服务准入在事务内拒绝非法配置_保留修订与原始配置_不发通知()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct); await Prepare(host);
        using var profile = new TestProfile(); using var current = tenant.BeginScope("a");
        var repository = Repository(host, tenant);
        var writer = new RegistryPatchWriter(repository, new RegistryProfileComposer(repository, new ProfileLoader(new SurfaceValidator()), profile.Options));
        await AssertCode(PlatformErrors.InputInvalid, () => writer.PutAsync(TenantA, "settings", Patch("""{"id":"settings","config":{"port":0}}"""), 0, "actor", Ct));
        Assert.Empty(await repository.ListAsync(TenantA, Ct));
        var first = await writer.PutAsync(TenantA, "settings", Patch("""{"id":"settings","config":{"port":29}}"""), 0, "actor", Ct);
        await using var listener = new NpgsqlConnection(postgres.AppConnectionString); await listener.OpenAsync(Ct);
        await using (var listen = new NpgsqlCommand("LISTEN " + PostgresPatchRepository.ChangeChannel, listener)) await listen.ExecuteNonQueryAsync(Ct);
        var invalid = await Assert.ThrowsAsync<QianErrorException>(() => writer.PutAsync(TenantA, "settings", Patch("""{"id":"settings","config":{"port":29,"secret":"DO-NOT-LEAK"}}"""), first.Revision, "actor", Ct));
        Assert.Equal("INPUT_INVALID", invalid.Error.Code.Code); Assert.DoesNotContain("DO-NOT-LEAK", invalid.ToString(), StringComparison.Ordinal);
        Assert.False(await listener.WaitAsync(300, Ct));
        var kept = await repository.GetAsync(TenantA, "settings", Ct); Assert.Equal(first.Revision, kept!.Revision);
        Assert.Equal(first.UpdatedAt, kept.UpdatedAt); Assert.Equal(29, kept.Patch.GetProperty("config").GetProperty("port").GetInt32());
        Assert.Equal(2, await writer.RemoveAsync(TenantA, "settings", 1, "actor", Ct));
    }

    [Fact]
    public async Task 不同条目并发准入不会共同创建同scope重复Provider()
    {
        var tenant = new AsyncLocalTenantContext(); var otherTenant = new AsyncLocalTenantContext(); var contention = new ScopeLockContention();
        using var host = Host(tenant, contention); using var otherHost = Host(otherTenant, contention);
        await host.StartAsync(Ct); await otherHost.StartAsync(Ct); await Prepare(host);
        using var profile = new TestProfile(); using var current = tenant.BeginScope("a");
        var repository = Repository(host, tenant); var otherRepository = Repository(otherHost, otherTenant);
        var writer = new RegistryPatchWriter(repository, new RegistryProfileComposer(repository, new ProfileLoader(new SurfaceValidator()), profile.Options));
        var otherWriter = new RegistryPatchWriter(otherRepository, new RegistryProfileComposer(otherRepository, new ProfileLoader(new SurfaceValidator()), profile.Options));
        var first = Write("first", writer, tenant);
        await contention.FirstAcquired.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        var second = Write("second", otherWriter, otherTenant);
        var results = await Task.WhenAll(first, second);
        Assert.Equal(2, contention.Attempts); Assert.Single(results, result => result is null); Assert.Single(results, result => result == "INPUT_INVALID");
        Assert.Single(await repository.ListAsync(TenantA, Ct));
        async Task<string?> Write(string id, RegistryPatchWriter target, ITenantContext context)
        {
            using var request = context.BeginScope("a");
            try
            {
                await target.PutAsync(TenantA, id, JsonSerializer.SerializeToElement(new { id, plugin = "test.settings", config = new { port = 31 } }), 0, "actor", Ct);
                return null;
            }
            catch (QianErrorException error) { return error.Error.Code.Code; }
        }
    }

    [Fact]
    public async Task 独立宿主监听真实通知_断线重新读库_不继承请求租户且释放后停止()
    {
        var writerTenant = new AsyncLocalTenantContext(); var readerTenant = new AsyncLocalTenantContext();
        using var writerHost = Host(writerTenant); using var readerHost = Host(readerTenant);
        await writerHost.StartAsync(Ct); await readerHost.StartAsync(Ct); await Prepare(writerHost);
        var repository = Repository(writerHost, writerTenant); var reader = Repository(readerHost, readerTenant);
        var updates = System.Threading.Channels.Channel.CreateUnbounded<int>();
        var inheritedTenant = false; var calls = 0;
        using var startupRequest = readerTenant.BeginScope("b");
        await using var listener = await PostgresPatchListener.StartAsync(readerHost.Services.GetRequiredService<IDataService>(), readerTenant, async ct =>
        {
            if (readerTenant.TenantId is not null) inheritedTenant = true;
            using var scope = readerTenant.BeginScope("a");
            var rows = await reader.ListAsync(TenantA, ct); Interlocked.Increment(ref calls);
            await updates.Writer.WriteAsync(rows.SingleOrDefault()?.Revision ?? 0, ct);
        }, NullLogger.Instance, Ct);
        Assert.Equal("b", readerTenant.TenantId); Assert.False(inheritedTenant);
        using var current = writerTenant.BeginScope("a");
        await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{}}"""), 0, "actor", Ct);
        await Observed(1);
        var disconnected = listener.BackendProcessId; Assert.True(disconnected > 0);
        await using (var admin = new NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(Ct);
            await using var terminate = new NpgsqlCommand("SELECT pg_terminate_backend(@id)", admin);
            terminate.Parameters.AddWithValue("id", disconnected); Assert.Equal(true, await terminate.ExecuteScalarAsync(Ct));
        }
        await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{"value":2}}"""), 1, "actor", Ct);
        await Observed(2); Assert.True(listener.BackendProcessId > 0); Assert.NotEqual(disconnected, listener.BackendProcessId); Assert.False(inheritedTenant);
        await repository.RemoveAsync(TenantA, "files", 2, "actor", Ct); await Observed(0);
        await listener.DisposeAsync(); Assert.Equal(0, listener.BackendProcessId);
        var stoppedCalls = Volatile.Read(ref calls);
        await repository.PutAsync(TenantA, "files", Patch("""{"id":"files","config":{}}"""), 0, "actor", Ct);
        await Task.Delay(150, Ct); Assert.Equal(stoppedCalls, Volatile.Read(ref calls));

        async Task Observed(int revision)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(5));
            while (await updates.Reader.ReadAsync(deadline.Token) != revision) { }
        }
    }

    [Fact]
    public async Task 监听器前置上下文不支持作用域时立即失败_不无限等待就绪()
    {
        var tenant = new UnsupportedTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Ct); deadline.CancelAfter(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAsync<NotSupportedException>(() => PostgresPatchListener.StartAsync(
            host.Services.GetRequiredService<IDataService>(), tenant, _ => ValueTask.CompletedTask, NullLogger.Instance, deadline.Token));
    }

    [Fact]
    public async Task 重新读取失败会重试_日志不包含底层异常中的配置原文()
    {
        var tenant = new AsyncLocalTenantContext();
        using var host = Host(tenant); await host.StartAsync(Ct);
        var logs = new RecordingLogger(); var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        await using var listener = await PostgresPatchListener.StartAsync(host.Services.GetRequiredService<IDataService>(), tenant, _ =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException("DO-NOT-LEAK-CONNECTION-OR-CONFIG");
            recovered.TrySetResult(); return ValueTask.CompletedTask;
        }, logs, Ct);
        await recovered.Task.WaitAsync(TimeSpan.FromSeconds(5), Ct);
        Assert.Contains(logs.Messages, message => message.Contains("InvalidOperationException", StringComparison.Ordinal));
        Assert.All(logs.Messages, message => Assert.DoesNotContain("DO-NOT-LEAK", message, StringComparison.Ordinal));
    }

    private sealed class RecordingLogger : ILogger
    {
        internal System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => Messages.Enqueue(formatter(state, exception));
    }

    private sealed class UnsupportedTenantContext : ITenantContext
    {
        public bool IsMultiTenant => true;
        public string? TenantId => null;
    }

    private sealed class ScopeLockContention : DbCommandInterceptor
    {
        internal TaskCompletionSource FirstAcquired { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _secondAttempt = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _attempts;
        internal int Attempts => Volatile.Read(ref _attempts);
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) && Interlocked.Increment(ref _attempts) == 2) _secondAttempt.TrySetResult();
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("pg_advisory_xact_lock", StringComparison.Ordinal) && Attempts == 1)
            {
                FirstAcquired.TrySetResult();
                await _secondAttempt.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }

    private sealed class TestProfile : IDisposable
    {
        internal string Directory { get; } = Path.Combine(Path.GetTempPath(), "qian-registry-profile-" + Guid.NewGuid().ToString("N"));
        internal string OriginalPatch { get; }
        internal ProfileLoadOptions Options => new() { WorkingDirectory = Directory, Profile = "test", IncludeHomePatch = false };
        internal TestProfile()
        {
            var root = new DirectoryInfo(AppContext.BaseDirectory);
            while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
            var template = Path.Combine(root!.FullName, "tck/profiles/base");
            foreach (var source in System.IO.Directory.EnumerateFiles(template, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(Directory, Path.GetRelativePath(template, source));
                System.IO.Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(source, target);
            }
            OriginalPatch = File.ReadAllText(Path.Combine(Directory, "profiles/test/qian.patch.yml"));
        }
        public void Dispose() => System.IO.Directory.Delete(Directory, true);
    }

    private IHost Host(ITenantContext tenant, IInterceptor? interceptor = null, bool capabilityChecks = false, Action<IServiceCollection>? services = null)
    {
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder();
        builder.Services.AddSingleton(tenant); builder.Services.AddKunEventBus();
        builder.Services.AddSingleton(ErrorCatalog.CreateDefault());
        if (capabilityChecks)
        {
            // 与 platform 宿主相同的登记期能力契约检查：组件经 SDK ProvideCapability 登记时即检查。
            builder.Services.AddSingleton<ISurfaceReader, SurfaceValidator>(); builder.Services.AddSingleton<IToolContractHasher, ToolContractHasher>();
            builder.Services.AddQianCapabilities();
        }
        builder.Services.AddQianData<RegistryDbContext>((_, options) =>
        {
            options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(RegistryDbContext.Schema);
            if (interceptor is not null) options.AddInterceptors(interceptor);
        });
        services?.Invoke(builder.Services);
        return builder.Build();
    }
    private static PostgresPatchRepository Repository(IHost host, ITenantContext tenant) => new(host.Services.GetRequiredService<IDataService>(), tenant, new SurfaceValidator());
    private async Task Prepare(IHost host)
    {
        using var scope = host.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<RegistryDbContext>();
        var model = db.GetService<IDesignTimeModel>().Model;
        var operations = db.GetService<IMigrationsModelDiffer>().GetDifferences(null, model.GetRelationalModel());
        var commands = db.GetService<IMigrationsSqlGenerator>().Generate(operations, model);
        var sql = string.Join('\n', commands.Select(command => command.CommandText));
        Assert.Contains("FORCE ROW LEVEL SECURITY", sql, StringComparison.Ordinal);
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var create = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_registry CASCADE;\n" + sql + "\nGRANT USAGE ON SCHEMA plugin_qian_registry TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_registry TO qian_app;", admin);
        await create.ExecuteNonQueryAsync(Ct);
    }
    private static async Task AssertCode(ErrorCode code, Func<Task> action)
    {
        var error = await Assert.ThrowsAsync<QianErrorException>(action); Assert.Equal(code.Code, error.Error.Code.Code);
    }
    private sealed class ReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _reads;
        internal bool Enabled { get; set; }
        internal int Reads => Volatile.Read(ref _reads);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal) && command.CommandText.Contains("scope_patches", StringComparison.Ordinal))
            {
                if (Interlocked.Increment(ref _reads) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
            }
            return result;
        }
    }
    private sealed class NotifyFailure : DbCommandInterceptor
    {
        internal bool Enabled { get; set; }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Enabled && command.CommandText.Contains("pg_notify", StringComparison.Ordinal)) throw new InvalidOperationException("Fixture: interrupt before notification and commit.");
            return ValueTask.FromResult(result);
        }
    }
}
