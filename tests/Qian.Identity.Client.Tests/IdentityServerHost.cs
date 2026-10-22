using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenIddict.Abstractions;
using Qian.Data;
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Identity.Server;
using Qian.Runtime;
using Qian.Tenancy;
using Qian.Testing;
using Qian.Testing.Containers;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Identity.Client.Tests;

/// <summary>
/// 真实的 qian-identity：内置账号模型（PostgreSQL 受限应用角色 + 强制 RLS）、OpenIddict 协议面（强制 PKCE S256、openid 必带 nonce、
/// 授权响应带 iss）与账号 HTTP 接口。登记一个机密 RP 客户端，回调指向测试里的 Client 宿主；租户目录是可控替身，其余都是生产代码。
/// 该客户端登记了两个资源，RP 只要 <see cref="Resource"/>：多资源客户端不带 resource 会被拒，访问令牌的受众因此能证明 RP 带了 resource。
/// </summary>
internal sealed class IdentityServerHost : IAsyncDisposable
{
    internal const string Resource = "https://api.qian.test";
    internal const string OtherResource = "https://files.qian.test";
    internal const string ClientId = "qian-web";
    internal const string Password = "Correct-Horse-9";
    internal const string Phone = "13900000001";
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly WebApplication _app;
    private readonly PluginLoader _loader;

    private IdentityServerHost(WebApplication app, PluginLoader loader, string issuer, string secret, X509Certificate2 certificate)
    {
        _app = app; _loader = loader; Issuer = issuer; Secret = secret; Certificate = certificate;
    }

    internal string Issuer { get; }
    /// <summary>RP 的客户端密钥；测试把它写进受控的 Secret 投射目录，Client 只经 secretRef 读取。</summary>
    internal string Secret { get; }
    /// <summary>发行方签名证书（含私钥），用来构造「签名有效但 nonce 被篡改」的 id_token。</summary>
    internal X509Certificate2 Certificate { get; }
    internal Guid Person { get; private set; }
    internal Guid Tenant { get; private set; }
    internal Guid Membership { get; private set; }

    // backchannel 给出时登记 RP 的后通道登出地址（与回调同源的 /auth/backchannel-logout），身份服务的投递客户端只信这张 RP 证书。
    // 发行方带端口，构建宿主前就要确定：端口在建表、挂载期间被占时换端口从头启动（S1-QA-016，同 S1-QA-014）。
    internal static Task<IdentityServerHost> Start(PostgresFixture postgres, Uri callback, X509Certificate2? backchannel = null) =>
        LoopbackPort.StartAsync(port => StartOn(port, postgres, callback, backchannel));

    private static async Task<IdentityServerHost> StartOn(int port, PostgresFixture postgres, Uri callback, X509Certificate2? backchannel)
    {
        var issuer = $"http://127.0.0.1:{port}/";
        using var rsa = RSA.Create(2048);
        var certificate = new CertificateRequest("CN=Qian identity client tests", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls(issuer); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus(events => events.RegisterIdentityEvents());
        builder.Services.AddKunMediator(mediator => mediator.AddQianIdentityEvents());
        builder.Services.AddQianEvents();
        builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(IdentityDbContext.Schema));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("qian.identity.signin").AddCookie("qian.identity.signin");
        builder.Services.AddHttpClient();
        var web = new OpenIddictApplicationDescriptor { ClientId = ClientId, ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
        web.RedirectUris.Add(callback);
        web.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api"]);
        builder.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = new Uri(issuer), AllowLoopbackHttp = true, SigningCertificates = [certificate], EncryptionCertificates = [certificate],
            Clients = [new() { Application = web, Resources = [Resource, OtherResource], BackchannelLogoutUri = backchannel is null ? null : new Uri(callback, "/auth/backchannel-logout") }]
        }, builder.Environment);
        // 身份服务登记的投递客户端之后再覆盖主处理器（后登记者生效）：只信测试 RP 的自签名证书，其余与生产一致。
        if (backchannel is not null)
            builder.Services.AddHttpClient("Qian.Identity.Backchannel").ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false, UseCookies = false,
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, certificate, _, _) => certificate?.GetCertHashString() == backchannel.GetCertHashString() }
            });
        var app = builder.Build(); app.UseQianIdentityServer(); app.UseAuthentication(); app.MapQianIdentityServer(); app.MapQianIdentityAccounts();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var sql = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript();
            await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_identity CASCADE;" + sql +
                "GRANT USAGE ON SCHEMA plugin_qian_identity TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_identity TO qian_app;", db);
            await command.ExecuteNonQueryAsync(Ct);
        }
        var tenancy = new FakeTenancy();
        var loader = new PluginLoader(app.Services);
        await loader.MountAsync([
            new(new PluginEntry("data", "qian.data", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new DataPlugin(), [DataDefinition.Key]),
            new(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), tenancy, [TenancyDefinition.Key]),
            new(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { issuer })), new IdentityServerPlugin(),
                [IdentityDefinition.Key, IdentityDefinition.AccountsKey, IdentityDefinition.SignInKey, IdentityDefinition.FactorsKey])], Ct);
        Assert.True(loader.GetState("identity", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("identity", ScopeKey.Global)?.ToString());
        // 启动失败（含端口被占）先释放这次建的宿主，LoopbackPort 才能换端口重来。
        try { await app.StartAsync(Ct); }
        catch { await loader.DisposeAsync(); await app.DisposeAsync(); certificate.Dispose(); throw; }
        var host = new IdentityServerHost(app, loader, issuer, secret, certificate);
        // 一个租户、一个带已验证手机号与初始密码的 Person、它在该租户的成员身份：登录走生产的账号接口与统一登录流水线。
        var accounts = loader.GetContext("identity", ScopeKey.Global).IdentityAccounts();
        host.Tenant = tenancy.Add("acme");
        using (var proof = new AuthenticationProof(Encoding.UTF8.GetBytes(Password)))
            host.Person = (await accounts.CreatePersonAsync(new CreatePerson("张三", [new(IdentifierKind.Phone, Phone, true)]) { InitialPassword = proof, RequirePasswordChange = false }, Ct)).Id;
        using (app.Services.GetRequiredService<ITenantContext>().BeginScope(host.Tenant.ToString("D")))
            host.Membership = (await accounts.CreateMembershipAsync(new CreateMembership(host.Person, "张三"), Ct)).Id;
        return host;
    }

    /// <summary>在发行方的账号接口完成密码登录（先取防伪令牌），浏览器因此持有 qian-identity 的登录 Cookie。</summary>
    internal async Task SignInAsync(HttpClient browser)
    {
        using var csrf = await browser.GetAsync(new Uri(new Uri(Issuer), "api/identity/csrf"), Ct);
        csrf.EnsureSuccessStatusCode();
        var token = await csrf.Content.ReadFromJsonAsync<JsonElement>(Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(Issuer), "api/identity/login"))
            { Content = JsonContent.Create(new { client_id = ClientId, identifier = Phone, password = Password, tenant_id = Tenant }) };
        request.Headers.Add(token.GetProperty("header").GetString()!, token.GetProperty("token").GetString());
        using var response = await browser.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.Equal("authenticated", JsonDocument.Parse(body).RootElement.GetProperty("status").GetString());
    }

    /// <summary>运营停用该 Person：身份服务吊销其全部会话，并向持有授权的 RP 投递后通道 logout_token。</summary>
    internal async Task DisablePersonAsync() =>
        await _loader.GetContext("identity", ScopeKey.Global).IdentityAccounts().SetPersonStatusAsync(Person, AccountStatuses.Disabled, "test", Ct);

    public async ValueTask DisposeAsync()
    {
        await _loader.DisposeAsync(); await _app.DisposeAsync(); Certificate.Dispose();
    }

    // 可控的租户目录与配额：只有测试登记的租户，配额不设上限。
    private sealed class FakeTenancy : IQianPlugin, ITenantDirectory, ITenantQuota
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, TenantDescriptor> _tenants = new();
        public IReadOnlyList<ServiceKey> Inject => [];

        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<ITenantDirectory>(TenancyDefinition.Key, this);
            ctx.Provide<ITenantQuota>(TenantSettings.QuotaKey, this);
            return ValueTask.CompletedTask;
        }

        internal Guid Add(string code)
        {
            var descriptor = new TenantDescriptor(Guid.CreateVersion7(), code, code.ToUpperInvariant(), TenantIsolation.Row, TenantStatus.Active);
            _tenants[descriptor.Id] = descriptor;
            return descriptor.Id;
        }

        public ValueTask<TenantDescriptor?> GetAsync(string code, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_tenants.Values.FirstOrDefault(item => item.Code == code));

        public ValueTask<TenantDescriptor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_tenants.TryGetValue(id, out var item) ? item : null);

        public ValueTask EnsureCanAddUserAsync(int currentUsers, TenantMemberKind kind, CancellationToken cancellationToken = default) => ValueTask.CompletedTask;
    }
}
