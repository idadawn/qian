using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Qian.Testing.Containers;

/// <summary>
/// Apollo 配置服务的协议替身（PRD §14.2：集成测试层的 Apollo 用 mock server）。
/// 容器内运行 <c>ApolloMock/apollo_mock.py</c>：实现客户端用到的 <c>/configs</c>、<c>/configfiles/json</c>、
/// <c>/notifications/v2</c> 长轮询、Meta Server 与访问密钥签名；测试经本类的方法发布或撤下配置。
/// 覆盖范围见脚本文件头注释与 tests/README.md。
/// </summary>
public sealed class ApolloFixture : ContainerFixture<IContainer>
{
    /// <summary>默认集群名。</summary>
    public const string DefaultCluster = "default";

    private const ushort ContainerPort = 8080;
    private const string ScriptPath = "/opt/apollo-mock/apollo_mock.py";
    private const string ScriptResource = "Qian.Testing.Containers.ApolloMock.apollo_mock.py";

    private HttpClient? _control;

    /// <summary>Config Service 地址（同时也是 Meta Server 地址），即客户端的 <c>Apollo__ConfigServer__0</c>。</summary>
    public Uri ConfigServiceUri { get; private set; } = new("http://localhost/");

    private HttpClient Control =>
        _control ?? throw new InvalidOperationException($"{nameof(ApolloFixture)} 尚未启动。");

    /// <summary>发布一个 namespace 的新版本：生成新的 releaseKey 并唤醒正在长轮询的客户端。</summary>
    public async Task<ApolloRelease> PublishAsync(
        string appId,
        string namespaceName,
        IReadOnlyDictionary<string, string> configurations,
        string cluster = DefaultCluster,
        CancellationToken cancellationToken = default)
    {
        using var response = await Control
            .PutAsJsonAsync(ConfigsPath(appId, cluster, namespaceName), configurations, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        return new ApolloRelease(
            body.GetProperty("releaseKey").GetString() ?? string.Empty,
            body.GetProperty("notificationId").GetInt64());
    }

    /// <summary>撤下一个 namespace：之后拉取返回 404，长轮询的客户端收到变更通知。不存在时返回 false。</summary>
    public async Task<bool> DeleteAsync(
        string appId,
        string namespaceName,
        string cluster = DefaultCluster,
        CancellationToken cancellationToken = default)
    {
        using var response = await Control
            .DeleteAsync(ConfigsPath(appId, cluster, namespaceName), cancellationToken)
            .ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    /// <summary>为 appId 开启访问密钥校验（Apollo AccessKey，HMAC-SHA1 签名）。</summary>
    public async Task SetAccessKeyAsync(string appId, string secret, CancellationToken cancellationToken = default)
    {
        using var response = await Control
            .PutAsJsonAsync($"__mock/access-keys/{Uri.EscapeDataString(appId)}", new { secret }, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>关闭 appId 的访问密钥校验。</summary>
    public async Task ClearAccessKeyAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var response = await Control
            .DeleteAsync($"__mock/access-keys/{Uri.EscapeDataString(appId)}", cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>调整长轮询挂起时长（Apollo 服务端为 60 秒）。测试「超时返回 304」时可调短。</summary>
    public async Task SetHoldAsync(TimeSpan hold, CancellationToken cancellationToken = default)
    {
        using var response = await Control
            .PutAsJsonAsync("__mock/settings", new { holdSeconds = hold.TotalSeconds }, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>清空全部配置、通知与访问密钥，长轮询时长恢复为 60 秒。</summary>
    public async Task ResetAsync(CancellationToken cancellationToken = default)
    {
        using var response = await Control.PostAsync("__mock/reset", content: null, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    protected override IContainer CreateContainer() =>
        new ContainerBuilder(ContainerImages.ApolloMock)
            .WithResourceMapping(ReadScript(), ScriptPath)
            .WithCommand("python3", "-u", ScriptPath, "--port", ContainerPort.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .WithPortBinding(ContainerPort, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(ContainerPort).ForPath("/__mock/health")))
            .Build();

    /// <inheritdoc />
    protected override ValueTask OnStartedAsync(IContainer container, CancellationToken cancellationToken)
    {
        ConfigServiceUri = new UriBuilder(Uri.UriSchemeHttp, container.Hostname, container.GetMappedPublicPort(ContainerPort)).Uri;
        _control = new HttpClient { BaseAddress = ConfigServiceUri };
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override ValueTask OnDisposingAsync()
    {
        Interlocked.Exchange(ref _control, null)?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string ConfigsPath(string appId, string cluster, string namespaceName) =>
        $"__mock/configs/{Uri.EscapeDataString(appId)}/{Uri.EscapeDataString(cluster)}/{Uri.EscapeDataString(namespaceName)}";

    private static byte[] ReadScript()
    {
        using var stream = typeof(ApolloFixture).Assembly.GetManifestResourceStream(ScriptResource)
            ?? throw new InvalidOperationException($"找不到嵌入资源 {ScriptResource}。");
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}

/// <summary>一次发布的结果。</summary>
/// <param name="ReleaseKey">本次发布的 releaseKey；客户端带上它拉取时，未变化返回 304。</param>
/// <param name="NotificationId">本次发布后该 namespace 的通知号，长轮询据此判断有无更新。</param>
public sealed record ApolloRelease(string ReleaseKey, long NotificationId);
