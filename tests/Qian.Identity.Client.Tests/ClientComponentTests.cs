using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Qian.Identity.Abstractions;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Identity.Client.Tests;

// 组件形态的约束：配置只接受与宿主一致的 issuer / clientId 与 platform / dev 的 secretRef，只允许 global scope；
// 依赖方 Provider 不伪造身份服务的管理能力；returnTo 只回同源白名单路径。
public sealed class ClientComponentTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static readonly IdentityClientOptions Options = new()
    {
        Authority = new Uri("https://id.qian.test/"), ClientId = "qian-web", RedirectUri = new Uri("https://app.qian.test/auth/callback"),
        AllowedReturnPaths = ["/app", "/me"], UseInMemorySessionStore = true
    };

    [Theory]
    [InlineData("/app", "/app")]
    [InlineData("/app/orders?id=1#top", "/app/orders?id=1#top")]
    [InlineData("/me", "/me")]
    [InlineData("/apps", "/")]
    [InlineData("/admin", "/")]
    [InlineData("//evil.example/app", "/")]
    [InlineData("https://evil.example/app", "/")]
    [InlineData("/app/../admin", "/")]
    [InlineData("/app/%2e%2e/admin", "/")]
    [InlineData("/app\\..\\admin", "/")]
    [InlineData("/app%0d%0aSet-Cookie:x", "/")]
    [InlineData("", "/")]
    public void returnTo只回同源白名单路径(string value, string expected) =>
        Assert.Equal(expected, IdentityClientEndpoints.ReturnPath(Options, new StringValues(value)));

    [Fact]
    public void returnTo缺失或重复时回缺省落点()
    {
        Assert.Equal("/", IdentityClientEndpoints.ReturnPath(Options, StringValues.Empty));
        Assert.Equal("/", IdentityClientEndpoints.ReturnPath(Options, new StringValues(["/app", "/me"])));
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("client")]
    [InlineData("unknown")]
    [InlineData("plaintext")]
    [InlineData("tenant-secret")]
    [InlineData("tenant-scope")]
    [InlineData("number")]
    public async Task 组件配置与宿主不一致或密钥不是secretRef时挂载失败(string violation)
    {
        await using var services = Services();
        await using var loader = new PluginLoader(services);
        object config = violation switch
        {
            "issuer" => new { issuer = "https://other.qian.test/", clientId = "qian-web" },
            "client" => new { issuer = "https://id.qian.test", clientId = "other" },
            "unknown" => new { issuer = "https://id.qian.test", clientId = "qian-web", authority = "https://id.qian.test" },
            "plaintext" => new { issuer = "https://id.qian.test", clientId = "qian-web", clientSecret = "synthetic-plaintext-secret" },
            "tenant-secret" => new { issuer = "https://id.qian.test", clientId = "qian-web", clientSecret = "secretRef://tenant/identity/client-secret" },
            "number" => new { issuer = "https://id.qian.test", clientId = 42 },
            _ => new { issuer = "https://id.qian.test", clientId = "qian-web" }
        };
        var scope = violation == "tenant-scope" ? new ScopeKey("tenant", Guid.CreateVersion7().ToString("D")) : ScopeKey.Global;
        await loader.MountAsync([new(new PluginEntry("identity", "qian.identity.client", "1.0.0", scope, 0, JsonSerializer.SerializeToElement(config)), new IdentityClientPlugin(), [IdentityDefinition.Key])], Ct);
        Assert.Equal(PluginState.Failed, loader.GetState("identity", scope));
        var failure = Assert.IsType<QianErrorException>(loader.GetFailure("identity", scope));
        Assert.Equal("INPUT_INVALID", failure.Error.Code.Code);
        Assert.DoesNotContain("synthetic-plaintext-secret", failure.ToString(), StringComparison.Ordinal);
        Assert.False(services.GetRequiredService<IdentityClientRuntime>().Active);
    }

    [Fact]
    public async Task 公开客户端不经Config即可挂载_依赖方Provider只回答当前主体_管理操作明确不支持()
    {
        await using var services = Services();
        await using var loader = new PluginLoader(services);
        // issuer 末尾斜杠不影响比对；公开客户端不配 clientSecret，因而不依赖 Config 组件。
        await loader.MountAsync([new(new PluginEntry("identity", "qian.identity.client", "1.0.0", ScopeKey.Global, 0,
            JsonSerializer.SerializeToElement(new { issuer = "https://id.qian.test/", clientId = "qian-web" })), new IdentityClientPlugin(), [IdentityDefinition.Key])], Ct);
        Assert.Equal(PluginState.Ready, loader.GetState("identity", ScopeKey.Global));
        var identity = loader.GetContext("identity", ScopeKey.Global).Identity();
        Assert.Null(identity.CurrentPrincipal);
        Assert.Empty(identity.Connectors.List()); Assert.Null(identity.Connectors.Find("oidc"));
        foreach (var call in new Func<Task>[]
        {
            () => identity.GetPersonAsync(Guid.CreateVersion7(), Ct), () => identity.GetMembershipAsync(Guid.CreateVersion7(), Ct),
            () => identity.ListSessionsAsync(Guid.CreateVersion7(), Ct), () => identity.RevokeSessionsAsync(new(Guid.CreateVersion7(), null, "test", "key"), Ct),
            () => { identity.Connectors.Register("oidc", null!); return Task.CompletedTask; }
        })
            Assert.Equal("IDENTITY_OPERATION_UNSUPPORTED", (await Assert.ThrowsAsync<QianErrorException>(call)).Error.Code.Code);
        Assert.True(services.GetRequiredService<IdentityClientRuntime>().Active);
        await loader.UnmountAsync("identity", ScopeKey.Global, Ct);
        Assert.False(services.GetRequiredService<IdentityClientRuntime>().Active);
    }

    [Fact]
    public void 装配参数在登记时校验()
    {
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddQianIdentityClient(new IdentityClientOptions { Authority = new Uri("http://id.qian.test/"), ClientId = "qian-web" }));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddQianIdentityClient(new IdentityClientOptions { Authority = new Uri("https://id.qian.test/"), ClientId = "qian-web", Scopes = ["profile"] }));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddQianIdentityClient(new IdentityClientOptions
            { Authority = new Uri("https://id.qian.test/"), ClientId = "qian-web", RedirectUri = new Uri("https://app.qian.test/cb?x=1") }));
        var keys = new JsonWebKeySet();
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddQianIdentityS2s(new IdentityS2sOptions { Issuer = "https://gateway.qian.test", Audience = "kun-internal", SigningKeys = keys }));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddQianIdentityS2s(new IdentityS2sOptions
            { Issuer = "https://gateway.qian.test", Audience = "qian-platform", SigningKeys = keys, JwksUri = new Uri("https://gateway.qian.test/jwks") }));
        Assert.Throws<ArgumentException>(() => new ServiceCollection().AddQianIdentityS2s(new IdentityS2sOptions { Issuer = "https://gateway.qian.test", Audience = "qian-platform" }));
        var once = new ServiceCollection().AddQianIdentityClient(new IdentityClientOptions { Authority = new Uri("https://id.qian.test/"), ClientId = "qian-web" });
        Assert.Throws<InvalidOperationException>(() => once.AddQianIdentityClient(new IdentityClientOptions { Authority = new Uri("https://id.qian.test/"), ClientId = "qian-web" }));
    }

    private static ServiceProvider Services() => new ServiceCollection().AddLogging().AddQianIdentityClient(Options).BuildServiceProvider();
}
