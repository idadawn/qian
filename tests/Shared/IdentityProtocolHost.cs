using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using OpenIddict.Abstractions;
using Qian.Data;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Testing.Containers;
using static OpenIddict.Abstractions.OpenIddictConstants;
using IdentitySession = Qian.Identity.Abstractions.Session;

using Qian.Identity.Server;

namespace Qian.Testing.Identity;

internal static class IdentityProtocolHost
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    // 新地址的端口要在构建宿主前确定：建表、挂载期间被占时换端口从头启动（S1-QA-014）。沿用已停主节点的地址时端口是给定的，不换。
    internal static Task<Running> Start(PostgresFixture postgres, Running? primary = null, Guid? serviceTenant = null, string serviceClient = "machine", string serviceScope = "qian.api", bool rotateSigningKey = false, bool reuseAddress = false) =>
        reuseAddress ? StartOn(primary!.Issuer, postgres, primary, serviceTenant, serviceClient, serviceScope, rotateSigningKey)
            : LoopbackPort.StartAsync(port => StartOn($"http://127.0.0.1:{port}/", postgres, primary, serviceTenant, serviceClient, serviceScope, rotateSigningKey));

    private static async Task<Running> StartOn(string address, PostgresFixture postgres, Running? primary, Guid? serviceTenant, string serviceClient, string serviceScope, bool rotateSigningKey)
    {
        var issuer = primary?.Issuer ?? address;
        using var rsa = RSA.Create(2048); var cert = new CertificateRequest("CN=Qian integration", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var secret = primary?.Secret ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        X509Certificate2? retired = null;
        if (primary is not null)
        {
            var copy = X509CertificateLoader.LoadPkcs12(primary.Certificate.Export(X509ContentType.Pkcs12), null);
            if (rotateSigningKey) retired = copy;
            else { cert.Dispose(); cert = copy; }
        }
        var logoutTokens = new ConcurrentQueue<string>(); var backchannel = new BackchannelStatus();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls(address); builder.Logging.ClearProviders(); builder.Logging.SetMinimumLevel(LogLevel.Trace);
        var logs = new ProtocolLogs(); builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext()); builder.Services.AddKunEventBus(_ => { });
        builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(IdentityDbContext.Schema));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("qian.identity.signin").AddCookie("qian.identity.signin");
        var web = new OpenIddictApplicationDescriptor { ClientId = "web", ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
        web.RedirectUris.Add(new Uri(issuer + "callback")); web.PostLogoutRedirectUris.Add(new Uri(issuer + "signed-out"));
        web.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession,
            Permissions.Endpoints.Revocation, Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken,
            Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api"]);
        var machine = new OpenIddictApplicationDescriptor { ClientId = serviceClient, ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
        machine.Permissions.UnionWith([Permissions.Endpoints.Token, Permissions.Endpoints.Introspection, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.ClientCredentials, Permissions.Prefixes.Scope + serviceScope]);
        var publicWeb = new OpenIddictApplicationDescriptor { ClientId = "public", ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Implicit };
        publicWeb.RedirectUris.Add(new Uri(issuer + "public-callback")); publicWeb.Permissions.UnionWith(web.Permissions);
        builder.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = new Uri(issuer), AllowLoopbackHttp = true, SigningCertificates = retired is null ? [cert] : [cert, retired], EncryptionCertificates = retired is null ? [cert] : [cert, retired],
            Clients = [new() { Application = publicWeb, Resources = ["https://api.qian.test"] }, new() { Application = web, Resources = ["https://api.qian.test"], BackchannelLogoutUri = new Uri(issuer + "_test/backchannel") }, new() { Application = machine, Resources = ["https://api.qian.test"], TenantId = serviceTenant, PluginId = serviceTenant is null ? null : "qian.sample.crm" }]
        }, builder.Environment);
        var app = builder.Build(); app.UseQianIdentityServer(); app.UseAuthentication(); app.MapQianIdentityServer();
        var person = Guid.CreateVersion7(); var tenant = Guid.CreateVersion7(); var sid = Guid.CreateVersion7();
        // 仅测试程序集的账号夹具：真实 Cookie 认证，协议服务中没有此路由或假登录。
        app.MapPost("/_test/authenticated-account", () => Results.SignIn(new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("sub", person.ToString()), new Claim("tenant_id", tenant.ToString()), new Claim("membership_id", Guid.CreateVersion7().ToString()),
            new Claim("identity_revision", "0"), new Claim("sid", sid.ToString()), new Claim("amr", "pwd"), new Claim("permissions", "must-never-escape")
        }, "test-account")), authenticationScheme: "qian.identity.signin"));
        app.MapPost("/_test/backchannel", async (HttpContext http) =>
        {
            var attempt = Interlocked.Increment(ref backchannel.Attempts);
            if (attempt <= backchannel.Failures) return Results.StatusCode(503);
            var form = await http.Request.ReadFormAsync(http.RequestAborted); logoutTokens.Enqueue(form["logout_token"].ToString());
            return Results.Ok();
        });
        if (primary is null) await using (var scope = app.Services.CreateAsyncScope())
        {
            var sql = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript();
            await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_identity CASCADE;" + sql +
                "GRANT USAGE ON SCHEMA plugin_qian_identity TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_identity TO qian_app;", db);
            await command.ExecuteNonQueryAsync(Ct);
        }
        var loader = new PluginLoader(app.Services);
        await loader.MountAsync([
            new(new PluginEntry("data", "qian.data", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new DataPlugin(), [DataDefinition.Key]),
            new(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { issuer })), new IdentityServerPlugin(new AccountFixture()), [IdentityDefinition.Key])], Ct);
        Assert.True(loader.GetState("identity", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("identity", ScopeKey.Global)?.ToString());
        // 启动失败（含端口被占）先释放这次建的宿主，LoopbackPort 才能换端口重来。
        try { await app.StartAsync(Ct); }
        catch { await loader.DisposeAsync(); await app.DisposeAsync(); cert.Dispose(); retired?.Dispose(); throw; }
        var browser = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri(address) };
        using var login = await browser.PostAsync("_test/authenticated-account", null, Ct); Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return new(app, loader, browser, cert, issuer, secret, person, tenant, sid, logoutTokens, logs, backchannel, retired);
    }

    internal sealed class Running(WebApplication app, PluginLoader loader, HttpClient browser, X509Certificate2 certificate, string issuer, string secret, Guid person, Guid tenant, Guid sessionId, ConcurrentQueue<string> logoutTokens, ProtocolLogs logs, BackchannelStatus backchannel, X509Certificate2? retired) : IAsyncDisposable
    {
        internal PluginLoader Loader => loader;
        internal string Secret => secret;
        internal X509Certificate2 Certificate => certificate;
        internal Guid SessionId => sessionId;
        internal ConcurrentQueue<string> LogoutTokens => logoutTokens;
        internal ProtocolLogs Logs => logs;
        internal BackchannelStatus Backchannel => backchannel;
        internal Task StopAsync() => app.StopAsync(Ct);
        internal HttpClient Browser => browser;
        internal string Issuer => issuer;
        internal string Redirect => issuer + "callback";
        internal Guid Person => person;
        internal Guid Tenant => tenant;
        internal KunOidcClient Kun(string id) => new(new KunOidcClientOptions { Name = id, Authority = issuer, ClientId = id, ClientSecret = id == "public" ? "" : secret, RequireHttpsMetadata = false }, app.Services.GetRequiredService<IHttpClientFactory>());
        internal async Task<(string Code, string Verifier, string Nonce)> Code(string client = "web")
        {
            var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48)); var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
            using var response = await browser.GetAsync(QueryHelpers.AddQueryString("connect/authorize", new Dictionary<string, string?>
            {
                ["client_id"] = client, ["redirect_uri"] = client == "public" ? issuer + "public-callback" : Redirect, ["response_type"] = "code", ["scope"] = "openid offline_access qian.api",
                ["nonce"] = nonce, ["code_challenge_method"] = "S256", ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            }), Ct);
            Assert.True(response.StatusCode == HttpStatusCode.Redirect, await response.Content.ReadAsStringAsync(Ct));
            var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
            Assert.True(query.ContainsKey("code"), response.Headers.Location!.ToString());
            return (query["code"].ToString(), verifier, nonce);
        }
        internal Task<HttpResponseMessage> Grant(string id, Dictionary<string, string> form) => ProtocolPost("connect/token", id, form);
        internal Task<HttpResponseMessage> ProtocolPost(string endpoint, string id, Dictionary<string, string> form)
        {
            form["client_id"] = id; if (id != "public") form["client_secret"] = secret;
            return browser.PostAsync(endpoint, new FormUrlEncodedContent(form), Ct);
        }
        public async ValueTask DisposeAsync() { await loader.DisposeAsync(); await app.DisposeAsync(); browser.Dispose(); certificate.Dispose(); retired?.Dispose(); }
    }
    internal sealed class BackchannelStatus
    {
        internal int Attempts;
        private int _failures;
        internal int Failures { get => Volatile.Read(ref _failures); set => Volatile.Write(ref _failures, value); }
    }

    internal sealed class ProtocolLogs : ILoggerProvider
    {
        internal ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(Messages);
        public void Dispose() { }
        private sealed class Sink(ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }
    private sealed class AccountFixture : IIdentityService
    {
        public Principal? CurrentPrincipal => null;
        public IIdentityConnectorRegistry Connectors => throw new NotSupportedException();
        public Task<Person?> GetPersonAsync(Guid personId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Membership?> GetMembershipAsync(Guid membershipId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<IdentitySession>> ListSessionsAsync(Guid personId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RevokeSessionsAsync(SessionRevocation request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
