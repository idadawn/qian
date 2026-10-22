using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;

namespace Qian.Identity.Server.Tests;

public sealed class ServerOptionsTests
{
    [Theory]
    [InlineData("Production", "http://127.0.0.1:5000/", true, false)]
    [InlineData("Production", "https://identity.example/", false, true)]
    [InlineData("Development", "http://127.0.0.1:5000/", true, true)]
    [InlineData("Development", "http://remote.example/", true, false)]
    [InlineData("Development", "http://127.0.0.1:5000/", false, false)]
    [InlineData("Production", "https://identity.example/?untrusted=1", false, false)]
    public void 不允许通过开发开关放松生产HTTPS_配置序列化不包含证书或客户端密钥(string environment, string issuer, bool allowHttp, bool accepted)
    {
        using var rsa = RSA.Create(2048);
        using var cert = new CertificateRequest("CN=options-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var options = new IdentityServerOptions
        {
            Issuer = new Uri(issuer), AllowLoopbackHttp = allowHttp, SigningCertificates = [cert], EncryptionCertificates = [cert],
            Clients = [new() { Application = new OpenIddictApplicationDescriptor
            { ClientId = "test", ClientType = OpenIddictConstants.ClientTypes.Confidential, ClientSecret = secret, ConsentType = OpenIddictConstants.ConsentTypes.Implicit }, Resources = ["https://api.example"] }]
        };
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = environment });
        if (accepted) builder.Services.AddQianIdentityServer(options, builder.Environment);
        else Assert.Throws<ArgumentException>(() => builder.Services.AddQianIdentityServer(options, builder.Environment));
        var json = JsonSerializer.Serialize(options);
        Assert.DoesNotContain(secret, json, StringComparison.Ordinal);
        Assert.DoesNotContain(nameof(IdentityServerOptions.SigningCertificates), json, StringComparison.Ordinal);
        Assert.Equal("IdentityServerOptions(redacted)", options.ToString());
    }

    [Fact]
    public void 客户端的rsrc权限由Resources推导_宿主另写白名单外的资源权限被拒()
    {
        using var rsa = RSA.Create(2048);
        using var cert = new CertificateRequest("CN=options-test", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1));
        IdentityServerOptions Options(params string[] permissions)
        {
            var application = new OpenIddictApplicationDescriptor
            { ClientId = "test", ClientType = OpenIddictConstants.ClientTypes.Confidential, ClientSecret = "secret", ConsentType = OpenIddictConstants.ConsentTypes.Implicit };
            application.Permissions.UnionWith(permissions);
            return new IdentityServerOptions
            {
                Issuer = new Uri("https://identity.example/"), SigningCertificates = [cert], EncryptionCertificates = [cert],
                Clients = [new() { Application = application, Resources = ["https://api.example", "https://files.example"] }]
            };
        }
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = "Production" });
        var accepted = Options(OpenIddictConstants.Permissions.Prefixes.Resource + "https://api.example");
        builder.Services.AddQianIdentityServer(accepted, builder.Environment);
        Assert.Equal(["rsrc:https://api.example", "rsrc:https://files.example"],
            accepted.Clients[0].Application.Permissions.Where(permission => permission.StartsWith("rsrc:", StringComparison.Ordinal)).Order(StringComparer.Ordinal));
        Assert.Throws<ArgumentException>(() => builder.Services.AddQianIdentityServer(Options(OpenIddictConstants.Permissions.Prefixes.Resource + "https://other.example"), builder.Environment));
    }
}
