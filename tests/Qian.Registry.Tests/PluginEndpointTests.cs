using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Kun.MultiTenancy;
using Kun.Security.Authentication;
using Kun.Security.DependencyInjection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Plugin.Sdk.AspNetCore;
using Qian.Runtime;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Surfaces.Abstractions;
using Qian.Tenancy;

namespace Qian.Registry.Tests;

public sealed partial class PatchPersistenceTests
{
    private static readonly string[] HttpErrorFields = ["type", "title", "status", "code", "message"];
    private static readonly string[] HttpAudiences = ["registry-http"];
    private const string HttpTenantA = "01900000-0000-7000-8000-000000000001";
    private const string HttpTenantB = "01900000-0000-7000-8000-000000000002";

    [Fact]
    public async Task 真正HTTP中间件消费真库scope视图_隐藏和不存在同为404_只影响对应租户()
    {
        var tenant = new AsyncLocalTenantContext(); await using var running = await CreateRunningRegistry(tenant);
        await using var http = await StartEndpointHost(running, tenant, true); var scopeA = new ScopeKey("tenant", HttpTenantA); var scopeB = new ScopeKey("tenant", HttpTenantB);
        using var anonymous = await http.Get("/surface", null); Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode); Assert.Equal(0, http.Calls);
        using var initial = await http.Get("/surface", HttpTenantA); Assert.Equal(HttpStatusCode.OK, initial.StatusCode); Assert.Equal(1, http.Calls);
        using (tenant.BeginScope(HttpTenantA))
            await Repository(running.Host, tenant).PutAsync(scopeA, "shared", Patch("""{"id":"shared","disabled":true}"""), 0, "operator-a", Ct);
        using var hidden = await http.Get("/surface", HttpTenantA); using var unknown = await http.Get("/unknown", HttpTenantA); using var missingRoute = await http.Get("/absent", HttpTenantA);
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode); Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode); Assert.Equal(HttpStatusCode.NotFound, missingRoute.StatusCode);
        Assert.Equal(await ErrorShape(unknown), await ErrorShape(hidden)); Assert.Equal(await ErrorShape(missingRoute), await ErrorShape(hidden)); Assert.Equal(1, http.Calls);
        using var unaffected = await http.Get("/surface", HttpTenantB); Assert.Equal(HttpStatusCode.OK, unaffected.StatusCode); Assert.Equal(2, http.Calls);
        using (running.Root.Restrict(scopeB, registration => registration.OwnerEntryId != "shared"))
        {
            using var restricted = await http.Get("/surface", HttpTenantB); Assert.Equal(HttpStatusCode.NotFound, restricted.StatusCode); Assert.Equal(2, http.Calls);
        }
        using var restored = await http.Get("/surface", HttpTenantB); Assert.Equal(HttpStatusCode.OK, restored.StatusCode);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task 已绑定端点漏装或错误排序可见性中间件时不能执行handler(bool middleware, bool beforeRouting)
    {
        var tenant = new AsyncLocalTenantContext(); await using var running = await CreateRunningRegistry(tenant);
        await using var http = await StartEndpointHost(running, tenant, middleware, beforeRouting);
        using var response = await http.Get("/surface", HttpTenantA); Assert.Equal(HttpStatusCode.NotFound, response.StatusCode); Assert.Equal(0, http.Calls);
    }

    private static async Task<string> ErrorShape(HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Ct)); var root = json.RootElement;
        return string.Join('|', HttpErrorFields.Select(field => root.GetProperty(field).ToString()));
    }
    private static async Task<EndpointHost> StartEndpointHost(RunningRegistry running, AsyncLocalTenantContext tenant, bool middleware, bool beforeRouting = false)
    {
        var signing = RSA.Create(2048); using var publicOnly = RSA.Create(); publicOnly.ImportParameters(signing.ExportParameters(false));
        var key = new RsaSecurityKey(signing) { KeyId = "registry-http" };
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(publicOnly) { KeyId = key.KeyId });
        var jwks = new JsonWebKeySet(JsonSerializer.Serialize(new { keys = new[] { new { kty = jwk.Kty, kid = jwk.Kid, n = jwk.N, e = jwk.E } } }));
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<ITenantContext>(tenant); builder.Services.AddQianErrors(); builder.Services.AddKunSecurity();
        builder.Services.AddScoped<TenantRequestBoundary>();
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddKunServiceAuthentication(options =>
        { options.Issuer = "registry-http-test"; options.Audience = "registry-http"; options.StaticKeySet = jwks; });
        builder.Services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options => options.MapInboundClaims = false);
        builder.Services.AddQianPluginEndpoints(_ => running.Root);
        var app = builder.Build(); var calls = 0;
        app.UseQianErrors();
        if (middleware && beforeRouting) app.UseQianPluginEndpoints();
        app.UseRouting(); app.UseAuthentication();
        app.Use((context, next) => context.RequestServices.GetRequiredService<TenantRequestBoundary>().InvokeAsync(context, next));
        if (middleware && !beforeRouting) app.UseQianPluginEndpoints();
        app.MapGet("/surface", () =>
        {
            Interlocked.Increment(ref calls);
            var document = running.Root.Scoped(new ScopeKey("tenant", tenant.TenantId!)).Inject<SurfaceDocument>(SurfaceKeys.Commands, "test.shared/hello");
            return Results.Text(document.Body);
        }).RequireQianPlugin(new PluginEndpointBinding("shared", "test.shared", SurfaceKeys.Commands, "test.shared/hello", "1.0.0"));
        app.MapGet("/unknown", () => { Interlocked.Increment(ref calls); return Results.Ok(); })
            .RequireQianPlugin(new PluginEndpointBinding("unknown", "test.unknown", SurfaceKeys.Commands, "test.unknown/hello"));
        try
        {
            await app.StartAsync(Ct); var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new EndpointHost(app, signing, key, new HttpClient { BaseAddress = new Uri(address) }, () => Volatile.Read(ref calls));
        }
        catch { await app.DisposeAsync(); signing.Dispose(); throw; }
    }
    private sealed class EndpointHost(WebApplication app, RSA signing, RsaSecurityKey key, HttpClient client, Func<int> count) : IAsyncDisposable
    {
        internal int Calls => count();
        internal async Task<HttpResponseMessage> Get(string path, string? tenant)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path); request.Headers.Add("X-Tenant-Id", tenant == HttpTenantB ? HttpTenantA : HttpTenantB);
            if (tenant is not null)
            {
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.CreateVersion7().ToString();
                var payload = JsonSerializer.Serialize(new { iss = "registry-http-test", aud = HttpAudiences, sub = "reader", principal_kind = "service", tenant_id = tenant,
                    roles = Array.Empty<string>(), scope = "surface.read", session_id = id, jti = id, iat = now, nbf = now, exp = now + 60 });
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256)));
            }
            return await client.SendAsync(request, Ct);
        }
        public async ValueTask DisposeAsync() { client.Dispose(); await app.StopAsync(Ct); await app.DisposeAsync(); signing.Dispose(); }
    }
}
