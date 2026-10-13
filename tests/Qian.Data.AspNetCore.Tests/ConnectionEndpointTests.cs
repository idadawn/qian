using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Json.Schema;
using Kun.EventBus.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Qian.Config;
using Qian.Config.Secrets;
using Qian.Data.Dialect.Pg;
using Qian.Testing.Containers;

namespace Qian.Data.AspNetCore.Tests;

/// <summary>
/// 真实 PostgreSQL 与受 RLS 约束的应用角色：数据连接档案按租户隔离、口令只存租户 secretRef；
/// POST /api/data/connections/{id}:test 对可达库返回成功，对不可达库、错误口令、缺失 schema、越权密钥返回结构化错误；任何响应都不含口令。
/// </summary>
public sealed class ConnectionEndpointTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>, IAsyncLifetime
{
    private const string TenantA = "01900000-0000-7000-8000-00000000a001";
    private const string TenantB = "01900000-0000-7000-8000-00000000b001";
    private const string AdminA = "admin@" + TenantA;
    private const string AdminB = "admin@" + TenantB;
    private const string WrongPassword = "not-the-probe-password";
    private static readonly ConcurrentDictionary<PostgresFixture, Lazy<Task<string>>> Probes = new();
    private static readonly ConcurrentDictionary<PostgresFixture, Lazy<Task>> Migrated = new();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private readonly string _mount = Directory.CreateTempSubdirectory("qian-data-secrets-").FullName;
    private readonly Observer _observer = new();
    private string _password = null!;
    private string _host = null!;
    private int _port;
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        _password = await Probes.GetOrAdd(postgres, fixture => new Lazy<Task<string>>(() => CreateProbeRoleAsync(fixture))).Value;
        var server = new NpgsqlConnectionStringBuilder(postgres.ConnectionString);
        (_host, _port) = (server.Host!, server.Port);
        WriteSecret("probe", _password);
        WriteSecret("wrong", WrongPassword);
        WriteSecret("denied", _password);
        var owner = await DataOwner.ConnectionStringAsync(postgres);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        TestIdentity.Add(builder.Services);
        builder.Services.AddKunEventBus();
        builder.Services.AddQianPgDialect();
        builder.Services.AddQianDataConnections((_, options) => options.UseNpgsql(postgres.AppConnectionString),
            options => options.TestTimeout = TimeSpan.FromSeconds(3));
        builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, _) => owner);
        builder.Services.AddSingleton<ISecretAccessObserver>(_observer);
        builder.Services.AddQianSecrets(new SecretResolverOptions
        {
            MountRoot = _mount,
            Bindings =
            [
                new("secretRef://tenant/" + TenantA + "/data/probe", new("qian", "probe", "token"), TenantA),
                new("secretRef://tenant/" + TenantA + "/data/wrong", new("qian", "wrong", "token"), TenantA),
                new("secretRef://tenant/" + TenantA + "/other/denied", new("qian", "denied", "token"), TenantA),
                new("secretRef://tenant/" + TenantB + "/data/probe", new("qian", "probe", "token"), TenantB)
            ],
            // qian.data 的宿主策略只批准租户作用域的 data/*：other/* 即使已绑定也不能解析。
            Policies = [new("qian.data", [new("data/*", "tenant")], [new("data/*", "tenant")])]
        });
        _app = builder.Build();
        TestIdentity.Use(_app);
        _app.MapQianData();
        var app = _app;
        await Migrated.GetOrAdd(postgres, _ => new Lazy<Task>(() => app.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct))).Value;
        await _app.StartAsync(Ct);
        _client = _app.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await _app.DisposeAsync();
        Directory.Delete(_mount, true);
    }

    [Fact]
    public async Task 连通测试对可达库成功_对不可达库错误口令缺失schema与越权密钥返回结构化错误_详情不回传密钥()
    {
        var reachable = await CreateAsync(AdminA, Draft("warehouse", schema: "public", encoding: "UTF8"));
        // 详情与列表只含凭证引用，口令不出现在任何响应里，也没有 password 成员。
        using (var details = await SendAsync(HttpMethod.Get, "/api/data/connections/" + reachable, AdminA))
        {
            Assert.Equal(HttpStatusCode.OK, details.StatusCode);
            var json = await details.Content.ReadAsStringAsync(Ct);
            AssertProfile(json);
            using var body = JsonDocument.Parse(json);
            Assert.Equal("secretRef://tenant/data/probe", body.RootElement.GetProperty("secretRef").GetString());
            Assert.Equal("admin", await CreatedByAsync(reachable));
        }
        using (var list = await SendAsync(HttpMethod.Get, "/api/data/connections", AdminA))
        {
            var json = await list.Content.ReadAsStringAsync(Ct);
            Assert.True(Definition("list").Evaluate(JsonDocument.Parse(json).RootElement).IsValid, json);
            Assert.DoesNotContain(_password, json, StringComparison.Ordinal);
        }
        using (var test = await SendAsync(HttpMethod.Post, "/api/data/connections/" + reachable + ":test", AdminA))
        {
            var json = await test.Content.ReadAsStringAsync(Ct);
            Assert.Equal(HttpStatusCode.OK, test.StatusCode);
            Assert.True(Definition("testResult").Evaluate(JsonDocument.Parse(json).RootElement).IsValid, json);
            using var body = JsonDocument.Parse(json);
            Assert.Equal("pg", body.RootElement.GetProperty("dialect").GetString());
            Assert.StartsWith("16", body.RootElement.GetProperty("serverVersion").GetString(), StringComparison.Ordinal);
            Assert.InRange(body.RootElement.GetProperty("latencyMs").GetInt32(), 0, 3000);
        }

        var closed = await CreateAsync(AdminA, Draft("closed-port", host: "127.0.0.1", port: ClosedPort()));
        await AssertTestFailsAsync(closed, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionUnreachable.Code, null);
        var wrong = await CreateAsync(AdminA, Draft("wrong-password", secretRef: "secretRef://tenant/data/wrong"));
        await AssertTestFailsAsync(wrong, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionRejected.Code, "authentication");
        var missingDatabase = await CreateAsync(AdminA, Draft("missing-database", database: "qian_missing"));
        await AssertTestFailsAsync(missingDatabase, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionRejected.Code, "database");
        var missingSchema = await CreateAsync(AdminA, Draft("missing-schema", schema: "qian_missing"));
        await AssertTestFailsAsync(missingSchema, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionRejected.Code, "schema");
        // 引用本租户已绑定、但不在 qian.data 已批准范围内的密钥：解析被拒，不会连接。
        var denied = await CreateAsync(AdminA, Draft("denied-secret", secretRef: "secretRef://tenant/other/denied"));
        await AssertTestFailsAsync(denied, HttpStatusCode.Forbidden, "SECRET_ACCESS_DENIED", null);

        // 审计只记引用、调用方与租户，不含值。
        Assert.Contains(_observer.Events, access => access.Allowed && access.Caller == "qian.data" && access.TenantId == TenantA);
        Assert.Contains(_observer.Events, access => !access.Allowed && access.Outcome == "SECRET_ACCESS_DENIED");
        Assert.DoesNotContain(_observer.Events, access => access.Reference.Contains(_password, StringComparison.Ordinal));
    }

    [Fact]
    public async Task 连通测试对域名解析失败与非PostgreSQL服务返回不可达_对不支持的认证方式返回authentication()
    {
        // .invalid 是保留域名，解析不会成功；驱动在这里可能抛出未包装的套接字异常，也必须是结构化错误而不是 500。
        var unresolved = await CreateAsync(AdminA, Draft("unresolved-host", host: "qian-probe.invalid"));
        await AssertTestFailsAsync(unresolved, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionUnreachable.Code, null);
        using (var http = FakeServer.Http())
        {
            var wrongService = await CreateAsync(AdminA, Draft("http-service", host: "127.0.0.1", port: http.Port));
            await AssertTestFailsAsync(wrongService, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionUnreachable.Code, null);
        }
        using (var kerberos = FakeServer.KerberosOnly())
        {
            var unsupported = await CreateAsync(AdminA, Draft("kerberos-only", host: "127.0.0.1", port: kerberos.Port));
            await AssertTestFailsAsync(unsupported, HttpStatusCode.UnprocessableEntity, DataErrors.ConnectionRejected.Code, "authentication");
        }
    }

    [Fact]
    public async Task 档案按租户隔离_同名冲突_修改与删除后名称可复用()
    {
        var shared = await CreateAsync(AdminA, Draft("shared"));
        // 租户 B 看不到也测不了 A 的档案；B 可以有自己的同名档案。
        using (var response = await SendAsync(HttpMethod.Get, "/api/data/connections/" + shared, AdminB))
            await AssertErrorAsync(response, HttpStatusCode.NotFound, DataErrors.ConnectionNotFound.Code);
        using (var response = await SendAsync(HttpMethod.Post, "/api/data/connections/" + shared + ":test", AdminB))
            await AssertErrorAsync(response, HttpStatusCode.NotFound, DataErrors.ConnectionNotFound.Code);
        using (var response = await SendAsync(HttpMethod.Delete, "/api/data/connections/" + shared, AdminB))
            await AssertErrorAsync(response, HttpStatusCode.NotFound, DataErrors.ConnectionNotFound.Code);
        var other = await CreateAsync(AdminB, Draft("shared"));
        Assert.DoesNotContain(shared, await NamesAsync(AdminB, "id"));
        using (var test = await SendAsync(HttpMethod.Post, "/api/data/connections/" + other + ":test", AdminB))
            Assert.Equal(HttpStatusCode.OK, test.StatusCode);

        using (var duplicate = await SendAsync(HttpMethod.Post, "/api/data/connections", AdminA, Draft("shared")))
            await AssertErrorAsync(duplicate, HttpStatusCode.Conflict, DataErrors.ConnectionNameConflict.Code);
        using (var update = await SendAsync(HttpMethod.Put, "/api/data/connections/" + shared, AdminA, Draft("shared", port: 6543)))
        {
            Assert.Equal(HttpStatusCode.OK, update.StatusCode);
            using var body = JsonDocument.Parse(await update.Content.ReadAsStringAsync(Ct));
            Assert.Equal(6543, body.RootElement.GetProperty("port").GetInt32());
            Assert.True(body.RootElement.TryGetProperty("updatedAt", out _));
        }
        using (var removed = await SendAsync(HttpMethod.Delete, "/api/data/connections/" + shared, AdminA))
            Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        using (var gone = await SendAsync(HttpMethod.Get, "/api/data/connections/" + shared, AdminA))
            await AssertErrorAsync(gone, HttpStatusCode.NotFound, DataErrors.ConnectionNotFound.Code);
        var again = await CreateAsync(AdminA, Draft("shared"));
        Assert.NotEqual(shared, again);

        // RLS 兜底：应用角色在租户 B 的会话里只看得到 B 的行（含软删行在内 A 有两行）。
        Assert.Equal(1L, await CountAsync(TenantB));
        Assert.Equal(2L, await CountAsync(TenantA));
    }

    [Fact]
    public async Task 非法输入逐项报错_不接受明文口令与平台密钥引用_权限与租户缺失时拒绝()
    {
        await AssertInvalidAsync(new { name = "plain", dialect = "pg", host = "db", port = 5432, database = "app", userName = "app", secretRef = "secretRef://tenant/data/probe", password = "p@ss" },
            ("#/password", "UNKNOWN_PROPERTY"));
        await AssertInvalidAsync(Draft("platform-ref", secretRef: "secretRef://platform/pg/qian-platform"), ("#/secretRef", "PATTERN_MISMATCH"));
        await AssertInvalidAsync(Draft("dev-ref", secretRef: "secretRef://dev/pg"), ("#/secretRef", "PATTERN_MISMATCH"));
        await AssertInvalidAsync(new { name = "no-secret", dialect = "pg", host = "db", port = 5432, database = "app", userName = "app" }, ("#/secretRef", "REQUIRED"));
        await AssertInvalidAsync("""{"name":"typed","dialect":"pg","host":"db","port":"abc","database":"app","userName":"app","secretRef":"secretRef://tenant/data/probe"}""",
            ("#/port", "TYPE_MISMATCH"));
        await AssertInvalidAsync(Draft(" padded ", dialect: "mysql", host: "bad host", port: 0, database: "app;drop"),
            ("#/name", "PATTERN_MISMATCH"), ("#/dialect", "VALUE_NOT_ALLOWED"), ("#/host", "PATTERN_MISMATCH"), ("#/port", "OUT_OF_RANGE"), ("#/database", "PATTERN_MISMATCH"));
        using (var plain = await _client.SendAsync(new HttpRequestMessage(HttpMethod.Post, "/api/data/connections")
               { Headers = { { TestIdentity.Header, AdminA } }, Content = new StringContent("name=x") }, Ct))
            Assert.Equal(HttpStatusCode.UnsupportedMediaType, plain.StatusCode);

        // 只读主体能列出、不能新建或测试；没有租户的主体不能访问任何档案。
        using (var list = await SendAsync(HttpMethod.Get, "/api/data/connections", "reader@" + TenantA))
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        using (var create = await SendAsync(HttpMethod.Post, "/api/data/connections", "reader@" + TenantA, Draft("by-reader")))
            Assert.Equal(HttpStatusCode.Forbidden, create.StatusCode);
        using (var test = await SendAsync(HttpMethod.Post, "/api/data/connections/" + Guid.CreateVersion7() + ":test", "reader@" + TenantA))
            Assert.Equal(HttpStatusCode.Forbidden, test.StatusCode);
        using (var platform = await SendAsync(HttpMethod.Get, "/api/data/connections", "admin"))
            await AssertErrorAsync(platform, HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        Assert.DoesNotContain("by-reader", await NamesAsync(AdminA, "name"));
    }

    private const string ProbeRole = "qian_probe";

    private object Draft(string name, string dialect = "pg", string? host = null, int? port = null, string database = PostgresFixture.DatabaseName,
        string secretRef = "secretRef://tenant/data/probe", string? schema = null, string? encoding = null) => new
    {
        name, dialect, host = host ?? _host, port = port ?? _port, database, userName = ProbeRole, secretRef, schema, encoding
    };

    private async Task<string> CreateAsync(string user, object draft)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/data/connections", user, draft);
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.Created, json);
        AssertProfile(json);
        var id = JsonDocument.Parse(json).RootElement.GetProperty("id").GetString()!;
        Assert.Equal("/api/data/connections/" + id, response.Headers.Location?.ToString());
        return id;
    }

    private void AssertProfile(string json)
    {
        Assert.True(Definition("profile").Evaluate(JsonDocument.Parse(json).RootElement).IsValid, json);
        Assert.DoesNotContain(_password, json, StringComparison.Ordinal);
        Assert.DoesNotContain(JsonDocument.Parse(json).RootElement.EnumerateObject(),
            member => member.Name.Contains("password", StringComparison.OrdinalIgnoreCase));
    }

    private async Task AssertTestFailsAsync(string id, HttpStatusCode status, string code, string? reason)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/data/connections/" + id + ":test", AdminA);
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, json);
        using var body = JsonDocument.Parse(json);
        Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
        if (reason is not null) Assert.EndsWith(reason, body.RootElement.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_password, json, StringComparison.Ordinal);
        Assert.DoesNotContain(WrongPassword, json, StringComparison.Ordinal);
    }

    private async Task AssertInvalidAsync(object draft, params (string Pointer, string Code)[] expected)
    {
        using var response = await SendAsync(HttpMethod.Post, "/api/data/connections", AdminA, draft);
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, json);
        using var body = JsonDocument.Parse(json);
        Assert.Equal("INPUT_INVALID", body.RootElement.GetProperty("code").GetString());
        var errors = body.RootElement.GetProperty("errors").EnumerateArray()
            .Select(error => (error.GetProperty("pointer").GetString()!, error.GetProperty("code").GetString()!)).ToArray();
        Assert.Equal(expected, errors);
    }

    private static async Task AssertErrorAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var json = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == status, json);
        Assert.Equal(code, JsonDocument.Parse(json).RootElement.GetProperty("code").GetString());
    }

    private async Task<string[]> NamesAsync(string user, string member)
    {
        using var response = await SendAsync(HttpMethod.Get, "/api/data/connections", user);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct));
        return body.RootElement.GetProperty("items").EnumerateArray().Select(item => item.GetProperty(member).GetString()!).ToArray();
    }

    private Task<HttpResponseMessage> SendAsync(HttpMethod method, string uri, string user, object? body = null) =>
        _client.SendAsync(TestIdentity.Request(method, uri, user, body), Ct);

    private static JsonSchema Definition(string name)
    {
        var options = new BuildOptions { SchemaRegistry = new SchemaRegistry() };
        var root = JsonSchema.FromText(DataContractSchemas.DataConnection, options);
        return JsonSchema.FromText("{\"$ref\":\"" + root.BaseUri + "#/$defs/" + name + "\"}", options);
    }

    private async Task<string?> CreatedByAsync(string id)
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString);
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT created_by FROM plugin_qian_data.data_connections WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", Guid.Parse(id));
        return (string?)await command.ExecuteScalarAsync(Ct);
    }

    private async Task<long> CountAsync(string tenant)
    {
        await using var connection = new NpgsqlConnection(postgres.AppConnectionString);
        await connection.OpenAsync(Ct);
        await using (var set = new NpgsqlCommand("SELECT set_config('app.current_tenant_id', @tenant, false)", connection))
        {
            set.Parameters.AddWithValue("tenant", tenant);
            await set.ExecuteScalarAsync(Ct);
        }
        await using var command = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_data.data_connections WHERE name = 'shared'", connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    private void WriteSecret(string name, string value)
    {
        var directory = Directory.CreateDirectory(Path.Combine(_mount, "qian", name)).FullName;
        File.WriteAllText(Path.Combine(directory, "token"), value);
    }

    // 拿一个刚释放的本机端口：此刻没有进程监听，连接会被立即拒绝。
    private static int ClosedPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task<string> CreateProbeRoleAsync(PostgresFixture postgres)
    {
        // 口令是本测试生成的十六进制串，可以安全地拼进 DDL（CREATE ROLE 不支持参数化）。
        var password = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16));
        await using var admin = new NpgsqlConnection(postgres.ConnectionString);
        await admin.OpenAsync();
        await using var command = new NpgsqlCommand($"""
            CREATE ROLE {ProbeRole} LOGIN PASSWORD '{password}' NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE;
            GRANT CONNECT ON DATABASE {PostgresFixture.DatabaseName} TO {ProbeRole};
            """, admin);
        await command.ExecuteNonQueryAsync();
        return password;
    }

    private sealed class Observer : ISecretAccessObserver
    {
        private readonly ConcurrentQueue<SecretAccessEvent> _events = new();
        public IReadOnlyCollection<SecretAccessEvent> Events => _events;
        public ValueTask RecordAsync(SecretAccessEvent access, CancellationToken cancellationToken = default)
        {
            _events.Enqueue(access);
            return ValueTask.CompletedTask;
        }
    }
}
