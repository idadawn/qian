using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Qian.Testing.Containers;

/// <summary>真实 Apollo 2.5.2 ConfigService/AdminService + MySQL 8，供 CFG 的最终集成验收。</summary>
public class RealApolloFixture : ContainerFixture<IContainer>
{
    private INetwork? _network;
    private IContainer? _mysql;
    private IContainer? _admin;
    private HttpClient? _control;
    private IContainer? _portal;
    /// <summary>仅管理端验收子类启动 Portal，普通 Config fixture 保持三个容器。</summary>
    protected virtual bool EnablePortal => false;
    /// <summary>隔离 PortalDB 的最小种子；不能包含可复用固定凭据。</summary>
    protected virtual string PortalSeed => "";
    /// <summary>仅可选 Portal 启动后有效。</summary>
    public Uri PortalUri { get; private set; } = new("http://localhost/");
    private readonly string _databasePassword = NewSecret();
    private readonly string _rootPassword = NewSecret();

    /// <summary>真实 ConfigService 地址。</summary>
    public Uri ConfigServiceUri { get; private set; } = new("http://localhost/");

    /// <inheritdoc />
    protected override async ValueTask OnStartingAsync(CancellationToken cancellationToken)
    {
        _network = new NetworkBuilder().Build();
        await _network.CreateAsync(cancellationToken).ConfigureAwait(false);
        var mysql = new ContainerBuilder(Image("QIAN_TEST_IMAGE_APOLLO_MYSQL", "mysql:8.0.43"))
            .WithNetwork(_network).WithNetworkAliases("apollo-mysql")
            .WithEnvironment("MYSQL_ROOT_PASSWORD", _rootPassword)
            .WithEnvironment("MYSQL_DATABASE", "ApolloConfigDB")
            .WithEnvironment("MYSQL_USER", "apollo").WithEnvironment("MYSQL_PASSWORD", _databasePassword)
            .WithResourceMapping(ReadSql("apolloconfigdb.sql"), "/docker-entrypoint-initdb.d/10-apolloconfigdb.sql")
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("port: 3306  MySQL Community Server"))
            ;
        if (EnablePortal) mysql = mysql
            .WithResourceMapping(ReadSql("apolloportaldb.sql"), "/docker-entrypoint-initdb.d/20-apolloportaldb.sql")
            .WithResourceMapping(System.Text.Encoding.UTF8.GetBytes("USE ApolloPortalDB; GRANT ALL ON ApolloPortalDB.* TO 'apollo'@'%';\n" + PortalSeed),
                "/docker-entrypoint-initdb.d/30-portal-seed.sql");
        _mysql = mysql.Build();
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
        .WithEnvironment("EUREKA_CLIENT_REGISTERWITHEUREKA", EnablePortal && alias == "apollo-admin" ? "true" : "false")
        .WithEnvironment("EUREKA_CLIENT_SERVICEURL_DEFAULTZONE", "http://apollo-config:8080/eureka/")
        .WithEnvironment("EUREKA_INSTANCE_HOSTNAME", alias)
        .WithEnvironment("EUREKA_INSTANCE_PREFERIPADDRESS", "false")
        .WithEnvironment("EUREKA_CLIENT_FETCHREGISTRY", EnablePortal ? "true" : "false")
        .WithPortBinding(port, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(port).ForPath("/health"),
            wait => wait.WithTimeout(TimeSpan.FromMinutes(3))));

    /// <inheritdoc />
    protected override async ValueTask OnStartedAsync(IContainer container, CancellationToken cancellationToken)
    {
        ConfigServiceUri = Address(container, 8080);
        if (!EnablePortal) return;
        using var meta = new HttpClient { BaseAddress = ConfigServiceUri };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(90);
        while (true)
        {
            using var response = await meta.GetAsync("services/admin?appId=qian-flags-fixture", cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false)).Contains("apollo-admin", StringComparison.Ordinal)) break;
            if (DateTimeOffset.UtcNow > deadline) throw new InvalidOperationException("真实 Apollo AdminService 未通过 Eureka 出现在 Meta 服务发现中。");
            await Task.Delay(500, cancellationToken).ConfigureAwait(false);
        }
        _portal = new ContainerBuilder(Image("QIAN_TEST_IMAGE_APOLLO_PORTAL", "apolloconfig/apollo-portal:2.5.2"))
            .WithNetwork(_network!).WithNetworkAliases("apollo-portal")
            .WithEnvironment("SPRING_DATASOURCE_URL", "jdbc:mysql://apollo-mysql:3306/ApolloPortalDB?characterEncoding=utf8&useSSL=false&allowPublicKeyRetrieval=true&serverTimezone=UTC")
            .WithEnvironment("SPRING_DATASOURCE_USERNAME", "apollo").WithEnvironment("SPRING_DATASOURCE_PASSWORD", _databasePassword)
            .WithEnvironment("JAVA_OPTS", "-Xms128m -Xmx384m -Dapollo.portal.envs=UAT -Duat.meta=http://apollo-config:8080")
            .WithPortBinding(8070, assignRandomHostPort: true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(8070).ForPath("/health"),
                wait => wait.WithTimeout(TimeSpan.FromMinutes(3)))).Build();
        await _portal.StartAsync(cancellationToken).ConfigureAwait(false);
        PortalUri = Address(_portal, 8070);
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

    /// <summary>在真实 AdminService 创建 properties Namespace，可用公共 Namespace 验证跨应用读取。</summary>
    public async Task CreateNamespaceAsync(string appId, string namespaceName, bool isPublic = false, CancellationToken cancellationToken = default)
    {
        using var response = await Control.PostAsJsonAsync($"apps/{Uri.EscapeDataString(appId)}/appnamespaces", new
        {
            appId, name = namespaceName, format = "properties", isPublic, comment = "Qian acceptance",
            dataChangeCreatedBy = "qian-tests", dataChangeLastModifiedBy = "qian-tests"
        }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>让测试应用关联已存在的公共 Namespace。</summary>
    public async Task AssociateNamespaceAsync(string appId, string namespaceName, CancellationToken cancellationToken = default)
    {
        using var response = await Control.PostAsJsonAsync($"apps/{Uri.EscapeDataString(appId)}/clusters/default/namespaces", new
        {
            appId, clusterName = "default", namespaceName,
            dataChangeCreatedBy = "qian-tests", dataChangeLastModifiedBy = "qian-tests"
        }, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <summary>编辑 application 配置项并正式发布，触发 ReleaseMessage 与 ConfigService 长轮询。</summary>
    public Task PublishAsync(string appId, string key, string value, CancellationToken cancellationToken = default) =>
        PublishNamespaceAsync(appId, "application", key, value, cancellationToken);

    /// <summary>编辑指定 Namespace 的真实配置项并正式发布；仅供隔离测试，不是生产管理写端。</summary>
    public async Task PublishNamespaceAsync(string appId, string namespaceName, string key, string value, CancellationToken cancellationToken = default)
    {
        var root = $"apps/{Uri.EscapeDataString(appId)}/clusters/default/namespaces/{Uri.EscapeDataString(namespaceName)}/";
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

    /// <summary>等待真实 ConfigService 的 namespace 缓存看见已发布值；不是固定延时，也不重试业务断言。</summary>
    public async Task WaitForPublishedAsync(string appId, string namespaceName, string key, string expected, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = ConfigServiceUri, Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (true)
        {
            using var response = await client.GetAsync("configs/" + Uri.EscapeDataString(appId) + "/default/" + Uri.EscapeDataString(namespaceName), cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var document = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
                if (document.GetProperty("configurations").TryGetProperty(key, out var value) && value.GetString() == expected) return;
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NotFound) response.EnsureSuccessStatusCode();
            if (DateTimeOffset.UtcNow > deadline) throw new InvalidOperationException("真实 ConfigService 在 30 秒内未看见目标发布值。");
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    protected override async ValueTask OnDisposingAsync()
    {
        Interlocked.Exchange(ref _control, null)?.Dispose();
        var portal = Interlocked.Exchange(ref _portal, null);
        if (portal is not null) await portal.DisposeAsync().ConfigureAwait(false);
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
    private static byte[] ReadSql(string name)
    {
        using var stream = typeof(RealApolloFixture).Assembly.GetManifestResourceStream("Qian.Testing.Containers.ApolloReal." + name)
            ?? throw new InvalidOperationException("缺少 Apollo 原版数据库脚本。");
        using var buffer = new MemoryStream(); stream.CopyTo(buffer); return buffer.ToArray();
    }
}
