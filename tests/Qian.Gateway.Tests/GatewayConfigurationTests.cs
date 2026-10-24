using System.Text.Json;
using System.Text.Json.Nodes;
using Json.Schema;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Qian.Gateway.Tests;

public sealed class GatewayConfigurationTests
{
    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("proxy")]
    [InlineData("cluster")]
    [InlineData("anonymous")]
    [InlineData("duplicate")]
    [InlineData("key")]
    public void 配置不能放宽发行方代理网段路由与密钥边界(string violation)
    {
        var node = JsonNode.Parse(Valid)!.AsObject();
        switch (violation)
        {
            case "issuer": node["identityIssuer"] = "https://user:password@id.qian.test/"; break;
            case "audience": node["identityAudience"] = ""; break;
            case "proxy": node["trustedProxyNetworks"] = new JsonArray("0.0.0.0/0"); break;
            case "cluster": node["routes"]![0]!["cluster"] = "unregistered"; break;
            case "anonymous": node["routes"]![0]!["authorizationPolicy"] = "Anonymous"; break;
            case "duplicate": node["clusters"]!.AsArray().Add(node["clusters"]![0]!.DeepClone()); break;
            case "key": node["signingKeyPath"] = "relative-private.pem"; break;
        }
        using var document = JsonDocument.Parse(node.ToJsonString());
        if (violation == "anonymous") Assert.Throws<JsonException>(() => GatewayHosting.ReadConfiguration(document.RootElement));
        else Assert.Throws<ArgumentException>(() => GatewayHosting.ReadConfiguration(document.RootElement));
    }

    [Fact]
    public void 省略限流配置或只改一个限额时保留其余缺省值()
    {
        using var original = JsonDocument.Parse(Valid);
        Assert.Equal(new GatewayRateLimits(), GatewayHosting.ReadConfiguration(original.RootElement).RateLimits);
        var node = JsonNode.Parse(Valid)!; node["rateLimits"] = new JsonObject { ["tenantPermitLimit"] = 25 };
        using var partial = JsonDocument.Parse(node.ToJsonString());
        Assert.Equal(new GatewayRateLimits { TenantPermitLimit = 25 }, GatewayHosting.ReadConfiguration(partial.RootElement).RateLimits);
    }

    [Fact]
    public void 路由策略必须显式声明且生成的每条Yarp路由保留策略()
    {
        using var document = JsonDocument.Parse(Valid);
        var configuration = GatewayHosting.ReadConfiguration(document.RootElement);
        var route = Assert.Single(configuration.Routes);
        var policies = new[] { "kun-default", "tenant", "user" };
        var services = new ServiceCollection().AddLogging().AddQianGateway(configuration with
        { Routes = policies.Select(policy => route with { Id = policy, RateLimiterPolicy = policy }).ToArray() });
        using var provider = services.BuildServiceProvider();
        var generated = provider.GetRequiredService<IProxyConfigProvider>().GetConfig().Routes;
        Assert.Equal(policies, generated.Select(item => item.RateLimiterPolicy));
        Assert.All(generated, item => Assert.Equal("Default", item.AuthorizationPolicy));
        var empty = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => empty.AddQianGateway(configuration with
        { Routes = [new GatewayRoute("missing", "/missing", "qian-platform")] }));
        Assert.Empty(empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("disable")]
    [InlineData("unknown")]
    public void 缺失或未知路由策略在Schema和宿主构建前均失败(string? policy)
    {
        var node = JsonNode.Parse(Valid)!;
        if (policy is null) node["routes"]![0]!.AsObject().Remove("rateLimiterPolicy");
        else node["routes"]![0]!["rateLimiterPolicy"] = policy;
        using var value = JsonDocument.Parse(node.ToJsonString());
        var metadata = new GatewayComponentQianCodeMetadata().Create();
        Assert.False(JsonSchema.FromText(metadata.ConfigSchema!.Value.GetRawText()).Evaluate(value.RootElement).IsValid);
        Assert.Throws<ArgumentException>(() => GatewayHosting.ReadConfiguration(value.RootElement));
    }

    [Theory]
    [InlineData("ipPermitLimit", "0")]
    [InlineData("tenantPermitLimit", "-1")]
    [InlineData("userPermitLimit", "1000001")]
    [InlineData("windowSeconds", "0")]
    [InlineData("windowSeconds", "86401")]
    [InlineData("keyPrefix", "\"bad prefix\"")]
    public void 限额与命名空间在Schema和宿主中均校验(string name, string raw)
    {
        var node = JsonNode.Parse(Valid)!; node["rateLimits"] = new JsonObject { [name] = JsonNode.Parse(raw) };
        using var value = JsonDocument.Parse(node.ToJsonString());
        var metadata = new GatewayComponentQianCodeMetadata().Create();
        Assert.False(JsonSchema.FromText(metadata.ConfigSchema!.Value.GetRawText()).Evaluate(value.RootElement).IsValid);
        Assert.Throws<ArgumentException>(() => GatewayHosting.ReadConfiguration(value.RootElement));
    }

    [Fact]
    public void 配置拒绝重复成员并保留单一受众和空信任列表()
    {
        using var value = JsonDocument.Parse(Valid);
        var configuration = GatewayHosting.ReadConfiguration(value.RootElement);
        Assert.Empty(configuration.TrustedProxyNetworks); Assert.Equal("qian-platform", Assert.Single(configuration.Clusters).Id);
        using var duplicate = JsonDocument.Parse(Valid.Replace("\"issuer\":", "\"issuer\":\"https://wrong.qian.test\",\"issuer\":", StringComparison.Ordinal));
        Assert.Throws<ArgumentException>(() => GatewayHosting.ReadConfiguration(duplicate.RootElement));
    }

    private const string Valid = """
    {"identityIssuer":"https://id.qian.test/","identityAudience":"https://api.qian.test","issuer":"https://gateway.qian.test/s2s",
     "signingKeyPath":"/tmp/private.pem","publicKeyPath":"/tmp/public.pem","trustedProxyNetworks":[],
     "routes":[{"id":"platform","path":"/api/{**rest}","cluster":"qian-platform","rateLimiterPolicy":"tenant"}],"clusters":[{"id":"qian-platform","address":"http://platform:8080/"}]}
    """;
}
