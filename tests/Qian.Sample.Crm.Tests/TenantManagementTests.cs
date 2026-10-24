using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Surfaces;
using Qian.Tenancy;
using Qian.Tenancy.AspNetCore;
using Qian.Testing.Containers;
using System.Collections.Concurrent;
using System.Text.Json.Nodes;

namespace Qian.Sample.Crm.Tests;

public sealed partial class TenantOperationsTests(PostgresFixture postgres) : CustomerCommandTests(postgres), IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 平台运营HTTP真实超管旁路与审计_创建编排_租户主体拒绝_目标仅路径_编辑并发_完整契约()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        var logs = new OperationsLog();
        await using var management = await ManagementHost(business, platform, logs);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        var path = "/api/tenancy/tenants";
        var source = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global);
        // 放行审计的条数：平台链上引导时安装、激活 system 组件的生命周期条目（S1-RT-037）不算。
        async Task<int> AuditCount() => (await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct))
            .Count(entry => !entry.Record.CapabilityId.StartsWith("qian.lifecycle/plugin-revision.", StringComparison.Ordinal));
        foreach (var token in new[] { (string?)null, business.Token(A, "", "root", "user"), business.Token(null, "", "intruder", "user") })
        {
            var status = token is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            var code = token is null ? "UNAUTHENTICATED" : "AUTHZ_DENIED";
            await AssertHttp(await Request(HttpMethod.Get, path, token), status, code);
            await AssertHttp(await Request(HttpMethod.Post, path, token, new CreateTenant("forbidden", "Forbidden") { Administrator = new(Guid.NewGuid(), "owner") }), status, code);
            await AssertHttp(await Request(HttpMethod.Put, path + "/" + A, token, new UpdateTenant("Forbidden", 0)), status, code);
        }
        var root = business.Token(null, "", "root", "user");
        await AssertHttp(await Request(HttpMethod.Get, path, root[..^5] + "wrong"), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id" })
        {
            var invalid = new Dictionary<string, object?> { ["code"] = "c", ["name"] = "C", [forbidden] = Guid.NewGuid() };
            await AssertHttp(await Request(HttpMethod.Post, path, root, invalid), HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        await AssertHttp(await Request(HttpMethod.Get, path + "?skip=-1", root), HttpStatusCode.BadRequest, "INPUT_INVALID");
        await AssertHttp(await Request(HttpMethod.Get, path + "?take=0", root), HttpStatusCode.BadRequest, "INPUT_INVALID");
        await AssertHttp(await Request(HttpMethod.Get, path + "?take=101", root), HttpStatusCode.BadRequest, "INPUT_INVALID");
        await AssertHttp(await Request(HttpMethod.Post, path, root, new { code = "bad", name = "Bad" }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        await AssertHttp(await Request(HttpMethod.Post, path, root, new { code = "bad", name = "Bad", administrator = (object?)null }), HttpStatusCode.BadRequest, "INPUT_INVALID");
        await AssertHttp(await Request(HttpMethod.Post, path, root, new CreateTenant("bad", "Bad", TenantIsolation.Schema)
            { Administrator = new(Guid.NewGuid(), "owner") }), HttpStatusCode.BadRequest, "INPUT_INVALID");
        Assert.Equal(0, await AuditCount()); // 400/401/403 均未得到成功旁路，不应写放行审计。
        Assert.Equal(6, logs.Entries.Count);
        Assert.Equal(3, logs.Entries.Count(entry => Equals(entry["Reason"], "tenant_principal") && Equals(entry["Subject"], "root")));
        Assert.Equal(3, logs.Entries.Count(entry => Equals(entry["Reason"], "not_superadmin") && Equals(entry["Subject"], "intruder")));
        var create = new CreateTenant("c", "C") { Administrator = new TenantAdministrator(Guid.CreateVersion7(), "owner-c") };
        using var response = await Request(HttpMethod.Post, path, root, create);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var created = (await response.Content.ReadFromJsonAsync<TenantDescriptor>(Ct))!;
        Assert.NotEqual(Guid.Empty, created.Id); Assert.Equal(TenantStatus.Pending, created.Status); Assert.True(created.Revision > 0);
        using var list = await Request(HttpMethod.Get, path + "?skip=0&take=100", root);
        Assert.Contains((await list.Content.ReadFromJsonAsync<TenantList>(Ct))!.Items, item => item.Id == created.Id);
        Assert.Equal(path + "/" + created.Id, response.Headers.Location!.OriginalString);
        using (var single = await Request(HttpMethod.Get, response.Headers.Location.OriginalString, root))
        { Assert.Equal(HttpStatusCode.OK, single.StatusCode); Assert.Equal(created, await single.Content.ReadFromJsonAsync<TenantDescriptor>(Ct)); }
        var beforeRejectedEdit = await AuditCount();
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id", "code", "isolation" })
            await AssertHttp(await Request(HttpMethod.Put, path + "/" + created.Id, root, new Dictionary<string, object?>
                { ["name"] = "bad", ["expectedRevision"] = created.Revision, [forbidden] = "forbidden" }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        Assert.Equal(beforeRejectedEdit, await AuditCount());
        using var updated = await Request(HttpMethod.Put, path + "/" + created.Id, root, new UpdateTenant("Updated", created.Revision));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await AssertHttp(await Request(HttpMethod.Put, path + "/" + created.Id, root, new UpdateTenant("Stale", created.Revision)), HttpStatusCode.Conflict, "CONFLICT");
        await AssertHttp(await Request(HttpMethod.Put, path + "/" + Guid.NewGuid(), root, new UpdateTenant("Missing", 0)), HttpStatusCode.NotFound, "NOT_FOUND");
        await AssertHttp(await Request(HttpMethod.Post, path, business.Token(A, "", "root", "user"), create), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        await AssertHttp(await Request(HttpMethod.Get, path + "/" + Guid.NewGuid(), root), HttpStatusCode.NotFound, "NOT_FOUND");
        using (var visible = await Request(HttpMethod.Get, "/__tests/tenant-openapi", business.Token(A, "", "root", "user")))
        {
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Empty((await visible.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths").EnumerateObject());
        }
        // 放行审计：平台链上引导时安装、激活 system 组件的生命周期条目（S1-RT-037）另算。
        var records = (await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct))
            .Where(entry => !entry.Record.CapabilityId.StartsWith("qian.lifecycle/plugin-revision.", StringComparison.Ordinal)).ToList();
        Assert.Equal(7, records.Count);
        Assert.Equal("create,read,read,edit,edit,edit,read", string.Join(",", records.Select(entry => entry.Record.Payload.GetProperty("permission").GetString()!.Split(':')[^1])));
        Assert.All(records, entry => Assert.Equal("superadmin", entry.Record.Payload.GetProperty("bypass").GetString()));
        var contract = await management.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.tenancy", Ct);
        Assert.Equal(8, contract.GetProperty("paths").EnumerateObject().Count());
        Assert.True(contract.GetProperty("paths").GetProperty(path).TryGetProperty("post", out var post));
        Assert.True(contract.GetProperty("paths").GetProperty(path + "/{id}").TryGetProperty("put", out _));
        var document = JsonNode.Parse(contract.GetRawText())!;
        Assert.Empty(TenantContractSchemaTests.TenantFields(document));
        var inputSchema = TenantContractSchemaTests.Resolve(document, document["paths"]![path]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);
        Assert.Contains("administrator", inputSchema["required"]!.AsArray().Select(value => value!.GetValue<string>()));
        var administrator = TenantContractSchemaTests.Resolve(document, inputSchema["properties"]!["administrator"]!);
        Assert.Equal("\"object\"", administrator["type"]!.ToJsonString());
        Assert.Equal("row", Assert.Single(inputSchema["properties"]!["isolation"]!["enum"]!.AsArray())!.GetValue<string>());
        var parameters = document["paths"]![path]!["get"]!["parameters"]!.AsArray();
        Assert.Equal(0, parameters.Single(item => item!["name"]!.GetValue<string>() == "skip")!["schema"]!["minimum"]!.GetValue<int>());
        var take = parameters.Single(item => item!["name"]!.GetValue<string>() == "take")!["schema"]!;
        Assert.Equal(1, take["minimum"]!.GetValue<int>()); Assert.Equal(100, take["maximum"]!.GetValue<int>()); Assert.Equal(50, take["default"]!.GetValue<int>());
        foreach (var route in document["paths"]!.AsObject())
            foreach (var operation in route.Value!.AsObject())
                foreach (var status in ExpectedErrors) Assert.NotNull(operation.Value!["responses"]![status]!["content"]![ErrorDetailJson.MediaType]);
        Assert.True((await source.Audit().VerifyPlatformAsync(source, Ct)).Valid);
        var contractPath = Path.Combine(RepositoryRoot(), "plugins/qian.tenancy/contracts/http-v1.openapi.json");
        if (Environment.GetEnvironmentVariable("QIAN_UPDATE_TENANCY_CONTRACT") == "1")
        { Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!); File.WriteAllText(contractPath, JsonSerializer.Serialize(contract, ContractJson) + "\n"); }
        var published = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(contractPath));
        Assert.True(JsonElement.DeepEquals(published, contract), "真实端点与发布契约漂移。");
        var runtime = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var probe = new OasDiffOpenApiProbe(Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH")!);
        foreach (var change in new[] { "remove-put", "response-type" })
        {
            var candidate = document.DeepClone();
            if (change == "remove-put") candidate["paths"]![path + "/{id}"]!.AsObject().Remove("put");
            else
            {
                var output = TenantContractSchemaTests.Resolve(candidate, candidate["paths"]![path]!["post"]!["responses"]!["201"]!["content"]!["application/json"]!["schema"]!);
                output["properties"]!["name"] = new JsonObject { ["type"] = "integer" };
            }
            await runtime.UnmountAsync("tenancy", ScopeKey.Global, Ct);
            await runtime.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
                JsonSerializer.SerializeToElement(new { })), new OpenApiContractPlugin(new TenancyPlugin(), probe, "qian.tenancy.http/v1", published,
                    JsonSerializer.SerializeToElement(candidate)), [TenancyDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.TenancyKey])], Ct);
            Assert.Equal(PluginState.Failed, runtime.GetState("tenancy", ScopeKey.Global));
            var rejected = Assert.IsType<QianErrorException>(runtime.GetFailure("tenancy", ScopeKey.Global));
            Assert.Equal("PRECONDITION_FAILED", rejected.Error.Code.Code);
            Assert.Equal("#/contracts/qian.tenancy.http~1v1", rejected.Error.Pointer);
        }
        var admitted = new OpenApiContractPlugin(new TenancyPlugin(), probe, "qian.tenancy.http/v1", published, contract);
        await runtime.UnmountAsync("tenancy", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), admitted, [TenancyDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.TenancyKey])], Ct);
        Assert.True(runtime.GetState("tenancy", ScopeKey.Global) == PluginState.Ready, runtime.GetFailure("tenancy", ScopeKey.Global)?.ToString());


        Task<HttpResponseMessage> Request(HttpMethod method, string url, string? token, object? body = null)
        {
            var request = new HttpRequestMessage(method, url);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
            return client.SendAsync(request, Ct);
        }
    }

    [Fact]
    public async Task 平台运营拒绝原因仅写结构化日志_配置缺席目录缺席审计缺席均无放行审计()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        var logs = new OperationsLog();
        await using var management = await ManagementHost(business, platform, logs);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", business.Token(null, "", "root", "user"));
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var source = loader.GetContext("tenancy", ScopeKey.Global);
        var authz = loader.DumpConfig().Single(entry => entry.Id == "authz");
        var audit = loader.DumpConfig().Single(entry => entry.Id == "audit");
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.tenancy", "1.0.0",
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.tenancy/metadata/permissions.json"))), JsonSerializer.SerializeToElement(Array.Empty<object>()));
        await Authz(false, true); await Denied("platform_role_not_configured");
        await Authz(true, false); await Denied("permission_not_registered");
        await Authz(true, true); await loader.UnmountAsync("audit", ScopeKey.Global, Ct); await Denied("audit_unavailable");
        await loader.MountAsync([new PluginDefinition(audit, new AuditPlugin(), [AuditDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuditKey])], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("audit", ScopeKey.Global));
        // 平台链上只有引导时安装、激活 system 组件的生命周期条目（S1-RT-037），没有放行审计。
        Assert.All(await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct), entry => Assert.StartsWith("qian.lifecycle/plugin-revision.", entry.Record.CapabilityId, StringComparison.Ordinal));
        Assert.Equal(3, logs.Entries.Count);

        async Task Authz(bool role, bool declared)
        {
            await loader.UnmountAsync("authz", ScopeKey.Global, Ct);
            await loader.MountAsync([new PluginDefinition(authz with { Config = role ? authz.Config : JsonSerializer.SerializeToElement(new { }) },
                new AuthzPlugin(declared ? [CrmPermissions.Compile(), catalog] : [CrmPermissions.Compile()]), [AuthzDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuthorizationKey])], Ct);
            Assert.Equal(PluginState.Ready, loader.GetState("authz", ScopeKey.Global));
        }
        async Task Denied(string reason)
        {
            using var response = await client.GetAsync("/api/tenancy/tenants", Ct);
            var body = await response.Content.ReadAsStringAsync(Ct);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode); Assert.Equal("AUTHZ_DENIED", ErrorDetailJson.Deserialize(body)!.Code);
            Assert.DoesNotContain(reason, body, StringComparison.Ordinal);
            var logged = logs.Entries.Last(); Assert.Equal(reason, logged["Reason"]); Assert.Equal("root", logged["Subject"]);
            Assert.Equal("qian.tenancy:tenant:read", logged["Permission"]);
        }
    }
}

public abstract partial class CustomerCommandTests
{
    private protected static readonly string[] ExpectedErrors = ["400", "401", "403", "404", "409"];
    private protected static readonly JsonSerializerOptions ContractJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private protected async Task PreparePlatformLogins(LifecyclePlatform platform)
    {
        // 平台审计登录：所有带生命周期的宿主（平台级操作入平台链）；Authz 平台登录只有管理面宿主。
        platform.AuditRole = "ten_audit_" + Guid.NewGuid().ToString("N")[..10]; platform.AuditConnection = await Login(platform.AuditRole);
        if (!platform.Management) return;
        platform.AuthzRole = "ten_auth_" + Guid.NewGuid().ToString("N")[..10]; platform.AuthzConnection = await Login(platform.AuthzRole);
        async Task<string> Login(string role)
        {
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            await Admin($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';");
            return new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
        }
    }

    private protected static async Task<WebApplication> ManagementHost(RunningApp business, LifecyclePlatform platform, OperationsLog? logs = null, Func<IPluginContext>? context = null,
        string? expectedIssuer = null, string? expectedAudience = null, Action<WebApplication>? configurePipeline = null)
    {
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var permissions = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.tenancy/metadata/permissions.json")));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.tenancy", "1.0.0", permissions, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        var registryCatalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.registry", "1.0.0",
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.registry/metadata/permissions.json"))),
            JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.registry/metadata/roles.json"))));
        await loader.UnmountAsync("authz", ScopeKey.Global, Ct); await loader.UnmountAsync("audit", ScopeKey.Global, Ct);
        await loader.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { chain = new { platformRole = platform.AuditRole } })), new AuditPlugin(), [AuditDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuditKey]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { platformRole = platform.AuthzRole })), new AuthzPlugin([CrmPermissions.Compile(), catalog, registryCatalog]), [AuthzDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuthorizationKey])], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("authz", ScopeKey.Global));
        await new PostgresPlatformRoleStore(business.App.Services.GetRequiredService<IDataService>(), business.App.Services.GetRequiredService<ITenantContext>(), platform.AuthzRole).AssignSuperadminAsync("root", "operator", Ct);
        var lifecycle = LifecyclePlatform.Lifecycle(business);
        foreach (var artifact in platform.SystemArtifacts.Values)
        { var revision = await lifecycle.InstallAsync(artifact, "root", Ct); await lifecycle.ActivateAsync(revision.PluginId, revision.Revision, "root", Ct); }
        if (platform.ProvisionCrmByDefault)
        {
            var crm = await lifecycle.InstallAsync(platform.Artifacts["1.0.0"], "root", Ct);
            await lifecycle.ActivateAsync(crm.PluginId, crm.Revision, "root", Ct);
        }
        var app = TenantOperationsHost.Build(() => context?.Invoke() ?? loader.GetContext("tenancy", ScopeKey.Global),
            new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = expectedIssuer ?? Issuer, ValidateAudience = true, ValidAudience = expectedAudience ?? Audience,
                ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = business.ManagementSigningKey,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero }, builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
                if (logs is not null) builder.Logging.AddProvider(logs);
            });
        // 仅测试路由：从真实已验签的租户请求读取可见聚合，平台路由不得泄露。
        app.MapGet("/__tests/tenant-openapi", async (HttpContext http) =>
        {
            var user = http.RequestServices.GetRequiredService<ICurrentUser>();
            var scoped = loader.GetContext("tenancy", ScopeKey.Global).Scoped(new ScopeKey("tenant", user.Principal!.TenantId!));
            using var tenant = business.App.Services.GetRequiredService<ITenantContext>().BeginScope(scoped.Scope.Id!);
            return Results.Json(await http.RequestServices.GetRequiredService<IHttpContractSource>().GetDocumentAsync(scoped, user, http.RequestAborted));
        }).RequireAuthorization();
        configurePipeline?.Invoke(app);
        await app.StartAsync(Ct); return app;
    }

    private protected sealed class OperationsLog : ILoggerProvider
    {
        internal ConcurrentQueue<IReadOnlyDictionary<string, object?>> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Entries);
        public void Dispose() { }
        private sealed class Sink(string category, ConcurrentQueue<IReadOnlyDictionary<string, object?>> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == "Qian.Tenancy.Operations" && eventId.Id == 1001 && state is IEnumerable<KeyValuePair<string, object?>> values)
                    entries.Enqueue(values.ToDictionary(pair => pair.Key, pair => pair.Value));
            }
        }
    }

    private protected static async Task AssertHttp(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        { var body = await response.Content.ReadAsStringAsync(Ct); Assert.True(response.StatusCode == status, body); Assert.Equal(code, ErrorDetailJson.Deserialize(body)!.Code); }
    }
}
