using System.Net;
using System.Text.Json;
using Kun.Core.MultiTenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Authz;
using Qian.Data;
using Qian.Identity.Client;
using Qian.Identity.Client.Tests;
using Qian.Testing;
using Qian.Testing.Containers;
using Qian.Testing.Gateway;

namespace Qian.Sample.Crm.Tests;

public sealed partial class TenantOperationsTests
{
    [Fact]
    public async Task 真实外壳Cookie经网关换S2S访问平台租户运营路由_匿名401_平台主体与受众正确()
    {
        const string gatewayIssuer = "https://gateway.qian.test/s2s";
        using var platform = new LifecyclePlatform { Management = true };
        await using var business = await Start(lifecycle: platform);
        string? received = null;
        await using var management = await ManagementHost(business, platform, expectedIssuer: gatewayIssuer, expectedAudience: "qian-platform",
            configurePipeline: app => app.Use(async (http, next) =>
            {
                Assert.False(http.Request.Headers.ContainsKey("Cookie"));
                received = http.Request.Headers.Authorization.ToString();
                await next(http);
            }));
        await using var redis = new RedisFixture(); await redis.InitializeAsync();
        using var tls = GatewayTestHost.Certificate(); using var reservation = LoopbackPort.Reserve();
        var address = new Uri($"https://localhost:{reservation.Port}/");
        await using var identity = await IdentityServerHost.Start(Postgres, new Uri(address, "auth/callback"), platform: true);
        await new PostgresPlatformRoleStore(business.App.Services.GetRequiredService<IDataService>(),
            business.App.Services.GetRequiredService<ITenantContext>(), platform.AuthzRole)
            .AssignSuperadminAsync(identity.Person.ToString("D"), "operator", Ct);
        await using var gateway = await GatewayTestHost.Start(reservation, tls, identity.Issuer, IdentityServerHost.ClientId, IdentityServerHost.Resource, identity.Secret,
            redis.ConnectionString, gatewayIssuer, [new("tenancy", "/api/tenancy/{**rest}", "qian-platform") { RateLimiterPolicy = "user" }],
            [new("qian-platform", management.Urls.Single())], business.ManagementSigningKey.Rsa);
        var (client, cookies) = GatewayTestHost.Browser(tls); using var browser = client;
        var target = new Uri(address, "api/tenancy/tenants");
        using (var anonymous = await browser.GetAsync(target, Ct)) Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Null(received);
        await identity.SignInAsync(browser); await gateway.CompleteLogin(browser);
        using var response = await browser.GetAsync(target, Ct);
        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        Assert.NotNull(received); Assert.StartsWith("Bearer ", received, StringComparison.Ordinal);
        var jwt = new JsonWebToken(received[7..]); using var payload = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(jwt.EncodedPayload));
        Assert.Equal("qian-platform", Assert.Single(payload.RootElement.GetProperty("aud").EnumerateArray()).GetString());
        Assert.Equal(JsonValueKind.Null, payload.RootElement.GetProperty("tenant_id").ValueKind);
        Assert.Equal(identity.Person.ToString("D"), payload.RootElement.GetProperty("sub").GetString());
        Assert.Equal(60, payload.RootElement.GetProperty("exp").GetInt64() - payload.RootElement.GetProperty("iat").GetInt64());
        Assert.NotEqual(cookies.GetCookies(address)[IdentityClientDefaults.SessionCookieName]!.Value,
            payload.RootElement.GetProperty("session_id").GetString());
    }
}
