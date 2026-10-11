using Microsoft.Extensions.Logging;

namespace Qian.Config.Apollo;

/// <summary>配置装配参数；引导值来自宿主源，Apollo 不能改变自己的访问地址或访问密钥引用。</summary>
public sealed class QianConfigurationOptions
{
    /// <summary>appsettings 文件与相对缓存路径的根目录。</summary>
    public required string BasePath { get; init; }
    /// <summary>可选 appsettings.{EnvironmentName}.json；不等同于 Apollo UAT/PRO 环境。</summary>
    public string? EnvironmentName { get; init; }
    /// <summary>独立模式可显式禁用 Apollo，仅使用标准配置源。</summary>
    public bool UseApollo { get; init; } = true;
    /// <summary>已挂载的 K8s Secret 文件目录；指定后缺目录即失败。</summary>
    public string? SecretDirectory { get; init; }
    /// <summary>可选环境变量前缀，默认无前缀。</summary>
    public string? EnvironmentPrefix { get; init; }
    /// <summary>命令行覆盖项，优先级最高。</summary>
    public IReadOnlyList<string> Arguments { get; init; } = [];
    /// <summary>每次签名前由宿主解析 AccessKey 的 secretRef；不接受明文引导值。</summary>
    public Func<string, string?>? ResolveAccessKey { get; init; }
    /// <summary>可选候选 Apollo 快照校验；拒绝时保留上一份快照和缓存。</summary>
    public Func<IReadOnlyDictionary<string, string?>, bool>? ValidateApolloSnapshot { get; init; }
    /// <summary>可选宿主管理的客户端，超时必须大于 60 秒且不能向不可信目标重定向。</summary>
    public HttpClient? HttpClient { get; init; }
    /// <summary>可选日志器；底层失败日志不输出配置或凭据。</summary>
    public ILogger? Logger { get; init; }
}
