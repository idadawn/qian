using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces;
using Qian.Tenancy;
using Qian.Tenancy.AspNetCore;

namespace Qian.Sample.Crm.Tests;

public sealed partial class CustomerCommandTests
{
    [Fact]
    public async Task 平台运营HTTP真实超管旁路与审计_创建编排_租户主体拒绝_目标仅路径_编辑并发_完整契约()
    {
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        await using var management = await ManagementHost(business, platform);
        using var client = new HttpClient { BaseAddress = new Uri(management.Urls.Single()) };
        var path = "/api/tenancy/tenants";
        foreach (var token in new[] { (string?)null, business.Token(A, "", "root", "user"), business.Token(null, "", "intruder", "user") })
        {
            var status = token is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden;
            var code = token is null ? "UNAUTHENTICATED" : "AUTHZ_DENIED";
            await AssertHttp(await Request(HttpMethod.Get, path, token), status, code);
            await AssertHttp(await Request(HttpMethod.Post, path, token, new CreateTenant("forbidden", "Forbidden") { Administrator = new(Guid.NewGuid(), "owner") }), status, code);
            await AssertHttp(await Request(HttpMethod.Put, path + "/" + A, token, new UpdateTenant("Forbidden", 0)), status, code);
        }
        var root = business.Token(null, "", "root", "user");
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id" })
        {
            var invalid = new Dictionary<string, object?> { ["code"] = "c", ["name"] = "C", [forbidden] = Guid.NewGuid() };
            await AssertHttp(await Request(HttpMethod.Post, path, root, invalid), HttpStatusCode.BadRequest, "BAD_REQUEST");
        }
        var create = new CreateTenant("c", "C") { Administrator = new TenantAdministrator(Guid.CreateVersion7(), "owner-c") };
        using var response = await Request(HttpMethod.Post, path, root, create);
        Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
        var created = (await response.Content.ReadFromJsonAsync<TenantDescriptor>(Ct))!;
        Assert.NotEqual(Guid.Empty, created.Id); Assert.Equal(TenantStatus.Pending, created.Status); Assert.True(created.Revision > 0);
        using var list = await Request(HttpMethod.Get, path, root);
        Assert.Contains((await list.Content.ReadFromJsonAsync<TenantList>(Ct))!.Items, item => item.Id == created.Id);
        foreach (var forbidden in new[] { "tenantId", "tenant_id", "id", "code", "isolation" })
            await AssertHttp(await Request(HttpMethod.Put, path + "/" + created.Id, root, new Dictionary<string, object?>
                { ["name"] = "bad", ["expectedRevision"] = created.Revision, [forbidden] = "forbidden" }), HttpStatusCode.BadRequest, "BAD_REQUEST");
        using var updated = await Request(HttpMethod.Put, path + "/" + created.Id, root, new UpdateTenant("Updated", created.Revision));
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        await AssertHttp(await Request(HttpMethod.Put, path + "/" + created.Id, root, new UpdateTenant("Stale", created.Revision)), HttpStatusCode.Conflict, "CONFLICT");
        await AssertHttp(await Request(HttpMethod.Put, path + "/" + Guid.NewGuid(), root, new UpdateTenant("Missing", 0)), HttpStatusCode.NotFound, "NOT_FOUND");
        await AssertHttp(await Request(HttpMethod.Post, path, business.Token(A, "", "root", "user"), create), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        var source = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader.GetContext("tenancy", ScopeKey.Global);
        var records = await source.Audit().ReadPlatformAsync(source, 0, 100, cancellationToken: Ct);
        Assert.NotEmpty(records); Assert.All(records, entry => Assert.Equal("superadmin", entry.Record.Payload.GetProperty("bypass").GetString()));
        var contract = await management.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.tenancy", Ct);
        Assert.Equal(2, contract.GetProperty("paths").EnumerateObject().Count());
        Assert.True(contract.GetProperty("paths").GetProperty(path).TryGetProperty("post", out var post));
        Assert.True(contract.GetProperty("paths").GetProperty(path + "/{id}").TryGetProperty("put", out _));
        Assert.DoesNotContain("tenantId", post.GetProperty("requestBody").GetRawText(), StringComparison.Ordinal);
        Assert.True((await source.Audit().VerifyPlatformAsync(source, Ct)).Valid);
        var contractPath = Path.Combine(RepositoryRoot(), "plugins/qian.tenancy/contracts/http-v1.openapi.json");
        if (Environment.GetEnvironmentVariable("QIAN_UPDATE_TENANCY_CONTRACT") == "1")
        { Directory.CreateDirectory(Path.GetDirectoryName(contractPath)!); File.WriteAllText(contractPath, contract.GetRawText() + "\n"); }
        var published = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(contractPath));
        Assert.True(JsonElement.DeepEquals(published, contract), "真实端点与发布契约漂移。");
        var runtime = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var probe = new OasDiffOpenApiProbe(Environment.GetEnvironmentVariable("QIAN_OASDIFF_PATH")!);
        var admitted = new OpenApiContractPlugin(new TenancyPlugin(), probe, "qian.tenancy.http/v1", published, contract);
        await runtime.UnmountAsync("tenancy", ScopeKey.Global, Ct);
        await runtime.MountAsync([new PluginDefinition(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { })), admitted, [TenancyDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.TenancyKey])], Ct);
        Assert.Equal(PluginState.Ready, runtime.GetState("tenancy", ScopeKey.Global));


        Task<HttpResponseMessage> Request(HttpMethod method, string url, string? token, object? body = null)
        {
            var request = new HttpRequestMessage(method, url);
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            if (body is not null) request.Content = JsonContent.Create(body, body.GetType());
            return client.SendAsync(request, Ct);
        }
    }

    private async Task PreparePlatformLogins(LifecyclePlatform platform)
    {
        platform.AuthzRole = "ten_auth_" + Guid.NewGuid().ToString("N")[..10];
        platform.AuditRole = "ten_audit_" + Guid.NewGuid().ToString("N")[..10];
        platform.AuthzConnection = await Login(platform.AuthzRole); platform.AuditConnection = await Login(platform.AuditRole);
        async Task<string> Login(string role)
        {
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            await Admin($"CREATE ROLE \"{role}\" LOGIN NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION PASSWORD '{password}';");
            return new NpgsqlConnectionStringBuilder(postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
        }
    }

    private static async Task<WebApplication> ManagementHost(RunningApp business, LifecyclePlatform platform)
    {
        var loader = business.App.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        var permissions = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.tenancy/metadata/permissions.json")));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.tenancy", "1.0.0", permissions, JsonSerializer.SerializeToElement(Array.Empty<object>()));
        await loader.UnmountAsync("authz", ScopeKey.Global, Ct); await loader.UnmountAsync("audit", ScopeKey.Global, Ct);
        await loader.MountAsync([
            new PluginDefinition(new PluginEntry("audit", "qian.audit", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { chain = new { platformRole = platform.AuditRole } })), new AuditPlugin(), [AuditDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuditKey]),
            new PluginDefinition(new PluginEntry("authz", "qian.authz", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { platformRole = platform.AuthzRole })), new AuthzPlugin([CrmPermissions.Compile(), catalog]), [AuthzDefinition.Key, Qian.Plugin.Sdk.CommandPolicies.AuthorizationKey])], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("authz", ScopeKey.Global));
        await new PostgresPlatformRoleStore(business.App.Services.GetRequiredService<IDataService>(), business.App.Services.GetRequiredService<ITenantContext>(), platform.AuthzRole).AssignSuperadminAsync("root", "operator", Ct);
        var lifecycle = LifecyclePlatform.Lifecycle(business);
        foreach (var artifact in platform.SystemArtifacts.Values)
        { var revision = await lifecycle.InstallAsync(artifact, "root", Ct); await lifecycle.ActivateAsync(revision.PluginId, revision.Revision, "root", Ct); }
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton(business.App.Services.GetRequiredService<ITenantContext>());
        builder.Services.AddHttpContextAccessor(); builder.Services.AddScoped<ICurrentUser, ManagementCurrentUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters { ValidateIssuer = true, ValidIssuer = Issuer, ValidateAudience = true,
                ValidAudience = Audience, ValidateLifetime = true, ValidateIssuerSigningKey = true, IssuerSigningKey = business.ManagementSigningKey,
                ValidAlgorithms = [SecurityAlgorithms.RsaSha256], ClockSkew = TimeSpan.Zero };
            options.Events.OnTokenValidated = context =>
            {
                if (context.SecurityToken is not JsonWebToken token) { context.Fail("缺少已验签令牌。"); return Task.CompletedTask; }
                using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(token.EncodedPayload));
                var principal = payload.RootElement.Deserialize(IdentityJson.PrincipalTypeInfo);
                if (principal is null || principal.Subject != context.Principal?.FindFirst("sub")?.Value) context.Fail("主体不一致。");
                else context.HttpContext.Items[ManagementCurrentUser.Snapshot] = principal;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddAuthorization(); builder.Services.AddQianErrors(); builder.Services.AddQianHttpContracts();
        builder.Services.ConfigureHttpJsonOptions(options => { options.SerializerOptions.PropertyNameCaseInsensitive = false; options.SerializerOptions.RespectRequiredConstructorParameters = true; });
        var app = builder.Build(); app.UseQianErrors(); app.UseAuthentication(); app.UseAuthorization();
        app.MapQianTenancy(_ => loader.GetContext("tenancy", ScopeKey.Global));
        await app.StartAsync(Ct); return app;
    }

    private static async Task AssertHttp(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        using (response)
        { var body = await response.Content.ReadAsStringAsync(Ct); Assert.True(response.StatusCode == status, body); Assert.Equal(code, ErrorDetailJson.Deserialize(body)!.Code); }
    }
    private sealed class ManagementCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        internal static readonly object Snapshot = new();
        public Principal? Principal => accessor.HttpContext?.Items[Snapshot] as Principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => [];
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;
    }
}
