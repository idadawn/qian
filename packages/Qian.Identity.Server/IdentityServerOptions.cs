using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Serialization;
using OpenIddict.Abstractions;

namespace Qian.Identity.Server;

/// <summary>宿主组合参数；私钥与客户端密钥由宿主经密钥引用装入，不写入组件清单。</summary>
public sealed class IdentityServerOptions
{
    /// <summary>每环境唯一的规范 HTTPS issuer。</summary>
    public required Uri Issuer { get; init; }
    /// <summary>当前及轮换中的签名证书；调用方负责证书的生命周期。</summary>
    [JsonIgnore]
    public IReadOnlyList<X509Certificate2> SigningCertificates { get; init; } = [];
    /// <summary>用于授权码、刷新令牌加密的证书。</summary>
    [JsonIgnore]
    public IReadOnlyList<X509Certificate2> EncryptionCertificates { get; init; } = [];
    /// <summary>账号流水线建立的可信服务端 Cookie scheme；不接受来自请求体的主体。</summary>
    public string SignInScheme { get; init; } = "qian.identity.signin";
    /// <summary>经宿主确认的官方客户端；数据库持久化管理由 OpenIddict 承担。</summary>
    [JsonIgnore]
    public IReadOnlyList<IdentityClientRegistration> Clients { get; init; } = [];
    /// <summary>只允许 Development 环境且 issuer 为 loopback 时关闭传输要求。</summary>
    public bool AllowLoopbackHttp { get; init; }
    /// <summary>账号模型与统一登录流水线的参数；宿主自带账号服务（<c>new IdentityServerPlugin(accounts)</c>）时不使用。</summary>
    public IdentityAccountOptions Accounts { get; init; } = new();
    /// <inheritdoc />
    public override string ToString() => "IdentityServerOptions(redacted)";
}

/// <summary>客户端描述与精确受众白名单；scope 权限仍由 OpenIddict 独立检查。</summary>
public sealed class IdentityClientRegistration
{
    /// <summary>OpenIddict 的客户端、回调与 grant/scope 权限；机密客户端必须带密钥。</summary>
    [JsonIgnore]
    public OpenIddictApplicationDescriptor Application { get; init; } = new();
    /// <summary>此客户端允许请求的 RFC 8707 resource；必须为无 fragment 的绝对 URI。</summary>
    public required IReadOnlyList<string> Resources { get; init; }
    /// <summary>机器身份归属的租户；null 表示平台服务，不从令牌请求读取。</summary>
    public Guid? TenantId { get; init; }
    /// <summary>机器主体的组件标识；来自受信客户端注册表。</summary>
    public string? PluginId { get; init; }
    /// <summary>登录端类型（如 pc、mobile），用于会话的单端策略与审计；字母开头、最长 16 位小写字母数字或 _-，缺省 pc。</summary>
    public string DeviceType { get; init; } = "pc";
    /// <summary>已登记的后通道登出端点；只有精确的此地址可被服务端调用。</summary>
    public Uri? BackchannelLogoutUri { get; init; }
    /// <inheritdoc />
    public override string ToString() => "IdentityClientRegistration(redacted)";
}
