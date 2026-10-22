using Qian.Runtime;

namespace Qian.Gateway;

/// <summary>网关 seam；Provider 安装到 gateway profile，业务组件只消费本契约。</summary>
public static class GatewayDefinition
{
    /// <summary>网关服务键。</summary>
    public static readonly ServiceKey Key = new("Gateway");
    /// <summary>取得当前已挂载网关。</summary>
    public static IGatewayService Gateway(this IPluginContext context) => context.Inject<IGatewayService>(Key);
}

/// <summary>网关只读状态；路由配置在构建宿主时固定，未挂载时禁止代理请求。</summary>
public interface IGatewayService
{
    /// <summary>对应组件当前是否挂载。</summary>
    bool IsReady { get; }
    /// <summary>取得静态下游的唯一受众；未登记的 cluster 拒绝。</summary>
    string AudienceFor(string clusterId);
}

/// <summary>受保护路由；不开放匿名策略覆盖。</summary>
public sealed record GatewayRoute(string Id, string Path, string Cluster);

/// <summary>静态下游；Id 同时是 S2S 的唯一受众。</summary>
public sealed record GatewayCluster(string Id, string Address);

/// <summary>网关声明式配置；只允许一个身份发行方，密钥值从不进入配置，PEM 路径指向宿主的 Secret 投射。</summary>
public sealed record GatewayConfiguration(string IdentityIssuer, string IdentityAudience, string Issuer,
    string SigningKeyPath, string PublicKeyPath, IReadOnlyList<string> TrustedProxyNetworks,
    IReadOnlyList<GatewayRoute> Routes, IReadOnlyList<GatewayCluster> Clusters);
