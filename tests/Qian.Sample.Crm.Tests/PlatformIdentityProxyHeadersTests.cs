using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Qian.Platform.Host;

namespace Qian.Sample.Crm.Tests;

// 同进程身份分支的可信代理配置（S1-DEP-011）：没有网段时不处理任何转发头（不挂中间件）；给了网段就替换 ASP.NET 的缺省名单，
// 网段或回溯上限不合格时宿主拒绝启动。转发头在真实宿主里的效果（Secure Cookie、会话记下的客户端地址）由 PlatformIdentityHostTests 第十一步覆盖。
public sealed class PlatformIdentityProxyHeadersTests
{
    [Fact]
    public void WithoutNetworksForwardedHeadersAreNotProcessed() => Assert.Null(PlatformIdentity.ProxyHeaders(Configuration(new())));

    [Fact]
    public void NetworksReplaceTheDefaultTrustList()
    {
        var options = PlatformIdentity.ProxyHeaders(Configuration(new()
        {
            ["Identity:TrustedProxyNetworks:0"] = "10.42.0.0/16", ["Identity:TrustedProxyNetworks:1"] = "192.168.1.0/24", ["Identity:ForwardLimit"] = "2"
        }))!;
        Assert.Equal(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto, options.ForwardedHeaders);
        Assert.Equal(2, options.ForwardLimit);
        Assert.Empty(options.KnownProxies);
        Assert.Equal(["10.42.0.0/16", "192.168.1.0/24"], options.KnownIPNetworks.Select(network => network.ToString()));
        Assert.Equal(1, PlatformIdentity.ProxyHeaders(Configuration(new() { ["Identity:TrustedProxyNetworks:0"] = "10.42.0.0/16" }))!.ForwardLimit);
    }

    [Theory]
    [InlineData("0.0.0.0/0", null)]
    [InlineData("::/0", null)]
    [InlineData("10.42.0.0/33", null)]
    [InlineData("traefik", null)]
    [InlineData("", null)]
    [InlineData("10.42.0.0/16", "0")]
    [InlineData("10.42.0.0/16", "6")]
    [InlineData("10.42.0.0/16", "two")]
    public void InvalidSettingsStopTheHost(string network, string? limit) =>
        Assert.Throws<InvalidOperationException>(() => PlatformIdentity.ProxyHeaders(Configuration(new()
        {
            ["Identity:TrustedProxyNetworks:0"] = network, ["Identity:ForwardLimit"] = limit
        })));

    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
