using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Qian.Data.Tests;

/// <summary>
/// 连通测试的出网约束（不需要数据库）：缺省拒绝回环、链路本地、云元数据、未指定、组播与广播地址，IPv4 映射的 IPv6 地址按 IPv4 判定；
/// 放行网段收窄范围、拒绝网段优先；网段写法无效时宿主启动即失败。
/// </summary>
public sealed class DataConnectionEgressTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.255.255.254")]
    [InlineData("::1")]
    [InlineData("169.254.169.254")]
    [InlineData("fe80::1")]
    [InlineData("100.100.100.200")]
    [InlineData("0.0.0.0")]
    [InlineData("::")]
    [InlineData("224.0.0.251")]
    [InlineData("ff02::1")]
    [InlineData("255.255.255.255")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    public void 缺省拒绝回环链路本地云元数据未指定组播与广播地址(string address) =>
        Assert.False(new DataConnectionEgressOptions().Permits(IPAddress.Parse(address)));

    [Theory]
    [InlineData("10.20.30.40")]
    [InlineData("172.16.5.4")]
    [InlineData("192.168.1.10")]
    [InlineData("203.0.113.7")]
    [InlineData("100.100.100.201")]
    [InlineData("2001:db8::10")]
    [InlineData("fd00::5")]
    [InlineData("::ffff:192.168.1.10")]
    public void 缺省放行私网与公网地址(string address) =>
        Assert.True(new DataConnectionEgressOptions().Permits(IPAddress.Parse(address)));

    [Fact]
    public void 配置放行网段后网段外地址一律拒绝_拒绝网段优先于放行网段()
    {
        var egress = new DataConnectionEgressOptions();
        egress.AllowedNetworks.Add("10.8.0.0/16");
        egress.AllowedNetworks.Add("fd00:8::/32");
        Assert.True(egress.Permits(IPAddress.Parse("10.8.3.4")));
        Assert.True(egress.Permits(IPAddress.Parse("fd00:8::1")));
        Assert.True(egress.Permits(IPAddress.Parse("::ffff:10.8.3.4")));
        Assert.False(egress.Permits(IPAddress.Parse("10.9.0.1")));
        Assert.False(egress.Permits(IPAddress.Parse("192.168.1.10")));
        Assert.False(egress.Permits(IPAddress.Parse("2001:db8::10")));

        // 放行全部地址也越不过拒绝网段；运维追加的集群内部网段同样生效。
        var everything = new DataConnectionEgressOptions();
        everything.AllowedNetworks.Add("0.0.0.0/0");
        everything.AllowedNetworks.Add("::/0");
        everything.BlockedNetworks.Add("10.43.0.0/16");
        Assert.False(everything.Permits(IPAddress.Parse("127.0.0.1")));
        Assert.False(everything.Permits(IPAddress.Parse("169.254.169.254")));
        Assert.False(everything.Permits(IPAddress.Parse("10.43.0.10")));
        Assert.True(everything.Permits(IPAddress.Parse("10.42.0.10")));

        // 本机开发可以移除回环。
        var local = new DataConnectionEgressOptions();
        local.BlockedNetworks.Remove("127.0.0.0/8");
        Assert.True(local.Permits(IPAddress.Parse("127.0.0.1")));
        Assert.False(local.Permits(IPAddress.Parse("::1")));
    }

    [Theory]
    [InlineData("10.0.0.0/33")]
    [InlineData("not-a-network")]
    [InlineData("")]
    public void 网段写法无效时宿主启动即失败(string network)
    {
        foreach (var allowed in new[] { false, true })
        {
            var services = new ServiceCollection();
            services.AddQianDataConnections((_, options) => options.UseNpgsql("Host=127.0.0.1"), options =>
            {
                if (allowed) options.Egress.AllowedNetworks.Add(network);
                else options.Egress.BlockedNetworks.Add(network);
            });
            using var provider = services.BuildServiceProvider();
            var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<QianDataConnectionOptions>>().Value);
            Assert.Contains("CIDR", error.Message, StringComparison.Ordinal);
        }
    }
}
