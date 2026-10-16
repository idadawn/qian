using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Authorization;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.MultiTenancy;
using Kun.Security.Authentication;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Registry.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces;
using Qian.Tenancy;

namespace Qian.Registry.Tests;

// S1-RT-004 管理面 scope 视图（R32 / R48 / R33）：真实 PostgreSQL、真实 RBAC 判定（Qian.Authz 的 RbacPermissionChecker 读真实角色授予）、
// 真实 Kestrel + JWT 与租户边界；没有替身判定器，令牌里的角色也不授予任何权限。
public sealed partial class PatchPersistenceTests
{
    private const string Holder = "registry-operator";
    private const string Reader = "registry-reader";
    private static readonly string[] ManagementErrorFields = ["type", "title", "status", "code", "message", "pointer"];
    // 令牌声称 Owner 也不授予任何权限：放行只来自数据库里的角色授予。
    private static readonly string[] ClaimedRoles = ["tenant.owner"];
    private static readonly string[] PlatformClaimedRoles = ["platform.superadmin"];

    [Fact]
    public async Task 管理面scope视图_A挂载B未挂载_持权限者得403原因码与pointer_无权限者与不存在同为404_别名同结果并带弃用头()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, services: services => AddAuthz(services, checker: true));
        await PrepareAuthz(running.Host);
        using var active = await GrantRegistryRoles(running, tenant);
        await using var http = await StartManagementHost(running, tenant);
        var scopeA = new ScopeKey("tenant", HttpTenantA); var scopeB = new ScopeKey("tenant", HttpTenantB);
        using (tenant.BeginScope(HttpTenantA))
            await Repository(running.Host, tenant).PutAsync(scopeA, "private", Patch("""{"id":"private","plugin":"test.private","config":{}}"""), 0, "operator-a", Ct);

        // A 写了 patch：持权限者读到 A 的局部条目与 patch 修订。
        using var mounted = await http.Get($"/api/registry/scopes/tenant:{HttpTenantA}/entries/private", HttpTenantA, Holder);
        Assert.Equal(HttpStatusCode.OK, mounted.StatusCode);
        var view = await Json(mounted);
        Assert.Equal("test.private", view.GetProperty("pluginId").GetString()); Assert.Equal("1.0.0", view.GetProperty("version").GetString());
        Assert.Equal("tenant:" + HttpTenantA, view.GetProperty("scope").GetString()); Assert.Equal(1, view.GetProperty("revision").GetInt32());

        // B 不写：持 qian.registry:scope:read 者得到 403 PLUGIN_NOT_MOUNTED（R48：原因即错误码），pointer 指向 B 的 scope patch 层。
        var path = $"/api/registry/scopes/tenant:{HttpTenantB}/entries/private";
        using var notMounted = await http.Get(path, HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.Forbidden, notMounted.StatusCode);
        var reason = await Json(notMounted);
        Assert.Equal("PLUGIN_NOT_MOUNTED", reason.GetProperty("code").GetString());
        Assert.Equal("registry/scope-patches.json#", reason.GetProperty("pointer").GetString());
        Assert.Equal("未挂载：平台 profile 与租户 scope patch 均未声明此条目", reason.GetProperty("message").GetString());

        // 无该权限者 404，与持权限者查询不存在的条目、跨租户查询 A 的 scope 完全相同。
        using var hidden = await http.Get(path, HttpTenantB, Reader);
        using var unknown = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries/absent", HttpTenantB, Holder);
        using var crossTenant = await http.Get($"/api/registry/scopes/tenant:{HttpTenantA}/entries/private", HttpTenantB, Holder);
        using var listHidden = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries", HttpTenantB, Reader);
        foreach (var response in new[] { hidden, unknown, crossTenant, listHidden }) Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var absent = await ErrorShape(unknown);
        Assert.Equal(absent, await ErrorShape(hidden)); Assert.Equal(absent, await ErrorShape(crossTenant)); Assert.Equal(absent, await ErrorShape(listHidden));

        // 视图只列 B 实际挂载且 Ready 的条目。
        using var list = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(["data", "registry", "shared"], Ids(await Json(list)));

        // v1 别名：同一结果，带 Deprecation 与指向后继路径的 Link: <…>; rel="successor-version"（R33，组件规格 §9.5）。
        using var alias = await http.Get($"/api/kernel/scopes/tenant:{HttpTenantB}/entries/private", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.Forbidden, alias.StatusCode);
        Assert.Equal(Fields(reason), Fields(await Json(alias)));
        Assert.Equal(DeprecatedAlias.Deprecation, Assert.Single(alias.Headers.GetValues("Deprecation")));
        Assert.Equal($"</api/registry/scopes/tenant:{HttpTenantB}/entries/private>; rel=\"successor-version\"", Assert.Single(alias.Headers.GetValues("Link")));
        using var listA = await http.Get($"/api/registry/scopes/tenant:{HttpTenantA}/entries", HttpTenantA, Holder);
        using var aliasListA = await http.Get($"/api/kernel/scopes/tenant:{HttpTenantA}/entries", HttpTenantA, Holder);
        Assert.Equal(HttpStatusCode.OK, aliasListA.StatusCode);
        var listedA = await Json(listA);
        Assert.Equal(["data", "private", "registry", "shared"], Ids(listedA));
        Assert.True(JsonElement.DeepEquals(listedA, await Json(aliasListA)));
        Assert.Equal($"</api/registry/scopes/tenant:{HttpTenantA}/entries>; rel=\"successor-version\"", Assert.Single(aliasListA.Headers.GetValues("Link")));
        Assert.False(listA.Headers.Contains("Deprecation"));

        // B 写禁用 patch：原因说明被哪一层禁用，pointer 指向 B 的 patch 条目。
        using (tenant.BeginScope(HttpTenantB))
            await Repository(running.Host, tenant).PutAsync(scopeB, "shared", Patch("""{"id":"shared","disabled":true}"""), 0, "operator-b", Ct);
        using var disabled = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries/shared", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.Forbidden, disabled.StatusCode);
        var disabling = await Json(disabled);
        Assert.Equal("PLUGIN_NOT_MOUNTED", disabling.GetProperty("code").GetString());
        Assert.Equal("被租户 scope patch 禁用", disabling.GetProperty("message").GetString());
        Assert.Equal("registry/scope-patches.json#/0", disabling.GetProperty("pointer").GetString());
        using var disabledHidden = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries/shared", HttpTenantB, Reader);
        Assert.Equal(absent, await ErrorShape(disabledHidden));

        // A 撤销 patch：不重启进程，条目回到未挂载；B 的禁用不受影响。
        using (tenant.BeginScope(HttpTenantA)) await Repository(running.Host, tenant).RemoveAsync(scopeA, "private", 1, "operator-a", Ct);
        using var revoked = await http.Get($"/api/registry/scopes/tenant:{HttpTenantA}/entries/private", HttpTenantA, Holder);
        Assert.Equal(HttpStatusCode.Forbidden, revoked.StatusCode); Assert.Equal("PLUGIN_NOT_MOUNTED", (await Json(revoked)).GetProperty("code").GetString());
        using var stillDisabled = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries/shared", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.Forbidden, stillDisabled.StatusCode);

        // 模拟正式写入入口之外的坏数据：B 的 scope 合入失败后整体收紧，诊断面仍可达并说明原因，视图为空，A 不受影响。
        using (tenant.BeginScope(HttpTenantB))
            await Repository(running.Host, tenant).PutAsync(scopeB, "shared", Patch("""{"id":"shared","config":{"label":42}}"""), 1, "operator-b", Ct);
        using var blocked = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries/registry", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        var blocking = await Json(blocked);
        Assert.Equal("PLUGIN_NOT_MOUNTED", blocking.GetProperty("code").GetString());
        Assert.Equal("租户 scope patch 无法装配，本 scope 的条目已全部撤下", blocking.GetProperty("message").GetString());
        Assert.Equal("registry/scope-patches.json#", blocking.GetProperty("pointer").GetString());
        using var emptied = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.OK, emptied.StatusCode); Assert.Empty(Ids(await Json(emptied)));
        using var unaffected = await http.Get($"/api/registry/scopes/tenant:{HttpTenantA}/entries", HttpTenantA, Holder);
        Assert.Equal(["data", "registry", "shared"], Ids(await Json(unaffected)));
    }

    [Fact]
    public async Task 管理面未装配权限判定端口时fail_closed_即使数据库已授予读取权限也只得到404()
    {
        var tenant = new AsyncLocalTenantContext();
        await using var running = await CreateRunningRegistry(tenant, services: services => AddAuthz(services, checker: false));
        await PrepareAuthz(running.Host);
        using var active = await GrantRegistryRoles(running, tenant);
        // 授予本身真实有效：直接用真实判定器求值为放行。
        var checker = new RbacPermissionChecker(running.Host.Services.GetRequiredService<IDataService>(), tenant, running.Host.Services.GetRequiredService<IPermissionRegistry>());
        using (tenant.BeginScope(HttpTenantB))
            Assert.True((await checker.CheckAsync(new PermissionRequest(new AuthorizationSubject(Holder, HttpTenantB, "service"), RegistryDefinition.ScopeReadPermission), Ct)).Allow);
        await using var http = await StartManagementHost(running, tenant);
        using var entry = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries/private", HttpTenantB, Holder);
        using var list = await http.Get($"/api/registry/scopes/tenant:{HttpTenantB}/entries", HttpTenantB, Holder);
        Assert.Equal(HttpStatusCode.NotFound, entry.StatusCode); Assert.Equal(HttpStatusCode.NotFound, list.StatusCode);
        Assert.Equal("NOT_FOUND", (await Json(entry)).GetProperty("code").GetString());
    }

    [Fact]
    public void 注册表判定的权限点由仓库metadata声明_经真实编译器_默认只授予租户Owner与Admin()
    {
        static JsonElement Read(string name) => JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins", "qian.registry", "metadata", name)));
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata("qian.registry", "1.0.0", Read("permissions.json"), Read("roles.json"));
        Assert.Equal([RegistryDefinition.ScopeReadPermission], catalog.Permissions.Select(permission => permission.Code));
        Assert.Equal(["tenant.admin", "tenant.owner"], catalog.Roles.Select(role => role.Role).Order(StringComparer.Ordinal));
        Assert.All(catalog.Roles, role => Assert.Equal([RegistryDefinition.ScopeReadPermission], role.Grants));
    }

    private void AddAuthz(IServiceCollection services, bool checker)
    {
        services.AddQianData<AuthzDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(AuthzDbContext.Schema));
        services.AddSingleton<IPermissionRegistry, PermissionRegistry>();
        if (checker)
            services.AddSingleton<IPermissionChecker>(sp => new RbacPermissionChecker(sp.GetRequiredService<IDataService>(), sp.GetRequiredService<ITenantContext>(), sp.GetRequiredService<IPermissionRegistry>()));
    }

    private async Task PrepareAuthz(IHost host)
    {
        using var scope = host.Services.CreateScope();
        var sql = "DROP SCHEMA IF EXISTS " + AuthzDbContext.Schema + " CASCADE;\n" + scope.ServiceProvider.GetRequiredService<AuthzDbContext>().Database.GenerateCreateScript() +
            $"\nGRANT USAGE ON SCHEMA {AuthzDbContext.Schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {AuthzDbContext.Schema} TO qian_app;";
        await using var admin = new NpgsqlConnection(postgres.ConnectionString); await admin.OpenAsync(Ct);
        await using var create = new NpgsqlCommand(sql, admin); await create.ExecuteNonQueryAsync(Ct);
    }

    // 权限声明取自注册表的预装快照（仓库 metadata），经真实编译器登记目录与活动声明；两租户的 Owner 持读取权限，使用者没有。
    private static async Task<IDisposable> GrantRegistryRoles(RunningRegistry running, AsyncLocalTenantContext tenant)
    {
        var metadata = Assert.Single(running.Registry.GetInstalledComponents(), component => component.PluginId == "qian.registry");
        var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromInstalled(metadata);
        var data = running.Host.Services.GetRequiredService<IDataService>();
        await new PostgresPermissionCatalogStore(data).PutAsync(catalog, Ct);
        var active = running.Host.Services.GetRequiredService<IPermissionRegistry>().Register(metadata.Version, catalog.Permissions);
        foreach (var id in new[] { HttpTenantA, HttpTenantB })
        {
            using var current = tenant.BeginScope(id);
            var roles = new PostgresTenantRoleStore(data, tenant);
            await roles.EnsureBuiltinsAsync("test-bootstrap", Ct);
            await roles.InstallDefaultsAsync("qian.registry", metadata.Version, null, "test-bootstrap", Ct);
            await roles.AssignAsync("tenant.owner", "service", Holder, "test-bootstrap", Ct);
            await roles.AssignAsync("tenant.user", "service", Reader, "test-bootstrap", Ct);
        }
        return active;
    }

    private static async Task<ManagementHost> StartManagementHost(RunningRegistry running, AsyncLocalTenantContext tenant, string? platformTargetTenant = null)
    {
        var signing = RSA.Create(2048); using var publicOnly = RSA.Create(); publicOnly.ImportParameters(signing.ExportParameters(false));
        var key = new RsaSecurityKey(signing) { KeyId = "registry-http" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicOnly) { KeyId = key.KeyId });
        var jwks = new JsonWebKeySet(JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } }));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddKunSecurity(); builder.Services.AddHttpContextAccessor();
        // 单容器的平台宿主由 RegistryPlugin 登记原因码；本测试的 HTTP 宿主与插件宿主分开，需在自己的目录中登记一次。
        builder.Services.AddQianErrors(catalog => RegistryErrors.Register(catalog));
        builder.Services.AddScoped<TenantRequestBoundary>();
        builder.Services.AddScoped<ICurrentUser, VerifiedServiceUser>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddKunServiceAuthentication(options =>
        { options.Issuer = "registry-http-test"; options.Audience = "registry-http"; options.StaticKeySet = jwks; });
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.MapInboundClaims = false);
        builder.Services.AddAuthorization();
        builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var previous = options.Events.OnTokenValidated;
            options.Events.OnTokenValidated = async context =>
            {
                if (previous is not null) await previous(context);
                if (context.Result is not null && !context.Result.Succeeded) return;
                VerifiedServiceUser.Capture(context, platformTargetTenant is not null);
            };
        });
        var app = builder.Build();
        app.UseQianErrors(); app.UseRouting(); app.UseAuthentication();
        app.Use(async (context, next) =>
        {
            // 平台运营宿主预先选择的可信目标，不从 path/header/body 推导；普通租户请求仍走原边界。
            if (platformTargetTenant is not null && context.RequestServices.GetRequiredService<ICurrentUser>().Principal is { Kind: PrincipalKind.User, TenantId: null })
            { using var scope = tenant.BeginScope(platformTargetTenant); await next(context); return; }
            await context.RequestServices.GetRequiredService<TenantRequestBoundary>().InvokeAsync(context, next);
        });
        app.UseAuthorization();
        app.MapQianRegistry(_ => running.Root);
        try
        {
            await app.StartAsync(Ct); var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new ManagementHost(app, signing, key, new HttpClient { BaseAddress = new Uri(address) });
        }
        catch { await app.DisposeAsync(); signing.Dispose(); throw; }
    }

    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Ct));
    private static string[] Ids(JsonElement list) => list.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("entryId").GetString()!).ToArray();
    private static string Fields(JsonElement error) => string.Join('|', ManagementErrorFields.Select(field => error.TryGetProperty(field, out var value) ? value.ToString() : "-"));
    private static string RepositoryRoot()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        return root?.FullName ?? throw new InvalidOperationException("找不到仓库根目录。");
    }

    private sealed class ManagementHost(WebApplication app, RSA signing, RsaSecurityKey key, HttpClient client) : IAsyncDisposable
    {
        // 请求头里的 X-Tenant-Id 指向另一个租户，证明管理面只认令牌里的租户。
        internal async Task<HttpResponseMessage> Get(string path, string tenant, string subject)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Add("X-Tenant-Id", tenant == HttpTenantB ? HttpTenantA : HttpTenantB);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
            var payload = JsonSerializer.Serialize(new { iss = "registry-http-test", aud = HttpAudiences, sub = subject, principal_kind = "service", tenant_id = tenant,
                roles = ClaimedRoles, scope = "registry.read", session_id = id, jti = id, iat = now, nbf = now, exp = now + 60 });
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
            return await client.SendAsync(request, Ct);
        }
        internal async Task<HttpResponseMessage> GetPlatform(string path, string subject)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Add("X-Tenant-Id", HttpTenantB);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var payload = JsonSerializer.Serialize(new { iss = "registry-http-test", aud = HttpAudiences, sub = subject, principal_kind = "user", tenant_id = (string?)null,
                identity_revision = 0, roles = PlatformClaimedRoles, scope = "registry.read", session_id = Guid.CreateVersion7(), jti = Guid.CreateVersion7(), iat = now, nbf = now, exp = now + 60 });
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
            return await client.SendAsync(request, Ct);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(Ct); await app.DisposeAsync(); signing.Dispose(); }
    }

    // 只消费认证事件已验证的令牌快照；请求头、正文与令牌里的角色都不是授权来源。
    private sealed class VerifiedServiceUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private static readonly object Key = new();
        public Principal? Principal => accessor.HttpContext?.Items[Key] as Principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => Principal?.Roles ?? [];
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;

        internal static void Capture(TokenValidatedContext context, bool acceptPlatformUser = false)
        {
            if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out var kind) || kind != "service" && !(acceptPlatformUser && kind == "user" && tenant is null))
            { context.Fail("无效机器身份。"); return; }
            var principal = JsonSerializer.Deserialize(Base64UrlEncoder.DecodeBytes(token.EncodedPayload), IdentityJson.PrincipalTypeInfo);
            if (principal is null || (principal.Kind != PrincipalKind.Service && !(acceptPlatformUser && principal.Kind == PrincipalKind.User && principal.TenantId is null)) || principal.Subject != subject || principal.TenantId != tenant || principal.Actor is not null)
            { context.Fail("身份快照不一致。"); return; }
            context.HttpContext.Items[Key] = principal;
        }
    }
}
