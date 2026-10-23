using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Config.AspNetCore;
using Qian.Identity.Abstractions;
using Qian.Runtime.Errors.AspNetCore;
using Qian.Testing.Containers;

namespace Qian.Config.Tests;

public sealed class FeatureFlagHttpTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly string[] Audiences = ["flags"];
    [Fact]
    public async Task 真实Bearer端点_租户与Agent拒绝_Cookie不能代替Bearer_输入确认权限与幂等生效()
    {
        var ct = TestContext.Current.CancellationToken;
        var backendBuilder = WebApplication.CreateSlimBuilder(); backendBuilder.WebHost.UseUrls("http://127.0.0.1:0"); backendBuilder.Logging.ClearProviders();
        await using var backend = backendBuilder.Build(); var writes = 0;
        backend.Run(context => { Interlocked.Increment(ref writes); context.Response.StatusCode = 200; return Task.CompletedTask; });
        await backend.StartAsync(ct);
        var accessor = new HttpContextAccessor();
        await using var management = await FlagManagementTestHost.Create(postgres, new Uri(backend.Urls.Single()), "http-app", "fixture-http-token", ct,
            configure: services => { services.AddSingleton<IHttpContextAccessor>(accessor); services.Replace(ServiceDescriptor.Scoped<ICurrentUser, TokenUser>()); });
        using var rsa = RSA.Create(2048); var key = new RsaSecurityKey(rsa) { KeyId = "flags-http" };
        var builder = WebApplication.CreateSlimBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0"); builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IHttpContextAccessor>(accessor); builder.Services.AddScoped<ICurrentUser, TokenUser>();
        builder.Services.AddQianErrors(_ => { });
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies").AddJwtBearer("Bearer", options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new() { ValidateIssuer = true, ValidIssuer = "flags-http", ValidateAudience = true, ValidAudience = "flags",
                ValidateIssuerSigningKey = true, IssuerSigningKey = key, ValidateLifetime = true, ClockSkew = TimeSpan.Zero };
            options.Events = new JwtBearerEvents { OnTokenValidated = context =>
            {
                if (context.SecurityToken is JsonWebToken token)
                {
                    var principal = JsonSerializer.Deserialize(Base64UrlEncoder.DecodeBytes(token.EncodedPayload), IdentityJson.PrincipalTypeInfo);
                    if (principal is not null) context.HttpContext.Items[TokenUser.Key] = principal;
                }
                return Task.CompletedTask;
            } };
        });
        builder.Services.AddAuthorization();
        await using var app = builder.Build();
        app.UseQianErrors(); app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
        app.MapQianFeatureFlags(_ => management.Context);
        app.MapPost("/fixture-cookie", async (HttpContext http) =>
        {
            await http.SignInAsync("Cookies", new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator")], "Cookies")));
            return Results.Ok();
        });
        await app.StartAsync(ct);
        using var client = new HttpClient(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = false }) { BaseAddress = new Uri(app.Urls.Single()) };
        var body = "{\"idempotencyKey\":\"" + Guid.NewGuid() + "\",\"name\":\"preview\",\"rule\":{\"default\":true},\"confirmed\":true}";
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(null, body));
        using (var cookie = await client.PostAsync("fixture-cookie", null, ct)) cookie.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, await Send(null, body));
        Assert.Equal(HttpStatusCode.Unauthorized, await Send("not-a-token", body));
        Assert.Equal(HttpStatusCode.Forbidden, await Send(Token("user", Guid.NewGuid().ToString("D")), body));
        Assert.Equal(HttpStatusCode.Forbidden, await Send(Token("agent", null), body));
        Assert.Equal(HttpStatusCode.BadRequest, await Send(Token("user", null), body.Replace("\"confirmed\":true", "\"confirmed\":false", StringComparison.Ordinal)));
        Assert.Equal(HttpStatusCode.BadRequest, await Send(Token("user", null), body[..^1] + ",\"portal\":\"http://untrusted.invalid/\"}"));
        Assert.Equal(HttpStatusCode.BadRequest, await Send(Token("user", null), body[..^1] + ",\"name\":\"other\"}"));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, await Send(Token("user", null), new string(' ', 70_000)));
        management.Authz.Allow = false;
        Assert.Equal(HttpStatusCode.Forbidden, await Send(Token("user", null), body));
        Assert.Equal(0, writes);
        management.Authz.Allow = true;
        Assert.Equal(HttpStatusCode.OK, await Send(Token("user", null), body));
        Assert.Equal(HttpStatusCode.OK, await Send(Token("user", null), body));
        Assert.Equal(1, writes);
        Assert.All(management.Authz.Requests, request =>
        { Assert.Equal("operator", request.Subject.Id); Assert.Null(request.Subject.TenantId); Assert.Equal(FeatureFlagManagement.SetPermission, request.Permission); });
        await app.StopAsync(ct); await backend.StopAsync(ct);

        async Task<HttpStatusCode> Send(string? token, string json)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/config/flags") { Content = new StringContent(json, Encoding.UTF8, "application/json") };
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await client.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                using var content = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                Assert.Equal("pending-publish", content.RootElement.GetProperty("status").GetString());
                Assert.True(response.Headers.CacheControl?.NoStore);
            }
            return response.StatusCode;
        }
        string Token(string kind, string? tenant)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds(); var id = Guid.NewGuid().ToString("D");
            var payload = JsonSerializer.Serialize(new { iss = "flags-http", aud = Audiences, sub = "operator", principal_kind = kind, tenant_id = tenant,
                identity_revision = 0, roles = Array.Empty<string>(), scope = "flags", session_id = id, jti = id, iat = now, nbf = now, exp = now + 60 });
            return new JsonWebTokenHandler().CreateToken(payload, new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        }
    }
    private sealed class TokenUser(IHttpContextAccessor accessor) : ICurrentUser
    {
        internal static readonly object Key = new();
        public Principal? Principal => accessor.HttpContext?.Items[Key] as Principal;
        public string? UserId => Principal?.Subject;
        public IReadOnlyList<string> Roles => Principal?.Roles ?? [];
        public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true && Principal is not null;
    }
}
