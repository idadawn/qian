using System.Collections.Concurrent;
using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Data;
using Qian.Testing.Containers;

namespace Qian.Sample.Crm.Tests;

public sealed class CustomerIsolationTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string TenantA = "10000000-0000-0000-0000-000000000001";
    private const string TenantB = "10000000-0000-0000-0000-000000000002";
    private const string Issuer = "qian-sample-acceptance";
    private const string Audience = "qian.sample.crm";
    private const string Client = "qian-sample-reader";

    [Fact]
    public async Task 两租户各千行_真实HTTP签名身份与数据库隔离_列表原SQL使用租户索引()
    {
        var ct = TestContext.Current.CancellationToken;
        using var signing = RSA.Create(2048);
        using var publicOnly = RSA.Create();
        publicOnly.ImportParameters(signing.ExportParameters(false));
        var privateKey = new RsaSecurityKey(signing) { KeyId = "crm-acceptance" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicOnly) { KeyId = privateKey.KeyId });
        var publicJwks = JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } });
        var capture = new ListQueryCapture();
        await using var app = CrmApplication.Build([], builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();
            builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Crm"] = postgres.AppConnectionString,
                ["S2S:Issuer"] = Issuer, ["S2S:Audience"] = Audience,
                ["S2S:ReaderClientId"] = Client, ["S2S:PublicJwks"] = publicJwks
            });
            builder.Services.ConfigureDbContext<CrmDbContext>((_, options) => options.AddInterceptors(capture));
        });
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
            await using var admin = new NpgsqlConnection(postgres.ConnectionString);
            await admin.OpenAsync(ct);
            await using (var create = new NpgsqlCommand(db.Database.GenerateCreateScript(), admin)) await create.ExecuteNonQueryAsync(ct);
            await using (var grant = new NpgsqlCommand("GRANT USAGE ON SCHEMA plugin_qian_sample_crm TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_sample_crm TO qian_app;", admin)) await grant.ExecuteNonQueryAsync(ct);
            foreach (var tenant in new[] { TenantA, TenantB })
            {
                await using var seed = new NpgsqlCommand("""
                    INSERT INTO plugin_qian_sample_crm.customers (id, tenant_id, name, created_at, is_deleted)
                    SELECT gen_random_uuid(), @tenant, 'customer-' || n, now(), false FROM generate_series(1, 1000) n;
                    """, admin);
                seed.Parameters.AddWithValue("tenant", tenant);
                Assert.Equal(1000, await seed.ExecuteNonQueryAsync(ct));
            }
            await using var analyze = new NpgsqlCommand("ANALYZE plugin_qian_sample_crm.customers", admin);
            await analyze.ExecuteNonQueryAsync(ct);
        }
        await app.StartAsync(ct);
        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var client = new HttpClient { BaseAddress = new Uri(address) };
        var tokenA = Token(TenantA, privateKey);
        var tokenB = Token(TenantB, privateKey);
        Guid firstA = default;
        var seen = new HashSet<Guid>();
        for (var offset = 0; offset < 1000; offset += 100)
        {
            using var response = await Get($"/api/customers?skip={offset}&take=100&tenant_id={TenantB}", tokenA, TenantB);
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<CustomerPage>(ct);
            Assert.NotNull(page); Assert.Equal(1000, page.Total); Assert.Equal(100, page.Items.Count);
            Assert.All(page.Items, item => { Assert.Equal(TenantA, item.TenantId); Assert.True(seen.Add(item.Id)); });
            if (offset == 0) firstA = page.Items[0].Id;
        }
        Assert.Equal(1000, seen.Count);
        using (var response = await Get("/api/customers", tokenB, TenantA))
        {
            response.EnsureSuccessStatusCode();
            var page = await response.Content.ReadFromJsonAsync<CustomerPage>(ct);
            Assert.NotNull(page); Assert.Equal(1000, page.Total);
            Assert.All(page.Items, item => { Assert.Equal(TenantB, item.TenantId); Assert.DoesNotContain(item.Id, seen); });
        }
        await Error($"/api/customers/{firstA}", tokenB, HttpStatusCode.NotFound, "NOT_FOUND");
        await Error("/api/customers", null, HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await Error("/api/customers", Token(null, privateKey), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await Error("/api/customers", Token(TenantA, privateKey, "untrusted-client"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        await Error("/api/customers", Token(TenantA, privateKey, scope: "crm.write"), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        using (var other = RSA.Create(2048))
            await Error("/api/customers", Token(TenantA, new RsaSecurityKey(other) { KeyId = "other" }), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");

        Assert.NotEmpty(capture.Commands);
        var actual = capture.Commands.First(command => command.Sql.Contains(TenantA, StringComparison.Ordinal) ||
            command.Parameters.Any(parameter => Equals(parameter.Value, TenantA)));
        var data = app.Services.GetRequiredService<IDataService>();
        using (app.Services.GetRequiredService<ITenantContext>().BeginScope(TenantA))
        await using (var session = await data.OpenAsync<CrmDbContext>(ct))
        {
            await session.Context.Database.OpenConnectionAsync(ct);
            await using var explain = new NpgsqlCommand("EXPLAIN (ANALYZE, BUFFERS, FORMAT JSON) " + actual.Sql,
                (NpgsqlConnection)session.Context.Database.GetDbConnection());
            foreach (var parameter in actual.Parameters) explain.Parameters.Add((NpgsqlParameter)((ICloneable)parameter).Clone());
            var plan = (string)(await explain.ExecuteScalarAsync(ct))!;
            using var parsed = JsonDocument.Parse(plan);
            TestContext.Current.TestOutputHelper!.WriteLine(plan);
            var evidence = Environment.GetEnvironmentVariable("QIAN_TEST_EVIDENCE_DIR");
            if (!string.IsNullOrWhiteSpace(evidence))
            {
                Directory.CreateDirectory(evidence);
                await File.WriteAllTextAsync(Path.Combine(evidence, "crm-customer-list-explain.json"), plan, ct);
            }
            Assert.True(UsesIndex(parsed.RootElement, CrmDbContext.ListIndex), plan);
        }
        await app.StopAsync(ct);

        async Task<HttpResponseMessage> Get(string path, string? token, string headerTenant = TenantA)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("X-Tenant-Id", headerTenant);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await client.SendAsync(request, ct);
        }
        async Task Error(string path, string? token, HttpStatusCode status, string code)
        {
            using var response = await Get(path, token);
            Assert.Equal(status, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            Assert.Equal(code, body.RootElement.GetProperty("code").GetString());
            Assert.Equal(32, body.RootElement.GetProperty("trace_id").GetString()!.Length);
        }
    }

    private static string Token(string? tenant, SecurityKey key, string client = Client, string scope = "crm.read")
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var jti = Guid.NewGuid().ToString();
        var payload = JsonSerializer.Serialize(new
        {
            iss = Issuer, aud = new[] { Audience }, sub = client, principal_kind = "service", tenant_id = tenant,
            roles = Array.Empty<string>(), scope, session_id = jti, iat = now, nbf = now, exp = now + 60, jti
        });
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(root.FullName, "contracts/v0/s2s-claims.schema.json")),
            new BuildOptions { SchemaRegistry = new SchemaRegistry() });
        using var json = JsonDocument.Parse(payload);
        Assert.True(schema.Evaluate(json.RootElement).IsValid);
        return new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
    }

    private static bool UsesIndex(JsonElement value, string name) => value.ValueKind switch
    {
        JsonValueKind.Object => value.EnumerateObject().Any(property =>
            property.Name == "Index Name" && property.Value.GetString() == name || UsesIndex(property.Value, name)),
        JsonValueKind.Array => value.EnumerateArray().Any(item => UsesIndex(item, name)),
        _ => false
    };

    private sealed record CapturedCommand(string Sql, NpgsqlParameter[] Parameters);
    private sealed class ListQueryCapture : DbCommandInterceptor
    {
        public ConcurrentQueue<CapturedCommand> Commands { get; } = new();
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(CrmApplication.ListQueryTag, StringComparison.Ordinal))
                Commands.Enqueue(new(command.CommandText, command.Parameters.Cast<NpgsqlParameter>()
                    .Select(parameter => (NpgsqlParameter)((ICloneable)parameter).Clone()).ToArray()));
            return ValueTask.FromResult(result);
        }
    }
}
