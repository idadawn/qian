using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Data;
using Qian.Runtime;
using Qian.Tenancy;
using Qian.Testing.Containers;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const string A = "01900000-0000-7000-8000-000000000001";
    private const string B = "01900000-0000-7000-8000-000000000002";
    private const string Writer = "crm-command-writer";
    private const string Issuer = "crm-command-acceptance";
    private const string Audience = "qian.sample.crm";
    private static readonly string[] DeclaredTokenRoles = ["tenant.owner"];

    [Fact]
    public async Task 真实HTTP创建经租户RBAC与审计监听_客户回执Outbox和审计同时提交_重复输入幂等()
    {
        await using var host = await Start();
        var key = Guid.CreateVersion7(); var input = new CreateCustomerCommand(key, "customer-private-sentinel");
        using var response = await host.Post(A, input); Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var customer = await response.Content.ReadFromJsonAsync<CustomerItem>(Ct); Assert.NotNull(customer); Assert.Equal(A, customer.TenantId);
        var data = host.App.Services.GetRequiredService<IDataService>(); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenant.BeginScope(A))
        {
            await using var session = await data.OpenAsync<CrmDbContext>(Ct);
            Assert.Equal(customer.Id, Assert.Single(await session.Context.Customers.ToListAsync(Ct)).Id);
            Assert.Single(await session.Context.CustomerReceipts.ToListAsync(Ct)); Assert.Single(await session.Context.OutboxMessages.ToListAsync(Ct));
            var audit = host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit();
            var record = Assert.Single(await audit.ReadAsync(0, 10, cancellationToken: Ct));
            Assert.Equal("qian.sample.crm/customer.create", record.Record.CapabilityId); Assert.Equal("service", record.Record.ActorType); Assert.Equal(Writer, record.Record.ActorId);
            var projected = JsonSerializer.SerializeToElement(new { idempotencyKey = key, name = "[REDACTED]" });
            var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(AuditCanonicalizer.Canonicalize(projected))));
            Assert.Equal(expectedHash, record.Record.InputHash);
            Assert.DoesNotContain("customer-private-sentinel", JsonSerializer.Serialize(record.Record));
            Assert.Equal(new AuditChainVerification(true, 1, null, null), await audit.VerifyAsync(Ct));
        }
        using var retry = await host.Post(A, input); Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
        Assert.Equal(customer.Id, (await retry.Content.ReadFromJsonAsync<CustomerItem>(Ct))!.Id);
        using var conflict = await host.Post(A, input with { Name = "changed" }); Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        using (tenant.BeginScope(A))
        {
            await using var session = await data.OpenAsync<CrmDbContext>(Ct);
            Assert.Single(await session.Context.Customers.ToListAsync(Ct)); Assert.Single(await session.Context.CustomerReceipts.ToListAsync(Ct));
            Assert.Single(await session.Context.OutboxMessages.ToListAsync(Ct));
            Assert.Equal(new AuditChainVerification(true, 2, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
        }
    }

    [Fact]
    public async Task 审计失败时真实HTTP命令的客户回执和Outbox全部回滚_恢复后链不跳号()
    {
        await using var host = await Start();
        await Admin("CREATE FUNCTION plugin_qian_audit.reject_command() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'forced audit rollback'; END $$; CREATE TRIGGER reject_command BEFORE INSERT ON plugin_qian_audit.audit_entries FOR EACH ROW EXECUTE FUNCTION plugin_qian_audit.reject_command();");
        var input = new CreateCustomerCommand(Guid.CreateVersion7(), "rollback customer");
        using var failed = await host.Post(A, input); Assert.Equal(HttpStatusCode.InternalServerError, failed.StatusCode);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Empty(await session.Context.Customers.ToListAsync(Ct)); Assert.Empty(await session.Context.CustomerReceipts.ToListAsync(Ct)); Assert.Empty(await session.Context.OutboxMessages.ToListAsync(Ct));
            Assert.Equal(new AuditChainVerification(true, 0, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
        }
        await Admin("DROP TRIGGER reject_command ON plugin_qian_audit.audit_entries;");
        using var recovered = await host.Post(A, input); Assert.Equal(HttpStatusCode.Created, recovered.StatusCode);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A))
            Assert.Equal(new AuditChainVerification(true, 1, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
    }

    [Fact]
    public async Task 正常签名但无授权或租户冻结均不能写入_移除审计后整个入口不可用()
    {
        await using var host = await Start(); var input = new CreateCustomerCommand(Guid.CreateVersion7(), "denied customer");
        using var denied = await host.Post(B, input); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = 'suspended' WHERE id = '{A}'::uuid;");
        using var suspended = await host.Post(A, input); Assert.Equal(HttpStatusCode.Forbidden, suspended.StatusCode);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = 'active' WHERE id = '{A}'::uuid;");
        await host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.UnmountAsync("audit", ScopeKey.Global, Ct);
        using var unmounted = await host.Post(A, input); Assert.Equal(HttpStatusCode.NotFound, unmounted.StatusCode);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Empty(await session.Context.Customers.ToListAsync(Ct)); Assert.Empty(await session.Context.OutboxMessages.ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task 两租户并发使用同一幂等键_业务和审计隔离_事件租户匹配()
    {
        await using var host = await Start(concurrentWrites: true); var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var data = host.App.Services.GetRequiredService<IDataService>();
        using (tenant.BeginScope(B)) await new PostgresTenantRoleStore(data, tenant).AssignAsync("tenant.owner", "service", Writer, "test-bootstrap", Ct);
        var input = new CreateCustomerCommand(Guid.CreateVersion7(), "two tenants");
        var responses = await Task.WhenAll(host.Post(A, input), host.Post(B, input));
        try
        {
            foreach (var response in responses)
            {
                var body = await response.Content.ReadAsStringAsync(Ct);
                using var json = JsonDocument.Parse(body);
                var errorCode = json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : "";
                Assert.True(response.StatusCode == HttpStatusCode.Created, errorCode + "\n" + host.FailureEvidence);
            }
            var first = (await responses[0].Content.ReadFromJsonAsync<CustomerItem>(Ct))!;
            var second = (await responses[1].Content.ReadFromJsonAsync<CustomerItem>(Ct))!;
            Assert.Equal(A, first.TenantId); Assert.Equal(B, second.TenantId); Assert.NotEqual(first.Id, second.Id);
            foreach (var id in new[] { A, B })
            {
                using var scope = tenant.BeginScope(id); await using var session = await data.OpenAsync<CrmDbContext>(Ct);
                var row = Assert.Single(await session.Context.Customers.ToListAsync(Ct)); Assert.Equal(id, row.TenantId);
                Assert.Equal(id == A ? first.Id : second.Id, row.Id);
                Assert.Single(await session.Context.CustomerReceipts.ToListAsync(Ct));
                // Kun Outbox 是发布器按服务扫描的传输表；逐项核验路由租户与实际客户，不套用业务列表语义。
                var messages = await session.Context.OutboxMessages.ToListAsync(Ct);
                Assert.Equal(2, messages.Count);
                var message = Assert.Single(messages, item => item.TenantId == id);
                Assert.Contains(row.Id.ToString(), message.Payload, StringComparison.Ordinal);
                Assert.Equal(new AuditChainVerification(true, 1, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
            }
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task 数据库序列化失败经EF包装后仍重试整笔真实HTTP命令()
    {
        await using var host = await Start();
        // 故障注入只验证实际 PG -> EF 包装 -> HTTP 重试路径；真正并发另有保存屏障用例。
        await Admin($"""
            CREATE SEQUENCE plugin_qian_sample_crm.retry_probe;
            GRANT USAGE ON SEQUENCE plugin_qian_sample_crm.retry_probe TO "{host.BusinessRole}";
            CREATE FUNCTION plugin_qian_sample_crm.fail_first_save() RETURNS trigger LANGUAGE plpgsql AS $f$
            BEGIN
              IF nextval('plugin_qian_sample_crm.retry_probe') = 1 THEN
                RAISE EXCEPTION 'test serialization failure' USING ERRCODE = '40001';
              END IF;
              RETURN NEW;
            END $f$;
            CREATE TRIGGER retry_probe BEFORE INSERT ON plugin_qian_sample_crm.customers FOR EACH ROW EXECUTE FUNCTION plugin_qian_sample_crm.fail_first_save();
            """);
        using var response = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "retry wrapped failure"));
        Assert.True(response.StatusCode == HttpStatusCode.Created, host.FailureEvidence);
        using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
        await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
        Assert.Single(await session.Context.Customers.ToListAsync(Ct)); Assert.Single(await session.Context.CustomerReceipts.ToListAsync(Ct)); Assert.Single(await session.Context.OutboxMessages.ToListAsync(Ct));
        Assert.Equal(new AuditChainVerification(true, 1, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
    }

    [Fact]
    public async Task 同租户同一幂等键并发争用后仍返回同一客户_整笔失败重试()
    {
        await using var host = await Start(concurrentWrites: true);
        var input = new CreateCustomerCommand(Guid.CreateVersion7(), "same tenant race");
        var responses = await Task.WhenAll(host.Post(A, input), host.Post(A, input));
        try
        {
            Assert.All(responses, response => Assert.True(response.StatusCode == HttpStatusCode.Created, host.FailureEvidence));
            var first = (await responses[0].Content.ReadFromJsonAsync<CustomerItem>(Ct))!;
            var second = (await responses[1].Content.ReadFromJsonAsync<CustomerItem>(Ct))!; Assert.Equal(first.Id, second.Id);
            using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
            await using var data = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Single(await data.Context.Customers.ToListAsync(Ct)); Assert.Single(await data.Context.CustomerReceipts.ToListAsync(Ct)); Assert.Single(await data.Context.OutboxMessages.ToListAsync(Ct));
            Assert.Equal(new AuditChainVerification(true, 2, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
        }
        finally { foreach (var response in responses) response.Dispose(); }
    }

    [Fact]
    public async Task 真实HTTP等待审计链锁时卸载审计会取消执行并回滚业务()
    {
        await using var host = await Start();
        await using var holder = new NpgsqlConnection(postgres.ConnectionString); await holder.OpenAsync(Ct);
        await using (var acquire = new NpgsqlCommand("SELECT pg_advisory_lock(hashtextextended(@key, 0))", holder))
        { acquire.Parameters.AddWithValue("key", "qian.audit:tenant:" + A); await acquire.ExecuteNonQueryAsync(Ct); }
        var pending = host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "cancelled customer"));
        try
        {
            var waiting = false; var deadline = DateTime.UtcNow.AddSeconds(10);
            while (!waiting && DateTime.UtcNow < deadline)
            {
                await using var inspect = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_stat_activity WHERE wait_event = 'advisory' AND query LIKE '%transaction_tip%')", holder);
                waiting = (bool)(await inspect.ExecuteScalarAsync(Ct))!;
                if (!waiting) await Task.Delay(20, Ct);
            }
            Assert.True(waiting, "必须观察到实际命令已经完成业务保存并在审计函数内等待链锁。");
            await host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.UnmountAsync("audit", ScopeKey.Global, Ct).AsTask().WaitAsync(TimeSpan.FromSeconds(10), Ct);
            using var response = await pending.WaitAsync(TimeSpan.FromSeconds(10), Ct); Assert.False(response.IsSuccessStatusCode);
            using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
            var data = host.App.Services.GetRequiredService<IDataService>();
            await using var session = await data.OpenAsync<CrmDbContext>(Ct);
            Assert.Empty(await session.Context.Customers.ToListAsync(Ct)); Assert.Empty(await session.Context.CustomerReceipts.ToListAsync(Ct)); Assert.Empty(await session.Context.OutboxMessages.ToListAsync(Ct));
            await using var audit = await data.OpenAsync<AuditDbContext>(Ct);
            Assert.Equal(0L, await audit.Context.Database.SqlQueryRaw<long>("SELECT count(*) AS \"Value\" FROM plugin_qian_audit.audit_entries").SingleAsync(Ct));
        }
        finally
        {
            await using var release = new NpgsqlCommand("SELECT pg_advisory_unlock_all()", holder); await release.ExecuteNonQueryAsync(CancellationToken.None);
            if (!pending.IsCompleted) { using var response = await pending.WaitAsync(TimeSpan.FromSeconds(10), Ct); }
        }
    }

    [Fact]
    public async Task 真实查询默认不审计_详情显式Audit后入链_跨租户仍是404()
    {
        await using var host = await Start();
        using var created = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "query customer")); Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var customer = (await created.Content.ReadFromJsonAsync<CustomerItem>(Ct))!;
        using var list = await host.Get(A, "/api/customers"); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Single((await list.Content.ReadFromJsonAsync<CustomerPage>(Ct))!.Items);
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        using (tenant.BeginScope(A)) Assert.Equal(new AuditChainVerification(true, 1, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
        using var detail = await host.Get(A, "/api/customers/" + customer.Id); Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        using var cross = await host.Get(B, "/api/customers/" + customer.Id); Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        using (tenant.BeginScope(A))
        {
            var audit = host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit();
            var rows = await audit.ReadAsync(0, 10, cancellationToken: Ct); Assert.Equal(2, rows.Count);
            Assert.Equal("qian.sample.crm/customer.get", rows[1].Record.CapabilityId);
            Assert.Equal(new AuditChainVerification(true, 2, null, null), await audit.VerifyAsync(Ct));
            await using var data = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Single(await data.Context.OutboxMessages.ToListAsync(Ct));
        }
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = 'suspended' WHERE id = '{A}'::uuid;");
        using var suspended = await host.Get(A, "/api/customers"); Assert.Equal(HttpStatusCode.Forbidden, suspended.StatusCode);
    }

    private async Task<RunningApp> Start(bool concurrentWrites = false)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8]; var role = "qian_crm_cmd_" + suffix; var owner = "qian_audit_fn_" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        await Admin($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}'; CREATE ROLE \"{owner}\" NOLOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION;");
        var connection = new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
        var signing = RSA.Create(2048); using var publicKey = RSA.Create(); publicKey.ImportParameters(signing.ExportParameters(false));
        var key = new RsaSecurityKey(signing) { KeyId = "crm-command-key" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicKey) { KeyId = key.KeyId });
        var publicJwks = JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } });
        var evidence = new FailureEvidence(); var barrier = new ConcurrentSaveBarrier();
        var app = CrmCommandApplication.Build([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddSingleton(evidence); builder.Services.AddExceptionHandler<FailureRecorder>();
            if (concurrentWrites) builder.Services.ConfigureDbContext<CrmDbContext>((_, options) => options.AddInterceptors(barrier));
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Crm"] = connection, ["ConnectionStrings:Authz"] = postgres.AppConnectionString,
                ["ConnectionStrings:Tenancy"] = postgres.AppConnectionString, ["ConnectionStrings:Audit"] = postgres.AppConnectionString,
                ["S2S:Issuer"] = Issuer, ["S2S:Audience"] = Audience, ["S2S:PublicJwks"] = publicJwks,
                ["S2S:ReaderClientId"] = "crm-command-reader", ["S2S:WriterClientId"] = Writer
            });
        });
        try
        {
            using (var scope = app.Services.CreateScope())
            {
                var schemas = new[] { CrmDbContext.Schema, AuthzDbContext.Schema, TenancyDbContext.Schema, AuditDbContext.Schema };
                var sql = string.Join('\n', schemas.Select(schema => "DROP SCHEMA IF EXISTS " + schema + " CASCADE;"));
                sql += scope.ServiceProvider.GetRequiredService<CrmDbContext>().Database.GenerateCreateScript();
                sql += scope.ServiceProvider.GetRequiredService<AuthzDbContext>().Database.GenerateCreateScript();
                sql += scope.ServiceProvider.GetRequiredService<TenancyDbContext>().Database.GenerateCreateScript();
                sql += scope.ServiceProvider.GetRequiredService<AuditDbContext>().Database.GenerateCreateScript();
                await Admin(sql);
                await Admin($"GRANT USAGE ON SCHEMA plugin_qian_sample_crm TO \"{role}\"; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_sample_crm TO \"{role}\"; GRANT USAGE ON SCHEMA plugin_qian_authz, plugin_qian_tenancy, plugin_qian_audit TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_authz, plugin_qian_tenancy, plugin_qian_audit TO qian_app;");
                await Admin("BEGIN;" + AuditTransactionAccess.CreateMigrationSql(owner, role) + "COMMIT;");
            }
            var data = app.Services.GetRequiredService<IDataService>(); var tenant = app.Services.GetRequiredService<ITenantContext>();
            await new PostgresPermissionCatalogStore(data).PutAsync(CrmPermissions.Compile(), Ct);
            foreach (var id in new[] { A, B })
            {
                await Admin($"INSERT INTO plugin_qian_tenancy.tenants(id, code, name, isolation, status, created_at) VALUES ('{id}'::uuid, '{(id == A ? "a" : "b")}', 'command tenant', 'row', 'active', now());");
                using var context = tenant.BeginScope(id); var roles = new PostgresTenantRoleStore(data, tenant);
                await roles.EnsureBuiltinsAsync("test-bootstrap", Ct); await roles.InstallDefaultsAsync("qian.sample.crm", "1.0.0", null, "test-bootstrap", Ct);
                await roles.AssignAsync(id == A ? "tenant.owner" : "tenant.user", "service", Writer, "test-bootstrap", Ct);
            }
            await app.StartAsync(Ct);
            using (tenant.BeginScope(A))
            {
                var decision = await app.Services.GetRequiredService<Kun.Authorization.IPermissionChecker>().CheckAsync(new Kun.Authorization.PermissionRequest(new Kun.Authorization.AuthorizationSubject(Writer, A, "service"), "qian.sample.crm:customer:write"), Ct);
                Assert.True(decision.Allow, decision.Reason);
            }
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new RunningApp(app, signing, key, new HttpClient { BaseAddress = new Uri(address) }, evidence, role);
        }
        catch { await app.DisposeAsync(); signing.Dispose(); throw; }
    }
    private async Task Admin(string sql)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(sql, connection); await command.ExecuteNonQueryAsync(Ct);
    }
    private sealed class ConcurrentSaveBarrier : SaveChangesInterceptor
    {
        private int _arrivals;
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (eventData.Context?.ChangeTracker.HasChanges() == true && Interlocked.Increment(ref _arrivals) <= 2)
            {
                if (Volatile.Read(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
    private sealed class FailureEvidence { internal ConcurrentQueue<string> Failures { get; } = new(); }
    private sealed class FailureRecorder(FailureEvidence evidence) : IExceptionHandler
    {
        public ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
        {
            var chain = new List<string>();
            for (var current = exception; current is not null; current = current.InnerException)
                chain.Add(current is PostgresException postgres ? current.GetType().Name + ":" + postgres.SqlState + ":" + postgres.ConstraintName : current.GetType().Name);
            evidence.Failures.Enqueue(string.Join(" -> ", chain) + "\n" + exception.StackTrace);
            return ValueTask.FromResult(false);
        }
    }

    private sealed class RunningApp(WebApplication app, RSA signing, RsaSecurityKey key, HttpClient client, FailureEvidence evidence, string role) : IAsyncDisposable
    {
        internal WebApplication App => app;
        internal string BusinessRole => role;
        internal string FailureEvidence => string.Join("\n", evidence.Failures);
        internal Task<HttpResponseMessage> Post(string tenant, CreateCustomerCommand command)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, "/api/customers") { Content = JsonContent.Create(command) };
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(tenant, "crm.write"));
            return Send(message);
        }
        internal Task<HttpResponseMessage> Get(string tenant, string path)
        {
            var message = new HttpRequestMessage(HttpMethod.Get, path);
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token(tenant, "crm.read"));
            return Send(message);
        }
        private string Token(string tenant, string scope)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
            var payload = JsonSerializer.Serialize(new { iss = Issuer, aud = new[] { Audience }, sub = Writer, principal_kind = "service", tenant_id = tenant,
                roles = DeclaredTokenRoles, scope, session_id = id, jti = id, iat = now, nbf = now, exp = now + 60 });
            return new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        }
        private async Task<HttpResponseMessage> Send(HttpRequestMessage message)
        { using (message) return await client.SendAsync(message, Ct); }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(Ct); await app.DisposeAsync(); signing.Dispose(); }
    }
}
