using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Org.BouncyCastle.Crypto.Generators;

namespace Qian.Testing.Containers;

/// <summary>
/// 独立实现的外部 OIDC 发行方（CNCF Dex），用来验证依赖方能对接 Qian 之外的任意发行方。内存存储、本地密码库里一个用户、一个启用 PKCE 的公开客户端，
/// 跳过授权确认页。Dex 的 issuer 必须在启动前写进配置，所以宿主端口与 RP 回调端口都在启动前选定；口令每次运行随机生成，配置里只有 bcrypt 哈希。
/// </summary>
public sealed class DexFixture : ContainerFixture<IContainer>
{
    /// <summary>登记在 Dex 的公开客户端标识。</summary>
    public const string ClientId = "qian-standalone";

    /// <summary>本地密码库里的用户邮箱（Dex 的登录名）。</summary>
    public const string Email = "alice@example.test";

    private const ushort ContainerPort = 5556;
    private const string ConfigPath = "/etc/dex/qian.yaml";
    private int _hostPort;

    /// <summary>Dex 的 issuer（discovery 位于其下的 <c>/.well-known/openid-configuration</c>）。</summary>
    public Uri Issuer => new($"http://127.0.0.1:{_hostPort}/dex");

    /// <summary>登记给客户端的唯一回调地址：测试里的依赖方须在这个端口以 HTTPS 监听。</summary>
    public Uri RedirectUri { get; private set; } = new("https://localhost/");

    /// <summary>用户口令，每次运行随机生成。</summary>
    public string Password { get; } = NewSecret();

    /// <inheritdoc />
    protected override ValueTask OnStartingAsync(CancellationToken cancellationToken)
    {
        _hostPort = FreePort();
        RedirectUri = new Uri($"https://localhost:{FreePort()}/auth/callback");
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    protected override IContainer CreateContainer() =>
        new ContainerBuilder(ContainerImages.Dex)
            .WithResourceMapping(Encoding.UTF8.GetBytes(Configuration()), ConfigPath)
            .WithEntrypoint("/usr/local/bin/dex")
            .WithCommand("serve", ConfigPath)
            .WithPortBinding(_hostPort, ContainerPort)
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilHttpRequestIsSucceeded(request => request.ForPort(ContainerPort).ForPath("/dex/.well-known/openid-configuration")))
            .Build();

    private string Configuration()
    {
        // Dex 只接受 cost ≥ 10 的 bcrypt 哈希。
        var hash = OpenBsdBCrypt.Generate(Password.ToCharArray(), RandomNumberGenerator.GetBytes(16), 10);
        return string.Create(CultureInfo.InvariantCulture, $"""
            issuer: {Issuer}
            storage:
              type: memory
            web:
              http: 0.0.0.0:{ContainerPort}
            oauth2:
              skipApprovalScreen: true
              responseTypes: ["code"]
            enablePasswordDB: true
            staticPasswords:
            - email: "{Email}"
              hash: "{hash}"
              username: "alice"
              userID: "{Guid.NewGuid():D}"
            staticClients:
            - id: {ClientId}
              name: "Qian standalone"
              public: true
              redirectURIs:
              - "{RedirectUri}"
            """);
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }
}
