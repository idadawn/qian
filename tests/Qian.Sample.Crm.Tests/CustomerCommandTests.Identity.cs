using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Reflection;
using Qian.Plugin.Sdk;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.Identity.Abstractions;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Qian.Runtime;
using Qian.Runtime.Errors;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Sample.Crm.Tests;

public abstract partial class CustomerCommandTests
{
    private protected const string IdentityPassword = "Correct-Horse-9";
    private protected const string IdentityResource = "https://api.qian.test";
    private protected const string GatewayClientId = "qian-web";

    /// <summary>身份侧的测试句柄：发行方、客户端密钥、签名证书，以及挂载后可用的账号端口。</summary>
    private protected sealed class IdentityParts : IDisposable
    {
        private readonly RSA _key;
        internal IdentityParts(string issuer, string secret, X509Certificate2 certificate, RSA key) { Issuer = issuer; Secret = secret; Certificate = certificate; _key = key; }
        internal string Issuer { get; }
        internal string Secret { get; }
        internal X509Certificate2 Certificate { get; }
        internal PluginLoader? Loader { get; set; }
        internal IIdentityAccounts Accounts => Loader!.GetContext("identity", ScopeKey.Global).IdentityAccounts();
        internal ISignInPipeline SignIn => Loader!.GetContext("identity", ScopeKey.Global).IdentitySignIn();
        public void Dispose() { Certificate.Dispose(); _key.Dispose(); }
    }

    // 协议库只在开发环境允许回环 HTTP 发行方；测试宿主本身不改环境，只在注册时声明这一点。
    private protected sealed class DevelopmentEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "qian.sample.crm.tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    // 发行方即宿主的监听地址，端口由 Start 经 LoopbackPort 取得（被占时换端口重建宿主，S1-QA-014）。
    private protected static IdentityParts NewIdentityParts(int port)
    {
        var key = RSA.Create(2048);
        var certificate = new CertificateRequest("CN=Qian crm identity", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        return new IdentityParts($"http://127.0.0.1:{port}/", Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), certificate, key);
    }

    // relyingParty 给出时另登记一个机密客户端 qian-web（回调即该地址，与 web 同一密钥），供网关 BFF 走完整的授权码登录（外壳 bootstrap 验收）。
    private protected void ConfigureIdentity(WebApplicationBuilder builder, IdentityParts parts, Uri? relyingParty = null)
    {
        builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(IdentityDbContext.Schema));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication().AddCookie("qian.identity.signin");
        builder.Services.AddHttpClient();
        var web = new OpenIddictApplicationDescriptor { ClientId = "web", ClientSecret = parts.Secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
        web.RedirectUris.Add(new Uri(parts.Issuer + "callback"));
        web.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api"]);
        // 同机混合部署：身份的账号接口在 /api 之下，而业务宿主对 /api 一律要求已认证的租户。身份请求走自己的分支，不经过业务管线。
        builder.Services.AddSingleton<IStartupFilter>(new IdentityBranchFilter());
        builder.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = new Uri(parts.Issuer), AllowLoopbackHttp = true, SigningCertificates = [parts.Certificate], EncryptionCertificates = [parts.Certificate],
            // 租户状态对账缩到 1 秒，联调测试不必等满默认的 30 秒（生产的上限就是 30 秒）。
            Accounts = new IdentityAccountOptions { Session = new SessionPolicy { ReconcileInterval = TimeSpan.FromSeconds(1) } },
            Clients = [new() { Application = web, Resources = [IdentityResource] }, .. relyingParty is null ? [] : new[] { new IdentityClientRegistration { Application = Gateway(relyingParty), Resources = [IdentityResource] } }]
        }, new DevelopmentEnvironment());

        OpenIddictApplicationDescriptor Gateway(Uri callback)
        {
            var descriptor = new OpenIddictApplicationDescriptor { ClientId = GatewayClientId, ClientSecret = parts.Secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
            descriptor.RedirectUris.Add(callback); descriptor.Permissions.UnionWith(web.Permissions);
            return descriptor;
        }
    }

    // 身份库的建表脚本（与生产同一套迁移）与应用角色授权。
    private protected static string IdentitySchemaSql(IServiceProvider services) =>
        services.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript() +
        "GRANT USAGE ON SCHEMA plugin_qian_identity TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_identity TO qian_app;";

    private protected static void MapIdentity(WebApplication app, IdentityParts parts)
    {
        // 测试里的资源服务：像任何 Qian 业务服务一样，只认已验签的用户令牌，租户只取自令牌，数据由 RLS 隔离。
        async Task<Guid?> TokenTenant(HttpContext http)
        {
            var header = http.Request.Headers.Authorization.ToString();
            if (!header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
            var result = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(header["Bearer ".Length..], new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = parts.Issuer, ValidateAudience = true, ValidAudience = IdentityResource,
                ValidateIssuerSigningKey = true, IssuerSigningKey = new X509SecurityKey(parts.Certificate), ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true
            });
            if (!result.IsValid) return null;
            var principal = new ClaimsPrincipal(result.ClaimsIdentity);
            return principal.FindFirst("principal_kind")?.Value == "user" && Guid.TryParseExact(principal.FindFirst("tenant_id")?.Value, "D", out var tenant) ? tenant : null;
        }
        app.MapGet("/_test/user-customers", async (HttpContext http, IDataService data, ITenantContext tenant, CancellationToken ct) =>
        {
            if (await TokenTenant(http) is not { } tenantId) return Results.Unauthorized();
            using var scope = tenant.BeginScope(tenantId.ToString("D"));
            await using var session = await data.OpenAsync<CrmDbContext>(ct);
            var items = await session.Context.Customers.AsNoTracking().OrderBy(customer => customer.Name).ToListAsync(ct);
            return Results.Ok(new CustomerPage(items.Count, [.. items.Select(customer => new CustomerItem(customer.Id, customer.TenantId!, customer.Name))]));
        });
        app.MapGet("/_test/user-customers/{id:guid}", async (Guid id, HttpContext http, IDataService data, ITenantContext tenant, CancellationToken ct) =>
        {
            if (await TokenTenant(http) is not { } tenantId) return Results.Unauthorized();
            using var scope = tenant.BeginScope(tenantId.ToString("D"));
            await using var session = await data.OpenAsync<CrmDbContext>(ct);
            var customer = await session.Context.Customers.AsNoTracking().SingleOrDefaultAsync(row => row.Id == id, ct);
            return customer is null ? Results.NotFound() : Results.Ok(new CustomerItem(customer.Id, customer.TenantId!, customer.Name));
        });
    }

    private protected static async Task MountIdentity(WebApplication app, IdentityParts parts)
    {
        var loader = app.Services.GetRequiredService<CrmCommandRuntime>().Loader;
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot(), "plugins/qian.identity.server/.qian-plugin/plugin.json")));
        var code = ((IComponentCodeMetadataFactory)Activator.CreateInstance(typeof(IdentityServerComponent).Assembly.GetCustomAttribute<QianCodeMetadataAttribute>()!.FactoryType)!).Create();
        var type = Type.GetType(manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString()!, throwOnError: true)!;
        Assert.Equal(code.RuntimeEntry, manifest.RootElement.GetProperty("runtime").GetProperty("entry").GetString());
        Assert.Equal(code.Provides, manifest.RootElement.GetProperty("provides").EnumerateArray().Select(item => item.GetString()!));
        await loader.MountAsync([new PluginDefinition(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { issuer = parts.Issuer })),
            (IQianPlugin)Activator.CreateInstance(type)!, code.Provides.Select(name => new ServiceKey(name)).ToArray())], Ct);
        Assert.True(loader.GetState("identity", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("identity", ScopeKey.Global)?.ToString());
        foreach (var key in code.Provides) Assert.NotNull(loader.GetContext("identity", ScopeKey.Global).TryInject<object>(new ServiceKey(key)));
        parts.Loader = loader;
    }

    private protected sealed class IdentityBranchFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.MapWhen(http => http.Request.Path.StartsWithSegments("/api/identity") || http.Request.Path.StartsWithSegments("/connect") || http.Request.Path.StartsWithSegments("/.well-known"), branch =>
            {
                branch.UseQianIdentityServer(); branch.UseRouting(); branch.UseAuthentication();
                branch.UseEndpoints(endpoints => { endpoints.MapQianIdentityServer(); endpoints.MapQianIdentityAccounts(); });
            });
            next(app);
        };
    }

    private protected static string NewPhone() => "139" + RandomNumberGenerator.GetInt32(10_000_000, 99_999_999);

    // 建一个带手机号与密码的 Person；成员身份由调用方（或开通编排）另行建立。
    private protected static async Task<(Person Person, string Phone)> NewPerson(RunningApp host, string name, bool platform = false)
    {
        var phone = NewPhone();
        using var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(IdentityPassword));
        var person = await host.Identity.Accounts.CreatePersonAsync(new CreatePerson(name, [new IdentifierInput(IdentifierKind.Phone, phone, true)]) { InitialPassword = proof, RequirePasswordChange = false, PlatformAccess = platform }, Ct);
        return (person, phone);
    }

    // 在租户的作用域里为 Person 建立成员身份（成员身份是租户表，只能在目标租户上下文中创建）。
    private protected static async Task<Membership> Join(RunningApp host, string tenant, Guid person, string name, bool administrator = false)
    {
        using var scope = host.App.Services.GetRequiredService<ITenantContext>().BeginScope(tenant);
        return await host.Identity.Accounts.CreateMembershipAsync(new CreateMembership(person, name) { IsTenantAdministrator = administrator }, Ct);
    }

    // 走完整的 HTTP 路径：防伪令牌 → 登录 → 授权码（PKCE + nonce）→ 已发布的 Kun 客户端换取令牌。
    private protected static async Task<OidcTokenResponse> UserTokens(RunningApp host, string phone)
    {
        var parts = host.Identity;
        using var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new() }) { BaseAddress = new Uri(parts.Issuer) };
        using var csrfResponse = await browser.GetAsync("api/identity/csrf", Ct); csrfResponse.EnsureSuccessStatusCode();
        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>(Ct);
        using var login = new HttpRequestMessage(HttpMethod.Post, "api/identity/login") { Content = JsonContent.Create(new { client_id = "web", identifier = phone, password = IdentityPassword }) };
        login.Headers.Add(csrf.GetProperty("header").GetString()!, csrf.GetProperty("token").GetString()!);
        using var loginResponse = await browser.SendAsync(login, Ct);
        var body = await loginResponse.Content.ReadAsStringAsync(Ct);
        Assert.True(loginResponse.IsSuccessStatusCode && JsonDocument.Parse(body).RootElement.GetProperty("status").GetString() == "authenticated", body);
        var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48)); var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        using var authorize = await browser.GetAsync(QueryHelpers.AddQueryString("connect/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = "web", ["redirect_uri"] = parts.Issuer + "callback", ["response_type"] = "code", ["scope"] = "openid offline_access qian.api", ["nonce"] = nonce,
            ["code_challenge_method"] = "S256", ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
        }), Ct);
        Assert.Equal(HttpStatusCode.Redirect, authorize.StatusCode);
        var code = QueryHelpers.ParseQuery(authorize.Headers.Location!.Query)["code"].ToString();
        using var kun = new KunOidcClient(new KunOidcClientOptions { Name = "web", Authority = parts.Issuer, ClientId = "web", ClientSecret = parts.Secret, RequireHttpsMetadata = false },
            host.App.Services.GetRequiredService<IHttpClientFactory>());
        return await kun.ExchangeCodeAsync(new(code, parts.Issuer + "callback", verifier, nonce), Ct);
    }

    private protected static Task<HttpResponseMessage> AsUser(RunningApp host, string? token, string path)
    {
        var message = new HttpRequestMessage(HttpMethod.Get, path);
        if (token is not null) message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return host.Send(message);
    }

    // 保留签名、只改载荷里的 tenant_id：任何一位的改动都必须使验签失败。
    private protected static string WithForgedTenant(string token, string tenant)
    {
        var parts = token.Split('.');
        var payload = System.Text.Json.Nodes.JsonNode.Parse(Base64UrlEncoder.DecodeBytes(parts[1]))!.AsObject();
        payload["tenant_id"] = tenant;
        return string.Join('.', parts[0], Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(payload.ToJsonString())), parts[2]);
    }
}

// 内置账号模型挂进真实的 CRM 参考宿主：租户目录、配额与开通编排都是宿主里真实的 Tenancy，
// 用来验收需要「身份 × 租户」两边都是真的才能证明的几条（R81 / R95 / R97 与开通编排第 5 步）。
public sealed partial class IdentityAuthzTests(PostgresFixture postgres) : CustomerCommandTests(postgres), IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task 租户管理员经统一登录流水线登录_令牌只带本租户_只看到本租户数据_以A的令牌请求B的记录id是404()
    {
        await using var host = await Start(identity: true);
        Guid[] customersA = [Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()], customersB = [Guid.CreateVersion7(), Guid.CreateVersion7()];
        foreach (var (tenant, ids) in new[] { (A, customersA), (B, customersB) })
            foreach (var id in ids)
                await Admin($"INSERT INTO plugin_qian_sample_crm.customers (id, tenant_id, name, created_at, is_deleted) VALUES ('{id}', '{tenant}', 'customer-{id:N}', now(), false);");
        var (personA, phoneA) = await NewPerson(host, "管理员甲"); var memberA = await Join(host, A, personA.Id, "管理员甲", administrator: true);
        var (personB, phoneB) = await NewPerson(host, "管理员乙"); var memberB = await Join(host, B, personB.Id, "管理员乙", administrator: true);

        var tokensA = await UserTokens(host, phoneA);
        var accessA = new JsonWebToken(tokensA.AccessToken);
        Assert.Equal((A, memberA.Id.ToString("D"), personA.Id.ToString("D"), "user"),
            (accessA.GetClaim("tenant_id").Value, accessA.GetClaim("membership_id").Value, accessA.Subject, accessA.GetClaim("principal_kind").Value));
        Assert.DoesNotContain(accessA.Claims, claim => claim.Type is "permissions" or "permission" or "roles" or "role");
        // 只看到本租户数据。
        using (var list = await AsUser(host, tokensA.AccessToken, "/_test/user-customers"))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var page = (await list.Content.ReadFromJsonAsync<CustomerPage>(Ct))!;
            Assert.Equal(customersA.Order(), page.Items.Select(item => item.Id).Order());
            Assert.All(page.Items, item => Assert.Equal(A, item.TenantId));
        }
        // 请求里写 B 也不起作用：租户只取自已验证的令牌。
        using (var spoofed = await AsUser(host, tokensA.AccessToken, $"/_test/user-customers?tenant_id={B}"))
            Assert.Equal(customersA.Order(), (await spoofed.Content.ReadFromJsonAsync<CustomerPage>(Ct))!.Items.Select(item => item.Id).Order());
        // 以 A 的令牌直接请求 B 的记录 id：404（不是 403，不泄露记录是否存在）；自己的记录正常。
        foreach (var id in customersB)
            using (var cross = await AsUser(host, tokensA.AccessToken, "/_test/user-customers/" + id))
                Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);
        using (var own = await AsUser(host, tokensA.AccessToken, "/_test/user-customers/" + customersA[0]))
            Assert.Equal(customersA[0], (await own.Content.ReadFromJsonAsync<CustomerItem>(Ct))!.Id);

        // 乙方管理员对称。
        var tokensB = await UserTokens(host, phoneB);
        Assert.Equal((B, memberB.Id.ToString("D")), (new JsonWebToken(tokensB.AccessToken).GetClaim("tenant_id").Value, new JsonWebToken(tokensB.AccessToken).GetClaim("membership_id").Value));
        using (var list = await AsUser(host, tokensB.AccessToken, "/_test/user-customers"))
            Assert.Equal(customersB.Order(), (await list.Content.ReadFromJsonAsync<CustomerPage>(Ct))!.Items.Select(item => item.Id).Order());
        foreach (var id in customersA)
            using (var cross = await AsUser(host, tokensB.AccessToken, "/_test/user-customers/" + id))
                Assert.Equal(HttpStatusCode.NotFound, cross.StatusCode);

        // 篡改令牌里的租户（保留签名）、没有令牌：一律 401。
        using (var forged = await AsUser(host, WithForgedTenant(tokensA.AccessToken, B), "/_test/user-customers/" + customersB[0]))
            Assert.Equal(HttpStatusCode.Unauthorized, forged.StatusCode);
        using (var anonymous = await AsUser(host, null, "/_test/user-customers"))
            Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task 用户配额为5时第6个成员创建被真实配额端口拒绝_租户管理员计入_平台超管不计入_并发创建不越过上限()
    {
        await using var host = await Start(identity: true);
        await Admin($"UPDATE plugin_qian_tenancy.tenants SET quotas = '{{\"users\":5}}' WHERE id = '{A}'::uuid;");
        var tenant = host.App.Services.GetRequiredService<ITenantContext>();
        var counter = host.Identity.Loader!.GetContext("identity", ScopeKey.Global).Inject<ITenantUserCounter>(TenantSettings.UserCounterKey);
        async Task<int> Users() { using var scope = tenant.BeginScope(A); return await counter.CountUsersAsync(Ct); }
        async Task<string?> TryJoin(Person person)
        {
            try { await Join(host, A, person.Id, "成员"); return null; }
            catch (QianErrorException failure) { Assert.Equal("5", failure.Error.Args["limit"]); return failure.Error.Code.Code; }
        }

        // 租户管理员计入：1 位管理员 + 3 位成员 = 4。
        await Join(host, A, (await NewPerson(host, "管理员")).Person.Id, "管理员", administrator: true);
        for (var index = 0; index < 3; index++) await Join(host, A, (await NewPerson(host, $"成员{index}")).Person.Id, $"成员{index}");
        Assert.Equal(4, await Users());
        // 4/5 时 6 个并发创建恰好 1 个成功，其余被拒绝：计数与写入在同一把锁里，不会越过上限。
        var candidates = new List<Person>();
        for (var index = 0; index < 6; index++) candidates.Add((await NewPerson(host, $"候选{index}")).Person);
        var results = await Task.WhenAll(candidates.Select(TryJoin));
        Assert.Equal(1, results.Count(code => code is null));
        Assert.Equal(5, results.Count(code => code == "CARDINALITY_USER_LIMIT"));
        Assert.Equal(5, await Users());
        // 第 6 个：真实配额端口给出结构化的 422 CARDINALITY_USER_LIMIT。
        var sixth = (await NewPerson(host, "第六个")).Person;
        var rejected = await Assert.ThrowsAsync<QianErrorException>(() => Join(host, A, sixth.Id, "第六个"));
        Assert.Equal(("CARDINALITY_USER_LIMIT", 422, "5"), (rejected.Error.Code.Code, rejected.Error.Code.Status, rejected.Error.Args["limit"]));
        // 平台超管不计入：满额时仍可加入，计数不变。
        var superadmin = (await NewPerson(host, "平台超管", platform: true)).Person;
        await Join(host, A, superadmin.Id, "平台超管");
        Assert.Equal(5, await Users());
        // 其他租户不受影响（B 没有设置上限）。
        await Join(host, B, sixth.Id, "第六个");
    }
}
