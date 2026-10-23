using System.Net;
using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Qian.Config.Apollo;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

public sealed class FeatureFlagManagementTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public void 元数据与执行同源_write_confirm_固定输入不接受目标或凭据()
    {
        var metadata = typeof(FeatureFlagManagementPlugin).Assembly.GetCustomAttributes<QianCodeMetadataAttribute>()
            .Select(attribute => ((IComponentCodeMetadataFactory)Activator.CreateInstance(attribute.FactoryType)!).Create())
            .Single(item => item.ComponentId == "qian.config.flags");
        Assert.Equal(["Audit", "AuthzEnforcement", "Config", "NativeData"], metadata.Inject);
        Assert.Equal(["FeatureFlagDrafts"], metadata.Provides);
        var definition = metadata.Capabilities.Single();
        Assert.Equal("write", definition.Metadata.GetProperty("sideEffect").GetString());
        Assert.Equal("confirm", definition.Metadata.GetProperty("policyHint").GetString());
        Assert.True(definition.Metadata.GetProperty("idempotent").GetBoolean());
        Assert.True(definition.Metadata.GetProperty("agent").GetProperty("requiresConfirmation").GetBoolean());
        var properties = definition.InputSchema["properties"]!.AsObject().Select(pair => pair.Key).Order().ToArray();
        Assert.Equal(["confirmed", "idempotencyKey", "name", "rule"], properties);
    }

    [Fact]
    public async Task 主体确认权限与开始审计分别拒绝_固定Portal请求与敏感回执_卸载旧引用失效()
    {
        await using var portal = await Portal.Start();
        await using var host = await FlagManagementTestHost.Create(postgres, portal.Uri, "trusted-app", "random-fixture-token", Ct);
        host.User.Value = null;
        await Error("UNAUTHENTICATED", () => host.Drafts.SetAsync(Request(), Ct));
        host.User.Value = FlagManagementTestHost.MutableUser.PlatformPrincipal(Guid.NewGuid().ToString("D"));
        await Error("AUTHZ_DENIED", () => host.Drafts.SetAsync(Request(), Ct));
        host.User.Value = FlagManagementTestHost.MutableUser.PlatformPrincipal(kind: PrincipalKind.Agent);
        await Error("AUTHZ_DENIED", () => host.Drafts.SetAsync(Request(), Ct));
        host.User.Value = FlagManagementTestHost.MutableUser.PlatformPrincipal();
        await Error("INPUT_INVALID", () => host.Drafts.SetAsync(Request(confirmed: false), Ct));
        host.Authz.Allow = false;
        await Error("AUTHZ_DENIED", () => host.Drafts.SetAsync(Request(), Ct));
        host.Authz.Allow = true; host.Audit.FailStart = true;
        await Assert.ThrowsAsync<InvalidOperationException>(() => host.Drafts.SetAsync(Request(), Ct).AsTask());
        Assert.Equal(0, portal.Requests);
        host.Audit.FailStart = false; host.Audit.FailCompletion = true;
        var result = await host.Drafts.SetAsync(Request(), Ct);
        Assert.Equal("pending-publish", result.Status);
        Assert.Equal("/openapi/v1/envs/UAT/apps/trusted-app/clusters/default/namespaces/qian.common.flags/items/flag.preview?createIfNotExists=true", portal.Path);
        using var body = JsonDocument.Parse(portal.Body!);
        Assert.Equal("flag.preview", body.RootElement.GetProperty("key").GetString());
        Assert.Equal("apollo", body.RootElement.GetProperty("dataChangeLastModifiedBy").GetString());
        Assert.Equal("random-fixture-token", portal.Authorization);
        Assert.All(host.Audit.Records, row => Assert.DoesNotContain("random-fixture-token", row.Payload.GetRawText(), StringComparison.Ordinal));
        var stale = host.Drafts;
        await host.Loader.UnmountAsync("flags", ScopeKey.Global, Ct);
        await Error("NOT_FOUND", () => stale.SetAsync(Request(), Ct));
        Assert.Equal(1, portal.Requests);
    }

    [Fact]
    public async Task 持久幂等跨实例重放不改较新草稿_同key不同输入主体冲突_数据库角色受限()
    {
        await using var portal = await Portal.Start();
        await using var first = await FlagManagementTestHost.Create(postgres, portal.Uri, "trusted-app", "test-token", Ct);
        var request = Request();
        var original = await first.Drafts.SetAsync(request, Ct);
        var audit = first.Audit.Records.Single(record => record.Result == "pending-publish");
        Assert.Equal(request.IdempotencyKey, audit.Payload.GetProperty("idempotencyKey").GetGuid());
        Assert.Equal(64, audit.Payload.GetProperty("targetHash").GetString()!.Length);
        var newer = Request(enabled: false);
        await first.Drafts.SetAsync(newer, Ct);
        await using var second = await FlagManagementTestHost.Create(postgres, portal.Uri, "trusted-app", "test-token", Ct,
            reuseConnection: first.PlatformConnection, reuseRole: first.Role);
        Assert.Equal(original.RuleHash, (await second.Drafts.SetAsync(request, Ct)).RuleHash);
        Assert.Equal(2, portal.Requests);
        using (var submitted = JsonDocument.Parse(portal.Body!))
        using (var policy = JsonDocument.Parse(submitted.RootElement.GetProperty("value").GetString()!))
            Assert.False(policy.RootElement.GetProperty("default").GetBoolean());
        await Error("CONFLICT", () => second.Drafts.SetAsync(Request(key: request.IdempotencyKey, enabled: false), Ct));
        second.User.Value = FlagManagementTestHost.MutableUser.PlatformPrincipal(subject: "other-user");
        await Error("CONFLICT", () => second.Drafts.SetAsync(request, Ct));
        await using var restricted = new NpgsqlConnection(first.PlatformConnection); await restricted.OpenAsync(Ct);
        await using (var receipt = new NpgsqlCommand("SELECT request_hash FROM plugin_qian_config_flags.draft_receipts WHERE idempotency_key=@key", restricted))
        { receipt.Parameters.AddWithValue("key", request.IdempotencyKey); Assert.Equal(audit.InputHash, await receipt.ExecuteScalarAsync(Ct)); }
        await using (var command = new NpgsqlCommand("DELETE FROM plugin_qian_config_flags.draft_receipts", restricted))
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct))).SqlState);
        await using (var command = new NpgsqlCommand("UPDATE plugin_qian_config_flags.draft_receipts SET request_hash=repeat('0',64)", restricted))
            Assert.Equal("42501", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct))).SqlState);
        await using (var command = new NpgsqlCommand("SELECT set_config('app.current_tenant_id',@tenant,false)", restricted))
        { command.Parameters.AddWithValue("tenant", Guid.NewGuid().ToString("D")); await command.ExecuteNonQueryAsync(Ct); }
        await using (var command = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_config_flags.draft_receipts", restricted))
            Assert.Equal(0L, await command.ExecuteScalarAsync(Ct));
    }

    [Fact]
    public async Task 并发同key只请求一次_外部提交后持久回执失败不能重发()
    {
        await using var portal = await Portal.Start();
        await using var host = await FlagManagementTestHost.Create(postgres, portal.Uri, "trusted-app", "test-token", Ct);
        var request = Request();
        portal.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = host.Drafts.SetAsync(request, Ct).AsTask();
        await portal.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await Error("CONFLICT", () => host.Drafts.SetAsync(request, Ct));
        portal.Hold.SetResult(); await first;
        Assert.Equal(1, portal.Requests);
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using (var command = new NpgsqlCommand("""
            CREATE FUNCTION plugin_qian_config_flags.fail_completion() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.state='pending-publish' THEN RAISE EXCEPTION 'fixture_receipt_failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER fail_completion BEFORE UPDATE ON plugin_qian_config_flags.draft_receipts FOR EACH ROW EXECUTE FUNCTION plugin_qian_config_flags.fail_completion();
            """, admin)) await command.ExecuteNonQueryAsync(Ct);
        var ambiguous = Request(enabled: false);
        try
        {
            await Error("UNAVAILABLE", () => host.Drafts.SetAsync(ambiguous, Ct));
            Assert.Equal(2, portal.Requests);
            await Error("CONFLICT", () => host.Drafts.SetAsync(ambiguous, Ct));
            Assert.Equal(2, portal.Requests);
            await using var state = new NpgsqlCommand("SELECT state FROM plugin_qian_config_flags.draft_receipts WHERE idempotency_key=@key", admin);
            state.Parameters.AddWithValue("key", ambiguous.IdempotencyKey);
            Assert.Equal("uncertain", await state.ExecuteScalarAsync(Ct));
        }
        finally
        {
            await using var remove = new NpgsqlCommand("DROP TRIGGER fail_completion ON plugin_qian_config_flags.draft_receipts; DROP FUNCTION plugin_qian_config_flags.fail_completion();", admin);
            await remove.ExecuteNonQueryAsync(Ct);
        }
    }

    [Fact]
    public async Task 正在编辑时卸载会取消_旧key不能重发_重定向不转发Token()
    {
        await using var portal = await Portal.Start();
        await using var host = await FlagManagementTestHost.Create(postgres, portal.Uri, "trusted-app", "test-token", Ct);
        var request = Request();
        portal.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var editing = host.Drafts.SetAsync(request, Ct).AsTask();
        await portal.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        await host.Loader.UnmountAsync("flags", ScopeKey.Global, Ct);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => editing);
        portal.Hold.SetResult();
        await host.MountFlags(Ct);
        await Error("CONFLICT", () => host.Drafts.SetAsync(request, Ct));
        Assert.Equal(1, portal.Requests);
        portal.Redirect = true;
        await Error("BAD_GATEWAY", () => host.Drafts.SetAsync(Request(), Ct));
        Assert.Equal(2, portal.Requests); // 若追随302，会出现第三次请求并暴露Authorization。
    }

    [Fact]
    public async Task 回执唯一键或最小权限漂移时失败关闭_不发Portal请求()
    {
        await using var portal = await Portal.Start();
        await using var host = await FlagManagementTestHost.Create(postgres, portal.Uri, "trusted-app", "test-token", Ct);
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await Ddl("GRANT UPDATE(actor) ON plugin_qian_config_flags.draft_receipts TO " + host.Role);
        await Error("CONFIG_BOOTSTRAP_INVALID", () => host.Drafts.SetAsync(Request(), Ct));
        await Ddl("REVOKE UPDATE(actor) ON plugin_qian_config_flags.draft_receipts FROM " + host.Role);
        await Ddl("ALTER TABLE plugin_qian_config_flags.draft_receipts DROP CONSTRAINT draft_receipts_pkey");
        try { await Error("CONFIG_BOOTSTRAP_INVALID", () => host.Drafts.SetAsync(Request(), Ct)); }
        finally { await Ddl("ALTER TABLE plugin_qian_config_flags.draft_receipts ADD PRIMARY KEY(target_hash,idempotency_key)"); }
        Assert.Equal(0, portal.Requests);
        Assert.Equal("pending-publish", (await host.Drafts.SetAsync(Request(), Ct)).Status);
        async Task Ddl(string sql) { await using var command = new NpgsqlCommand(sql, admin); await command.ExecuteNonQueryAsync(Ct); }
    }

    private static FeatureFlagDraft Request(Guid? key = null, bool enabled = true, bool confirmed = true) => new()
    { Name = "preview", Rule = new FeatureFlagPolicy { Default = enabled }, Confirmed = confirmed, IdempotencyKey = key ?? Guid.NewGuid() };
    private static async Task Error(string code, Func<ValueTask<FeatureFlagDraftResult>> call) =>
        Assert.Equal(code, (await Assert.ThrowsAsync<QianErrorException>(() => call().AsTask())).Error.Code.Code);

    private sealed class Portal(WebApplication app) : IAsyncDisposable
    {
        internal Uri Uri => new(app.Urls.Single());
        internal int Requests;
        internal bool Redirect;
        internal string? Body, Path, Authorization;
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource? Hold { get; set; }
        internal static async Task<Portal> Start()
        {
            var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
            var app = builder.Build(); var portal = new Portal(app);
            app.Run(async context =>
            {
                Interlocked.Increment(ref portal.Requests); portal.Path = context.Request.Path + context.Request.QueryString;
                portal.Authorization = context.Request.Headers.Authorization.ToString();
                using var reader = new StreamReader(context.Request.Body); portal.Body = await reader.ReadToEndAsync(Ct);
                portal.Entered.TrySetResult();
                if (portal.Hold is { } hold) await hold.Task.WaitAsync(Ct);
                context.Response.StatusCode = portal.Redirect ? (int)HttpStatusCode.Found : (int)HttpStatusCode.OK;
                if (portal.Redirect) context.Response.Headers.Location = "/credential-leak";
            });
            await app.StartAsync(Ct); return portal;
        }
        public async ValueTask DisposeAsync() { Hold?.TrySetResult(); await app.StopAsync(Ct); await app.DisposeAsync(); }
    }
}
