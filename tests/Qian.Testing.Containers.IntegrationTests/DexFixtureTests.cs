using System.Text.Json;

namespace Qian.Testing.Containers.IntegrationTests;

public sealed class DexFixtureTests(DexFixture dex) : IClassFixture<DexFixture>
{
    [Fact]
    public async Task Discovery_matches_the_configured_issuer_and_supports_PKCE_S256()
    {
        using var http = new HttpClient();
        using var discovery = JsonDocument.Parse(await http.GetStringAsync(new Uri(dex.Issuer + "/.well-known/openid-configuration"), TestContext.Current.CancellationToken));
        var root = discovery.RootElement;
        Assert.Equal(dex.Issuer.ToString(), root.GetProperty("issuer").GetString());
        Assert.Contains("S256", root.GetProperty("code_challenge_methods_supported").EnumerateArray().Select(item => item.GetString()));
        Assert.StartsWith(dex.Issuer.ToString(), root.GetProperty("authorization_endpoint").GetString(), StringComparison.Ordinal);
        Assert.Equal("https", dex.RedirectUri.Scheme);
    }
}
