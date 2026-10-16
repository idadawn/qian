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
}
