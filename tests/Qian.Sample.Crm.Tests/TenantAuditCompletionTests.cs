using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Qian.Audit;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class TenantOperationsTests
{
    [Fact]
    public async Task 扫描对模型隐藏但调度服务可用_每次成功失败均有成对平台审计_拒绝调用不留记录()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        await using var management = await ManagementHost(business, platform);
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        await loader.MountAsync([new PluginDefinition(new PluginEntry("scan-scheduler", "qian.scheduler", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), new ScanSchedulerProbe(), [])], Ct);
        var scheduler = loader.GetContext("scan-scheduler", ScopeKey.Global);
        var scan = source.Inject<IExecutableCapability>(CapabilityBindingRegistration.Key, "qian.tenancy/tenant.scan-expired");
        Assert.False(scan.Contract.Metadata.GetProperty("agent").GetProperty("expose").GetBoolean());
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET valid_until=now()-interval '1 second' WHERE id='{A}'::uuid;");
        var input = JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), limit = 100 });
        Assert.Equal(1, (await scan.InvokeAsync(scheduler, input, Ct)).GetProperty("expired").GetInt32());
        Assert.Equal(0, (await scan.InvokeAsync(scheduler, input, Ct)).GetProperty("expired").GetInt32());
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET valid_until=now()-interval '1 second' WHERE id='{B}'::uuid;");
        const string sensitive = "测试敏感内容不得进入审计";
        await Admin("""
            CREATE FUNCTION plugin_qian_tenancy.reject_expiry() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN RAISE EXCEPTION '测试敏感内容不得进入审计'; END $$;
            CREATE TRIGGER reject_expiry BEFORE UPDATE ON plugin_qian_tenancy.tenants
              FOR EACH ROW WHEN (NEW.status = 'grace') EXECUTE FUNCTION plugin_qian_tenancy.reject_expiry();
            """);
        try { await Assert.ThrowsAsync<DbUpdateException>(() => scan.InvokeAsync(scheduler, input, Ct).AsTask()); }
        finally { await Admin("DROP TRIGGER reject_expiry ON plugin_qian_tenancy.tenants; DROP FUNCTION plugin_qian_tenancy.reject_expiry();"); }
        Assert.Equal(TenantStatus.Active, (await source.Tenancy().GetByIdAsync(Guid.Parse(B), Ct))!.Status);
        await Assert.ThrowsAsync<QianErrorException>(() => scan.InvokeAsync(loader.GetContext("registry", ScopeKey.Global), input, Ct).AsTask());
        await Assert.ThrowsAsync<QianErrorException>(() => scan.InvokeAsync(scheduler,
            JsonSerializer.SerializeToElement(new { idempotencyKey = Guid.CreateVersion7(), limit = 0 }), Ct).AsTask());
        var entries = (await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct))
            .Where(entry => entry.Record.CapabilityId == scan.Contract.Id).Select(entry => entry.Record).ToArray();
        Assert.Equal(6, entries.Length);
        Assert.Equal(["started", "success", "started", "success", "started", "failure"], entries.Select(record => record.Result));
        Assert.Equal(1, entries[1].Payload.GetProperty("expired").GetInt32());
        Assert.Equal(0, entries[3].Payload.GetProperty("expired").GetInt32());
        Assert.False(entries[5].Payload.TryGetProperty("expired", out _));
        Assert.All(entries, record =>
        {
            Assert.Equal("qian.scheduler", record.ActorId); Assert.Equal("service", record.ActorType); Assert.Equal("job", record.Channel);
            Assert.DoesNotContain(sensitive, record.Payload.GetRawText(), StringComparison.Ordinal);
        });
        var attempts = entries.GroupBy(record => record.Payload.GetProperty("attempt").GetGuid()).ToArray();
        Assert.Equal(3, attempts.Length);
        foreach (var attempt in attempts)
        { Assert.Equal(2, attempt.Count()); Assert.Single(attempt.Select(record => record.TraceId).Distinct()); Assert.Single(attempt.Select(record => record.InputHash).Distinct()); }
        Assert.True((await source.Audit().VerifyPlatformAsync(source, Ct)).Valid);
    }

    [Fact]
    public async Task 设置已提交后补充审计真实写入失败仍返回保存结果_旧修订重试冲突_恢复后审计链连续()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        await using var management = await ManagementHost(business, platform);
        var logs = new CompletionLogs(); business.App.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs);
        var source = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
        using var created = await client.PostAsJsonAsync("/api/tenancy/tenants", new CreateTenant("audittest", "审计故障测试")
            { Administrator = new(Guid.CreateVersion7(), "owner-audit") }, Ct);
        Assert.True(created.StatusCode == HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        var id = (await created.Content.ReadFromJsonAsync<TenantDescriptor>(Ct))!.Id;
        var path = "/api/tenancy/tenants/" + id + "/settings/qian.audit";
        var settings = JsonSerializer.SerializeToElement(new { chain = new { store = "pg" } });
        await Admin("""
            CREATE FUNCTION plugin_qian_audit.reject_settings_completion() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
              IF NEW.capability_id = 'qian.tenancy/settings.write' THEN RAISE EXCEPTION '测试敏感内容不得进入日志'; END IF;
              RETURN NEW;
            END $$;
            CREATE TRIGGER reject_settings_completion BEFORE INSERT ON plugin_qian_audit.audit_entries
              FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_settings_completion();
            """);
        try
        {
            using var put = await client.PutAsJsonAsync(path, new UpdateTenantSettings(settings, 0), Ct);
            Assert.True(put.StatusCode == HttpStatusCode.OK, await put.Content.ReadAsStringAsync(Ct));
            var saved = (await put.Content.ReadFromJsonAsync<TenantSettingsDocument>(Ct))!;
            Assert.Equal(1, saved.Revision); Assert.Equal("pg", saved.Config.GetProperty("chain").GetProperty("store").GetString());
            using var get = await client.GetAsync(path, Ct);
            Assert.Equal(1, (await get.Content.ReadFromJsonAsync<TenantSettingsDocument>(Ct))!.Revision);
            await AssertHttp(await client.PutAsJsonAsync(path, new UpdateTenantSettings(settings, 0), Ct), HttpStatusCode.Conflict, "REGISTRY_REVISION_CONFLICT");
            var log = Assert.Single(logs.Entries); Assert.Equal("qian.tenancy/settings.write", log["Capability"]);
            Assert.Equal(id.ToString(), log["Tenant"]); Assert.Equal("success", log["Result"]); Assert.Equal("qian.audit", log["Resource"]);
            Assert.All(log.Values, value => Assert.DoesNotContain("测试敏感内容", value?.ToString() ?? "", StringComparison.Ordinal));
            using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(id.ToString()))
            {
                Assert.DoesNotContain(await source.Audit().ReadAsync(0, 100, cancellationToken: Ct), record => record.Record.CapabilityId == "qian.tenancy/settings.write");
                Assert.True((await source.Audit().VerifyAsync(Ct)).Valid);
            }
            Assert.Contains(await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct), entry =>
                entry.Record.Payload.TryGetProperty("permission", out var permission) && permission.GetString() == "qian.tenancy:tenant:edit");
        }
        finally
        {
            await Admin("DROP TRIGGER reject_settings_completion ON plugin_qian_audit.audit_entries; DROP FUNCTION plugin_qian_audit.reject_settings_completion();");
        }
        using var recovered = await client.PutAsJsonAsync(path, new UpdateTenantSettings(settings, 1), Ct);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(1, (await recovered.Content.ReadFromJsonAsync<TenantSettingsDocument>(Ct))!.Revision);
        using (business.App.Services.GetRequiredService<ITenantContext>().BeginScope(id.ToString()))
        {
            var completion = Assert.Single(await source.Audit().ReadAsync(0, 100, cancellationToken: Ct), entry => entry.Record.CapabilityId == "qian.tenancy/settings.write");
            Assert.Equal(1, completion.Record.Payload.GetProperty("revision").GetInt32());
            Assert.True((await source.Audit().VerifyAsync(Ct)).Valid);
        }
        Assert.Single(logs.Entries);
    }
}

public abstract partial class CustomerCommandTests
{
    private protected sealed class ScanSchedulerProbe : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) => ValueTask.CompletedTask;
    }
    private protected sealed class CompletionLogs : ILoggerProvider
    {
        internal ConcurrentQueue<IReadOnlyDictionary<string, object?>> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Entries);
        public void Dispose() { }
        private sealed class Sink(string category, ConcurrentQueue<IReadOnlyDictionary<string, object?>> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel level, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == "Qian.Tenancy.AuditCompletion" && eventId.Id == 1002 && state is IEnumerable<KeyValuePair<string, object?>> values)
                { Assert.Null(exception); Assert.Equal(LogLevel.Error, level); entries.Enqueue(values.ToDictionary(pair => pair.Key, pair => pair.Value)); }
            }
        }
    }
}
