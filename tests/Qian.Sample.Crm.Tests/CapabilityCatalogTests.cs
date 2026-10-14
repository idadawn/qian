using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
using System.Globalization;
using System.Security.Cryptography;
using Json.Schema;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qian.Audit;
using Qian.Authz;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;
using Qian.Data;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Profiles;
using Qian.Profiles.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    private const string CreateId = "qian.sample.crm/customer.create";
    private static readonly string[] CatalogScopes = ["global", "tenant"];

    [Fact]
    public async Task 目录别名与真实调用共享授权_生成条目满足冻结契约_无权限与缺scope均无能力()
    {
        await using var host = await Start(capabilities: true);
        using var anonymous = await host.Client.GetAsync("/api/capabilities", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var response = await CatalogRequest(host, A, "/api/capabilities");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var catalog = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("private", catalog.GetProperty("cacheScope").GetString());
        Assert.True(catalog.GetProperty("ttlMs").GetInt32() > 0);
        Assert.True(response.Headers.CacheControl!.Private);
        var items = catalog.GetProperty("items").EnumerateArray().ToArray(); Assert.Equal(3, items.Length);
        var schema = JsonSchema.FromText(File.ReadAllText(Path.Combine(RepositoryRoot(), "contracts/v0/capability-entry.schema.json")));
        foreach (var entry in items)
        {
            var check = schema.Evaluate(entry, new EvaluationOptions { OutputFormat = OutputFormat.List });
            Assert.True(check.IsValid, JsonSerializer.Serialize(check));
            Assert.False(entry.TryGetProperty("binding", out _)); Assert.False(entry.TryGetProperty("config", out _));
        }
        var created = Assert.Single(items, item => item.GetProperty("id").GetString() == CreateId);
        Assert.True(created.GetProperty("inputSchema").GetProperty("properties").GetProperty("name").GetProperty("sensitive").GetBoolean());
        using var alias = await CatalogRequest(host, A, "/api/kernel/capabilities");
        Assert.True(alias.Headers.Contains("Deprecation"));
        Assert.True(JsonElement.DeepEquals(catalog, await alias.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        using var denied = await CatalogRequest(host, B, "/api/capabilities");
        var deniedCatalog = await denied.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.DoesNotContain(deniedCatalog.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetString() == CreateId);
        using var missingScope = await CatalogRequest(host, A, "/api/capabilities", "crm.read");
        var scopeCatalog = await missingScope.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.DoesNotContain(scopeCatalog.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetString() == CreateId);
        using var filtered = await CatalogRequest(host, A, "/api/capabilities?kind=command&q=create");
        Assert.Single((await filtered.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        using var invoked = await Invoke(host, A);
        Assert.True(invoked.StatusCode == HttpStatusCode.OK, host.FailureEvidence + await invoked.Content.ReadAsStringAsync(Ct));
        var customer = (await invoked.Content.ReadFromJsonAsync<CustomerItem>(Ct))!;
        Assert.Equal(A, customer.TenantId);
        using (host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A))
        {
            await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
            Assert.Equal(customer.Id, Assert.Single(await session.Context.Customers.ToListAsync(Ct)).Id);
            Assert.Single(await session.Context.OutboxMessages.ToListAsync(Ct));
            Assert.Equal(new AuditChainVerification(true, 1, null, null), await host.App.Services.GetRequiredService<CrmCommandRuntime>().Source().Audit().VerifyAsync(Ct));
        }
        using var forbidden = await Invoke(host, B); Assert.Equal(HttpStatusCode.NotFound, forbidden.StatusCode);
    }

    [Fact]
    public async Task 真实权限与租户restrict在目录提示执行三处一致_屏蔽后无写入_卸载不残留()
    {
        await using var host = await Start(capabilities: true);
        using var initial = await CatalogRequest(host, A, "/api/acceptance/prompt");
        Assert.Contains(CreateId, await initial.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        using var unauthorized = await CatalogRequest(host, B, "/api/acceptance/prompt");
        Assert.DoesNotContain(CreateId, await unauthorized.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        var loader = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using (loader.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != CapabilityBindingRegistration.Key))
        {
            await AssertAbsent(host, A);
            using var other = await CatalogRequest(host, B, "/api/capabilities");
            Assert.NotEmpty((await other.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        }
        using var restored = await CatalogRequest(host, A, "/api/capabilities");
        Assert.Equal(3, (await restored.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").GetArrayLength());
        await loader.UnmountAsync("crm", ScopeKey.Global, Ct);
        await AssertAbsent(host, A);
        using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(A);
        await using var session = await host.App.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct);
        Assert.Empty(await session.Context.Customers.ToListAsync(Ct)); Assert.Empty(await session.Context.OutboxMessages.ToListAsync(Ct));
    }

    [Fact]
    public async Task 真库scope禁用_撤权_租户冻结即时在三处拒绝且恢复后可见()
    {
        await using var host = await Start(capabilities: true);
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var data = host.App.Services.GetRequiredService<IDataService>();
        var patches = new PostgresPatchRepository(data, tenant, new SurfaceValidator());
        using (tenant.BeginScope(A))
            await patches.PutAsync(new ScopeKey("tenant", A), "crm", JsonSerializer.SerializeToElement(new { id = "crm", disabled = true }), 0, "acceptance", Ct);
        await AssertAbsent(host, A);
        using var other = await CatalogRequest(host, B, "/api/capabilities");
        Assert.NotEmpty((await other.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        using (tenant.BeginScope(A)) await patches.RemoveAsync(new ScopeKey("tenant", A), "crm", 1, "acceptance", Ct);
        using (tenant.BeginScope(A))
        {
            await using var session = await data.OpenAsync<AuthzDbContext>(Ct);
            await session.Context.RoleAssignments.Where(row => row.SubjectType == "service" && row.SubjectId == Writer).ExecuteDeleteAsync(Ct);
        }
        await AssertAbsent(host, A);
        using (tenant.BeginScope(A)) await new PostgresTenantRoleStore(data, tenant).AssignAsync("tenant.owner", "service", Writer, "acceptance", Ct);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = 'suspended' WHERE id = '{A}'::uuid;");
        await AssertAbsent(host, A);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET status = 'active' WHERE id = '{A}'::uuid;");
        using var restored = await CatalogRequest(host, A, "/api/capabilities");
        Assert.Equal(3, (await restored.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").GetArrayLength());
        using (tenant.BeginScope(A))
        {
            await using var session = await data.OpenAsync<CrmDbContext>(Ct);
            Assert.Empty(await session.Context.Customers.ToListAsync(Ct)); Assert.Empty(await session.Context.OutboxMessages.ToListAsync(Ct));
        }
    }

    [Fact]
    public async Task 认证OpenAPI只包含可调用绑定_请求响应Schema对应真实执行_隐藏后移除()
    {
        await using var host = await Start(capabilities: true);
        using var anonymous = await host.Client.GetAsync("/api/capabilities/openapi.json", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        using var response = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
        Assert.True(response.StatusCode == HttpStatusCode.OK, host.FailureEvidence);
        var api = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("3.1.1", api.GetProperty("openapi").GetString());
        using var alias = await CatalogRequest(host, A, "/api/kernel/capabilities/openapi.json");
        Assert.True(alias.Headers.Contains("Deprecation"));
        Assert.True(JsonElement.DeepEquals(api, await alias.Content.ReadFromJsonAsync<JsonElement>(Ct)));
        var parsed = Microsoft.OpenApi.OpenApiDocument.Parse(api.GetRawText(), "json");
        Assert.NotNull(parsed.Document); Assert.Empty(parsed.Diagnostic!.Errors);
        var paths = api.GetProperty("paths"); Assert.Equal(12, paths.EnumerateObject().Count());
        Assert.True(paths.GetProperty("/api/capabilities/navigation").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/customers").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/api/customers").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/customers/{id}").TryGetProperty("get", out _));
        var businessSchemas = ReferencedBusinessSchemas(api);
        Assert.NotEmpty(businessSchemas);
        var operation = paths.GetProperty("/api/capabilities/" + CreateId + "/invoke").GetProperty("post");
        Assert.Equal(CreateId, operation.GetProperty("x-qian-capability").GetString());
        var reference = operation.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
        var input = api.GetProperty("components").GetProperty("schemas").GetProperty(reference.Split('/')[^1]);
        using var accepted = await Invoke(host, A);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var actualRequest = JsonSerializer.SerializeToElement(new CreateCustomerCommand(Guid.CreateVersion7(), "schema proof"), CrmCommandJson.Default.CreateCustomerCommand);
        Assert.True(JsonSchema.FromText(input.GetRawText()).Evaluate(actualRequest).IsValid);
        var outputReference = operation.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema").GetProperty("$ref").GetString()!;
        var output = api.GetProperty("components").GetProperty("schemas").GetProperty(outputReference.Split('/')[^1]);
        Assert.True(JsonSchema.FromText(output.GetRawText()).Evaluate(await accepted.Content.ReadFromJsonAsync<JsonElement>(Ct)).IsValid);
        using var denied = await CatalogRequest(host, B, "/api/capabilities/openapi.json");
        var deniedDocument = await denied.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.False(deniedDocument.GetProperty("paths").TryGetProperty("/api/capabilities/" + CreateId + "/invoke", out _));
        Assert.False(deniedDocument.GetProperty("paths").GetProperty("/api/customers").TryGetProperty("post", out _));
        Assert.True(deniedDocument.GetProperty("paths").GetProperty("/api/customers").TryGetProperty("get", out _));
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using (runtime.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != CapabilityBindingRegistration.Key))
        {
            using var hidden = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
            var empty = await hidden.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.DoesNotContain(empty.GetProperty("paths").EnumerateObject(), item => item.Name.StartsWith("/api/customers", StringComparison.Ordinal) || item.Name.Contains("/qian.sample.crm/", StringComparison.Ordinal));
            foreach (var schemaName in businessSchemas) Assert.False(empty.GetProperty("components").GetProperty("schemas").TryGetProperty(schemaName, out _));
            using var directRest = await host.Get(A, "/api/customers"); Assert.Equal(HttpStatusCode.NotFound, directRest.StatusCode);
        }
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        using var unmounted = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
        Assert.DoesNotContain((await unmounted.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths").EnumerateObject(), item => item.Name.StartsWith("/api/customers", StringComparison.Ordinal) || item.Name.Contains("/qian.sample.crm/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task 真实契约检查先于业务Apply_不兼容组件挂载失败且兼容修订能实际执行()
    {
        await using var host = await Start(capabilities: true);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var repository = runtime.GetContext("capabilities", ScopeKey.Global).Contracts();
        Assert.Equal(2, repository.GetContracts("qian.sample.crm", "0.9.0").Count);
        Assert.Equal(2, repository.GetContracts("qian.sample.crm", "1.0.0").Count);
        Assert.Equal(2, repository.GetContracts("qian.sample.crm", "1.1.0").Count);
        var entry = runtime.DumpConfig().Single(item => item.Id == "crm");
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(entry with { Version = "1.1.0" },
            OpenApiContractPlugin.FromRepository(new CrmPlugin(), repository, "qian.sample.crm", "0.9.0", "1.1.0", "qian.sample.crm.api/v1"),
            [CrmDefinition.Key])], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("crm", ScopeKey.Global));
        var failure = Assert.IsType<Qian.Runtime.Errors.QianErrorException>(runtime.GetFailure("crm", ScopeKey.Global));
        Assert.Equal("PRECONDITION_FAILED", failure.Error.Code.Code);
        await AssertAbsent(host, A);
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(entry,
            OpenApiContractPlugin.FromRepository(new CrmPlugin(), repository, "qian.sample.crm", "0.9.0", "1.0.0", "qian.sample.crm.api/v1"),
            [CrmDefinition.Key])], Ct);
        Assert.True(runtime.GetState("crm", ScopeKey.Global) == PluginState.Ready, runtime.GetFailure("crm", ScopeKey.Global)?.ToString());
        using var created = await Invoke(host, A);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    [Fact]
    public async Task 输入Schema不合格的能力在登记阶段被拒绝_组件挂载失败_目录与执行入口都没有该能力_修复后恢复()
    {
        await using var host = await Start(capabilities: true);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var entry = runtime.DumpConfig().Single(item => item.Id == "crm");
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(entry, new TenantFieldProbePlugin(new CrmPlugin()), [CrmDefinition.Key])], Ct);
        // 平台宿主登记了契约检查：ProvideCapability 生成条目时即拒绝，异常带组件、能力短名、条目内位置与原因码。
        Assert.Equal(PluginState.Failed, runtime.GetState("crm", ScopeKey.Global));
        var failure = Assert.IsType<CapabilityContractException>(runtime.GetFailure("crm", ScopeKey.Global));
        Assert.Equal(("qian.sample.crm", "customer.tenant-probe", TenantProbeQuery.Id + "#/inputSchema/properties/tenantId", "tenant-field"),
            (failure.ComponentId, failure.Name, failure.Location, failure.Rule));
        Assert.Equal("INPUT_INVALID", failure.Data["reasonCode"]);
        // 组件 Apply 失败，已登记的能力随作用域一起撤销；目录、提示与执行入口都没有该能力，执行入口是 404 而不是 500。
        Assert.DoesNotContain(runtime.GetContext("capabilities", ScopeKey.Global).Scoped(new ScopeKey("tenant", A)).List(CapabilityBindingRegistration.Key),
            item => item.Name == TenantProbeQuery.Id);
        await AssertAbsent(host, A);
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/" + TenantProbeQuery.Id + "/invoke") { Content = new StringContent("""{"tenantId":"x","take":1}""", Encoding.UTF8, "application/json") };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(A, "crm.read crm.write"));
        using (var probe = await host.Send(request)) Assert.Equal(HttpStatusCode.NotFound, probe.StatusCode);
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(entry, new CrmPlugin(), [CrmDefinition.Key])], Ct);
        Assert.True(runtime.GetState("crm", ScopeKey.Global) == PluginState.Ready, runtime.GetFailure("crm", ScopeKey.Global)?.ToString());
        using var created = await Invoke(host, A);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
    }

    // 在真实 CRM 组件之后再登记一个输入带租户字段的能力（租户只能来自令牌，PRD 7.2.1）。
    private sealed class TenantFieldProbePlugin(IQianPlugin inner) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => inner.Inject;
        public async ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            await inner.ApplyAsync(ctx, config, ct);
            ctx.ProvideCapability<TenantProbeQuery, CustomerPage>();
        }
    }

    internal sealed record TenantProbeQuery(string TenantId, int Take) : ICapabilityRequest<CustomerPage>
    {
        internal const string Id = "qian.sample.crm/customer.tenant-probe";
        public static IReadOnlyList<string> RequiredPermissions { get; } = ["qian.sample.crm:customer:read"];
        public static CapabilityContract QianCapability { get; } = new("qian.sample.crm",
            """{"name":"customer.tenant-probe","description":"按调用方给出的租户读取客户；契约不合格，平台宿主在登记阶段拒绝。","sideEffect":"read","policyHint":"allow","idempotent":false,"async":"none","requiredScopes":["crm.read"],"requiredSecrets":[],"dataClassification":"internal","costTier":"low","contractVersion":"crm-v1","audit":false,"agent":{"expose":false,"requiresConfirmation":false},"binding":{"kind":"command","id":"qian.sample.crm/customer.tenant-probe"},"requiredPermission":"qian.sample.crm:customer:read"}""",
            ProbeJson.Default.TenantProbeQuery, CrmCommandJson.Default.CustomerPage);
    }

    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true)]
    [JsonSerializable(typeof(TenantProbeQuery))]
    internal sealed partial class ProbeJson : JsonSerializerContext;

    [Fact]
    public async Task 完整HTTP制品独立于调用者权限_原生路由删除在挂载前被拒绝()
    {
        await using var host = await Start(capabilities: true);
        using var limited = await CatalogRequest(host, B, "/api/capabilities/openapi.json");
        Assert.False((await limited.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths").GetProperty("/api/customers").TryGetProperty("post", out _));
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var repository = runtime.GetContext("capabilities", ScopeKey.Global).Contracts();
        var baseline = repository.Find("qian.sample.crm", "0.9.0", "qian.sample.crm.http/v1"); Assert.NotNull(baseline);
        using var full = JsonDocument.Parse(baseline.ReadContent());
        Assert.True(full.RootElement.GetProperty("paths").GetProperty("/api/customers").TryGetProperty("post", out _));
        Assert.True((await repository.CheckOpenApiAsync("qian.sample.crm", "0.9.0", "1.0.0", "qian.sample.crm.http/v1", Ct)).IsCompatible);
        Assert.False((await repository.CheckOpenApiAsync("qian.sample.crm", "0.9.0", "1.1.0", "qian.sample.crm.http/v1", Ct)).IsCompatible);
        var entry = runtime.DumpConfig().Single(item => item.Id == "crm");
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(entry with { Version = "1.1.0" },
            OpenApiContractPlugin.FromRepository(new CrmPlugin(), repository, "qian.sample.crm", "0.9.0", "1.1.0", "qian.sample.crm.http/v1"), [CrmDefinition.Key])], Ct);
        Assert.Equal(PluginState.Failed, runtime.GetState("crm", ScopeKey.Global));
        using var rejected = await host.Post(A, new CreateCustomerCommand(Guid.CreateVersion7(), "must not exist"));
        Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
    }

    private static HashSet<string> ReferencedBusinessSchemas(JsonElement document)
    {
        var names = new HashSet<string>(StringComparer.Ordinal); var pending = new Queue<string>();
        foreach (var path in document.GetProperty("paths").EnumerateObject())
            if (path.Name.StartsWith("/api/customers", StringComparison.Ordinal) || path.Name.Contains("/qian.sample.crm/", StringComparison.Ordinal)) Visit(path.Value);
        while (pending.TryDequeue(out var key)) Visit(document.GetProperty("components").GetProperty("schemas").GetProperty(key));
        return names;
        void Visit(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
                foreach (var member in value.EnumerateObject())
                {
                    if (member.Name == "$ref" && member.Value.ValueKind == JsonValueKind.String && member.Value.GetString() is { } reference && reference.StartsWith("#/components/schemas/", StringComparison.Ordinal))
                    { var key = reference.Split('/')[3]; if (names.Add(key)) pending.Enqueue(key); }
                    else Visit(member.Value);
                }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Visit(item);
        }
    }

    private static async Task AssertAbsent(RunningApp host, string tenant)
    {
        using var list = await CatalogRequest(host, tenant, "/api/capabilities"); Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Empty((await list.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("items").EnumerateArray());
        using var prompt = await CatalogRequest(host, tenant, "/api/acceptance/prompt"); Assert.Equal(HttpStatusCode.OK, prompt.StatusCode);
        Assert.DoesNotContain("qian.sample.crm/", await prompt.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        using var invoke = await Invoke(host, tenant); Assert.Equal(HttpStatusCode.NotFound, invoke.StatusCode);
    }
    private static Task<HttpResponseMessage> CatalogRequest(RunningApp host, string tenant, string path, string scopes = "crm.read crm.write")
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, scopes));
        return host.Send(request);
    }
    private static Task<HttpResponseMessage> Invoke(RunningApp host, string tenant)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/capabilities/qian.sample.crm/customer.create/invoke")
            { Content = JsonContent.Create(new CreateCustomerCommand(Guid.CreateVersion7(), "catalog customer")) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", host.Token(tenant, "crm.read crm.write"));
        return host.Send(request);
    }
    private static void ConfigureCapabilities(WebApplicationBuilder builder, string connection, string navigationConnection, string migrations)
    {
        builder.Services.AddKunSecurity();
        builder.Services.AddQianHttpContracts();
        builder.Services.AddQianPluginEndpoints(sp => sp.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("registry", ScopeKey.Global));
        builder.Services.AddQianData<CapabilitiesDbContext>((_, options) => options.UseNpgsql(navigationConnection).UseKunSchema(CapabilitiesDbContext.Schema));
        // 只有能力上下文随包带迁移；迁移连接用属主角色，在线连接仍是受限业务角色。
        builder.Services.AddQianDataMigrations(options => options.ResolveConnection = (_, type) => type == typeof(CapabilitiesDbContext) ? migrations : null);
        builder.Services.AddQianData<RegistryDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(RegistryDbContext.Schema));
        builder.Services.AddSingleton<ISurfaceReader, SurfaceValidator>();
        builder.Services.AddSingleton<IToolContractHasher, ToolContractHasher>();
        // 登记期契约检查：组件 ProvideCapability 时生成目录条目并预编译输入校验器，不合格的契约在登记阶段拒绝。
        builder.Services.AddQianCapabilities();
        builder.Services.AddSingleton<IOpenApiCompatibilityProbe>(_ => new OasDiffOpenApiProbe(Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH")
            ?? throw new InvalidOperationException("契约检查器必须预装。")));
    }
    private static IPluginContext CapabilitySource(HttpContext http)
    {
        var services = http.RequestServices;
        var tenant = services.GetRequiredService<ITenantContext>().TenantId!;
        return services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("capabilities", ScopeKey.Global).Scoped(new ScopeKey("tenant", tenant));
    }
    private static void MapCapabilities(WebApplication app)
    {
        app.Use(async (http, next) =>
        {
            try { await next(http); }
            catch (Exception error)
            {
                app.Services.GetRequiredService<FailureEvidence>().Failures.Enqueue(error.GetType().Name + "\n" + error.Data["contractDiagnostics"] + "\n" + error.Data["contractErrors"] + "\n" + error.StackTrace);
                throw;
            }
        });
        app.UseQianPluginEndpoints();
        app.MapQianCapabilities(CapabilitySource);
        // 仅测试宿主的入口胶水；目录、提示与执行全部调用同一个生产 Provider。
        app.MapGet("/api/acceptance/prompt", async (HttpContext http) =>
        {
            var caller = CapabilitySource(http);
            return Results.Text(await caller.Capabilities().ComposePromptAsync(caller,
                http.RequestServices.GetRequiredService<Qian.Identity.Abstractions.ICurrentUser>(), http.RequestAborted));
        }).RequireAuthorization();
    }
    // probes 随 crm 条目一起登记（同一组件、同一安装条目）；capabilitiesConfig 是 qian.capabilities 条目的组件配置，缺省为 {}。
    private static async Task AttachCapabilities(WebApplication app, IReadOnlyList<IExecutableCapability> probes, object? capabilitiesConfig = null)
    {
        var loader = app.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var directory = Path.Combine(Path.GetTempPath(), "qian-capabilities-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        app.Lifetime.ApplicationStopped.Register(() => Directory.Delete(directory, true));
        var validator = new SurfaceValidator(); var installed = new List<RegistryComponentBinding>();
        var options = new ProfileLoadOptions { WorkingDirectory = directory, Profile = "platform", IncludeHomePatch = false };
        var definitions = new Dictionary<string, PluginDefinition>(StringComparer.Ordinal);
        PluginDefinition? scopedCrm = null;
        Add("data", "qian.data", new DataPlugin(), [DataDefinition.Key]);
        Add("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey]);
        Add("authz", "qian.authz", new RbacCommandPolicyPlugin(), [CommandPolicies.AuthorizationKey]);
        Add("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]);
        Add("crm", "qian.sample.crm", new CrmPlugin(), [CrmDefinition.Key]);
        var registry = new RegistryPlugin(new RegistryRuntimeOptions
        {
            InstalledComponents = installed, Runtime = loader, Profiles = new ProfileLoader(validator), Profile = options, Surfaces = validator,
            Resolve = entry => (entry.Plugin == "qian.sample.crm@1.0.1" ? scopedCrm! : definitions[entry.Id]) with { Entry = new PluginEntry(entry.Id, entry.Plugin.Split('@')[0], entry.Plugin.Split('@')[1],
                entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]), entry.Layer, entry.Config) }
        });
        Add("registry", "qian.registry", registry, [RegistryDefinition.Key]);
        Add("capabilities", "qian.capabilities", new CapabilitiesPlugin(), [CapabilitiesDefinition.Key, CapabilitiesDefinition.ContractsKey, CapabilitiesDefinition.NavigationKey], capabilitiesConfig);
        // 组件配置按仓库交付的 config.schema.json 校验（profile 装载要求有配置的组件提供它）。
        Write("plugins/capabilities/schemas/config.schema.json", File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.capabilities/schemas/config.schema.json")));
        Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
        Write("profiles/platform/qian.patch.yml", JsonSerializer.Serialize(definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin + "@" + item.Entry.Version, config = item.Entry.Config })));
        var fullContract = CapabilityContractCompiler.CreateOpenApi(
            [CreateCustomerCommand.QianCapability, ListCustomersQuery.QianCapability, GetCustomerQuery.QianCapability], validator, new ToolContractHasher());
        Write("plugins/crm/metadata/contracts.json", JsonSerializer.Serialize(new[]
            { new { id = "qian.sample.crm.api/v1", kind = "openapi", path = "./contracts/http-v1.openapi.json" },
              new { id = "qian.sample.crm.http/v1", kind = "openapi", path = "./contracts/rest-v1.openapi.json" } }));
        Write("plugins/crm/contracts/http-v1.openapi.json", fullContract.GetRawText());
        var nativeContract = await app.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.sample.crm", Ct);
        Write("plugins/crm/contracts/rest-v1.openapi.json", nativeContract.GetRawText());
        Write("plugins/crm/ui/manifest.json", """{"pages":[{"route":"/customers","expose":"CustomersPage","permission":"qian.sample.crm:customer:read","nav":{"title":"客户","icon":"users","order":10}},{"route":"/customers/new","expose":"CreateCustomerPage","permission":"qian.sample.crm:customer:write","nav":{"title":"新建客户","order":20}}]}""");
        foreach (var definition in definitions.Values.ToArray())
        {
            var validation = validator.ValidateComponent(Path.Combine(directory, "plugins", definition.Entry.Id),
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
            Assert.True(validation.IsValid, definition.Entry.Id + ": " + JsonSerializer.Serialize(validation.Diagnostics));
            var binding = BindArtifact(definition, Path.Combine(directory, "plugins", definition.Entry.Id));
            installed.Add(binding);
            if (definition.Entry.Id == "crm")
            {
                var plugin = probes.Count == 0 ? definition.Plugin : new ProbePlugin(definition.Plugin, probes);
                definitions["crm"] = definition with { Plugin = new NavigationManifestPlugin(plugin, binding.Metadata, validator) };
                installed.Add(BindArtifact(definition with { Entry = definition.Entry with { Version = "0.9.0" } }, Path.Combine(directory, "plugins", "crm")));
                installed.Add(BindArtifact(definition with { Entry = definition.Entry with { Version = "1.1.0" } }, Path.Combine(directory, "plugins", "crm"), incompatible: true));
            }
        }
        foreach (var file in Directory.EnumerateFiles(Path.Combine(directory, "plugins/crm"), "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(directory, "plugins/crm-scope", Path.GetRelativePath(Path.Combine(directory, "plugins/crm"), file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(file, target);
        }
        var scopedManifest = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "plugins/crm-scope/.qian-plugin/plugin.json")))!;
        scopedManifest["version"] = "1.0.1"; Write("plugins/crm-scope/.qian-plugin/plugin.json", scopedManifest.ToJsonString());
        var scopedUi = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "plugins/crm-scope/ui/manifest.json")))!;
        scopedUi["pages"]![0]!["nav"]!["title"] = "租户专属客户"; Write("plugins/crm-scope/ui/manifest.json", scopedUi.ToJsonString());
        var scopedDefinition = definitions["crm"] with { Entry = definitions["crm"].Entry with { Version = "1.0.1" }, Plugin = new CrmPlugin() };
        var scopedBinding = BindArtifact(scopedDefinition, Path.Combine(directory, "plugins/crm-scope")); installed.Add(scopedBinding);
        scopedCrm = scopedDefinition with { Plugin = new NavigationManifestPlugin(scopedDefinition.Plugin, scopedBinding.Metadata, validator) };
        await loader.MountAsync([definitions["registry"], definitions["capabilities"]], Ct);
        await loader.UnmountAsync("crm", ScopeKey.Global, Ct);
        await loader.MountAsync([definitions["crm"]], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("registry", ScopeKey.Global));
        Assert.Equal(PluginState.Ready, loader.GetState("capabilities", ScopeKey.Global));
        await loader.GetContext("registry", ScopeKey.Global).Registry().StartAsync(Ct);
        return;

        RegistryComponentBinding BindArtifact(PluginDefinition definition, string source, bool incompatible = false)
        {
            var files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToDictionary(
                file => Path.GetRelativePath(source, file).Replace(Path.DirectorySeparatorChar, '/'),
                file => new ReadOnlyMemory<byte>(File.ReadAllBytes(file)), StringComparer.Ordinal);
            var manifest = System.Text.Json.Nodes.JsonNode.Parse(files[".qian-plugin/plugin.json"].Span)!;
            manifest["version"] = definition.Entry.Version;
            files[".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes(manifest.ToJsonString());
            if (incompatible)
            {
                foreach (var name in new[] { "contracts/http-v1.openapi.json", "contracts/rest-v1.openapi.json" })
                {
                    var document = System.Text.Json.Nodes.JsonNode.Parse(files[name].Span)!;
                    document["paths"] = new System.Text.Json.Nodes.JsonObject(); files[name] = Encoding.UTF8.GetBytes(document.ToJsonString());
                }
            }
            var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
            var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
            return RegistryComponentBinding.FromArtifact(definition, files, digest, validator,
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
        }

        void Add(string id, string component, IQianPlugin plugin, ServiceKey[] provides, object? config = null)
        {
            var definition = new PluginDefinition(new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(config ?? new { })), plugin, provides);
            definitions.Add(id, definition);
            Write($"plugins/{id}/.qian-plugin/plugin.json", JsonSerializer.Serialize(new
            {
                id = component, version = "1.0.0", description = "能力目录集成验收使用实际 Provider 的平台组合。", author = new { name = "Qian" }, license = "Apache-2.0",
                @interface = new { displayName = id, category = "testing" }, inject = plugin.Inject.Select(key => key.Name), provides = provides.Select(key => key.Name),
                policy = new { tier = "official", sandbox = "none", authentication = "ON_INSTALL", required = false }, scopes = CatalogScopes
            }));
        }
        void Write(string path, string content)
        {
            var file = Path.Combine(directory, path); Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, content);
        }
    }
    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到契约根目录。");
    }
}
