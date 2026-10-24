using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Qian.Authz.Abstractions;
using Qian.Data;
using Qian.Gateway;
using Qian.Identity.Client;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    private protected const string ShellGatewayIssuer = "https://gateway.qian.test/s2s";
    // JWT 的头与载荷段都以 base64url 编码的 "{" 开头；任何令牌样式串（访问令牌、S2S、id_token）都会命中。
    private protected static readonly Regex TokenLike = new(@"eyJ[A-Za-z0-9_-]{2,}\.[A-Za-z0-9_-]{2,}", RegexOptions.CultureInvariant);
}

// 外壳 bootstrap 聚合端点（S1-YUE-012）：真实身份（内置账号模型，与业务运行时同进程）→ 网关 Identity.Client BFF 会话 → 网关按路由表换 S2S →
// 平台表面参考宿主（真实 Registry / Capabilities / Authz，生产模式 PEP）。浏览器只持有 qian.sid，bootstrap 响应里不得出现任何令牌。
public sealed partial class ShellSurfaceTests(PostgresFixture postgres) : CustomerCommandTests(postgres), IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 外壳bootstrap经网关聚合会话目录导航与权限_匿名401_平台身份代管_切换租户后远程与权限随之变化_旧修订401刷新后恢复_不含令牌()
    {
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        await using var business = await Start(identity: true, capabilities: true, relyingParty: new Uri(address, "auth/callback"));
        var loader = Runtime(business).Loader;
        var seen = new ConcurrentQueue<string>();
        await using var surface = PlatformSurfaceHost.Build(id => loader.GetContext(id, ScopeKey.Global), new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = ShellGatewayIssuer, ValidateAudience = true, ValidAudience = PlatformSurfaceHost.Audience, ValidateLifetime = true,
            ValidateIssuerSigningKey = true, IssuerSigningKey = business.ManagementSigningKey, ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            RequireSignedTokens = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero
        }, builder =>
        {
            builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddSingleton<IStartupFilter>(new RequestLog(seen));
        });
        await surface.StartAsync(Ct);
        await using var redis = new RedisFixture(); await redis.InitializeAsync();
        await using var gateway = await GatewayTestHost.Start(reservation, tls, business.Identity.Issuer, GatewayClientId, IdentityResource, business.Identity.Secret,
            redis.ConnectionString, ShellGatewayIssuer,
            [new("capabilities", "/api/capabilities/{**rest}", PlatformSurfaceHost.Audience) { RateLimiterPolicy = "user" }, new("authz", "/api/authz/{**rest}", PlatformSurfaceHost.Audience) { RateLimiterPolicy = "user" }],
            [new(PlatformSurfaceHost.Audience, surface.Urls.Single())], business.ManagementSigningKey.Rsa, map: app => app.MapQianShellBootstrap());
        var tenant = business.App.Services.GetRequiredService<ITenantContext>();

        // 匿名：401 ErrorDetail，不调任何下游。
        var (anonymousClient, _) = GatewayTestHost.Browser(tls); using (anonymousClient)
        using (var anonymous = await anonymousClient.GetAsync(new Uri(address, "api/shell/bootstrap"), Ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.True(anonymous.Headers.CacheControl is { Private: true, NoStore: true });
            Assert.Equal("UNAUTHENTICATED", JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("code").GetString());
        }
        Assert.Empty(seen);

        // 平台身份（没有成员身份）：代管上下文，不调任何下游。
        var (operatorPerson, operatorPhone) = await NewPerson(business, "平台运营", platform: true);
        var (operatorClient, operatorCookies) = GatewayTestHost.Browser(tls); using (operatorClient)
        {
            await ShellSignIn(operatorClient, business, operatorPhone, null); await gateway.CompleteLogin(operatorClient);
            var managed = await Bootstrap(operatorClient, address, operatorCookies);
            Assert.Equal(await SessionSnapshot(operatorClient, address), managed["session"]!.ToJsonString());
            Assert.Equal((operatorPerson.Id.ToString("D"), "user", null, null), ((string?)managed["session"]!["subject"], (string?)managed["session"]!["kind"],
                (string?)managed["session"]!["tenantId"], (string?)managed["session"]!["membershipId"]));
            Assert.True((bool)managed["managed"]!);
            Assert.Null(managed["context"]!["tenant"]); Assert.Null(managed["context"]!["membership"]); Assert.Null(managed["navigation"]);
            foreach (var name in new[] { "remotes", "mounted", "entitled", "permissions" }) Assert.Empty(managed[name]!.AsArray());
            Assert.Empty(seen);
        }

        // 成员：A（tenant.user，只读）与 B（tenant.user）两个成员身份；B 的 scope 禁用参考应用（未挂载）。
        var (person, phone) = await NewPerson(business, "成员甲");
        await Join(business, A, person.Id, "成员甲"); await Join(business, B, person.Id, "成员甲");
        var subject = person.Id.ToString("D");
        var authz = loader.GetContext("authz", ScopeKey.Global);
        foreach (var id in new[] { A, B })
            using (tenant.BeginScope(id)) await authz.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.user", "user", subject, "acceptance", Ct);
        using (tenant.BeginScope(B))
            await new PostgresPatchRepository(business.App.Services.GetRequiredService<IDataService>(), tenant, new SurfaceValidator())
                .PutAsync(new ScopeKey("tenant", B), "crm", JsonSerializer.SerializeToElement(new { id = "crm", disabled = true }), 0, "acceptance", Ct);

        var (browser, cookies) = GatewayTestHost.Browser(tls); using var _ = browser;
        await ShellSignIn(browser, business, phone, A); await gateway.CompleteLogin(browser);
        var first = await Bootstrap(browser, address, cookies);
        var firstPermissions = await AssertConsistent(browser, address, first, A, "成员甲");
        var remote = Assert.Single(first["remotes"]!.AsArray())!;
        Assert.Equal(("qian.sample.crm", "1.0.0", "global"), ((string?)remote["id"], (string?)remote["version"], (string?)remote["scope"]));
        Assert.Equal("/remotes/qian.sample.crm/mf-manifest.json", (string?)remote["manifest"]!["federation"]!["manifest"]);
        Assert.Equal(["global|qian.sample.crm"], first["mounted"]!.AsArray().Select(item => (string)item!));
        Assert.Contains("qian.sample.crm:customer:read", firstPermissions); Assert.DoesNotContain("qian.sample.crm:customer:write", firstPermissions);
        Assert.Equal(["/customers"], first["navigation"]!.AsArray().Select(item => (string)item!["route"]!));

        // 授权变更（A 里加授 tenant.owner）：身份修订递增、平台表面的修订下限同步抬高。旧令牌在三条平台表面与 bootstrap 上都是 401（不做权限判定），
        // 刷新会话换到新修订后恢复，权限与菜单随之更新。
        var revision = (long)first["session"]!["identityRevision"]!;
        using (tenant.BeginScope(A)) await authz.Inject<ITenantRoleGrant>(TenantRoleGrant.Key).AssignAsync("tenant.owner", "user", subject, "acceptance", Ct);
        foreach (var path in new[] { "api/shell/bootstrap", "api/capabilities/navigation", "api/capabilities/shell", "api/authz/me/permissions" })
            using (var stale = await browser.GetAsync(new Uri(address, path), Ct))
            {
                var body = await stale.Content.ReadAsStringAsync(Ct);
                Assert.True(stale.StatusCode == HttpStatusCode.Unauthorized, path + " " + body);
                Assert.Equal("UNAUTHENTICATED", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
            }
        var refreshed = JsonNode.Parse(await RefreshSession(browser, address))!;
        Assert.True((long)refreshed["identityRevision"]! > revision);
        var renewed = await Bootstrap(browser, address, cookies);
        var ownerPermissions = await AssertConsistent(browser, address, renewed, A, "成员甲");
        Assert.Equal((long)refreshed["identityRevision"]!, (long)renewed["session"]!["identityRevision"]!);
        Assert.Contains("qian.sample.crm:customer:write", ownerPermissions);
        Assert.Equal(["/customers", "/customers/new"], renewed["navigation"]!.AsArray().Select(item => (string)item!["route"]!));

        // 切换到 B（另一次登录选 B）：会话、租户、远程与权限都随之变化；未挂载的参考应用不出现，其远程清单地址也不在响应里。
        await ShellSignIn(browser, business, phone, B); await gateway.CompleteLogin(browser);
        var (switched, text) = await BootstrapText(browser, address, cookies);
        var otherPermissions = await AssertConsistent(browser, address, switched, B, "成员甲");
        foreach (var name in new[] { "remotes", "mounted", "entitled" }) Assert.Empty(switched[name]!.AsArray());
        Assert.DoesNotContain("mf-manifest.json", text, StringComparison.Ordinal); Assert.DoesNotContain("/remotes/", text, StringComparison.Ordinal);
        Assert.Empty(switched["navigation"]!.AsArray());
        Assert.NotEqual(ownerPermissions, otherPermissions);
        Assert.DoesNotContain("qian.sample.crm:customer:write", otherPermissions);
    }

    [Fact]
    public async Task 外壳bootstrap下游404与缺路由按空处理_清单地址不是同源路径的远程不交给外壳_都记警告()
    {
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        await using var business = await Start(identity: true, relyingParty: new Uri(address, "auth/callback"));
        // 平台表面的替身：先对一切都回 404（平台表面未部署、组件未挂载）；切到目录模式后，外壳目录给出上面那份清单。
        var catalog = false; var seen = new ConcurrentQueue<string>();
        var stub = WebApplication.CreateSlimBuilder(); stub.WebHost.UseUrls("http://127.0.0.1:0"); stub.Logging.ClearProviders();
        await using var surface = stub.Build();
        surface.Run(async http =>
        {
            seen.Enqueue(http.Request.Path.Value ?? "");
            if (!catalog || http.Request.Path != "/api/capabilities/shell") { http.Response.StatusCode = StatusCodes.Status404NotFound; return; }
            http.Response.ContentType = "application/json";
            await http.Response.WriteAsync(ShellCatalogWithForeignManifests.Replace("TENANT-A", A, StringComparison.Ordinal), http.RequestAborted);
        });
        await surface.StartAsync(Ct);
        await using var redis = new RedisFixture(); await redis.InitializeAsync();
        var logs = new ShellLogs();
        // 路由表只有能力表面一条（外壳目录与能力导航经它到替身），有效权限没有路由。
        await using var gateway = await GatewayTestHost.Start(reservation, tls, business.Identity.Issuer, GatewayClientId, IdentityResource, business.Identity.Secret,
            redis.ConnectionString, ShellGatewayIssuer, [new("capabilities", "/api/capabilities/{**rest}", PlatformSurfaceHost.Audience) { RateLimiterPolicy = "user" }],
            [new(PlatformSurfaceHost.Audience, surface.Urls.Single())], business.ManagementSigningKey.Rsa,
            map: app => { app.Services.GetRequiredService<ILoggerFactory>().AddProvider(logs); app.MapQianShellBootstrap(); });
        var (person, phone) = await NewPerson(business, "成员乙");
        await Join(business, A, person.Id, "成员乙");
        var (browser, cookies) = GatewayTestHost.Browser(tls); using var _ = browser;
        await ShellSignIn(browser, business, phone, A); await gateway.CompleteLogin(browser);

        // 一、外壳目录与能力导航下游 404，有效权限没有路由：会话照常，租户名退回租户 id，远程、挂载、权益与权限全空，导航为 null；不是 502。
        var missing = await Bootstrap(browser, address, cookies);
        Assert.Equal(await SessionSnapshot(browser, address), missing["session"]!.ToJsonString());
        Assert.False((bool)missing["managed"]!);
        Assert.Equal((A, A), ((string?)missing["context"]!["tenant"]!["id"], (string?)missing["context"]!["tenant"]!["name"]));
        Assert.Equal((string?)missing["session"]!["membershipId"], (string?)missing["context"]!["membership"]!["id"]);
        Assert.Empty(missing["context"]!["membership"]!["permissions"]!.AsArray());
        foreach (var name in new[] { "remotes", "mounted", "entitled", "permissions" }) Assert.Empty(missing[name]!.AsArray());
        Assert.Null(missing["navigation"]);
        // 有路由的两条确实打到了下游；没有路由的有效权限不发出站请求。
        Assert.Equal(["/api/capabilities/navigation", "/api/capabilities/shell"], seen.Order(StringComparer.Ordinal));
        var surfaceMissing = Assert.Single(logs.Entries);
        Assert.Equal((62, LogLevel.Warning), (surfaceMissing.EventId, surfaceMissing.Level));
        Assert.Equal(("404", "404", "unrouted"), ((string?)surfaceMissing.Values["Shell"], (string?)surfaceMissing.Values["Navigation"], (string?)surfaceMissing.Values["Permissions"]));

        // 二、外壳目录有一个同源远程与三个清单地址不合格的远程：只交出同源那个，其余跳过并记警告，不拖垮整页。
        catalog = true; seen.Clear(); logs.Entries.Clear();
        var (filtered, text) = await BootstrapText(browser, address, cookies);
        Assert.Equal("甲方", (string?)filtered["context"]!["tenant"]!["name"]);
        var kept = Assert.Single(filtered["remotes"]!.AsArray())!;
        Assert.Equal(("qian.sample.crm", "/remotes/qian.sample.crm/mf-manifest.json"), ((string?)kept["id"], (string?)kept["manifest"]!["federation"]!["manifest"]));
        Assert.Equal(["global|qian.sample.crm"], filtered["mounted"]!.AsArray().Select(item => (string)item!));
        Assert.Equal(["global|qian.sample.crm"], filtered["entitled"]!.AsArray().Select(item => (string)item!));
        foreach (var foreign in new[] { "cdn.example", "evil.example", "cdn.remote", "relative.remote", "missing.federation" }) Assert.DoesNotContain(foreign, text, StringComparison.Ordinal);
        Assert.Equal(new[] { (62, LogLevel.Warning), (63, LogLevel.Warning) }, logs.Entries.Select(entry => (entry.EventId, entry.Level)).Order());
        Assert.Equal("cdn.remote,relative.remote,missing.federation", (string?)logs.Entries.Single(entry => entry.EventId == 63).Values["Components"]);
    }
}

public abstract partial class CustomerCommandTests
{
    // 外壳目录替身：一个同源远程与三个清单地址不合格的远程（绝对地址、协议相对、缺 federation）；租户与 scope 都合法，只有清单地址不合格。
    private protected const string ShellCatalogWithForeignManifests = """
        {"tenant":{"id":"TENANT-A","name":"甲方"},"remotes":[
          {"id":"qian.sample.crm","version":"1.0.0","scope":"global","manifest":{"$schema":"https://qian.dev/schemas/ui/v2","federation":{"manifest":"/remotes/qian.sample.crm/mf-manifest.json","exposes":["CustomersPage"]}}},
          {"id":"cdn.remote","version":"1.0.0","scope":"global","manifest":{"federation":{"manifest":"https://cdn.example/mf-manifest.json","exposes":["Page"]}}},
          {"id":"relative.remote","version":"1.0.0","scope":"tenant:TENANT-A","manifest":{"federation":{"manifest":"//evil.example/mf-manifest.json","exposes":["Page"]}}},
          {"id":"missing.federation","version":"1.0.0","scope":"global","manifest":{}}]}
        """;

    // 与浏览器直调同一路由、同一受众：session 与 GET /auth/session 逐字段相同；remotes 与外壳目录、navigation 与能力导航、permissions 与有效权限里放行的权限码一致。
    private protected static async Task<string[]> AssertConsistent(HttpClient browser, Uri address, JsonNode bootstrap, string tenantId, string displayName)
    {
        Assert.Equal(await SessionSnapshot(browser, address), bootstrap["session"]!.ToJsonString());
        Assert.Equal((tenantId, displayName, "user"), ((string?)bootstrap["session"]!["tenantId"], (string?)bootstrap["session"]!["displayName"], (string?)bootstrap["session"]!["kind"]));
        Assert.False((bool)bootstrap["managed"]!);
        var context = bootstrap["context"]!;
        Assert.Equal((tenantId, "command tenant", "zh-CN", "light"), ((string?)context["tenant"]!["id"], (string?)context["tenant"]!["name"], (string?)context["locale"], (string?)context["theme"]));
        Assert.Null(context["app"]);
        Assert.Equal((string?)bootstrap["session"]!["membershipId"], (string?)context["membership"]!["id"]);
        var shell = JsonNode.Parse(await Through(browser, address, "api/capabilities/shell"))!;
        Assert.Equal(tenantId, (string?)shell["tenant"]!["id"]);
        Assert.True(JsonNode.DeepEquals(shell["remotes"], bootstrap["remotes"]), bootstrap["remotes"]!.ToJsonString());
        Assert.Equal(bootstrap["mounted"]!.ToJsonString(), bootstrap["entitled"]!.ToJsonString());
        var navigation = JsonNode.Parse(await Through(browser, address, "api/capabilities/navigation"))!;
        Assert.True(JsonNode.DeepEquals(navigation["items"], bootstrap["navigation"]), bootstrap["navigation"]?.ToJsonString());
        var allowed = JsonNode.Parse(await Through(browser, address, "api/authz/me/permissions"))!["items"]!.AsArray()
            .Where(item => (bool)item!["allowed"]!).Select(item => (string)item!["permission"]!).Order(StringComparer.Ordinal).ToArray();
        var permissions = bootstrap["permissions"]!.AsArray().Select(item => (string)item!).ToArray();
        Assert.Equal(allowed, permissions);
        Assert.Equal(permissions, context["membership"]!["permissions"]!.AsArray().Select(item => (string)item!));
        return permissions;
    }

    private protected static async Task<JsonNode> Bootstrap(HttpClient browser, Uri address, CookieContainer cookies) => (await BootstrapText(browser, address, cookies)).Node;

    // 200、private no-store；响应体不含令牌样式串、不含 qian.sid 的值，也不出现令牌字段名。
    private protected static async Task<(JsonNode Node, string Text)> BootstrapText(HttpClient browser, Uri address, CookieContainer cookies)
    {
        using var response = await browser.GetAsync(new Uri(address, "api/shell/bootstrap"), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true });
        Assert.DoesNotMatch(TokenLike, text);
        var sid = cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]?.Value;
        Assert.False(string.IsNullOrEmpty(sid)); Assert.DoesNotContain(sid, text, StringComparison.Ordinal);
        foreach (var name in new[] { "access_token", "refresh_token", "id_token", "accessToken", "refreshToken", "idToken", "Bearer" })
            Assert.DoesNotContain(name, text, StringComparison.OrdinalIgnoreCase);
        return (JsonNode.Parse(text)!, text);
    }

    private protected static async Task<string> SessionSnapshot(HttpClient browser, Uri address) => JsonNode.Parse(await Through(browser, address, "auth/session"))!.ToJsonString();

    private protected static async Task<string> Through(HttpClient browser, Uri address, string path)
    {
        using var response = await browser.GetAsync(new Uri(address, path), Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, path + " " + text);
        return text;
    }

    // POST /auth/session/refresh：先取防伪令牌（Kun Cookie 中间件对已认证的非安全方法校验）。
    private protected static async Task<string> RefreshSession(HttpClient browser, Uri address)
    {
        var csrf = JsonNode.Parse(await Through(browser, address, "auth/csrf"))!;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(address, "auth/session/refresh"));
        request.Headers.Add((string)csrf["headerName"]!, (string)csrf["token"]!);
        using var response = await browser.SendAsync(request, Ct);
        var text = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
        return text;
    }

    // 在发行方的账号接口完成密码登录（先取防伪令牌）；tenant 为 null 即平台身份。
    private protected static async Task ShellSignIn(HttpClient browser, RunningApp host, string phone, string? tenant)
    {
        var issuer = new Uri(host.Identity.Issuer);
        using var csrf = await browser.GetAsync(new Uri(issuer, "api/identity/csrf"), Ct); csrf.EnsureSuccessStatusCode();
        var token = await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(issuer, "api/identity/login"))
            { Content = JsonContent.Create(new { client_id = GatewayClientId, identifier = phone, password = IdentityPassword, tenant_id = tenant is null ? (Guid?)null : Guid.Parse(tenant) }) };
        request.Headers.Add(token.GetProperty("header").GetString()!, token.GetProperty("token").GetString());
        using var response = await browser.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("authenticated", JsonDocument.Parse(body).RootElement.GetProperty("status").GetString());
    }

    // 网关外壳 bootstrap 的两条警告（事件 62 平台表面缺失、63 清单地址不是同源路径）：级别与结构化参数。
    private protected sealed class ShellLogs : ILoggerProvider
    {
        internal ConcurrentQueue<(int EventId, LogLevel Level, IReadOnlyDictionary<string, object?> Values)> Entries { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(categoryName, Entries);
        public void Dispose() { }
        private sealed class Sink(string category, ConcurrentQueue<(int EventId, LogLevel Level, IReadOnlyDictionary<string, object?> Values)> entries) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == "Qian.Gateway" && eventId.Id is 62 or 63 && state is IEnumerable<KeyValuePair<string, object?>> values)
                    entries.Enqueue((eventId.Id, logLevel, values.ToDictionary(pair => pair.Key, pair => pair.Value)));
            }
        }
    }

    // 平台表面宿主收到的每个请求（路径），证明匿名与平台身份的 bootstrap 不调下游。
    private protected sealed class RequestLog(ConcurrentQueue<string> seen) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (http, inner) => { seen.Enqueue(http.Request.Path.Value ?? ""); await inner(http); });
            next(app);
        };
    }
}
