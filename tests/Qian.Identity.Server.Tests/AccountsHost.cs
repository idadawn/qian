using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.EntityFrameworkCore.Extensions;
using Kun.EventBus.DependencyInjection;
using Kun.Identity.Configuration;
using Kun.Identity.Implementations;
using Kun.Mediator.DependencyInjection;
using Kun.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
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
using Qian.Events;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Tenancy;
using Qian.Testing.Containers;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace Qian.Identity.Server.Tests;

// 内置账号模型的真实装配：PostgreSQL（受限应用角色 + 强制 RLS）、OpenIddict 协议面、账号 HTTP 接口、同事务 Outbox。
// 租户目录与配额是可控的替身（真实的 Tenancy 组件由 CRM 示例的集成测试覆盖），其余全部是生产代码。
internal static class AccountsHost
{
    internal const string Resource = "https://api.qian.test";
    internal static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static async Task<Running> Start(PostgresFixture postgres, IdentityAccountOptions? accounts = null, string flavor = "default")
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0); socket.Start(); var port = ((IPEndPoint)socket.LocalEndpoint).Port; socket.Stop();
        var issuer = $"http://127.0.0.1:{port}/";
        using var rsa = RSA.Create(2048);
        var cert = new CertificateRequest("CN=Qian accounts", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseUrls(issuer); builder.Logging.ClearProviders(); builder.Logging.SetMinimumLevel(LogLevel.Warning);
        var logs = new CapturedLogs(); builder.Logging.AddProvider(logs);
        builder.Services.AddSingleton<ITenantContext>(new AsyncLocalTenantContext());
        builder.Services.AddKunEventBus(events => events.RegisterIdentityEvents());
        builder.Services.AddKunMediator(mediator => mediator.AddQianIdentityEvents());
        builder.Services.AddQianEvents();
        builder.Services.AddQianData<IdentityDbContext>((_, options) => options.UseNpgsql(postgres.AppConnectionString).UseKunSchema(IdentityDbContext.Schema));
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("qian.identity.signin").AddCookie("qian.identity.signin");
        builder.Services.AddHttpClient();
        var web = new OpenIddictApplicationDescriptor { ClientId = "web", ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
        web.RedirectUris.Add(new Uri(issuer + "callback")); web.PostLogoutRedirectUris.Add(new Uri(issuer + "signed-out"));
        web.Permissions.UnionWith([Permissions.Endpoints.Authorization, Permissions.Endpoints.Token, Permissions.Endpoints.EndSession, Permissions.Endpoints.Revocation,
            Permissions.GrantTypes.AuthorizationCode, Permissions.GrantTypes.RefreshToken, Permissions.ResponseTypes.Code, Permissions.Prefixes.Scope + "qian.api"]);
        var mobile = new OpenIddictApplicationDescriptor { ClientId = "mobile", ClientType = ClientTypes.Public, ConsentType = ConsentTypes.Implicit };
        mobile.RedirectUris.Add(new Uri(issuer + "mobile-callback")); mobile.Permissions.UnionWith(web.Permissions);
        var machine = new OpenIddictApplicationDescriptor { ClientId = "machine", ClientSecret = secret, ClientType = ClientTypes.Confidential, ConsentType = ConsentTypes.Implicit };
        machine.Permissions.UnionWith([Permissions.Endpoints.Token, Permissions.GrantTypes.ClientCredentials, Permissions.Prefixes.Scope + "qian.api"]);
        builder.Services.AddQianIdentityServer(new IdentityServerOptions
        {
            Issuer = new Uri(issuer), AllowLoopbackHttp = true, SigningCertificates = [cert], EncryptionCertificates = [cert], Accounts = accounts ?? new IdentityAccountOptions(),
            Clients = [new() { Application = web, Resources = [Resource] }, new() { Application = mobile, Resources = [Resource], DeviceType = "mobile" }, new() { Application = machine, Resources = [Resource] }]
        }, builder.Environment);
        var app = builder.Build(); app.UseQianIdentityServer(); app.UseAuthentication(); app.MapQianIdentityServer(); app.MapQianIdentityAccounts();
        await using (var scope = app.Services.CreateAsyncScope())
        {
            var sql = scope.ServiceProvider.GetRequiredService<IdentityDbContext>().GetService<IMigrator>().GenerateScript();
            await using var db = new NpgsqlConnection(postgres.ConnectionString); await db.OpenAsync(Ct);
            await using var command = new NpgsqlCommand("DROP SCHEMA IF EXISTS plugin_qian_identity CASCADE;" + sql +
                "GRANT USAGE ON SCHEMA plugin_qian_identity TO qian_app; GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA plugin_qian_identity TO qian_app;", db);
            await command.ExecuteNonQueryAsync(Ct);
        }
        var tenancy = new FakeTenancy(app.Services.GetRequiredService<ITenantContext>());
        var loader = new PluginLoader(app.Services);
        await loader.MountAsync([
            new(new PluginEntry("data", "qian.data", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), new DataPlugin(), [DataDefinition.Key]),
            new(new PluginEntry("tenancy", "qian.tenancy", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), tenancy, [TenancyDefinition.Key]),
            new(new PluginEntry("identity", "qian.identity.server", "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { issuer })), new IdentityServerPlugin(),
                [IdentityDefinition.Key, IdentityDefinition.AccountsKey, IdentityDefinition.SignInKey, IdentityDefinition.FactorsKey])], Ct);
        Assert.True(loader.GetState("identity", ScopeKey.Global) == PluginState.Ready, loader.GetFailure("identity", ScopeKey.Global)?.ToString());
        await app.StartAsync(Ct);
        var clock = new ManualClock(DateTimeOffset.UtcNow);
        var service = app.Services.GetRequiredService<IdentityAccountGate>().Service!;
        service.Clock = clock;
        return new Running(app, loader, issuer, secret, cert, tenancy, clock, service, postgres, logs);
    }

    internal sealed class Running(WebApplication app, PluginLoader loader, string issuer, string secret, X509Certificate2 certificate, FakeTenancy tenancy, ManualClock clock,
        IdentityAccountService service, PostgresFixture postgres, CapturedLogs logs) : IAsyncDisposable
    {
        private readonly List<HttpClient> _browsers = [];
        internal string Issuer => issuer;
        internal string Secret => secret;
        internal FakeTenancy Tenants => tenancy;
        internal ManualClock Clock => clock;
        internal IdentityAccountService Service => service;
        internal CapturedLogs Logs => logs;
        internal IServiceProvider Services => app.Services;
        internal IIdentityAccounts Accounts => loader.GetContext("identity", ScopeKey.Global).IdentityAccounts();
        internal ISignInPipeline SignIn => loader.GetContext("identity", ScopeKey.Global).IdentitySignIn();
        internal IAuthenticatorFactorRegistry Factors => loader.GetContext("identity", ScopeKey.Global).IdentityFactors();
        internal PluginLoader Loader => loader;
        internal ITenantContext Tenant => app.Services.GetRequiredService<ITenantContext>();
        internal string Redirect => issuer + "callback";
        internal KunOidcClient Kun(string id) => new(new KunOidcClientOptions { Name = id, Authority = issuer, ClientId = id, ClientSecret = id == "web" ? secret : "", RequireHttpsMetadata = false },
            app.Services.GetRequiredService<IHttpClientFactory>());

        /// <summary>带 Cookie 的浏览器；每个实例各有自己的会话。</summary>
        internal Browser NewBrowser()
        {
            var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = true, CookieContainer = new() }) { BaseAddress = new Uri(issuer) };
            _browsers.Add(http);
            return new Browser(http, this);
        }

        /// <summary>以超级用户执行 SQL 并返回第一列文本，用于断言库内事实（绕过 RLS，所以仅用于验证）。</summary>
        internal async Task<string> Text(string sql, params (string Name, object Value)[] args)
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value);
            return Convert.ToString(await command.ExecuteScalarAsync(Ct), System.Globalization.CultureInfo.InvariantCulture) ?? "";
        }

        internal async Task Exec(string sql, params (string Name, object Value)[] args)
        {
            await using var connection = new NpgsqlConnection(postgres.ConnectionString); await connection.OpenAsync(Ct);
            await using var command = new NpgsqlCommand(sql, connection);
            foreach (var (name, value) in args) command.Parameters.AddWithValue(name, value);
            await command.ExecuteNonQueryAsync(Ct);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var browser in _browsers) browser.Dispose();
            await loader.DisposeAsync(); await app.DisposeAsync(); certificate.Dispose();
        }
    }

    /// <summary>模拟用户的浏览器：先取防伪令牌，再以 JSON 调账号接口，并沿用 Cookie 走授权码流程。</summary>
    internal sealed class Browser(HttpClient http, Running host)
    {
        private (string Header, string Token)? _csrf;
        internal HttpClient Http => http;

        internal async Task<(string Header, string Token)> Csrf()
        {
            if (_csrf is { } known) return known;
            using var response = await http.GetAsync("api/identity/csrf", Ct); response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
            return (_csrf = (json.GetProperty("header").GetString()!, json.GetProperty("token").GetString()!)).Value;
        }

        internal async Task<HttpResponseMessage> Post(string path, object body, bool csrf = true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
            if (csrf) { var (header, token) = await Csrf(); request.Headers.Add(header, token); }
            return await http.SendAsync(request, Ct);
        }

        internal async Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw)> Json(HttpMethod method, string path, object? body = null, bool csrf = true)
        {
            using var request = new HttpRequestMessage(method, path);
            if (body is not null) request.Content = JsonContent.Create(body);
            if (csrf && method != HttpMethod.Get) { var (header, token) = await Csrf(); request.Headers.Add(header, token); }
            var response = await http.SendAsync(request, Ct);
            var text = await response.Content.ReadAsStringAsync(Ct);
            return (response.StatusCode, text.Length == 0 ? default : JsonDocument.Parse(text).RootElement.Clone(), response);
        }

        internal Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw)> Login(string identifier, string password, string client = "web", Guid? tenant = null, object? captcha = null) =>
            Json(HttpMethod.Post, "api/identity/login", new { client_id = client, identifier, password, tenant_id = tenant, captcha });

        internal Task<(HttpStatusCode Status, JsonElement Body, HttpResponseMessage Raw)> Select(string ticket, Guid? membership) =>
            Json(HttpMethod.Post, "api/identity/login/select", new { ticket, membership_id = membership });

        /// <summary>完成授权码流程，返回 code 与 PKCE 材料；没有有效账号 Cookie 时返回 null（被引导去登录）。</summary>
        internal async Task<(string Code, string Verifier, string Nonce)?> Code(string client = "web")
        {
            var verifier = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(48)); var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
            var redirect = client == "web" ? host.Redirect : host.Issuer + "mobile-callback";
            using var response = await http.GetAsync(QueryHelpers.AddQueryString("connect/authorize", new Dictionary<string, string?>
            {
                ["client_id"] = client, ["redirect_uri"] = redirect, ["response_type"] = "code", ["scope"] = "openid offline_access qian.api", ["nonce"] = nonce,
                ["code_challenge_method"] = "S256", ["code_challenge"] = Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)))
            }), Ct);
            if (response.StatusCode != HttpStatusCode.Redirect) return null;
            var query = QueryHelpers.ParseQuery(response.Headers.Location!.Query);
            return query.TryGetValue("code", out var code) ? (code.ToString(), verifier, nonce) : null;
        }
    }

    internal sealed class ManualClock(DateTimeOffset start) : TimeProvider
    {
        private long _ticks = start.UtcTicks;
        public override DateTimeOffset GetUtcNow() => new(Interlocked.Read(ref _ticks), TimeSpan.Zero);
        internal void Advance(TimeSpan by) => Interlocked.Add(ref _ticks, by.Ticks);
    }

    internal sealed class CapturedLogs : ILoggerProvider
    {
        internal System.Collections.Concurrent.ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Sink(Messages);
        public void Dispose() { }
        private sealed class Sink(System.Collections.Concurrent.ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
        }
    }

    /// <summary>可控的租户目录与配额：状态与人数上限由测试设定；配额判定与生产同一规则（达到上限即拒绝，平台超管不计）。</summary>
    internal sealed class FakeTenancy(ITenantContext tenant) : IQianPlugin, ITenantDirectory, ITenantQuota
    {
        private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (TenantDescriptor Descriptor, int? Limit)> _tenants = new();
        public IReadOnlyList<ServiceKey> Inject => [];

        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide<ITenantDirectory>(TenancyDefinition.Key, this);
            ctx.Provide<ITenantQuota>(TenantSettings.QuotaKey, this);
            return ValueTask.CompletedTask;
        }

        internal TenantDescriptor Add(string code, TenantStatus status = TenantStatus.Active, int? limit = null)
        {
            var descriptor = new TenantDescriptor(Guid.CreateVersion7(), code, code.ToUpperInvariant(), TenantIsolation.Row, status);
            _tenants[descriptor.Id] = (descriptor, limit);
            return descriptor;
        }

        internal void SetStatus(Guid id, TenantStatus status) => _tenants.AddOrUpdate(id, _ => throw new KeyNotFoundException(), (_, current) => (current.Descriptor with { Status = status }, current.Limit));

        public ValueTask<TenantDescriptor?> GetAsync(string code, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_tenants.Values.Select(item => item.Descriptor).FirstOrDefault(item => item.Code == code));

        public ValueTask<TenantDescriptor?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(_tenants.TryGetValue(id, out var item) ? item.Descriptor : null);

        public ValueTask EnsureCanAddUserAsync(int currentUsers, TenantMemberKind kind, CancellationToken cancellationToken = default)
        {
            var id = Guid.ParseExact(tenant.TenantId!, "D");
            if (kind != TenantMemberKind.PlatformSuperadmin && _tenants[id].Limit is { } limit && currentUsers >= limit)
                throw new Qian.Runtime.Errors.QianErrorException(new Qian.Runtime.Errors.QianError(TenancyErrors.UserLimit, new Dictionary<string, string> { ["limit"] = limit.ToString(System.Globalization.CultureInfo.InvariantCulture) }));
            return ValueTask.CompletedTask;
        }
    }
}
