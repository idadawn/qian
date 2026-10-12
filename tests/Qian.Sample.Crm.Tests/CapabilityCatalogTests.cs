using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
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
        var paths = api.GetProperty("paths"); Assert.Equal(3, paths.EnumerateObject().Count());
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
        Assert.False((await denied.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths").TryGetProperty("/api/capabilities/" + CreateId + "/invoke", out _));
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using (runtime.GetContext("data", ScopeKey.Global).Restrict(new ScopeKey("tenant", A), registration => registration.Key != CapabilityBindingRegistration.Key))
        {
            using var hidden = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
            var empty = await hidden.Content.ReadFromJsonAsync<JsonElement>(Ct);
            Assert.Empty(empty.GetProperty("paths").EnumerateObject());
            Assert.Empty(empty.GetProperty("components").GetProperty("schemas").EnumerateObject());
        }
        await runtime.UnmountAsync("crm", ScopeKey.Global, Ct);
        using var unmounted = await CatalogRequest(host, A, "/api/capabilities/openapi.json");
        Assert.Empty((await unmounted.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("paths").EnumerateObject());
    }

    [Fact]
    public async Task 真实契约检查先于业务Apply_不兼容组件挂载失败且兼容修订能实际执行()
    {
        await using var host = await Start(capabilities: true);
        var runtime = host.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var repository = runtime.GetContext("capabilities", ScopeKey.Global).Contracts();
        Assert.Single(repository.GetContracts("qian.sample.crm", "0.9.0"));
        Assert.Single(repository.GetContracts("qian.sample.crm", "1.0.0"));
        Assert.Single(repository.GetContracts("qian.sample.crm", "1.1.0"));
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
    private static void ConfigureCapabilities(WebApplicationBuilder builder, string connection)
    {
        builder.Services.AddKunSecurity();
        builder.Services.AddQianData<RegistryDbContext>((_, options) => options.UseNpgsql(connection).UseKunSchema(RegistryDbContext.Schema));
        builder.Services.AddSingleton<ISurfaceReader, SurfaceValidator>();
        builder.Services.AddSingleton<IToolContractHasher, ToolContractHasher>();
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
                app.Services.GetRequiredService<FailureEvidence>().Failures.Enqueue(error.GetType().Name + "\n" + error.StackTrace);
                throw;
            }
        });
        app.MapQianCapabilities(CapabilitySource);
        // 仅测试宿主的入口胶水；目录、提示与执行全部调用同一个生产 Provider。
        app.MapGet("/api/acceptance/prompt", async (HttpContext http) =>
        {
            var caller = CapabilitySource(http);
            return Results.Text(await caller.Capabilities().ComposePromptAsync(caller,
                http.RequestServices.GetRequiredService<Qian.Identity.Abstractions.ICurrentUser>(), http.RequestAborted));
        }).RequireAuthorization();
    }
    private static async Task AttachCapabilities(WebApplication app)
    {
        var loader = app.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var directory = Path.Combine(Path.GetTempPath(), "qian-capabilities-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        app.Lifetime.ApplicationStopped.Register(() => Directory.Delete(directory, true));
        var validator = new SurfaceValidator(); var installed = new List<RegistryComponentBinding>();
        var options = new ProfileLoadOptions { WorkingDirectory = directory, Profile = "platform", IncludeHomePatch = false };
        var definitions = new Dictionary<string, PluginDefinition>(StringComparer.Ordinal);
        Add("data", "qian.data", new DataPlugin(), [DataDefinition.Key]);
        Add("tenancy", "qian.tenancy", new TenancyPlugin(), [TenancyDefinition.Key, CommandPolicies.TenancyKey]);
        Add("authz", "qian.authz", new RbacCommandPolicyPlugin(), [CommandPolicies.AuthorizationKey]);
        Add("audit", "qian.audit", new AuditPlugin(), [AuditDefinition.Key, CommandPolicies.AuditKey]);
        Add("crm", "qian.sample.crm", new CrmPlugin(), [CrmDefinition.Key]);
        var registry = new RegistryPlugin(new RegistryRuntimeOptions
        {
            InstalledComponents = installed, Runtime = loader, Profiles = new ProfileLoader(validator), Profile = options, Surfaces = validator,
            Resolve = entry => definitions[entry.Id] with { Entry = new PluginEntry(entry.Id, entry.Plugin.Split('@')[0], "1.0.0",
                entry.Scope == "global" ? ScopeKey.Global : new ScopeKey(entry.Scope.Split(':', 2)[0], entry.Scope.Split(':', 2)[1]), entry.Layer, entry.Config) }
        });
        Add("registry", "qian.registry", registry, [RegistryDefinition.Key]);
        Add("capabilities", "qian.capabilities", new CapabilitiesPlugin(), [CapabilitiesDefinition.Key, CapabilitiesDefinition.ContractsKey]);
        Write("profiles/platform/qian.profile.yml", """{"name":"platform","bundles":[],"patch":"./qian.patch.yml","hmr":{"surfaces":true,"code":false},"build":{"aot":false},"hooks":{"allowCommand":false}}""");
        Write("profiles/platform/qian.patch.yml", JsonSerializer.Serialize(definitions.Values.Select(item => new { id = item.Entry.Id, plugin = item.Entry.Plugin, config = new { } })));
        var fullContract = CapabilityContractCompiler.CreateOpenApi(
            [CreateCustomerCommand.QianCapability, ListCustomersQuery.QianCapability, GetCustomerQuery.QianCapability], validator, new ToolContractHasher());
        Write("plugins/crm/metadata/contracts.json", JsonSerializer.Serialize(new[]
            { new { id = "qian.sample.crm.api/v1", kind = "openapi", path = "./contracts/http-v1.openapi.json" } }));
        Write("plugins/crm/contracts/http-v1.openapi.json", fullContract.GetRawText());
        foreach (var definition in definitions.Values)
        {
            var validation = validator.ValidateComponent(Path.Combine(directory, "plugins", definition.Entry.Id),
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
            Assert.True(validation.IsValid, definition.Entry.Id + ": " + JsonSerializer.Serialize(validation.Diagnostics));
            var binding = BindArtifact(definition, Path.Combine(directory, "plugins", definition.Entry.Id));
            installed.Add(binding);
            if (definition.Entry.Id == "crm")
            {
                installed.Add(BindArtifact(definition with { Entry = definition.Entry with { Version = "0.9.0" } }, Path.Combine(directory, "plugins", "crm")));
                installed.Add(BindArtifact(definition with { Entry = definition.Entry with { Version = "1.1.0" } }, Path.Combine(directory, "plugins", "crm"), incompatible: true));
            }
        }
        await loader.MountAsync([definitions["registry"], definitions["capabilities"]], Ct);
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
                var document = System.Text.Json.Nodes.JsonNode.Parse(files["contracts/http-v1.openapi.json"].Span)!;
                document["paths"] = new System.Text.Json.Nodes.JsonObject();
                files["contracts/http-v1.openapi.json"] = Encoding.UTF8.GetBytes(document.ToJsonString());
            }
            var inventory = string.Concat(files.OrderBy(file => file.Key, StringComparer.Ordinal).Select(file => file.Key + "\t" + file.Value.Length.ToString(CultureInfo.InvariantCulture) + "\t" + Convert.ToHexStringLower(SHA256.HashData(file.Value.Span)) + "\n"));
            var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(inventory)));
            return RegistryComponentBinding.FromArtifact(definition, files, digest, validator,
                new SurfaceValidationOptions { Profile = "platform", ExecutionPlane = "server", CatalogComplete = true });
        }

        void Add(string id, string component, IQianPlugin plugin, ServiceKey[] provides)
        {
            var definition = new PluginDefinition(new PluginEntry(id, component, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), plugin, provides);
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
