using Qian.Config.Apollo;

namespace Qian.Gateway.Host;

// Apollo 加入 ConfigurationManager 时会立即 Load；其签名回调不能再读取尚未合成的配置根。
internal static class GatewayBootstrap
{
    internal const string AccessKeyReference = "secretRef://platform/apollo/access-key";
    internal static QianConfigurationOptions CreateOptions(string basePath, string? environmentName, IReadOnlyList<string> arguments)
    {
        var root = Path.GetFullPath(basePath);
        if (environmentName is not null && (environmentName.Length == 0 || environmentName.IndexOfAny(['/', '\\']) >= 0))
            throw new ArgumentException("宿主环境名无效。", nameof(environmentName));
        // 目录本身只从宿主 JSON、带前缀环境变量及 CLI 获取；目录内的 Secret 文件不能递归改变目录。
        string? secretDirectory;
        var initial = Sources().AddEnvironmentVariables("QIAN_").AddCommandLine(arguments.ToArray()).Build();
        using ((IDisposable)initial)
            secretDirectory = initial["Secrets:Directory"] is { Length: > 0 } directory ? Path.GetFullPath(directory, root) : null;
        var bootstrapSources = Sources();
        if (secretDirectory is not null) bootstrapSources.AddKeyPerFile(secretDirectory, optional: false);
        var bootstrap = bootstrapSources.AddEnvironmentVariables("QIAN_").AddCommandLine(arguments.ToArray()).Build();
        using var bootstrapOwner = (IDisposable)bootstrap;
        var accessKeyPath = bootstrap["ConnectionFiles:ApolloAccessKey"];
        return new QianConfigurationOptions
        {
            BasePath = root, EnvironmentName = environmentName, SecretDirectory = secretDirectory, EnvironmentPrefix = "QIAN_", Arguments = arguments.ToArray(),
            ResolveAccessKey = reference => reference == AccessKeyReference
                ? ReadCredential(accessKeyPath ?? throw new InvalidOperationException("缺少网关引导配置：ConnectionFiles:ApolloAccessKey。"))
                : throw new InvalidOperationException("Apollo 引导密钥引用未登记。")
        };

        IConfigurationBuilder Sources()
        {
            var source = new ConfigurationBuilder().SetBasePath(root).AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
            if (environmentName is not null) source.AddJsonFile("appsettings." + environmentName + ".json", optional: true, reloadOnChange: false);
            return source;
        }
    }
    internal static string ReadCredential(string path)
    {
        if (!Path.IsPathFullyQualified(path)) throw new InvalidOperationException("引导凭据必须来自指定的 Secret 文件。");
        var value = File.ReadAllText(path).Trim();
        return value.Length > 0 ? value : throw new InvalidOperationException("引导凭据文件为空。");
    }
}
