using System.Text.Json;
using System.Text.Json.Nodes;

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
     "routes":[{"id":"platform","path":"/api/{**rest}","cluster":"qian-platform"}],"clusters":[{"id":"qian-platform","address":"http://platform:8080/"}]}
    """;
}
