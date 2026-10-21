using System.Net;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kun.Authorization;
using Kun.Security.Authentication;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Authz.Abstractions;
using Qian.Capabilities;
using Qian.Capabilities.AspNetCore;
using Qian.Config.Apollo;
using Qian.Config.AspNetCore;
using Qian.Identity.Abstractions;
using Qian.Registry;
using Qian.Runtime;
using Qian.Runtime.Errors.AspNetCore;

namespace Qian.Config.Tests;

// S1-CFG-003 验收 4：GET /api/config/schemas/{plugin} 对已认证调用方可读（不含值）；配置值只对平台主体、经执行端口放行且非敏感时可读。
// 真实 Kestrel + RS256 JWT；执行端口与注册表用记录型替身，配置走真实 qian.config 组件与 IConfiguration。
public sealed class ConfigEndpointsTests
{
    private const string Plugin = "test.sample";
    private const string Operator = "config-operator";
    private const string Reader = "config-reader";
    private const string AuditGone = "config-audit-gone";
    private const string Tenant = "0f9a3b0e-6d0c-4a8e-9b51-7c2d4e6f8a10";
    private static readonly string[] Hidden = ["secretRef://platform/sample", "synthetic-token-literal", "user:pass"];
    private static readonly string[] Audiences = ["config-http"];
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly byte[] Schema = Encoding.UTF8.GetBytes("""
        {"$schema":"https://json-schema.org/draft/2020-12/schema","type":"object","additionalProperties":false,
         "properties":{"pageSize":{"type":"integer","default":20},"title":{"type":"string"},"endpoint":{"type":"string"},
           "storage":{"type":"object","additionalProperties":false,"properties":{"credential":{"type":"string","x-qian-sensitive":true},"token":{"type":"string"}}}}}
        """);

    [Fact]
    public async Task Schema对已认证调用方可读_配置值需平台主体且执行端口放行_敏感键只给键名()
    {
        var enforcement = new RecordingEnforcement();
        await using var host = await ConfigHost.StartAsync(enforcement);

        // 匿名 401；Schema 对租户用户与平台用户都可读，原文返回、不含任何配置值。
        using (var anonymous = await host.Client.GetAsync("/api/config/schemas/" + Plugin, Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        foreach (var tenant in new[] { Tenant, null })
        {
            using var schema = await host.Get("/api/config/schemas/" + Plugin, Reader, tenant);
            Assert.Equal(HttpStatusCode.OK, schema.StatusCode);
            Assert.Equal(ConfigEndpoints.SchemaMediaType, schema.Content.Headers.ContentType?.MediaType);
            NoStore(schema);
            Assert.Equal(Schema, await schema.Content.ReadAsByteArrayAsync(Ct));
        }
        // 未预装、只有静态绑定（没有文件快照）、非法组件 ID：一律 404。
        foreach (var plugin in new[] { "test.unknown", "test.static", "Bad_Id" })
        {
            using var missing = await host.Get("/api/config/schemas/" + plugin, Reader, null);
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        Assert.Empty(enforcement.Requests);

        // 租户主体读值：判定前拒绝，执行端口不被调用。
        using (var tenantValues = await host.Get("/api/config/values/" + Plugin, Operator, Tenant))
        {
            Assert.Equal(HttpStatusCode.Forbidden, tenantValues.StatusCode);
            Assert.Equal("AUTHZ_DENIED", (await Json(tenantValues)).GetProperty("code").GetString());
        }
        Assert.Empty(enforcement.Requests);

        // 平台主体未获授权：403；未授权时未知组件同样 403，看不出哪些组件已预装。
        foreach (var plugin in new[] { Plugin, "test.unknown" })
        {
            using var denied = await host.Get("/api/config/values/" + plugin, Reader, null);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            NoStore(denied);
            Assert.DoesNotContain("pageSize", await denied.Content.ReadAsStringAsync(Ct), StringComparison.Ordinal);
        }
        var request = enforcement.Requests[0];
        Assert.Equal((Reader, null, "user", ConfigDefinition.ValuesReadPermission, "config/values/" + Plugin, "api", AuthzEnforcement.HttpEntryPoint, "GET /api/config/values/{plugin}"),
            (request.Subject.Id, request.Subject.TenantId, request.Subject.Kind, request.Permission, request.Resource, request.Channel, request.EntryPoint, request.Target));

        // 放行后读值：Schema 标敏感、疑似密钥键名、值里内嵌凭据的键只给键名。
        using (var allowed = await host.Get("/api/config/values/" + Plugin, Operator, null))
        {
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
            NoStore(allowed);
            var text = await allowed.Content.ReadAsStringAsync(Ct);
            foreach (var value in Hidden) Assert.DoesNotContain(value, text, StringComparison.Ordinal);
            var body = JsonSerializer.Deserialize<JsonElement>(text);
            Assert.Equal((Plugin, "qian.plugin." + Plugin), (body.GetProperty("plugin").GetString(), body.GetProperty("section").GetString()));
            Assert.Equal([("endpoint", true, null), ("pageSize", false, "20"), ("storage:credential", true, null), ("storage:token", true, null), ("title", false, "乾")],
                body.GetProperty("items").EnumerateArray().Select(item => (item.GetProperty("key").GetString(), item.GetProperty("sensitive").GetBoolean(),
                    item.GetProperty("value").ValueKind == JsonValueKind.Null ? null : item.GetProperty("value").GetString())));
        }
        // 放行后才区分存在：未预装 404；审计组件不可用时整个入口与不存在同为 404。
        using (var unknown = await host.Get("/api/config/values/test.unknown", Operator, null)) Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        using (var gone = await host.Get("/api/config/values/" + Plugin, AuditGone, null)) Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);

        // OpenAPI：值端点写出 x-qian-permission，Schema 端点在处理器内授权、不声明单一权限点；两者都要求 bearer。
        var document = await host.App.Services.GetRequiredService<IHttpContractSource>().CompileDocumentAsync("qian.config", Ct);
        var values = document.GetProperty("paths").GetProperty("/api/config/values/{plugin}").GetProperty("get");
        var schemas = document.GetProperty("paths").GetProperty("/api/config/schemas/{plugin}").GetProperty("get");
        Assert.Equal(ConfigDefinition.ValuesReadPermission, values.GetProperty("x-qian-permission").GetString());
        Assert.False(schemas.TryGetProperty("x-qian-permission", out _));
        Assert.True(schemas.GetProperty("responses").GetProperty("200").GetProperty("content").TryGetProperty(ConfigEndpoints.SchemaMediaType, out _));
        foreach (var operation in new[] { values, schemas }) Assert.Equal(1, operation.GetProperty("security").GetArrayLength());
    }

    [Fact]
    public void 权限点声明与组件元数据一致()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Qian.slnx"))) root = root.Parent;
        using var permissions = JsonDocument.Parse(File.ReadAllText(Path.Combine(root!.FullName, "plugins/qian.config/metadata/permissions.json")));
        var declared = permissions.RootElement.EnumerateArray().SelectMany(item => item.GetProperty("actions").EnumerateArray()
            .Select(action => "qian.config:" + item.GetProperty("resource").GetString() + ":" + action.GetString())).ToArray();
        Assert.Equal([ConfigDefinition.ValuesReadPermission], declared);
    }

    private static void NoStore(HttpResponseMessage response) => Assert.True(response.Headers.CacheControl is { Private: true, NoStore: true });
    private static async Task<JsonElement> Json(HttpResponseMessage response) => JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync(Ct));

    private sealed class ConfigHost : IAsyncDisposable
    {
        private readonly RSA _signing;
        private readonly RsaSecurityKey _key;
        private readonly PluginLoader _runtime;
        private readonly ServiceProvider _services;
        private ConfigHost(WebApplication app, RSA signing, RsaSecurityKey key, HttpClient client, PluginLoader runtime, ServiceProvider services)
        { App = app; _signing = signing; _key = key; Client = client; _runtime = runtime; _services = services; }
        public WebApplication App { get; }
        public HttpClient Client { get; }

        public static async Task<ConfigHost> StartAsync(RecordingEnforcement enforcement)
        {
            // 插件树：真实 qian.config 组件读 IConfiguration；注册表只给出预装组件快照，执行端口记录每次判定。
            var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["qian.plugin.test.sample:pageSize"] = "20", ["qian.plugin.test.sample:title"] = "乾",
                ["qian.plugin.test.sample:endpoint"] = "https://user:pass@example.test/hook",
                ["qian.plugin.test.sample:storage:credential"] = "secretRef://platform/sample/credential",
                ["qian.plugin.test.sample:storage:token"] = "synthetic-token-literal",
                ["qian.plugin.other:pageSize"] = "99"
            }).Build();
            var services = new ServiceCollection().AddQianConfiguration(configuration).BuildServiceProvider();
            var runtime = new PluginLoader(services); var surface = new HttpSurface();
            var manifest = JsonSerializer.SerializeToElement(new { id = Plugin, version = "1.0.0" });
            InstalledComponentMetadata[] installed = [
                new(Plugin, "1.0.0", manifest, new Dictionary<string, JsonElement>(), "sha256:" + new string('a', 64),
                    new Dictionary<string, ReadOnlyMemory<byte>> { ["schemas/config.schema.json"] = Schema, [".qian-plugin/plugin.json"] = Encoding.UTF8.GetBytes("{}") }),
                new("test.static", "1.0.0", JsonSerializer.SerializeToElement(new { id = "test.static", version = "1.0.0" }), new Dictionary<string, JsonElement>())];
            await runtime.MountAsync([
                Define("config", "qian.config", new ConfigurationPlugin(), [ConfigDefinition.Key]),
                Define("registry", "test.registry", new Provider(RegistryDefinition.Key, RegistryProxy.Create(installed)), [RegistryDefinition.Key]),
                Define("authz", "test.authz", new Provider(AuthzEnforcement.Key, enforcement), [AuthzEnforcement.Key]),
                Define("http", "test.config-http", surface, [])], Ct);

            var signing = RSA.Create(2048); using var publicOnly = RSA.Create(); publicOnly.ImportParameters(signing.ExportParameters(false));
            var key = new RsaSecurityKey(signing) { KeyId = "config-http" };
            var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicOnly) { KeyId = key.KeyId });
            var jwks = new JsonWebKeySet(JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } }));
            var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
            builder.Services.AddKunSecurity(); builder.Services.AddHttpContextAccessor(); builder.Services.AddQianErrors(_ => { });
            builder.Services.AddScoped<ICurrentUser, TokenUser>(); builder.Services.AddQianHttpContracts();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddKunServiceAuthentication(options =>
            { options.Issuer = "config-http-test"; options.Audience = "config-http"; options.StaticKeySet = jwks; });
            builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.MapInboundClaims = false);
            builder.Services.AddAuthorization();
            builder.Services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                var previous = options.Events.OnTokenValidated;
                options.Events.OnTokenValidated = async context =>
                {
                    if (previous is not null) await previous(context);
                    if (context.Result is not null && !context.Result.Succeeded) return;
                    TokenUser.Capture(context);
                };
            });
            var app = builder.Build();
            app.UseQianErrors(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.MapQianConfig(_ => surface.Context!);
            try
            {
                await app.StartAsync(Ct); var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
                return new ConfigHost(app, signing, key, new HttpClient { BaseAddress = new Uri(address) }, runtime, services);
            }
            catch { await app.DisposeAsync(); signing.Dispose(); await runtime.DisposeAsync(); await services.DisposeAsync(); throw; }
        }

        public async Task<HttpResponseMessage> Get(string path, string subject, string? tenant)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
            var payload = JsonSerializer.Serialize(new { iss = "config-http-test", aud = Audiences, sub = subject, principal_kind = "user", tenant_id = tenant,
                identity_revision = 0, roles = Array.Empty<string>(), scope = "config.read", session_id = id, jti = id, iat = now, nbf = now, exp = now + 60 });
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(_key, SecurityAlgorithms.RsaSha256)));
            return await Client.SendAsync(request, Ct);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose(); await App.StopAsync(Ct); await App.DisposeAsync(); _signing.Dispose();
            await _runtime.DisposeAsync(); await _services.DisposeAsync();
        }
    }

    private static PluginDefinition Define(string id, string plugin, IQianPlugin implementation, IReadOnlyList<ServiceKey> provides) =>
        new(new PluginEntry(id, plugin, "1.0.0", ScopeKey.Global, 0, JsonSerializer.SerializeToElement(new { })), implementation, provides);

    // 宿主的平台上下文：能注入注册表、配置与执行端口。
    private sealed class HttpSurface : IQianPlugin
    {
        public IPluginContext? Context { get; private set; }
        public IReadOnlyList<ServiceKey> Inject => [RegistryDefinition.Key, ConfigDefinition.Key, AuthzEnforcement.Key];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct) { Context = ctx; return ValueTask.CompletedTask; }
    }

    private sealed class Provider(ServiceKey key, object service) : IQianPlugin
    {
        public IReadOnlyList<ServiceKey> Inject => [];
        public ValueTask ApplyAsync(IPluginContext ctx, JsonElement config, CancellationToken ct)
        {
            ctx.Provide(key, service);
            return ValueTask.CompletedTask;
        }
    }

    // 只给出预装组件快照；端点不应调用注册表的其它成员。
    public class RegistryProxy : DispatchProxy
    {
        private IReadOnlyList<InstalledComponentMetadata> _installed = [];
        internal static IRegistryService Create(IReadOnlyList<InstalledComponentMetadata> installed)
        {
            var proxy = Create<IRegistryService, RegistryProxy>();
            ((RegistryProxy)(object)proxy)._installed = installed;
            return proxy;
        }
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => targetMethod?.Name switch
        {
            nameof(IRegistryService.GetInstalledComponents) => _installed,
            nameof(IAsyncDisposable.DisposeAsync) => ValueTask.CompletedTask,
            _ => throw new NotSupportedException(targetMethod?.Name)
        };
    }

    internal sealed class RecordingEnforcement : IAuthzEnforcement
    {
        private readonly List<EnforcementRequest> _requests = [];
        public IReadOnlyList<EnforcementRequest> Requests { get { lock (_requests) return [.. _requests]; } }
        public ValueTask<AuthorizationDecision> EnforceAsync(EnforcementRequest request, CancellationToken cancellationToken = default)
        {
            lock (_requests) _requests.Add(request);
            var (allow, reason) = request.Subject.Id switch
            {
                Operator => (true, "granted"),
                AuditGone => (false, AuthzEnforcement.AuditUnavailable),
                _ => (false, "permission_missing")
            };
            return ValueTask.FromResult(new AuthorizationDecision(Guid.CreateVersion7(), request,
                new AuthorizationExplanation(new PermissionDecision(allow, reason, []), [], [], allow ? [] : [request.Permission ?? ""]), false, true, DateTimeOffset.UtcNow));
        }
    }

    // 只消费认证事件已验证的令牌快照；请求头与正文不是身份来源。
    private sealed class TokenUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        private static readonly object Key = new();
        public Principal? Principal => accessor.HttpContext?.Items[Key] as Principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => Principal?.Roles ?? [];
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;

        internal static void Capture(TokenValidatedContext context)
        {
            if (context.SecurityToken is not JsonWebToken token || !PrincipalClaims.TryRead(context.Principal, out var subject, out var tenant, out _))
            { context.Fail("无效身份。"); return; }
            var principal = JsonSerializer.Deserialize(Base64UrlEncoder.DecodeBytes(token.EncodedPayload), IdentityJson.PrincipalTypeInfo);
            if (principal is null || principal.Subject != subject || principal.TenantId != tenant || principal.Actor is not null) { context.Fail("身份快照不一致。"); return; }
            context.HttpContext.Items[Key] = principal;
        }
    }
}
