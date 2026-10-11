using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Qian.Testing.Containers;

/// <summary>真实 Apollo 2.5.2 ConfigService/AdminService + MySQL 8，供 CFG 的最终集成验收。</summary>
public sealed class RealApolloFixture : ContainerFixture<IContainer>
{
    private INetwork? _network;
    private IContainer? _mysql;
    private IContainer? _admin;
    private HttpClient? _control;
    private readonly string _databasePassword = NewSecret();
    private readonly string _rootPassword = NewSecret();

    /// <summary>真实 ConfigService 地址。</summary>
    public Uri ConfigServiceUri { get; private set; } = new("http://localhost/");

    /// <inheritdoc />
    protected override async ValueTask OnStartingAsync(CancellationToken cancellationToken)
    {
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync(cancellationToken).ConfigureAwait(false);
        _mysql = new ContainerBuilder(Image("QIAN_TEST_IMAGE_APOLLO_MYSQL", "mysql:8.0.43"))
            .WithNetwork(_network).WithNetworkAliases("apollo-mysql")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", _rootPassword)
            .WithEnvironment("MYSQL_DATABASE", "ApolloConfigDB")
            .WithEnvironment("MYSQL_USER", "apollo").WithEnvironment("MYSQL_PASSWORD", _databasePassword)
            .WithResourceMapping(ReadSql(), "/docker-entrypoint-initdb.d/apolloconfigdb.sql")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("port: 3306  MySQL Community Server"))
            .Build();
        await _mysql.StartAsync(cancellationToken).ConfigureAwait(false);
        _admin = Service(Image("QIAN_TEST_IMAGE_APOLLO_ADMIN", "apolloconfig/apollo-adminservice:2.5.2"), 8090, "apollo-admin").Build();
        await _admin.StartAsync(cancellationToken).ConfigureAwait(false);
        _control = new HttpClient { BaseAddress = Address(_admin, 8090), Timeout = TimeSpan.FromSeconds(30) };
    }

    /// <inheritdoc />
    protected override IContainer CreateContainer() =>
        Service(Image("QIAN_TEST_IMAGE_APOLLO_CONFIG", "apolloconfig/apollo-configservice:2.5.2"), 8080, "apollo-config").Build();

    private ContainerBuilder Service(string image, ushort port, string alias) => new ContainerBuilder(image)
        .WithNetwork(_network!).WithNetworkAliases(alias)
        .WithEnvironment("SPRING_DATASOURCE_URL", "jdbc:mysql://apollo-mysql:3306/ApolloConfigDB?characterEncoding=utf8&useSSL=false&allowPublicKeyRetrieval=true&serverTimezone=UTC")
        .WithEnvironment("SPRING_DATASOURCE_USERNAME", "apollo").WithEnvironment("SPRING_DATASOURCE_PASSWORD", _databasePassword)
        .WithEnvironment("JAVA_OPTS", "-Xms128m -Xmx384m")
        // ConfigService 自带 Eureka Server，不能禁用其必需的 Client bean；只关闭外部注册与拉取。
        .WithEnvironment("EUREKA_CLIENT_REGISTERWITHEUREKA", "false")
        .WithEnvironment("EUREKA_CLIENT_FETCHREGISTRY", "false")
        .WithPortBinding(port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(port).ForPath("/health"),
            wait => wait.WithTimeout(TimeSpan.FromMinutes(3))));

    /// <inheritdoc />
    protected override ValueTask OnStartedAsync(IContainer container, CancellationToken cancellationToken)
    {
        ConfigServiceUri = Address(container, 8080);
        return ValueTask.CompletedTask;
    }

    /// <summary>通过真实 AdminService 创建应用及默认 application namespace。</summary>
    public async Task CreateAppAsync(string appId, CancellationToken cancellationToken = default)
    {
        using var response = await Control.PostAsJsonAsync("apps", new
        {
            appId, name = appId, orgId = "qian", orgName = "Qian", ownerName = "qian-tests",
            ownerEmail = "tests@example.invalid", dataChangeCreatedBy = "qian-tests", dataChangeLastModifiedBy = "qian-tests"
        }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>编辑真实配置项并正式发布，触发 ReleaseMessage 与 ConfigService 长轮询。</summary>
    public async Task PublishAsync(string appId, string key, string value, CancellationToken cancellationToken = default)
    {
        var root = $"apps/{Uri.EscapeDataString(appId)}/clusters/default/namespaces/application/";
        using var namespaceResponse = await Control.GetAsync(root.TrimEnd('/'), cancellationToken).ConfigureAwait(false);
        namespaceResponse.EnsureSuccessStatusCode();
        var namespaceData = await namespaceResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        var namespaceId = namespaceData.GetProperty("id").GetInt64();
        using var existing = await Control.GetAsync(root + "items/" + Uri.EscapeDataString(key), cancellationToken).ConfigureAwait(false);
        long? id = null;
        if (existing.IsSuccessStatusCode)
        {
            var data = await existing.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
            id = data.GetProperty("id").GetInt64();
        }
        else if (existing.StatusCode != System.Net.HttpStatusCode.NotFound) existing.EnsureSuccessStatusCode();
        var item = new { id = id ?? 0, namespaceId, key, value, comment = "Qian acceptance", lineNum = 1,
            dataChangeCreatedBy = "qian-tests", dataChangeLastModifiedBy = "qian-tests" };
        using var update = id is null
            ? await Control.PostAsJsonAsync(root + "items", item, cancellationToken).ConfigureAwait(false)
            : await Control.PutAsJsonAsync(root + "items/" + id.Value.ToString(System.Globalization.CultureInfo.InvariantCulture), item, cancellationToken).ConfigureAwait(false);
        if (!update.IsSuccessStatusCode)
            throw new InvalidOperationException("Apollo 配置项更新失败：" + await update.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
        using var release = await Control.PostAsync(root + "releases?name=qian-" + Guid.NewGuid().ToString("N") + "&operator=qian-tests", null, cancellationToken).ConfigureAwait(false);
        if (!release.IsSuccessStatusCode)
            throw new InvalidOperationException("Apollo 发布失败：" + await release.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
    }

    /// <inheritdoc />
    protected override async ValueTask OnDisposingAsync()
    {
        Interlocked.Exchange(ref _control, null)?.Dispose();
        var admin = Interlocked.Exchange(ref _admin, null);
        if (admin is not null) await admin.DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    protected override async ValueTask OnDisposedAsync()
    {
        var mysql = Interlocked.Exchange(ref _mysql, null);
        try { if (mysql is not null) await mysql.DisposeAsync().ConfigureAwait(false); }
        finally
        {
            var network = Interlocked.Exchange(ref _network, null);
            if (network is not null) await network.DisposeAsync().ConfigureAwait(false);
        }
    }

    private HttpClient Control => _control ?? throw new InvalidOperationException("Apollo AdminService 尚未启动。");
    private static Uri Address(IContainer container, ushort port) => new UriBuilder("http", container.Hostname, container.GetMappedPublicPort(port)).Uri;
    private static string Image(string name, string fallback) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } image ? image : fallback;
    private static byte[] ReadSql()
    {
        using var stream = typeof(RealApolloFixture).Assembly.GetManifestResourceStream("Qian.Testing.Containers.ApolloReal.apolloconfigdb.sql")
            ?? throw new InvalidOperationException("缺少 Apollo 原版数据库脚本。");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
    }
}
