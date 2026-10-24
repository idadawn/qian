using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Generators;

namespace Qian.Testing.Containers;

/// <summary>真实 Portal 2.5.2 + ConfigService/AdminService；编辑和发布使用不同的 namespace ACL。</summary>
public sealed class RealApolloPortalFixture : RealApolloFixture
{
    /// <summary>隔离容器内固定的应用标识。</summary>
    public string AppId { get; } = "qian-flags-acceptance";
    /// <summary>仅有 qian.common.flags/UAT 修改权限的随机 Token。</summary>
    public string EditToken { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    /// <summary>真实但无任何权限的随机 Token。</summary>
    public string UnauthorizedToken { get; } = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    private readonly string _publishToken = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
    /// <inheritdoc />
    protected override bool EnablePortal => true;
    /// <inheritdoc />
    protected override string PortalSeed
    {
        get
        {
            var password = OpenBsdBCrypt.Generate(NewSecret().ToCharArray(), RandomNumberGenerator.GetBytes(16), 10);
            // 官方 RoleUtils + ConfigConsts.CLUSTER_NAMESPACE_SEPARATOR("+")，权限绑定到环境和 namespace。
            // 服务发现走数据库（RealApolloFixture 的 database-discovery），不需要改写 Eureka 地址。
            return $$"""
                UPDATE Users SET Password='{{password}}' WHERE Username='apollo';
                UPDATE ServerConfig SET Value='UAT' WHERE `Key`='apollo.portal.envs';
                UPDATE ServerConfig SET Value='{"UAT":"http://apollo-config:8080"}' WHERE `Key`='apollo.portal.meta.servers';
                INSERT INTO Consumer (Id,AppId,Name,OwnerName) VALUES (1,'flags-editor','flags editor','apollo'),(2,'flags-publisher','flags publisher','apollo'),(3,'no-access','no access','apollo');
                INSERT INTO ConsumerToken (ConsumerId,Token) VALUES (1,'{{EditToken}}'),(2,'{{_publishToken}}'),(3,'{{UnauthorizedToken}}');
                INSERT INTO Role (Id,RoleName) VALUES (1,'flags-editor'),(2,'flags-publisher');
                INSERT INTO Permission (Id,PermissionType,TargetId) VALUES (1,'ModifyNamespace','{{AppId}}+qian.common.flags+UAT'),(2,'ReleaseNamespace','{{AppId}}+qian.common.flags+UAT');
                INSERT INTO RolePermission (RoleId,PermissionId) VALUES (1,1),(2,2);
                INSERT INTO ConsumerRole (ConsumerId,RoleId) VALUES (1,1),(2,2);
                """;
        }
    }
    /// <summary>建立真实目标应用和公共 namespace，AdminService 只用于隔离测试准备。</summary>
    /// <remarks>
    /// 每套共享栈只能调用一次：应用用固定的 <see cref="AppId"/>（Portal 的权限按它播种），第二次调用会因应用已存在而失败。
    /// Config 测试的 real-apollo 集合共用一套栈，集合里只有 RealApolloPortalProtocolTests 调用。
    /// </remarks>
    public async Task PrepareFlagsAsync(CancellationToken cancellationToken = default)
    {
        await CreateAppAsync(AppId, cancellationToken).ConfigureAwait(false);
        await PublishAsync(AppId, "Fixture", "ready", cancellationToken).ConfigureAwait(false);
        await CreateNamespaceAsync(AppId, "qian.common.flags", true, cancellationToken).ConfigureAwait(false);
        await PublishNamespaceAsync(AppId, "qian.common.flags", "flag.preview", "{\"default\":false}", cancellationToken).ConfigureAwait(false);
        await WaitForPublishedAsync(AppId, "qian.common.flags", "flag.preview", "{\"default\":false}", cancellationToken).ConfigureAwait(false);
    }
    /// <summary>人工发布边界的独立操作员：真实 Portal Release API，与被测组件的编辑 Token 分离。</summary>
    public async Task PublishManuallyAsync(CancellationToken cancellationToken = default)
    {
        var status = await TryPublishAsync(_publishToken, cancellationToken).ConfigureAwait(false);
        if (status != HttpStatusCode.OK) throw new InvalidOperationException("独立 Portal 发布操作失败，状态=" + (int)status);
    }
    /// <summary>用指定 Token 尝试真实发布，供证明编辑 Token 没有发布权限。</summary>
    public async Task<HttpStatusCode> TryPublishAsync(string token, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = PortalUri };
        using var request = new HttpRequestMessage(HttpMethod.Post, "openapi/v1/envs/UAT/apps/" + AppId + "/clusters/default/namespaces/qian.common.flags/releases");
        request.Headers.TryAddWithoutValidation("Authorization", token);
        request.Content = JsonContent.Create(new { releaseTitle = "Qian manual acceptance", releaseComment = "independent publisher", releasedBy = "apollo" });
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        return response.StatusCode;
    }
    /// <summary>读取真实 Portal 草稿；不会读取 ConfigService 已发布快照来冒充草稿。</summary>
    public async Task<string?> ReadDraftAsync(string key, CancellationToken cancellationToken = default)
    {
        using var client = new HttpClient { BaseAddress = PortalUri };
        using var request = new HttpRequestMessage(HttpMethod.Get, "openapi/v1/envs/UAT/apps/" + AppId + "/clusters/default/namespaces/qian.common.flags/items/" + Uri.EscapeDataString(key));
        request.Headers.TryAddWithoutValidation("Authorization", EditToken);
        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var item = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken).ConfigureAwait(false);
        return item.GetProperty("value").GetString();
    }
}
