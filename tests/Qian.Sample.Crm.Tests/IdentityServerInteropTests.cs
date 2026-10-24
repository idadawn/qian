using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Kun.Gateway;
using Kun.Gateway.Configuration;
using Kun.Gateway.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Qian.Testing.Identity;

namespace Qian.Sample.Crm.Tests;

public sealed partial class IdentityAuthzTests
{
    [Fact]
    public async Task 真实Identity机器令牌经Kun网关签发器换取S2S_调用真实CRM写能力并保留服务主体()
    {
        await using var business = await Start();
        await using var identity = await IdentityProtocolHost.Start(Postgres, serviceTenant: Guid.Parse(A), serviceClient: Writer, serviceScope: "crm.write");
        using var client = identity.Kun(Writer); var token = await client.ClientCredentialsAsync("crm.write", Ct);
        using var discovery = JsonDocument.Parse(await identity.Browser.GetStringAsync(".well-known/openid-configuration", Ct));
        var jwks = new JsonWebKeySet(await identity.Browser.GetStringAsync(discovery.RootElement.GetProperty("jwks_uri").GetString()!, Ct));
        var validation = await new JsonWebTokenHandler { MapInboundClaims = false }.ValidateTokenAsync(token.AccessToken, new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = identity.Issuer, ValidateAudience = true, ValidAudience = "https://api.qian.test",
            ValidateIssuerSigningKey = true, IssuerSigningKeys = jwks.GetSigningKeys(), ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.Zero
        });
        Assert.True(validation.IsValid, validation.Exception?.Message);
        var external = new ClaimsPrincipal(validation.ClaimsIdentity);
        Assert.Equal(Writer, external.FindFirst("sub")!.Value); Assert.Equal("service", external.FindFirst("principal_kind")!.Value);
        var directory = Directory.CreateTempSubdirectory("qian-idp-s2s-");
        try
        {
            var privateKey = Path.Combine(directory.FullName, "private.pem"); var publicKey = Path.Combine(directory.FullName, "public.pem");
            var fileOptions = new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.CreateNew };
            if (!OperatingSystem.IsWindows()) fileOptions.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var file = new StreamWriter(new FileStream(privateKey, fileOptions)))
                await file.WriteAsync(business.ManagementSigningKey.Rsa.ExportRSAPrivateKeyPem().AsMemory(), Ct);
            await File.WriteAllTextAsync(publicKey, business.ManagementSigningKey.Rsa.ExportRSAPublicKeyPem(), Ct);
            using var gateway = new S2sTokenIssuer(Options.Create(new KunGatewayOptions
            {
                ExternalJwksUri = discovery.RootElement.GetProperty("jwks_uri").GetString()!, ExternalIssuer = identity.Issuer, ExternalAudience = "https://api.qian.test", InternalIssuer = Issuer, InternalAudience = Audience,
                SigningKeyPath = privateKey, PublicKeyPath = publicKey, InternalTokenLifetime = TimeSpan.FromSeconds(60)
            }), business.App.Environment, NoOpS2sPrincipalResolver.Instance);
            var s2s = await gateway.IssueForAsync(external, Ct);
            var payload = new JsonWebToken(s2s);
            Assert.Equal(Writer, payload.Subject); Assert.Equal("service", payload.GetClaim("principal_kind").Value);
            Assert.Equal(payload.GetClaim("jti").Value, payload.GetClaim("session_id").Value);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/customers")
                { Content = JsonContent.Create(new CreateCustomerCommand(Guid.CreateVersion7(), "真实身份服务客户")) };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", s2s);
            using var response = await business.Send(request);
            Assert.True(response.StatusCode == HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Ct));
            Assert.Equal("1", await Text($"SELECT count(*)::text FROM plugin_qian_sample_crm.customers WHERE tenant_id='{A}' AND name='真实身份服务客户'"));
        }
        finally { directory.Delete(recursive: true); }
    }
}
