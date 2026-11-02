using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Authz;
using Qian.Data;
using Qian.Gateway;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Tenancy.Signup;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;

namespace Qian.Sample.Crm.Tests;

// 平台控制台（S1-YUE-018）：平台组件（清单 scopes 只含 global）真实挂在全局 scope——租户运营 qian.tenancy 与开通申请 qian.tenancy.signup，
// 各带制品快照里的 UI 清单；Authz 带平台角色登录与两份权限目录（管理面宿主同一套装配）。平台表面参考宿主复用同一 Runtime，按 Authz 的只读判定
// 决定给平台身份哪些页面；外壳 bootstrap 经真实身份、网关 BFF 会话与按路由表换的 S2S 调它。
public sealed partial class ShellSurfaceTests
{
    [Fact]
    public async Task 平台控制台目录只给持平台超管的平台身份_按只读判定列出平台组件的远程菜单与权限码_不写审计_租户主体Agent与不持角色的平台身份拿不到_租户外壳目录不含平台组件()
    {
        await using var console = await StartConsole();
        var business = console.Business;
        using var direct = new HttpClient { BaseAddress = new Uri(console.Surface.Urls.Single()) };
        var before = await AuditRows();

        // 持平台超管（管理面宿主引导时授予 root）：两个平台组件的远程（全局 scope、实际挂载的版本、制品快照里的 UI 清单原文）、
        // 菜单（qian:global:{组件}:page:{路由}，按 order）与只读判定放行的权限码。
        var granted = await ConsoleCatalog(direct, SurfacePlatformToken(business, "user", "root"));
        Assert.Equal(["qian.tenancy", "qian.tenancy.signup"], granted["remotes"]!.AsArray().Select(remote => (string)remote!["id"]!));
        foreach (var remote in granted["remotes"]!.AsArray())
        {
            Assert.Equal(("1.0.0", "global"), ((string?)remote!["version"], (string?)remote["scope"]));
            var source = JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins", (string)remote["id"]!, "ui/manifest.json")));
            Assert.True(JsonNode.DeepEquals(source, remote["manifest"]), remote["manifest"]!.ToJsonString());
        }
        Assert.Equal([("qian:global:qian.tenancy:page:%2Ftenants", "qian.tenancy", "/tenants", "租户管理", "building", 10),
            ("qian:global:qian.tenancy.signup:page:%2Ftenant-applications", "qian.tenancy.signup", "/tenant-applications", "开通申请", "inbox", 20)],
            granted["navigation"]!.AsArray().Select(item => ((string)item!["resourceRef"]!, (string)item["pluginId"]!, (string)item["route"]!, (string)item["title"]!, (string)item["icon"]!, (int)item["order"]!)));
        Assert.Equal(["qian.tenancy.signup:application:read", "qian.tenancy:tenant:read"], granted["permissions"]!.AsArray().Select(item => (string)item!));
        Assert.Equal("private", (string?)granted["cacheScope"]);
        // 只读：这次判定没有写任何审计（平台链、租户链与决策日志同在一张表）。
        Assert.Equal(before, await AuditRows());

        // 不持角色的平台用户：200，远程、菜单与权限码都为空。
        var none = await ConsoleCatalog(direct, SurfacePlatformToken(business, "user", "operator-without-role"));
        foreach (var name in new[] { "remotes", "navigation", "permissions" }) Assert.Empty(none[name]!.AsArray());

        // 平台 Agent、平台服务主体、租户里的用户 / 服务 / Agent 主体：与不存在一样 404（即使主体 id 正是持角色的 root）。
        foreach (var token in new[] { SurfacePlatformToken(business, "agent", "root"), SurfacePlatformToken(business, "service", "root"),
            SurfaceUserToken(business, "root", A), SurfaceMachineToken(business, "service", Writer, A), SurfaceMachineToken(business, "agent", "root", A) })
            using (var denied = await SurfaceGet(direct, "/api/capabilities/console", token))
            {
                var body = await denied.Content.ReadAsStringAsync(Ct);
                Assert.True(denied.StatusCode == HttpStatusCode.NotFound, body);
                Assert.Equal("NOT_FOUND", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
            }

        // 租户的外壳目录与菜单不含平台组件：全局注册在租户 scope 里同样可见，目录与菜单显式排除。
        var tenantToken = SurfaceMachineToken(business, "service", Writer, A);
        var shell = JsonNode.Parse(await SurfaceText(direct, "/api/capabilities/shell", tenantToken))!;
        Assert.Equal(A, (string?)shell["tenant"]!["id"]);
        Assert.DoesNotContain(shell["remotes"]!.AsArray(), remote => ((string)remote!["id"]!).StartsWith("qian.tenancy", StringComparison.Ordinal));
        var navigation = JsonNode.Parse(await SurfaceText(direct, "/api/capabilities/navigation", tenantToken))!;
        Assert.DoesNotContain(navigation["items"]!.AsArray(), item => (string)item!["route"]! is "/tenants" or "/tenant-applications");

        // 撤销角色后下一次调用即为空（每次调用由服务端重新判定，不缓存）；全程没有写任何审计。
        var roles = new PostgresPlatformRoleStore(business.App.Services.GetRequiredService<IDataService>(), business.App.Services.GetRequiredService<ITenantContext>(), console.Platform.AuthzRole);
        Assert.True(await roles.RevokeSuperadminAsync("root", "operator", Ct));
        var revoked = await ConsoleCatalog(direct, SurfacePlatformToken(business, "user", "root"));
        foreach (var name in new[] { "remotes", "navigation", "permissions" }) Assert.Empty(revoked[name]!.AsArray());
        Assert.Equal(before, await AuditRows());
    }

    [Fact]
    public async Task 外壳bootstrap给持平台超管的平台身份交出平台组件的远程菜单与权限码_不代管且与控制台目录一致_不持角色的平台身份仍代管_租户成员看不到平台组件_不含令牌()
    {
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        var seen = new ConcurrentQueue<string>();
        await using var console = await StartConsole(new Uri(address, "auth/callback"), seen);
        var business = console.Business;
        await using var redis = new RedisFixture(); await redis.InitializeAsync();
        await using var gateway = await GatewayTestHost.Start(reservation, tls, business.Identity.Issuer, GatewayClientId, IdentityResource, business.Identity.Secret,
            redis.ConnectionString, ShellGatewayIssuer,
            [new("capabilities", "/api/capabilities/{**rest}", PlatformSurfaceHost.Audience) { RateLimiterPolicy = "user" }, new("authz", "/api/authz/{**rest}", PlatformSurfaceHost.Audience) { RateLimiterPolicy = "user" }],
            [new(PlatformSurfaceHost.Audience, console.Surface.Urls.Single())], business.ManagementSigningKey.Rsa, map: app => app.MapQianShellBootstrap());
        var roles = new PostgresPlatformRoleStore(business.App.Services.GetRequiredService<IDataService>(), business.App.Services.GetRequiredService<ITenantContext>(), console.Platform.AuthzRole);

        // 平台运营（持平台超管）：bootstrap 只问控制台目录，交出平台组件的远程、挂载与权益键、菜单与权限码，不代管；与经网关直调控制台目录逐项一致。
        var (operatorPerson, operatorPhone) = await NewPerson(business, "平台运营", platform: true);
        await roles.AssignSuperadminAsync(operatorPerson.Id.ToString("D"), "root", Ct);
        var (operatorClient, operatorCookies) = GatewayTestHost.Browser(tls); using (operatorClient)
        {
            await ShellSignIn(operatorClient, business, operatorPhone, null); await gateway.CompleteLogin(operatorClient);
            seen.Clear();
            var granted = await Bootstrap(operatorClient, address, operatorCookies);
            Assert.Equal(["/api/capabilities/console"], seen);
            Assert.Equal(await SessionSnapshot(operatorClient, address), granted["session"]!.ToJsonString());
            Assert.Equal((operatorPerson.Id.ToString("D"), "user", null, null), ((string?)granted["session"]!["subject"], (string?)granted["session"]!["kind"],
                (string?)granted["session"]!["tenantId"], (string?)granted["session"]!["membershipId"]));
            Assert.False((bool)granted["managed"]!);
            Assert.Null(granted["context"]!["tenant"]); Assert.Null(granted["context"]!["membership"]); Assert.Null(granted["context"]!["app"]);
            Assert.Equal(("zh-CN", "light"), ((string?)granted["context"]!["locale"], (string?)granted["context"]!["theme"]));
            Assert.Equal(["global|qian.tenancy", "global|qian.tenancy.signup"], granted["mounted"]!.AsArray().Select(item => (string)item!));
            Assert.Equal(granted["mounted"]!.ToJsonString(), granted["entitled"]!.ToJsonString());
            Assert.Equal(["/remotes/qian.tenancy/mf-manifest.json", "/remotes/qian.tenancy.signup/mf-manifest.json"],
                granted["remotes"]!.AsArray().Select(remote => (string)remote!["manifest"]!["federation"]!["manifest"]!));
            Assert.Equal(["/tenants", "/tenant-applications"], granted["navigation"]!.AsArray().Select(item => (string)item!["route"]!));
            Assert.Equal(["qian.tenancy.signup:application:read", "qian.tenancy:tenant:read"], granted["permissions"]!.AsArray().Select(item => (string)item!));
            var catalog = JsonNode.Parse(await Through(operatorClient, address, "api/capabilities/console"))!;
            Assert.True(JsonNode.DeepEquals(catalog["remotes"], granted["remotes"]), granted["remotes"]!.ToJsonString());
            Assert.True(JsonNode.DeepEquals(catalog["navigation"], granted["navigation"]), granted["navigation"]!.ToJsonString());
            Assert.True(JsonNode.DeepEquals(catalog["permissions"], granted["permissions"]), granted["permissions"]!.ToJsonString());
        }

        // 不持角色的平台身份：只问了控制台目录（为空），仍是代管上下文。
        var (_, plainPhone) = await NewPerson(business, "平台访客", platform: true);
        var (plainClient, plainCookies) = GatewayTestHost.Browser(tls); using (plainClient)
        {
            await ShellSignIn(plainClient, business, plainPhone, null); await gateway.CompleteLogin(plainClient);
            seen.Clear();
            var managed = await Bootstrap(plainClient, address, plainCookies);
            Assert.Equal(["/api/capabilities/console"], seen);
            Assert.True((bool)managed["managed"]!);
            Assert.Null(managed["context"]!["tenant"]); Assert.Null(managed["navigation"]);
            foreach (var name in new[] { "remotes", "mounted", "entitled", "permissions" }) Assert.Empty(managed[name]!.AsArray());
        }

        // 租户成员：照常走外壳目录、能力导航与有效权限三条，结果里没有平台组件；经网关直调控制台目录 404。
        var (member, memberPhone) = await NewPerson(business, "成员丙");
        await Join(business, A, member.Id, "成员丙");
        var (memberClient, memberCookies) = GatewayTestHost.Browser(tls); using (memberClient)
        {
            await ShellSignIn(memberClient, business, memberPhone, A); await gateway.CompleteLogin(memberClient);
            seen.Clear();
            var (tenantView, text) = await BootstrapText(memberClient, address, memberCookies);
            Assert.Equal(["/api/authz/me/permissions", "/api/capabilities/navigation", "/api/capabilities/shell"], seen.Order(StringComparer.Ordinal));
            Assert.False((bool)tenantView["managed"]!);
            Assert.Equal(A, (string?)tenantView["context"]!["tenant"]!["id"]);
            Assert.DoesNotContain("qian.tenancy", text, StringComparison.Ordinal);
            Assert.DoesNotContain("/tenants", text, StringComparison.Ordinal);
            using var denied = await memberClient.GetAsync(new Uri(address, "api/capabilities/console"), Ct);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        }
    }
}

public abstract partial class CustomerCommandTests
{
    // 平台控制台的验收宿主：生命周期平台（管理面：Authz 带平台角色登录与 Tenancy / 开通申请两份权限目录，引导时给 root 授平台超管）+ 内置账号模型；
    // Tenancy 以带 UI 清单的包装重挂，开通申请组件（数据上下文与建表脚本同 S1-TEN-019 的验收宿主）带 UI 清单挂在全局 scope；平台表面参考宿主复用同一 Runtime。
    private protected async Task<ConsoleHost> StartConsole(Uri? relyingParty = null, ConcurrentQueue<string>? seen = null)
    {
        var platform = new LifecyclePlatform { Management = true, Console = true };
        RunningApp? business = null; WebApplication? management = null; WebApplication? surface = null;
        try
        {
            business = await Start(lifecycle: platform, identity: true, relyingParty: relyingParty, configure: builder =>
                builder.Services.AddQianData<TenancySignupDbContext>((services, options) => options.UseNpgsql(services.GetRequiredService<IConfiguration>().GetConnectionString("Tenancy"))
                    .UseKunSchema(TenancySignupDbContext.Schema)), schema: SignupSchemaSql);
            var catalog = new PermissionCatalogCompiler(new SurfaceValidator()).FromMetadata(TenancySignupDefinition.ComponentId, "1.0.0",
                JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.tenancy.signup/metadata/permissions.json"))),
                JsonSerializer.SerializeToElement(Array.Empty<object>()));
            management = await ManagementHost(business, platform, catalogs: [catalog]);
            var loader = Runtime(business).Loader;
            await loader.MountAsync([platform.Signup!], Ct);
            Assert.True(loader.GetState("signup", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("signup", ScopeKey.Global)?.ToString());
            surface = PlatformSurfaceHost.Build(id => loader.GetContext(id, ScopeKey.Global), new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = ShellGatewayIssuer, ValidateAudience = true, ValidAudience = PlatformSurfaceHost.Audience, ValidateLifetime = true,
                ValidateIssuerSigningKey = true, IssuerSigningKey = business.ManagementSigningKey, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                RequireSignedTokens = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero
            }, builder =>
            {
                builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
                if (seen is not null) builder.Services.AddSingleton<IStartupFilter>(new RequestLog(seen));
            });
            await surface.StartAsync(Ct);
            return new ConsoleHost(platform, business, management, surface);
        }
        catch
        {
            if (surface is not null) await surface.DisposeAsync();
            if (management is not null) { await management.StopAsync(Ct); await management.DisposeAsync(); }
            if (business is not null) await business.DisposeAsync();
            platform.Dispose(); throw;
        }
    }

    // 开通申请组件的建表脚本：生产同一套迁移，授权给与身份服务相同的应用登录（同 S1-TEN-019 的验收宿主）。
    private static string SignupSchemaSql(IServiceProvider services) =>
        $"DROP SCHEMA IF EXISTS {TenancySignupDbContext.Schema} CASCADE;" + services.GetRequiredService<TenancySignupDbContext>().GetService<IMigrator>().GenerateScript() +
        $"GRANT USAGE ON SCHEMA {TenancySignupDbContext.Schema} TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {TenancySignupDbContext.Schema} TO qian_app;";

    // 全部审计行（租户链、平台链与决策日志同一张表，管理员连接不受 RLS 限制）。
    private protected async Task<long> AuditRows()
    {
        await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FROM plugin_qian_audit.audit_entries", connection);
        return (long)(await command.ExecuteScalarAsync(Ct))!;
    }

    // 平台主体（tenant_id 为 null）的同形令牌：没有成员与身份修订；用户、服务与 Agent 都只能这样构造（身份服务在 S1 不签发 Agent 令牌）。
    private protected static string SurfacePlatformToken(RunningApp host, string kind, string subject) =>
        SignSurfaceToken(host, new Dictionary<string, object?> { ["sub"] = subject, ["principal_kind"] = kind, ["tenant_id"] = null });

    // 租户用户主体的同形令牌（不经身份服务，没有成员与身份修订）。
    private protected static string SurfaceUserToken(RunningApp host, string subject, string tenant) =>
        SignSurfaceToken(host, new Dictionary<string, object?> { ["sub"] = subject, ["principal_kind"] = "user", ["tenant_id"] = tenant });

    private protected static async Task<HttpResponseMessage> SurfaceGet(HttpClient client, string path, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, Ct);
    }

    private protected static async Task<string> SurfaceText(HttpClient client, string path, string token)
    {
        using var response = await SurfaceGet(client, path, token);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, path + " " + body);
        Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true });
        return body;
    }

    private protected static async Task<JsonNode> ConsoleCatalog(HttpClient client, string token) => JsonNode.Parse(await SurfaceText(client, "/api/capabilities/console", token))!;

    private protected sealed record ConsoleHost(LifecyclePlatform Platform, RunningApp Business, WebApplication Management, WebApplication Surface) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Surface.DisposeAsync(); await Management.StopAsync(Ct); await Management.DisposeAsync(); await Business.DisposeAsync(); Platform.Dispose();
        }
    }
}
