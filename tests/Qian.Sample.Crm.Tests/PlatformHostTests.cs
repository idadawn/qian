using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kun.Core.MultiTenancy;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Qian.Audit;
using Qian.Authz;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Data;
using Qian.Gateway;
using Qian.Lifecycle;
using Qian.Platform.Host;
using Qian.Plugin.Sdk;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Registry;
using Qian.Runtime;
using Qian.Surfaces;
using Qian.Tenancy;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;

namespace Qian.Sample.Crm.Tests;

// 生产平台宿主（S1-DEP-009）：hosts/Qian.Platform.Host 的同一装配入口（PlatformHost.Build），按仓库交付的 platform profile、宿主 overlay 与一份环境 overlay
// 装配平台插件树；参考应用作为 module 应用，经组合的扩展登记并进同一插件树。身份服务由参考宿主（同进程身份）充当，数据库与它共用。
public sealed class PlatformHostTests(PostgresFixture postgres) : CustomerCommandTests(postgres), IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 生产平台宿主按platform_profile装配平台插件树_运行期连接都是受限登录_只收网关S2S且按匹配到的端点分受众_错误签发方受众过期算法签名与Cookie外部令牌都401_扩展端点兜底要求认证且未声明权限点缺省拒绝_平台身份访问租户表面401()
    {
        await using var business = await Start(identity: true, relyingParty: new Uri("https://localhost/auth/callback"));
        var signing = business.ManagementSigningKey;
        await using var jwks = await JwksServer(signing);
        var jwksUri = new Uri(new Uri(jwks.Urls.Single()), ".well-known/jwks.json");
        // 用例自己的扩展端点（四、七用）：平台扩展端点一个不带组件绑定，一个带组件端点绑定却不声明权限点，一个带路由参数、能匹配运营前缀下的路径；
        // 一个运营扩展端点经 AddOperations 挂在 /api/host-operations 下。
        var binding = new PluginEndpointBinding("crm", "qian.sample.crm", CapabilityBindingRegistration.Key, ListId);
        await using var platform = await StartPlatformHost(business, jwksUri, extend: composition => composition
            .MapEndpoints((endpoints, _) =>
            {
                endpoints.MapGet("/api/host-extension/plain", () => Results.Text("plain"));
                endpoints.MapGet("/api/host-extension/component", () => Results.Text("component")).RequireQianPlugin(binding);
                endpoints.MapGet("/api/{area}/host-extension-probe", (string area) => Results.Text(area));
            })
            .AddOperations("/api/host-operations", (endpoints, _) => endpoints.MapGet("/api/host-operations/ping", () => Results.Text("operations"))));
        var runtime = platform.Runtime;

        // 一、插件树：仓库交付的 platform profile（base / shell / model-gateway 三个组合包）经宿主 overlay 关掉没有 Provider 的条目，config 换成 qian.config；
        //     参考应用由扩展 overlay 加进全局 scope。全部条目 Ready，平台组件清单没有骨架标记，组件目录就是仓库里的 plugins/<组件>。
        Assert.True(runtime.IsReady);
        Assert.Equal(["audit", "authz", "capabilities", "config", "crm", "data", "events", "lifecycle", "registry", "tenancy"],
            runtime.Profile.Entries.Select(entry => entry.Id).Order(StringComparer.Ordinal));
        Assert.Equal(["qian.bundle.base@1.0.0", "qian.bundle.model-gateway@1.0.0", "qian.bundle.shell@1.0.0"],
            runtime.Profile.Layers.Where(layer => layer.Kind == "bundle").Select(layer => layer.Reference!).Order(StringComparer.Ordinal));
        Assert.Equal("qian.config@1.0.0", runtime.Profile.Entries.Single(entry => entry.Id == "config").Plugin);
        foreach (var entry in runtime.Profile.Entries)
        {
            Assert.True(runtime.Loader.GetState(entry.Id, ScopeKey.Global) == PluginState.Ready, entry.Id + " " + runtime.Loader.GetFailure(entry.Id, ScopeKey.Global));
            Assert.False(entry.Manifest.TryGetProperty("x-skeleton", out _), entry.Id);
            if (entry.Id != "crm") Assert.Equal(Path.Combine(RepositoryRoot(), "plugins", entry.Plugin.Split('@')[0]), entry.ComponentDirectory);
        }
        using var client = new HttpClient { BaseAddress = platform.Address };
        Assert.Equal("ok", await client.GetStringAsync("/healthz", Ct));
        Assert.Equal("ready", await client.GetStringAsync("/readyz", Ct));

        // 二、运行期连接：每个数据上下文实际连上的登录都是受限角色（NOSUPERUSER / NOBYPASSRLS / NOCREATEDB / NOCREATEROLE），
        //     只有应用登录与两个平台登录三种；迁移属主的登录只给 --migrate-only 用，在线副本不连。
        var logins = new HashSet<string>(StringComparer.Ordinal);
        foreach (var type in new[] { typeof(TenancyDbContext), typeof(AuthzDbContext), typeof(PlatformAuthzDbContext), typeof(AuditDbContext), typeof(PlatformAuditDbContext),
            typeof(RegistryDbContext), typeof(CapabilitiesDbContext), typeof(LifecycleDbContext), typeof(CrmDbContext) })
        {
            await using var scope = platform.Services.CreateAsyncScope();
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(type);
            await using var connection = new NpgsqlConnection(context.Database.GetConnectionString());
            await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(
                "SELECT current_user::text, r.rolsuper, r.rolbypassrls, r.rolcreatedb, r.rolcreaterole FROM pg_catalog.pg_roles r WHERE r.rolname = current_user", connection);
            await using var reader = await command.ExecuteReaderAsync(Ct);
            Assert.True(await reader.ReadAsync(Ct));
            Assert.True((reader.GetBoolean(1), reader.GetBoolean(2), reader.GetBoolean(3), reader.GetBoolean(4)) == (false, false, false, false), type.Name + " " + reader.GetString(0));
            logins.Add(reader.GetString(0));
        }
        Assert.Equal(new[] { platform.AppRole, platform.AuditRole, platform.AuthzRole }.Order(StringComparer.Ordinal), logins.Order(StringComparer.Ordinal));

        // 三、对照：网关形状的 S2S 照常放行——租户里的服务主体读能力目录（平台受众），持平台超管的平台身份读租户列表（运营受众）。
        await new PostgresPlatformRoleStore(platform.Services.GetRequiredService<IDataService>(), platform.Services.GetRequiredService<ITenantContext>(), platform.AuthzRole)
            .AssignSuperadminAsync("root", "operator", Ct);
        const string Catalog = "/api/capabilities", Tenants = "/api/tenancy/tenants";
        var member = PlatformS2s(signing, A, Writer, "service");
        var root = PlatformS2s(signing, null, "root", audience: PlatformHostSettings.OperationsAudience);
        Assert.Contains(CreateId, JsonNode.Parse(await PlatformText(client, Catalog, member))!["items"]!.AsArray().Select(item => (string?)item!["id"]));
        var tenantIds = Ids(await PlatformText(client, Tenants, root));
        Assert.Contains(A, tenantIds);

        // 四、受众按路由实际匹配到的端点选，不按路径写法（R144 ①）：端点路由不区分大小写、忽略结尾斜杠，同一端点的各种写法受众相同。
        //     运营端点的大小写与结尾斜杠变体：平台受众 401，运营受众与规范路径同样放行、结果相同；平台端点的大小写变体：平台受众照常放行，运营受众 401。
        //     没有匹配到端点的运营前缀路径（含大小写变体）与方法不符的运营路径：运营受众得到 404 / 405（不是会被外壳当成会话失效的 401），平台受众 401。
        var platformRoot = PlatformS2s(signing, null, "root");
        var memberOperations = PlatformS2s(signing, A, Writer, "service", audience: PlatformHostSettings.OperationsAudience);
        foreach (var path in new[] { "/api/Tenancy/tenants", "/API/tenancy/tenants", "/Api/TENANCY/Tenants", "/api/tenancy/tenants/" })
        {
            await AssertHttp(await PlatformGet(client, path, platformRoot), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
            Assert.Equal(tenantIds, Ids(await PlatformText(client, path, root)));
        }
        var stateMachine = await PlatformText(client, "/api/tenancy/meta/state-machine", root);
        await AssertHttp(await PlatformGet(client, "/api/TENANCY/meta/state-machine", platformRoot), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        Assert.Equal(stateMachine, await PlatformText(client, "/api/TENANCY/meta/state-machine", root));
        Assert.Contains(CreateId, JsonNode.Parse(await PlatformText(client, "/API/Capabilities", member))!["items"]!.AsArray().Select(item => (string?)item!["id"]));
        foreach (var (path, token) in new[] { ("/api/Capabilities", memberOperations), ("/API/capabilities/navigation", memberOperations), ("/api/AUTHZ/me/permissions", memberOperations),
            ("/API/capabilities/console", root) })
            await AssertHttp(await PlatformGet(client, path, token), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        // 运营前缀下匹配到的是平台端点（带路由参数的平台扩展端点）：受众跟端点走，仍只收平台受众，运营受众 401——按路径前缀选就会把运营受众放进平台端点。
        Assert.Equal("tenancy", await PlatformText(client, "/api/tenancy/host-extension-probe", member));
        await AssertHttp(await PlatformGet(client, "/api/tenancy/host-extension-probe", root), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        foreach (var path in new[] { "/api/tenancy/no-such-endpoint", "/api/TENANCY/no-such-endpoint" })
        {
            using (var missing = await PlatformGet(client, path, root)) Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
            await AssertHttp(await PlatformGet(client, path, platformRoot), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }
        foreach (var (token, status) in new[] { (root, HttpStatusCode.MethodNotAllowed), (platformRoot, HttpStatusCode.Unauthorized) })
            using (var request = new HttpRequestMessage(HttpMethod.Delete, "/api/Tenancy/tenants"))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var response = await client.SendAsync(request, Ct);
                Assert.Equal(status, response.StatusCode);
            }

        // 五、令牌不合格一律 401 UNAUTHENTICATED：没有令牌、签发方不对、受众不对（运营受众到平台端点、平台受众到运营端点、两个受众）、过期、尚未生效、
        //     非 RS256（RS384 / HS256 / 不签名）、未知密钥签名、改过载荷；浏览器 Cookie 与身份服务签发的外部访问令牌都不是本宿主的凭据。
        using var unknown = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var machine = new KunOidcClient(new KunOidcClientOptions { Name = MachineClientId, Authority = business.Identity.Issuer, ClientId = MachineClientId,
            ClientSecret = business.Identity.Secret, RequireHttpsMetadata = false }, business.App.Services.GetRequiredService<IHttpClientFactory>());
        var external = (await machine.ClientCredentialsAsync("crm.read crm.write", Ct)).AccessToken;
        var rejected = new (string Case, string Path, string? Token)[]
        {
            ("没有令牌", Catalog, null),
            ("签发方", Catalog, PlatformS2s(signing, A, Writer, "service", issuer: "https://other-gateway.qian.test/s2s")),
            ("运营受众到平台端点", Catalog, PlatformS2s(signing, A, Writer, "service", audience: PlatformHostSettings.OperationsAudience)),
            ("平台受众到运营端点", Tenants, PlatformS2s(signing, null, "root")),
            ("两个受众", Catalog, PlatformS2s(signing, A, Writer, "service", edit: payload => payload["aud"] = new[] { PlatformHostSettings.PlatformAudience, PlatformHostSettings.OperationsAudience })),
            ("过期", Catalog, PlatformS2s(signing, A, Writer, "service", edit: payload => { payload["iat"] = payload["nbf"] = now - 120; payload["exp"] = now - 60; })),
            ("尚未生效", Catalog, PlatformS2s(signing, A, Writer, "service", edit: payload => { payload["nbf"] = now + 60; payload["exp"] = now + 120; })),
            ("RS384", Catalog, PlatformS2s(signing, A, Writer, "service", algorithm: SecurityAlgorithms.RsaSha384)),
            ("HS256", Catalog, PlatformS2s(new SymmetricSecurityKey(RandomNumberGenerator.GetBytes(32)) { KeyId = signing.KeyId }, A, Writer, "service", algorithm: SecurityAlgorithms.HmacSha256)),
            ("不签名", Catalog, Unsigned(PlatformS2s(signing, A, Writer, "service"))),
            ("未知密钥", Catalog, PlatformS2s(new RsaSecurityKey(unknown) { KeyId = signing.KeyId }, A, Writer, "service")),
            ("改过载荷", Catalog, Tampered(PlatformS2s(signing, A, Writer, "service"), "tenant_id", B)),
            ("外部访问令牌", Catalog, external)
        };
        foreach (var (name, path, token) in rejected)
            using (var response = await PlatformGet(client, path, token))
            {
                var body = await response.Content.ReadAsStringAsync(Ct);
                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized, name + " " + body);
                Assert.Equal("UNAUTHENTICATED", JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
            }
        using (var cookie = new HttpRequestMessage(HttpMethod.Get, Catalog))
        {
            cookie.Headers.Add("Cookie", "qian.sid=" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
            await AssertHttp(await client.SendAsync(cookie, Ct), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }

        // 六、平台身份（没有租户）访问租户表面：能力目录、导航、外壳目录、有效权限都 401（平台身份没有租户表面）；租户主体访问平台控制台目录按不存在 404。
        foreach (var path in new[] { Catalog, "/api/capabilities/navigation", "/api/capabilities/shell", "/api/authz/me/permissions" })
            await AssertHttp(await PlatformGet(client, path, platformRoot), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await AssertHttp(await PlatformGet(client, "/api/capabilities/console", member), HttpStatusCode.NotFound, "NOT_FOUND");

        // 七、扩展端点（经 MapEndpoints / AddOperations 挂上）：宿主的兜底授权策略要求已认证，匿名一律 401。不带组件绑定的平台扩展端点对平台受众放行、对运营受众 401；
        //     带组件端点绑定却没有声明权限点的端点，PEP 按生产模式缺省拒绝，已认证的租户主体也是 403；运营扩展端点与租户运营接口同在运营端点组，只收运营受众（大小写变体同样）。
        foreach (var path in new[] { "/api/host-extension/plain", "/api/host-extension/component", "/api/host-operations/ping" })
            await AssertHttp(await PlatformGet(client, path, null), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        Assert.Equal("plain", await PlatformText(client, "/api/host-extension/plain", member));
        await AssertHttp(await PlatformGet(client, "/api/host-extension/plain", memberOperations), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        await AssertHttp(await PlatformGet(client, "/api/host-extension/component", member), HttpStatusCode.Forbidden, "AUTHZ_DENIED");
        foreach (var path in new[] { "/api/host-operations/ping", "/API/Host-Operations/PING" })
        {
            Assert.Equal("operations", await PlatformText(client, path, root));
            await AssertHttp(await PlatformGet(client, path, platformRoot), HttpStatusCode.Unauthorized, "UNAUTHENTICATED");
        }

        // 八、运营端点组与声明的运营前缀不一致时宿主不启动：平台扩展端点放在运营前缀下（大小写不同也算），或运营扩展端点不在它声明的前缀下；运营前缀不能以斜杠结尾。
        foreach (var rogue in new Action<PlatformHostComposition>[]
        {
            composition => composition.MapEndpoints((endpoints, _) => endpoints.MapGet("/API/Tenancy/rogue", () => Results.Text("rogue"))),
            composition => composition.AddOperations("/api/host-operations", (endpoints, _) => endpoints.MapGet("/api/elsewhere", () => Results.Text("rogue")))
        })
            Assert.Contains("运营", (await Assert.ThrowsAsync<InvalidOperationException>(() => StartPlatformHost(business, jwksUri, extend: rogue))).Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<ArgumentException>(() => StartPlatformHost(business, jwksUri, extend: composition => composition.AddOperations("/api/host-operations/")));
    }

    // 租户列表响应里的租户 Id（按响应顺序）。
    private static string[] Ids(string list) => [.. JsonNode.Parse(list)!["items"]!.AsArray().Select(item => (string)item!["id"]!)];

    // 网关用网关宿主 appsettings.json 的生产路由表（R144 ②：能力与有效权限进 qian-platform，租户运营进 qian-operations，两个簇同一服务地址），
    // 只把簇地址换成本宿主的回环地址；本宿主经网关的 JWKS 验签。与参考宿主的外壳 bootstrap（ShellBootstrapTests）、经外壳执行应用能力（ShellInvokeTests）同一契约。
    [Fact]
    public async Task 生产网关路由表加本宿主跑外壳bootstrap与参考宿主同一契约_持平台超管的运营经运营簇读租户_用户经通用执行入口以本人执行module应用能力且审计记user()
    {
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve(); using var hostPort = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        await using var business = await Start(identity: true, relyingParty: new Uri(address, "auth/callback"));
        await using var redis = new RedisFixture(); await redis.InitializeAsync();
        var (routes, clusters) = ProductionRoutes(new Uri($"http://127.0.0.1:{hostPort.Port}/"));
        await using var gateway = await GatewayTestHost.Start(reservation, tls, business.Identity.Issuer, GatewayClientId, IdentityResource, business.Identity.Secret,
            redis.ConnectionString, ShellGatewayIssuer, routes, clusters, business.ManagementSigningKey.Rsa, map: app => app.MapQianShellBootstrap(), scopes: ["crm.read", "crm.write"]);
        var seen = new ConcurrentQueue<string>();
        await using var platform = await StartPlatformHost(business, new Uri(gateway.HttpAddress, ".well-known/jwks.json"), hostPort, seen);
        // 授权变更走身份侧（参考宿主同进程身份）：递增身份修订并抬高修订下限；本宿主的 PEP 每次判定读库比对下限（S1-DEC-027）。
        var tenant = business.App.Services.GetRequiredService<ITenantContext>();
        var grants = Runtime(business).Loader.GetContext("authz", ScopeKey.Global).Inject<ITenantRoleGrant>(TenantRoleGrant.Key);
        var platformTenant = platform.Services.GetRequiredService<ITenantContext>();

        // 一、匿名：网关直接 401，不调本宿主。
        var (anonymousClient, _) = GatewayTestHost.Browser(tls); using (anonymousClient)
        using (var anonymous = await anonymousClient.GetAsync(new Uri(address, "api/shell/bootstrap"), Ct))
        {
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
            Assert.True(anonymous.Headers.CacheControl is { Private: true, NoStore: true });
            Assert.Equal("UNAUTHENTICATED", JsonDocument.Parse(await anonymous.Content.ReadAsStringAsync(Ct)).RootElement.GetProperty("code").GetString());
        }
        Assert.Empty(seen);

        // 二、不持角色的平台身份：只问平台控制台目录（为空），回代管上下文。
        var (_, plainPhone) = await NewPerson(business, "平台访客", platform: true);
        var (plainClient, plainCookies) = GatewayTestHost.Browser(tls); using (plainClient)
        {
            await ShellSignIn(plainClient, business, plainPhone, null); await gateway.CompleteLogin(plainClient);
            var managed = await Bootstrap(plainClient, address, plainCookies);
            Assert.Equal(["/api/capabilities/console"], seen); seen.Clear();
            Assert.Equal(await SessionSnapshot(plainClient, address), managed["session"]!.ToJsonString());
            Assert.True((bool)managed["managed"]!);
            Assert.Null(managed["context"]!["tenant"]); Assert.Null(managed["context"]!["membership"]); Assert.Null(managed["navigation"]);
            foreach (var name in new[] { "remotes", "mounted", "entitled", "permissions" }) Assert.Empty(managed[name]!.AsArray());
        }

        // 三、持平台超管的平台身份：控制台目录给出平台组件 qian.tenancy 的远程（制品快照里的 UI 清单原文）、菜单与只读权限码，不代管——
        //     宿主给声明了 UI 清单的条目包上导航包装（R144 ②）；同一身份经网关的运营簇（受众 qian-operations）读租户列表 200。
        var (operatorPerson, operatorPhone) = await NewPerson(business, "平台运营", platform: true);
        await new PostgresPlatformRoleStore(platform.Services.GetRequiredService<IDataService>(), platformTenant, platform.AuthzRole)
            .AssignSuperadminAsync(operatorPerson.Id.ToString("D"), "root", Ct);
        var (operatorClient, operatorCookies) = GatewayTestHost.Browser(tls); using (operatorClient)
        {
            await ShellSignIn(operatorClient, business, operatorPhone, null); await gateway.CompleteLogin(operatorClient);
            var granted = await Bootstrap(operatorClient, address, operatorCookies);
            Assert.Equal(["/api/capabilities/console"], seen); seen.Clear();
            Assert.False((bool)granted["managed"]!);
            Assert.Equal(["global|qian.tenancy"], granted["mounted"]!.AsArray().Select(item => (string)item!));
            Assert.Equal(granted["mounted"]!.ToJsonString(), granted["entitled"]!.ToJsonString());
            var console = Assert.Single(granted["remotes"]!.AsArray())!;
            Assert.Equal(("qian.tenancy", "1.0.0", "global"), ((string?)console["id"], (string?)console["version"], (string?)console["scope"]));
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins", "qian.tenancy", "ui", "manifest.json"))), console["manifest"]));
            Assert.Equal(["/tenants"], granted["navigation"]!.AsArray().Select(item => (string)item!["route"]!));
            Assert.Equal(["qian.tenancy:tenant:read"], granted["permissions"]!.AsArray().Select(item => (string)item!));
            var tenants = Ids(await Through(operatorClient, address, "api/tenancy/tenants"));
            Assert.Contains(A, tenants);
            Assert.Equal(["/api/tenancy/tenants"], seen); seen.Clear();
            // 网关路由不区分大小写：运营路径的大小写与结尾斜杠变体同样经运营簇（受众 qian-operations）原样转给本宿主，本宿主按匹配到的端点选运营受众，结果与规范路径相同。
            foreach (var variant in new[] { "api/Tenancy/tenants", "API/tenancy/tenants", "api/tenancy/tenants/" })
            {
                Assert.Equal(tenants, Ids(await Through(operatorClient, address, variant)));
                Assert.Equal(["/" + variant], seen); seen.Clear();
            }
            Assert.Equal(await Through(operatorClient, address, "api/tenancy/meta/state-machine"), await Through(operatorClient, address, "api/TENANCY/meta/state-machine"));
            Assert.Equal(["/api/tenancy/meta/state-machine", "/api/TENANCY/meta/state-machine"], seen); seen.Clear();
        }

        // 四、用户经网关与本宿主的通用执行入口执行 module 应用能力，以本人执行（S1-YUE-015 裁决 ④）。甲方：所有者（tenant.owner）与使用者（tenant.user，只读）；
        //     使用者在乙方也是 tenant.user，另有一个只属于乙方的用户。所有者列表、新建、按 Id 取回；使用者看得到同租户的客户、新建与不存在一样 404；
        //     乙方看不到甲方的客户（按 Id 直取 404）；照网关形状直调本宿主时不带 act 照常执行，多一个 act 即 404。
        var (owner, ownerPhone) = await NewPerson(business, "外壳所有者"); var (reader, readerPhone) = await NewPerson(business, "外壳使用者");
        var (outsider, outsiderPhone) = await NewPerson(business, "乙方用户");
        string ownerId = owner.Id.ToString("D"), readerId = reader.Id.ToString("D"), outsiderId = outsider.Id.ToString("D");
        await Join(business, A, owner.Id, "外壳所有者"); await Join(business, A, reader.Id, "外壳使用者");
        await Join(business, B, reader.Id, "外壳使用者"); await Join(business, B, outsider.Id, "乙方用户");
        using (tenant.BeginScope(A))
        {
            await grants.AssignAsync("tenant.owner", "user", ownerId, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", readerId, "acceptance", Ct);
        }
        using (tenant.BeginScope(B))
        {
            await grants.AssignAsync("tenant.user", "user", readerId, "acceptance", Ct);
            await grants.AssignAsync("tenant.user", "user", outsiderId, "acceptance", Ct);
        }
        var (ownerBrowser, _) = GatewayTestHost.Browser(tls); using var ownerSession = ownerBrowser;
        await ShellSignIn(ownerBrowser, business, ownerPhone, A); await gateway.CompleteLogin(ownerBrowser);
        Assert.Empty((await ShellList(ownerBrowser, address)).Items);
        var key = Guid.CreateVersion7(); Guid customer;
        using (var created = await ShellInvoke(ownerBrowser, address, CreateId, CustomerInput(key, "外壳客户"), key))
        {
            var body = await created.Content.ReadAsStringAsync(Ct);
            Assert.True(created.StatusCode == HttpStatusCode.OK, body);
            var item = JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!;
            Assert.Equal((A, "外壳客户"), (item.TenantId, item.Name));
            customer = item.Id;
        }
        using (var fetched = await ShellInvoke(ownerBrowser, address, GetId, CustomerKey(customer)))
        {
            var body = await fetched.Content.ReadAsStringAsync(Ct);
            Assert.True(fetched.StatusCode == HttpStatusCode.OK, body);
            Assert.Equal((customer, A), (JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!.Id, JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!.TenantId));
        }
        Assert.Equal([customer], (await ShellList(ownerBrowser, address)).Items.Select(item => item.Id));
        var (readerBrowser, _) = GatewayTestHost.Browser(tls); using var readerSession = readerBrowser;
        await ShellSignIn(readerBrowser, business, readerPhone, A); await gateway.CompleteLogin(readerBrowser);
        Assert.Equal([customer], (await ShellList(readerBrowser, address)).Items.Select(item => item.Id));
        var hiddenKey = Guid.CreateVersion7();
        using (var hidden = await ShellInvoke(readerBrowser, address, CreateId, CustomerInput(hiddenKey, "越权客户"), hiddenKey))
            await ErrorDetail(hidden, HttpStatusCode.NotFound, "NOT_FOUND");
        await ShellSignIn(readerBrowser, business, readerPhone, B); await gateway.CompleteLogin(readerBrowser);
        Assert.Empty((await ShellList(readerBrowser, address)).Items);
        var (outsiderBrowser, _) = GatewayTestHost.Browser(tls); using var outsiderSession = outsiderBrowser;
        await ShellSignIn(outsiderBrowser, business, outsiderPhone, B); await gateway.CompleteLogin(outsiderBrowser);
        Assert.Empty((await ShellList(outsiderBrowser, address)).Items);
        using (var foreign = await ShellInvoke(outsiderBrowser, address, GetId, CustomerKey(customer)))
            await ErrorDetail(foreign, HttpStatusCode.NotFound, "NOT_FOUND");
        var session = JsonNode.Parse(await SessionSnapshot(ownerBrowser, address))!;
        var gatewayKey = await GatewaySigningKey(gateway, business.ManagementSigningKey);
        var snapshot = new Dictionary<string, object?> { ["membership_id"] = (string?)session["membershipId"], ["identity_revision"] = (long)session["identityRevision"]! };
        using var direct = new HttpClient { BaseAddress = platform.Address };
        var plainKey = Guid.CreateVersion7(); Guid plainCustomer;
        using (var plain = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke", PlatformS2s(gatewayKey, A, ownerId, claims: snapshot),
            CustomerInput(plainKey, "手工令牌客户"), plainKey), Ct))
        {
            var body = await plain.Content.ReadAsStringAsync(Ct);
            Assert.True(plain.StatusCode == HttpStatusCode.OK, body);
            plainCustomer = JsonSerializer.Deserialize<CustomerItem>(body, JsonSerializerOptions.Web)!.Id;
        }
        var delegatedKey = Guid.CreateVersion7();
        using (var delegated = await direct.SendAsync(Bearer(HttpMethod.Post, "/api/capabilities/" + CreateId + "/invoke", PlatformS2s(gatewayKey, A, ownerId, claims: snapshot,
            edit: payload => payload["act"] = new { sub = AgentId, principal_kind = "agent" }), CustomerInput(delegatedKey, "委托客户"), delegatedKey), Ct))
            await ErrorDetail(delegated, HttpStatusCode.NotFound, "NOT_FOUND");

        // 落库与审计（读本宿主自己的插件树）：甲方只有所有者的两次新建，客户建档事件的 actor 是所有者本人；参考应用的命令审计按链序是新建、取回、新建，
        // 行事方都是所有者本人（user、person_id），通道 api；命令 PEP 的放行判定按链序逐条核对，全按本人记，没有拒绝判定。
        // 乙方没有写入，只有两位乙方成员那两次列表的放行（乙方用户取甲方客户那次已过 PEP，执行失败、整个命令事务连同放行记录一起回滚）。
        var (ownerTrail, ownerDecisions, ownerEvents) = await PlatformTrail(platform, A, customers: 2);
        Assert.Equal(2, ownerEvents.Length);
        Assert.All(ownerEvents, customerEvent => Assert.Equal((CrmEvents.CustomerCreated, "user", ownerId), (customerEvent.GetProperty("type").GetString(),
            customerEvent.GetProperty("actor").GetProperty("kind").GetString(), customerEvent.GetProperty("actor").GetProperty("id").GetString())));
        var commands = ownerTrail.Where(record => record.CapabilityId.StartsWith("qian.sample.crm/", StringComparison.Ordinal)).ToArray();
        Assert.Equal([CreateId, GetId, CreateId], commands.Select(command => command.CapabilityId));
        Assert.All(commands, command => Assert.Equal(("user", ownerId, (string?)null, "api", "success"),
            (command.ActorType, command.ActorId, command.OnBehalfOf, command.Channel, command.Result)));
        Assert.Equal([Allowed(ownerId, ListId), Allowed(ownerId, CreateId), Allowed(ownerId, GetId), Allowed(ownerId, ListId), Allowed(readerId, ListId), Allowed(ownerId, CreateId)],
            ownerDecisions);
        var (otherTrail, otherDecisions, otherEvents) = await PlatformTrail(platform, B, customers: 0);
        Assert.Empty(otherEvents);
        Assert.DoesNotContain(otherTrail, record => record.CapabilityId.StartsWith("qian.sample.crm/", StringComparison.Ordinal));
        Assert.Equal([Allowed(readerId, ListId), Allowed(outsiderId, ListId)], otherDecisions);
        Assert.Contains(plainCustomer, (await ShellList(ownerBrowser, address)).Items.Select(item => item.Id));
        seen.Clear();

        // 五、外壳 bootstrap 的成员契约：成员甲在甲方、乙方都是 tenant.user；乙方的 scope 禁用参考应用（未挂载）。
        var (person, phone) = await NewPerson(business, "成员甲");
        await Join(business, A, person.Id, "成员甲"); await Join(business, B, person.Id, "成员甲");
        var subject = person.Id.ToString("D");
        foreach (var id in new[] { A, B })
            using (tenant.BeginScope(id)) await grants.AssignAsync("tenant.user", "user", subject, "acceptance", Ct);
        using (platformTenant.BeginScope(B))
            await new PostgresPatchRepository(platform.Services.GetRequiredService<IDataService>(), platformTenant, new SurfaceValidator())
                .PutAsync(new ScopeKey("tenant", B), "crm", JsonSerializer.SerializeToElement(new { id = "crm", disabled = true }), 0, "acceptance", Ct);
        var (browser, cookies) = GatewayTestHost.Browser(tls); using var memberSession = browser;
        await ShellSignIn(browser, business, phone, A); await gateway.CompleteLogin(browser);
        var first = await Bootstrap(browser, address, cookies);
        Assert.Equal(["/api/authz/me/permissions", "/api/capabilities/navigation", "/api/capabilities/shell"], seen.Order(StringComparer.Ordinal));
        var firstPermissions = await AssertConsistent(browser, address, first, A, "成员甲");
        var remote = Assert.Single(first["remotes"]!.AsArray())!;
        Assert.Equal(("qian.sample.crm", "1.0.0", "global"), ((string?)remote["id"], (string?)remote["version"], (string?)remote["scope"]));
        Assert.Equal("/remotes/qian.sample.crm/mf-manifest.json", (string?)remote["manifest"]!["federation"]!["manifest"]);
        Assert.Equal(["global|qian.sample.crm"], first["mounted"]!.AsArray().Select(item => (string)item!));
        Assert.Contains("qian.sample.crm:customer:read", firstPermissions); Assert.DoesNotContain("qian.sample.crm:customer:write", firstPermissions);
        Assert.Equal(["/customers"], first["navigation"]!.AsArray().Select(item => (string)item!["route"]!));

        // 授权变更（身份侧在甲方加授 tenant.owner）：旧令牌在 bootstrap 与三条平台表面都是 401（不做权限判定），刷新会话换到新修订后恢复，权限与菜单随之更新。
        var revision = (long)first["session"]!["identityRevision"]!;
        using (tenant.BeginScope(A)) await grants.AssignAsync("tenant.owner", "user", subject, "acceptance", Ct);
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

        // 切换到乙方：会话、租户、远程与权限都随之变化；未挂载的参考应用不出现，其远程清单地址也不在响应里。
        await ShellSignIn(browser, business, phone, B); await gateway.CompleteLogin(browser);
        var (switched, text) = await BootstrapText(browser, address, cookies);
        var otherPermissions = await AssertConsistent(browser, address, switched, B, "成员甲");
        foreach (var name in new[] { "remotes", "mounted", "entitled" }) Assert.Empty(switched[name]!.AsArray());
        Assert.DoesNotContain("mf-manifest.json", text, StringComparison.Ordinal); Assert.DoesNotContain("/remotes/", text, StringComparison.Ordinal);
        Assert.Empty(switched["navigation"]!.AsArray());
        Assert.NotEqual(ownerPermissions, otherPermissions);
        Assert.DoesNotContain("qian.sample.crm:customer:write", otherPermissions);

        static (string, string, string, string, string) Allowed(string actor, string target) => (actor, "allowed", AuthzEnforcement.CommandEntryPoint, target, "role_grant");
    }
}

public abstract partial class CustomerCommandTests
{
    // 网关签给本宿主的 S2S 同形令牌（RS256、网关 issuer、单一受众、60 秒）；edit 在签名前改载荷，用来构造不合格的令牌。
    private protected static string PlatformS2s(SecurityKey key, string? tenant, string subject, string kind = "user", string audience = PlatformHostSettings.PlatformAudience,
        string issuer = ShellGatewayIssuer, string algorithm = SecurityAlgorithms.RsaSha256, Action<Dictionary<string, object?>>? edit = null, IReadOnlyDictionary<string, object?>? claims = null)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
        var payload = new Dictionary<string, object?>
        {
            ["iss"] = issuer, ["aud"] = new[] { audience }, ["sub"] = subject, ["principal_kind"] = kind, ["tenant_id"] = tenant, ["roles"] = Array.Empty<string>(),
            ["scope"] = "crm.read crm.write", ["session_id"] = id, ["jti"] = id, ["iat"] = now, ["nbf"] = now, ["exp"] = now + 60
        };
        foreach (var (name, value) in claims ?? new Dictionary<string, object?>()) payload[name] = value;
        edit?.Invoke(payload);
        return new JsonWebTokenHandler().CreateToken(JsonSerializer.Serialize(payload), new SigningCredentials(key, algorithm));
    }

    // 同一载荷的不签名形式（alg none，签名段为空）。
    private protected static string Unsigned(string token)
    {
        var payload = Encoding.UTF8.GetString(Base64UrlEncoder.DecodeBytes(token.Split('.')[1]));
        return new JsonWebTokenHandler().CreateToken(payload);
    }

    // 保留头与签名、只改载荷里的一个字段。
    private protected static string Tampered(string token, string name, string value)
    {
        var parts = token.Split('.');
        var payload = JsonNode.Parse(Base64UrlEncoder.DecodeBytes(parts[1]))!; payload[name] = value;
        return parts[0] + "." + Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload.ToJsonString())) + "." + parts[2];
    }

    private protected static async Task<HttpResponseMessage> PlatformGet(HttpClient client, string path, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request, Ct);
    }

    private protected static async Task<string> PlatformText(HttpClient client, string path, string token)
    {
        using var response = await PlatformGet(client, path, token);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, path + " " + body);
        return body;
    }

    private static readonly string[] PlatformSurfaceRoutes = ["/api/capabilities/{**rest}", "/api/authz/{**rest}"];

    // 网关宿主交付的路由表与簇（hosts/Qian.Gateway.Host/appsettings.json），簇地址换成本宿主的回环地址。
    private protected static (GatewayRoute[] Routes, GatewayCluster[] Clusters) ProductionRoutes(Uri platform)
    {
        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(RepositoryRoot(), "hosts", "Qian.Gateway.Host", "appsettings.json"), optional: false, reloadOnChange: false).Build();
        var routes = configuration.GetSection("Gateway:Routes").GetChildren().Select(item => new GatewayRoute(item["Id"] ?? "", item["Path"] ?? "", item["Cluster"] ?? "")
            { RateLimiterPolicy = item["RateLimiterPolicy"] ?? "" }).ToArray();
        var clusters = configuration.GetSection("Gateway:Clusters").GetChildren().ToArray();
        Assert.Equal([PlatformHostSettings.OperationsAudience, PlatformHostSettings.PlatformAudience], clusters.Select(cluster => cluster["Id"]!).Order(StringComparer.Ordinal));
        Assert.Single(clusters.Select(cluster => cluster["Address"]).Distinct(StringComparer.Ordinal));
        Assert.Equal(PlatformHostSettings.OperationsAudience, routes.Single(route => route.Path == "/api/tenancy/{**rest}").Cluster);
        foreach (var path in PlatformSurfaceRoutes) Assert.Equal(PlatformHostSettings.PlatformAudience, routes.Single(route => route.Path == path).Cluster);
        return (routes, [.. clusters.Select(cluster => new GatewayCluster(cluster["Id"]!, platform.AbsoluteUri))]);
    }

    // 网关签 S2S 的那把钥（与它公开的 JWKS 同一 kid），用来照网关形状直调本宿主。
    private protected static async Task<RsaSecurityKey> GatewaySigningKey(GatewayTestHost gateway, RsaSecurityKey signing)
    {
        using var client = new HttpClient();
        var keys = JsonNode.Parse(await client.GetStringAsync(new Uri(gateway.HttpAddress, ".well-known/jwks.json"), Ct))!["keys"]!.AsArray();
        return new RsaSecurityKey(signing.Rsa) { KeyId = (string)Assert.Single(keys)!["kid"]! };
    }

    // 本宿主插件树里某租户的审计链（全部记录与决策日志）与参考应用的 Outbox 事件信封；顺带核对客户行数与整条链的哈希。
    private protected static async Task<(AuditRecord[] Records, (string Actor, string Result, string EntryPoint, string Target, string Reason)[] Decisions, JsonElement[] Events)> PlatformTrail(
        PlatformHostRun platform, string tenantId, int customers)
    {
        using (platform.Services.GetRequiredService<ITenantContext>().BeginScope(tenantId))
        {
            List<string> events;
            await using (var session = await platform.Services.GetRequiredService<IDataService>().OpenAsync<CrmDbContext>(Ct))
            {
                Assert.Equal(customers, await session.Context.Customers.CountAsync(Ct));
                events = await session.Context.OutboxMessages.Where(row => row.TenantId == tenantId).Select(row => row.Payload).ToListAsync(Ct);
            }
            var trail = platform.Runtime.Global("audit").Scoped(new ScopeKey("tenant", tenantId)).Audit();
            var records = (await trail.ReadAsync(0, 200, cancellationToken: Ct)).Select(entry => entry.Record).ToArray();
            Assert.True((await trail.VerifyAsync(Ct)).Valid);
            return (records, [.. records.Where(record => record.CapabilityId == AuthzEnforcement.DecisionCapabilityId).Select(record => (record.ActorId, record.Result,
                record.Payload.GetProperty("entryPoint").GetString()!, record.Payload.GetProperty("target").GetString()!, record.Payload.GetProperty("reason").GetString()!))],
                [.. events.Select(text => JsonSerializer.Deserialize<JsonElement>(text))]);
        }
    }

    // 网关只公开 JWKS；不经网关的用例用同一把签名钥起一个只发 JWKS 的替身。
    private protected static async Task<WebApplication> JwksServer(RsaSecurityKey signing)
    {
        var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        var app = builder.Build();
        using var publicKey = RSA.Create(); publicKey.ImportParameters(signing.Rsa.ExportParameters(false));
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicKey) { KeyId = signing.KeyId });
        var document = JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, use = "sig", alg = SecurityAlgorithms.RsaSha256, n = jwk.N, e = jwk.E } } });
        app.MapGet("/.well-known/jwks.json", () => Results.Text(document, "application/json"));
        await app.StartAsync(Ct);
        return app;
    }

    // 起生产平台宿主：受限登录（应用登录、平台授权与平台审计登录、审计函数属主、迁移属主）各建一个，连接串写成 0600 的 Secret 文件；
    // 环境 overlay 给两个平台登录的角色名、events 不接 broker（Outbox 用 skip-locked 领取）；参考应用以 module 应用的组件文件夹与扩展 overlay 加进全局 scope。
    // 没有包内迁移的 Registry 由管理员建表，Capabilities / Lifecycle 经宿主的 IDataMigrator 以迁移属主执行（与 --migrate-only 同一入口），之后才授权、启动。
    // extend 在参考应用之后再加扩展登记（用例自己的扩展端点等）。
    private protected async Task<PlatformHostRun> StartPlatformHost(RunningApp business, Uri jwks, LoopbackPort.Reservation? port = null, ConcurrentQueue<string>? seen = null,
        Action<PlatformHostComposition>? extend = null)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        string app = "qian_plat_app_" + suffix, writer = "qian_plat_fn_" + suffix, authz = "qian_plat_az_" + suffix, audit = "qian_plat_au_" + suffix, migrator = "qian_plat_mig_" + suffix;
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        const string Restricted = "NOINHERIT NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE NOREPLICATION";
        await Admin($"""
            CREATE ROLE "{app}" LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE "{authz}" LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE "{audit}" LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE "{migrator}" LOGIN {Restricted} PASSWORD '{password}';
            CREATE ROLE "{writer}" NOLOGIN {Restricted};
            GRANT CONNECT, CREATE ON DATABASE {PostgresFixture.DatabaseName} TO "{migrator}";
            """);
        var connections = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["App"] = Login(app), ["PlatformAuthz"] = Login(authz), ["PlatformAudit"] = Login(audit), ["Migrations"] = Login(migrator)
        };
        var directory = Directory.CreateTempSubdirectory("qian-platform-host-");
        try
        {
            var settings = new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Root"] = RepositoryRoot(), ["S2S:Issuer"] = ShellGatewayIssuer, ["S2S:JwksUri"] = jwks.AbsoluteUri, ["S2S:AllowHttpJwks"] = "true",
                ["Secrets:MountRoot"] = directory.FullName
            };
            foreach (var (name, connection) in connections)
            {
                var file = Path.Combine(directory.FullName, name.ToLowerInvariant() + "-connection");
                await GatewayTestHost.WritePrivate(file, connection);
                settings["ConnectionFiles:" + name] = file;
            }
            var environment = Path.Combine(directory.FullName, "environment.yml");
            await File.WriteAllTextAsync(environment, JsonSerializer.Serialize(new object[]
            {
                new { id = "authz", config = new { advancedMode = false, platformRole = authz } },
                new { id = "audit", config = new { chain = new { store = "pg", platformRole = audit } } },
                new { id = "events", config = new { } }
            }), Ct);
            var components = Path.Combine(directory.FullName, "components");
            CrmComponent(Path.Combine(components, "crm"));
            var modules = Path.Combine(directory.FullName, "modules.yml");
            await File.WriteAllTextAsync(modules, """[{"id":"crm","plugin":"qian.sample.crm","config":{}}]""", Ct);
            var host = PlatformHost.Build(["--patch", environment], composition =>
            {
                composition
                    .AddComponentDirectory(components).AddPatch(modules)
                    .AddComponent(new PlatformComponent("qian.sample.crm", "Qian.Sample.Crm.CrmPlugin, Qian.Sample.Crm", _ => new CrmPlugin(),
                        services => services.AddAppData<CrmDbContext>(CrmDbContext.Schema)))
                    .AddEvents(registrar => registrar.RegisterCrmEvents())
                    .AddMediator(mediator => mediator
                        .AddQianCapability<CreateCustomerCommand, CustomerItem, CreateCustomerHandler, CrmDbContext>(Crm)
                        .AddQianCapability<ListCustomersQuery, CustomerPage, ListCustomersHandler, CrmDbContext>(Crm)
                        .AddQianCapability<GetCustomerQuery, CustomerItem, GetCustomerHandler, CrmDbContext>(Crm)
                        .AddQianCapability<PurgeCustomersCommand, CustomerPurgeResult, PurgeCustomersHandler, CrmDbContext>(Crm))
                    .AddLifecycleHooks((id, _) => id == "qian.sample.crm" ? new CrmPlugin() : null);
                extend?.Invoke(composition);
            }, builder =>
            {
                builder.WebHost.UseUrls(port is null ? "http://127.0.0.1:0" : $"http://127.0.0.1:{port.Port}"); builder.Logging.ClearProviders();
                if (seen is not null) builder.Services.AddSingleton<IStartupFilter>(new RequestLog(seen));
                builder.Configuration.AddInMemoryCollection(settings);
            });
            try
            {
                using (var scope = host.Services.CreateScope())
                    await Admin("DROP SCHEMA IF EXISTS plugin_qian_registry CASCADE; DROP SCHEMA IF EXISTS plugin_qian_capabilities CASCADE; DROP SCHEMA IF EXISTS plugin_qian_lifecycle CASCADE;" +
                        scope.ServiceProvider.GetRequiredService<RegistryDbContext>().Database.GenerateCreateScript());
                var reports = await host.Services.GetRequiredService<IDataMigrator>().MigrateAsync(Ct);
                foreach (var context in new[] { typeof(CapabilitiesDbContext), typeof(LifecycleDbContext) })
                    Assert.NotEmpty(Assert.Single(reports, report => report.ContextType == context).Applied);
                const string Schemas = "plugin_qian_tenancy, plugin_qian_authz, plugin_qian_audit, plugin_qian_registry, plugin_qian_capabilities, plugin_qian_lifecycle, plugin_qian_sample_crm";
                await Admin($"GRANT USAGE ON SCHEMA {Schemas} TO \"{app}\"; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA {Schemas} TO \"{app}\";");
                await Admin("BEGIN;" + AuditTransactionAccess.CreateMigrationSql(writer, app) + AuthzPlatformAccess.CreateMigrationSql(authz) +
                    AuditPlatformAccess.CreateMigrationSql(audit, writer, app) + "COMMIT;");
                port?.Dispose();
                await host.StartAsync(Ct);
                return new PlatformHostRun(host, directory, [.. connections.Values], app, authz, audit);
            }
            catch { await host.DisposeAsync(); throw; }
        }
        catch
        {
            ClearPools(connections.Values); directory.Delete(true);
            throw;
        }

        string Login(string role) => new NpgsqlConnectionStringBuilder(Postgres.AppConnectionString) { Username = role, Password = password }.ConnectionString;
        static IPluginContext Crm(IServiceProvider services) => services.GetRequiredService<PlatformRuntime>().Tenant("crm");
    }

    private static void ClearPools(IEnumerable<string> connections)
    {
        foreach (var connection in connections) { using var pool = new NpgsqlConnection(connection); NpgsqlConnection.ClearPool(pool); }
    }

    private static readonly string[] CrmComponentFiles = ["metadata/permissions.json", "metadata/roles.json", "schemas/config.schema.json"];

    // 参考应用的 module 组件文件夹：清单、metadata、工具与配置 Schema 取自 samples/Qian.Sample.Crm（代码派生的那份），
    // UI 清单取自远程模块 samples/qian-sample-crm-ui（与外壳联调环境托管的同一份），清单加上 surfaces.ui。
    private static void CrmComponent(string folder)
    {
        var source = Path.Combine(RepositoryRoot(), "samples", "Qian.Sample.Crm");
        var files = CrmComponentFiles
            .Concat(Directory.EnumerateFiles(Path.Combine(source, "tools"), "*.json").Select(file => "tools/" + Path.GetFileName(file)));
        foreach (var relative in files) Copy(Path.Combine(source, relative), relative);
        Copy(Path.Combine(RepositoryRoot(), "samples", "qian-sample-crm-ui", "ui", "manifest.json"), "ui/manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(source, ".qian-plugin", "plugin.json")))!;
        manifest["surfaces"]!["ui"] = "./ui/manifest.json";
        Directory.CreateDirectory(Path.Combine(folder, ".qian-plugin"));
        File.WriteAllText(Path.Combine(folder, ".qian-plugin", "plugin.json"), manifest.ToJsonString());

        void Copy(string from, string relative)
        {
            var target = Path.Combine(folder, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!); File.Copy(from, target);
        }
    }

    private protected sealed class PlatformHostRun(WebApplication app, DirectoryInfo directory, IReadOnlyList<string> connections, string appRole, string authzRole, string auditRole)
        : IAsyncDisposable
    {
        internal IServiceProvider Services => app.Services;
        internal PlatformRuntime Runtime => app.Services.GetRequiredService<PlatformRuntime>();
        internal Uri Address => new(app.Urls.Single());
        internal string AppRole => appRole;
        internal string AuthzRole => authzRole;
        internal string AuditRole => auditRole;

        public async ValueTask DisposeAsync()
        {
            await app.StopAsync(Ct); await app.DisposeAsync();
            ClearPools(connections); directory.Delete(true);
        }
    }
}
