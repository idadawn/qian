using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Qian.Data.Tests;

/// <summary>
/// 连通测试的出网约束（不需要数据库）：缺省拒绝回环、链路本地、云元数据、未指定、组播与广播地址，以及 K3s 的 Pod / Service 网段，
/// IPv4 映射的 IPv6 地址按 IPv4 判定；开发环境缺省放行回环，显式配置优先；放行网段收窄范围、拒绝网段优先；网段写法无效时宿主启动即失败。
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
    [InlineData("10.42.0.1")]
    [InlineData("10.42.255.254")]
    [InlineData("10.43.0.1")]
    [InlineData("10.43.0.10")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:10.43.0.10")]
    public void 缺省拒绝回环链路本地云元数据未指定组播广播与K3s集群网段(string address) =>
        Assert.False(new DataConnectionEgressOptions().Permits(IPAddress.Parse(address)));

    [Theory]
    [InlineData("10.20.30.40")]
    [InlineData("10.41.255.255")]
    [InlineData("10.44.0.1")]
    [InlineData("172.16.5.4")]
    [InlineData("192.168.1.10")]
    [InlineData("203.0.113.7")]
    [InlineData("100.100.100.201")]
    [InlineData("2001:db8::10")]
    [InlineData("fd00::5")]
    [InlineData("::ffff:192.168.1.10")]
    public void 缺省放行其余私网与公网地址(string address) =>
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

        // 放行全部地址也越不过拒绝网段；运维追加的网段（如另一集群的 Service 网段）同样生效。
        var everything = new DataConnectionEgressOptions();
        everything.AllowedNetworks.Add("0.0.0.0/0");
        everything.AllowedNetworks.Add("::/0");
        everything.BlockedNetworks.Add("10.96.0.0/12");
        Assert.False(everything.Permits(IPAddress.Parse("127.0.0.1")));
        Assert.False(everything.Permits(IPAddress.Parse("169.254.169.254")));
        Assert.False(everything.Permits(IPAddress.Parse("10.43.0.10")));
        Assert.False(everything.Permits(IPAddress.Parse("10.96.0.1")));
        Assert.True(everything.Permits(IPAddress.Parse("10.20.30.40")));

        // 集群改用了自定义网段时，可以移除 K3s 的两项。
        var custom = new DataConnectionEgressOptions();
        custom.BlockedNetworks.Remove("10.42.0.0/16");
        custom.BlockedNetworks.Remove("10.43.0.0/16");
        Assert.True(custom.Permits(IPAddress.Parse("10.42.0.10")));
        Assert.True(custom.Permits(IPAddress.Parse("10.43.0.10")));
        Assert.False(custom.Permits(IPAddress.Parse("127.0.0.1")));
    }

    [Fact]
    public void 开发环境缺省放行回环_其他环境与未登记宿主环境仍拒绝_其余缺省拒绝项照常生效()
    {
        var development = Configured(Environments.Development);
        Assert.True(development.Permits(IPAddress.Loopback));
        Assert.True(development.Permits(IPAddress.IPv6Loopback));
        Assert.True(development.Permits(IPAddress.Parse("::ffff:127.0.0.1")));
        foreach (var address in new[] { "169.254.169.254", "fe80::1", "100.100.100.200", "0.0.0.0", "10.42.0.10", "10.43.0.1" })
            Assert.False(development.Permits(IPAddress.Parse(address)), address);

        foreach (var environment in new[] { Environments.Production, Environments.Staging, "Test", null })
        {
            var other = Configured(environment);
            Assert.False(other.Permits(IPAddress.Loopback), environment);
            Assert.False(other.Permits(IPAddress.IPv6Loopback), environment);
        }
    }

    [Fact]
    public void 开发环境下宿主显式加回回环时以显式配置为准_无论登记在装配之前还是之后()
    {
        var parameter = Configured(Environments.Development, options: options => options.Egress.BlockedNetworks.Add("127.0.0.0/8"));
        Assert.False(parameter.Permits(IPAddress.Loopback));
        Assert.True(parameter.Permits(IPAddress.IPv6Loopback));

        // 运维整体替换拒绝网段（其中含 ::1），且这条配置登记在装配之前：开发环境的缺省值不能改动它。
        var before = Configured(Environments.Development, before: services => services.Configure<QianDataConnectionOptions>(options =>
        {
            options.Egress.BlockedNetworks.Clear();
            options.Egress.BlockedNetworks.Add("::1/128");
        }));
        Assert.Equal(["::1/128"], before.BlockedNetworks);
        Assert.False(before.Permits(IPAddress.IPv6Loopback));
        Assert.True(before.Permits(IPAddress.Loopback));

        var after = Configured(Environments.Development,
            after: services => services.Configure<QianDataConnectionOptions>(options => options.Egress.BlockedNetworks.Add("127.0.0.0/8")));
        Assert.False(after.Permits(IPAddress.Loopback));
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

    /// <summary>
    /// 按宿主环境装配数据连接档案，取出生效的出网约束。environment 为 null 表示宿主没有登记 IHostEnvironment；
    /// before / after 在 AddQianDataConnections 之前 / 之后登记服务。
    /// </summary>
    private static DataConnectionEgressOptions Configured(string? environment, Action<IServiceCollection>? before = null,
        Action<IServiceCollection>? after = null, Action<QianDataConnectionOptions>? options = null)
    {
        var services = new ServiceCollection();
        if (environment is not null)
            services.AddSingleton<IHostEnvironment>(new HostingEnvironment { EnvironmentName = environment, ApplicationName = "qian-tests" });
        before?.Invoke(services);
        services.AddQianDataConnections((_, builder) => builder.UseNpgsql("Host=127.0.0.1"), options);
        after?.Invoke(services);
        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<QianDataConnectionOptions>>().Value.Egress;
    }
}
