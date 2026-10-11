using Microsoft.Extensions.Primitives;
using Qian.Runtime;
using Qian.Runtime.Errors;

namespace Qian.Config;

/// <summary>平台/服务级只读配置 seam；租户设置由 Tenancy 管理。</summary>
public static class ConfigDefinition
{
    /// <summary>当前 scope 可见的配置服务键。</summary>
    public static readonly ServiceKey Key = new("Config");
    /// <summary>解析当前已挂载的配置 Provider。</summary>
    public static IConfigService Config(this IPluginContext context) => context.Inject<IConfigService>(Key);
    /// <summary>按当前插件的声明、审批与租户上下文解析，已解析值不能进入模型消息。</summary>
    public static ValueTask<SecretValue> ResolveSecretAsync(this IPluginContext context, string reference, CancellationToken cancellationToken = default) =>
        (context.Config().Secrets ?? throw new QianErrorException(new QianError(SecretErrors.Unavailable)))
            .ForPlugin(context).ResolveAsync(reference, cancellationToken);
}
/// <summary>供受信任代码读取配置；不提供写入入口，也不自动公开为模型工具。</summary>
public interface IConfigService
{
    /// <summary>可选秘密解析能力；未装配时显式不可用，不退回读取明文配置。</summary>
    ISecretResolverFactory? Secrets { get; }
    /// <summary>按标准 IConfiguration 层次键读取值。</summary>
    string? GetValue(string key);
    /// <summary>读取必需值，缺失时报告统一配置错误，不打印值。</summary>
    string GetRequiredValue(string key);
    /// <summary>复制指定非空节的相对键和值；返回只读快照。</summary>
    IReadOnlyDictionary<string, string?> ReadSection(string path);
    /// <summary>取得下一次变更令牌，监听句柄应交给 ctx.Effect。</summary>
    IChangeToken GetReloadToken();
}
/// <summary>配置组件的稳定错误词汇。</summary>
public static class ConfigErrors
{
    /// <summary>引导项不合法。</summary>
    public static readonly ErrorCode BootstrapInvalid = ErrorCode.Protocol("CONFIG_BOOTSTRAP_INVALID", 500, "qian.config",
        new Dictionary<string, string> { ["zh-CN"] = "配置引导项无效", ["en"] = "Invalid configuration bootstrap" },
        new Dictionary<string, string> { ["zh-CN"] = "配置引导项 {key} 不满足要求。", ["en"] = "Bootstrap setting {key} does not meet its requirements." });
    /// <summary>运行所需的配置缺失。</summary>
    public static readonly ErrorCode RequiredMissing = ErrorCode.Protocol("CONFIG_REQUIRED_MISSING", 500, "qian.config",
        new Dictionary<string, string> { ["zh-CN"] = "缺少必要配置", ["en"] = "Required configuration missing" },
        new Dictionary<string, string> { ["zh-CN"] = "缺少必要配置项 {key}。", ["en"] = "Required setting {key} is missing." });

    /// <summary>登记错误词汇；释放时只撤销本次登记。</summary>
    public static IDisposable Register(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var first = catalog.Register(BootstrapInvalid);
        try { return new Registration(first, catalog.Register(RequiredMissing)); }
        catch { first.Dispose(); throw; }
    }
    private sealed class Registration(IDisposable first, IDisposable second) : IDisposable
    {
        private int _disposed;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            second.Dispose(); first.Dispose();
        }
    }
}
