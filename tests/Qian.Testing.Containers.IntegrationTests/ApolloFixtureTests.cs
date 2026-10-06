using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Qian.Testing.Containers.IntegrationTests;

/// <summary>以 Apollo 客户端的视角验证协议替身：拉配置、304、长轮询、访问密钥签名、Meta Server。</summary>
[Collection(SharedInfrastructure.Name)]
public sealed class ApolloFixtureTests(InfrastructureFixture infrastructure) : IDisposable
{
    private const string Cluster = ApolloFixture.DefaultCluster;
    private readonly ApolloFixture _apollo = infrastructure.Apollo;
    private readonly HttpClient _client = new() { Timeout = TimeSpan.FromSeconds(90) };
    private readonly string _appId = $"qian-test-{Guid.NewGuid():N}";

    public void Dispose() => _client.Dispose();

    [Fact]
    public async Task Published_namespace_is_served_and_unchanged_release_returns_304()
    {
        var ct = TestContext.Current.CancellationToken;
        var release = await _apollo.PublishAsync(_appId, "qian.plugin.sample", Config(("pageSize", "20"), ("title", "乾")), cancellationToken: ct);

        using var first = await _client.GetAsync(ConfigsUri("qian.plugin.sample"), ct);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await first.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal(release.ReleaseKey, body.GetProperty("releaseKey").GetString());
        Assert.Equal("20", body.GetProperty("configurations").GetProperty("pageSize").GetString());
        Assert.Equal("乾", body.GetProperty("configurations").GetProperty("title").GetString());

        using var unchanged = await _client.GetAsync(ConfigsUri("qian.plugin.sample", $"releaseKey={release.ReleaseKey}"), ct);
        Assert.Equal(HttpStatusCode.NotModified, unchanged.StatusCode);

        using var json = await _client.GetAsync(At($"configfiles/json/{_appId}/{Cluster}/qian.plugin.sample"), ct);
        var flat = await json.Content.ReadFromJsonAsync<Dictionary<string, string>>(ct);
        Assert.Equal("20", flat!["pageSize"]);
    }

    [Fact]
    public async Task Unpublished_or_deleted_namespace_returns_404()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var missing = await _client.GetAsync(ConfigsUri("application"), ct))
        {
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }

        await _apollo.PublishAsync(_appId, "application", Config(("k", "v")), cancellationToken: ct);
        Assert.True(await _apollo.DeleteAsync(_appId, "application", cancellationToken: ct));
        Assert.False(await _apollo.DeleteAsync(_appId, "application", cancellationToken: ct));

        using var deleted = await _client.GetAsync(ConfigsUri("application"), ct);
        Assert.Equal(HttpStatusCode.NotFound, deleted.StatusCode);
    }

    [Fact]
    public async Task Long_poll_returns_at_once_when_client_is_behind_and_wakes_on_publish()
    {
        var ct = TestContext.Current.CancellationToken;
        var first = await _apollo.PublishAsync(_appId, "application", Config(("pageSize", "10")), cancellationToken: ct);

        // 客户端首次带 -1：已有发布，立即返回当前通知号。
        using (var behind = await _client.GetAsync(NotificationsUri(("application", -1)), ct))
        {
            Assert.Equal(HttpStatusCode.OK, behind.StatusCode);
            var items = await behind.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.Equal(first.NotificationId, items[0].GetProperty("notificationId").GetInt64());
        }

        // 客户端已是最新：挂起；挂起期间发布新版本，长轮询应立即返回新通知号（模拟「在 Apollo 修改后宿主感知」）。
        var poll = _client.GetAsync(NotificationsUri(("application.properties", first.NotificationId)), ct);
        await Task.Delay(TimeSpan.FromMilliseconds(300), ct);
        Assert.False(poll.IsCompleted);

        var second = await _apollo.PublishAsync(_appId, "application", Config(("pageSize", "50")), cancellationToken: ct);
        using var woke = await poll.WaitAsync(TimeSpan.FromSeconds(10), ct);
        Assert.Equal(HttpStatusCode.OK, woke.StatusCode);
        var changed = await woke.Content.ReadFromJsonAsync<JsonElement>(ct);
        Assert.Equal("application.properties", changed[0].GetProperty("namespaceName").GetString());
        Assert.Equal(second.NotificationId, changed[0].GetProperty("notificationId").GetInt64());
        Assert.NotEqual(first.ReleaseKey, second.ReleaseKey);
    }

    [Fact]
    public async Task Long_poll_returns_304_when_nothing_changes_within_hold()
    {
        var ct = TestContext.Current.CancellationToken;
        var release = await _apollo.PublishAsync(_appId, "application", Config(("k", "v")), cancellationToken: ct);
        await _apollo.SetHoldAsync(TimeSpan.FromSeconds(1), ct);
        try
        {
            using var response = await _client.GetAsync(NotificationsUri(("application", release.NotificationId)), ct);
            Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        }
        finally
        {
            await _apollo.SetHoldAsync(TimeSpan.FromSeconds(60), ct);
        }
    }

    [Fact]
    public async Task Access_key_requires_valid_hmac_signature()
    {
        var ct = TestContext.Current.CancellationToken;
        const string secret = "qian-test-access-key";
        await _apollo.PublishAsync(_appId, "application", Config(("k", "v")), cancellationToken: ct);
        await _apollo.SetAccessKeyAsync(_appId, secret, ct);
        try
        {
            var uri = ConfigsUri("application", "ip=10.0.0.1");
            using (var unsigned = await _client.GetAsync(uri, ct))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, unsigned.StatusCode);
            }

            using (var wrongSecret = await _client.SendAsync(Signed(uri, "not-the-secret"), ct))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
            }

            using (var stale = await _client.SendAsync(Signed(uri, secret, DateTimeOffset.UtcNow.AddMinutes(-5)), ct))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, stale.StatusCode);
            }

            using var signed = await _client.SendAsync(Signed(uri, secret), ct);
            Assert.Equal(HttpStatusCode.OK, signed.StatusCode);
        }
        finally
        {
            await _apollo.ClearAccessKeyAsync(_appId, ct);
        }
    }

    [Fact]
    public async Task Meta_server_points_to_the_config_service()
    {
        var ct = TestContext.Current.CancellationToken;
        var services = await _client.GetFromJsonAsync<JsonElement>(At("services/config"), ct);
        var homepage = new Uri(services[0].GetProperty("homepageUrl").GetString()!);
        Assert.Equal(_apollo.ConfigServiceUri.Port, homepage.Port);
    }

    private static Dictionary<string, string> Config(params (string Key, string Value)[] items) =>
        items.ToDictionary(item => item.Key, item => item.Value);

    private Uri At(string relative) => new(_apollo.ConfigServiceUri, relative);

    private Uri ConfigsUri(string namespaceName, string? query = null) =>
        At($"configs/{_appId}/{Cluster}/{namespaceName}" + (query is null ? string.Empty : "?" + query));

    private Uri NotificationsUri(params (string Namespace, long NotificationId)[] notifications)
    {
        var payload = JsonSerializer.Serialize(notifications.Select(n => new { namespaceName = n.Namespace, notificationId = n.NotificationId }));
        return At($"notifications/v2?appId={_appId}&cluster={Cluster}&notifications={Uri.EscapeDataString(payload)}");
    }

    /// <summary>按 Apollo 访问密钥规则签名：Base64(HMAC-SHA1(secret, timestamp + "\n" + pathWithQuery))。</summary>
    private HttpRequestMessage Signed(Uri uri, string secret, DateTimeOffset? at = null)
    {
        var timestamp = (at ?? DateTimeOffset.UtcNow).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
#pragma warning disable CA5350 // Apollo 协议规定签名算法为 HMAC-SHA1
        var signature = Convert.ToBase64String(HMACSHA1.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}\n{uri.PathAndQuery}")));
#pragma warning restore CA5350
        var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.Add("Timestamp", timestamp);
        request.Headers.TryAddWithoutValidation("Authorization", $"Apollo {_appId}:{signature}");
        return request;
    }
}
