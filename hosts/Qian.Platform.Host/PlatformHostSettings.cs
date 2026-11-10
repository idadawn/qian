using System.Globalization;

namespace Qian.Platform.Host;

/// <summary>
/// 平台宿主的启动输入。来源与网关宿主相同：<c>appsettings.json</c>、<c>QIAN_</c> 前缀的环境变量、命令行，测试可再加内存配置源。
/// 凭据不进配置值：配置里只写 Secret 文件的绝对路径，宿主在建上下文或连接时现读文件。
/// </summary>
public sealed class PlatformHostSettings
{
    /// <summary>网关为能力目录、导航、外壳目录与有效权限签发 S2S 时用的受众（网关的簇 id）。</summary>
    public const string PlatformAudience = "qian-platform";
    /// <summary>网关为平台运营接口签发 S2S 时用的受众（R144：与平台表面同进程、同端口，受众独立）。</summary>
    public const string OperationsAudience = "qian-operations";
    /// <summary>宿主 overlay 的缺省文件名，随发布产物放在程序目录。</summary>
    public const string HostOverlayFile = "platform.overlay.yml";

    /// <summary>profile 与组件目录的根（镜像里是 <c>QIAN_ROOT=/app/qian</c>），其下有 <c>profiles/</c> 与 <c>plugins/</c>。</summary>
    public required string Root { get; init; }
    /// <summary>宿主 overlay：关掉本宿主没有 Provider 的条目，规定数据连接的绑定方式；先于其它 overlay 应用。</summary>
    public required string HostOverlay { get; init; }
    /// <summary>环境 overlay（ConfigMap 挂载后经 <c>--patch</c> 传入），按出现顺序在宿主 overlay 与扩展 overlay 之后应用。</summary>
    public IReadOnlyList<string> Patches { get; init; } = [];
    /// <summary>额外的组件根目录，如随镜像发布的 module 应用的组件文件夹。</summary>
    public IReadOnlyList<string> ComponentDirectories { get; init; } = [];
    /// <summary>网关 S2S 的签发方（网关配置 <c>Gateway:Issuer</c>）。</summary>
    public required string S2sIssuer { get; init; }
    /// <summary>网关公开 JWKS 的地址（<c>/.well-known/jwks.json</c>）；密钥轮换时按需刷新。</summary>
    public required Uri S2sJwks { get; init; }
    /// <summary>令牌有效期允许的时钟偏差；缺省 0（S2S 只有 60 秒寿命）。</summary>
    public TimeSpan S2sClockSkew { get; init; }
    /// <summary>为 true 时只执行迁移（<c>--migrate-only</c>）：在线登录的连接改用迁移属主的连接占位，迁移 Job 不需要挂在线凭据。</summary>
    public bool MigrateOnly { get; init; }
    /// <summary>Secret 挂载根目录（平台信任根公钥等 secretRef 的解析位置）；为空时不装秘密解析。</summary>
    public string? SecretMountRoot { get; init; }
    /// <summary>各登录与外部依赖的 Secret 文件：<c>App</c>、<c>PlatformAuthz</c>、<c>PlatformAudit</c>、<c>Migrations</c>、<c>Redis</c>、<c>EventsBroker</c>。</summary>
    public IReadOnlyDictionary<string, string> ConnectionFiles { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>从配置与启动参数读出设置；缺必填项或值不合法即抛错，宿主不启动。</summary>
    public static PlatformHostSettings Read(IConfiguration configuration, IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(configuration); ArgumentNullException.ThrowIfNull(arguments);
        var root = Required(configuration, "Root", "QIAN_ROOT");
        if (!Path.IsPathFullyQualified(root)) throw new InvalidOperationException("平台宿主配置 Root（QIAN_ROOT）必须是绝对路径。");
        var issuer = Required(configuration, "S2S:Issuer");
        var jwks = Required(configuration, "S2S:JwksUri");
        if (!Uri.TryCreate(jwks, UriKind.Absolute, out var jwksUri) || jwksUri.Query.Length != 0 || jwksUri.Fragment.Length != 0 ||
            jwksUri.Scheme is not ("https" or "http") || jwksUri.Scheme == "http" && !configuration.GetValue<bool>("S2S:AllowHttpJwks"))
            throw new InvalidOperationException("S2S:JwksUri 必须是 https 地址；集群内明文地址须另设 S2S:AllowHttpJwks=true。");
        var skew = configuration["S2S:ClockSkewSeconds"] is { Length: > 0 } text
            ? int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds <= 60 ? TimeSpan.FromSeconds(seconds)
                : throw new InvalidOperationException("S2S:ClockSkewSeconds 必须是 0–60 的整数。")
            : TimeSpan.Zero;
        var patches = configuration.GetSection("Profile:Patches").GetChildren().Select(item => item.Value).OfType<string>().ToList();
        for (var index = 0; index < arguments.Count; index++)
            if (arguments[index] == "--patch")
                patches.Add(index + 1 < arguments.Count ? arguments[++index] : throw new InvalidOperationException("--patch 后缺少 overlay 文件路径。"));
        var files = configuration.GetSection("ConnectionFiles").GetChildren().Where(item => !string.IsNullOrEmpty(item.Value))
            .ToDictionary(item => item.Key, item => item.Value!, StringComparer.Ordinal);
        foreach (var (name, path) in files)
            if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("ConnectionFiles:" + name + " 必须是 Secret 文件的绝对路径。");
        return new PlatformHostSettings
        {
            Root = Path.GetFullPath(root),
            HostOverlay = configuration["Profile:HostOverlay"] is { Length: > 0 } overlay ? Path.GetFullPath(overlay) : Path.Combine(AppContext.BaseDirectory, HostOverlayFile),
            Patches = patches.Select(patch => Path.GetFullPath(patch)).ToArray(),
            ComponentDirectories = configuration.GetSection("Profile:ComponentDirectories").GetChildren().Select(item => item.Value).OfType<string>().Select(Path.GetFullPath).ToArray(),
            S2sIssuer = issuer, S2sJwks = jwksUri, S2sClockSkew = skew,
            MigrateOnly = arguments.Contains("--migrate-only", StringComparer.Ordinal),
            SecretMountRoot = configuration["Secrets:MountRoot"] is { Length: > 0 } secrets ? Path.GetFullPath(secrets) : null,
            ConnectionFiles = files
        };
    }

    /// <summary>读一个连接的 Secret 文件；迁移模式下在线登录改用迁移属主的连接（迁移器只用它执行有包内迁移的上下文，其余上下文不连库）。</summary>
    public string Connection(string name)
    {
        if (MigrateOnly && name != "Migrations") name = "Migrations";
        return ConnectionFiles.TryGetValue(name, out var path) ? ReadSecret(path)
            : throw new InvalidOperationException("缺少平台宿主配置：ConnectionFiles:" + name + (MigrateOnly ? "（迁移任务只需要迁移属主的连接）。" : "。"));
    }

    /// <summary>该连接是否配置了 Secret 文件。</summary>
    public bool HasConnection(string name) => ConnectionFiles.ContainsKey(name);

    internal static string ReadSecret(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("引导凭据必须来自指定的 Secret 文件。");
        var value = File.ReadAllText(path).Trim();
        return value.Length > 0 ? value : throw new InvalidOperationException("引导凭据文件为空。");
    }

    private static string Required(IConfiguration configuration, string key, string? display = null) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException("缺少平台宿主配置：" + (display ?? key) + "。");
}
