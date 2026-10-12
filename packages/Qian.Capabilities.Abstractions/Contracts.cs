using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>平台能力目录 seam，独立模式不因此依赖平台 Provider。</summary>
public static class CapabilitiesDefinition
{
    /// <summary>当前 scope 的能力服务。</summary>
    public static readonly ServiceKey Key = new("Capabilities");
    /// <summary>解析实际已挂载的能力目录。</summary>
    public static ICapabilityService Capabilities(this IPluginContext context) => context.Inject<ICapabilityService>(Key);
}

/// <summary>冻结 capability-entry 形状的目录结果；各 JSON 条目均为只读副本，不能反向修改注册契约。</summary>
public sealed class CapabilityCatalog
{
    /// <summary>复制条目并声明仅限当前身份缓存。</summary>
    public CapabilityCatalog(IReadOnlyList<JsonElement> items, int ttlMs)
    {
        ArgumentNullException.ThrowIfNull(items); ArgumentOutOfRangeException.ThrowIfNegative(ttlMs);
        Items = Array.AsReadOnly(items.Select(item => item.Clone()).ToArray()); TtlMs = ttlMs;
    }
    /// <summary>经过同一权限和 scope 过滤的冻结契约条目。</summary>
    public IReadOnlyList<JsonElement> Items { get; }
    /// <summary>客户端建议缓存毫秒数；执行入口始终重新判定。</summary>
    public int TtlMs { get; }
    /// <summary>固定 private，不能作为跨用户公共缓存。</summary>
    public string CacheScope { get; } = "private";
}

/// <summary>权限与作用域一致的目录、模型提示和实际执行端口；身份来自已验证服务端口。</summary>
public interface ICapabilityService
{
    /// <summary>读取当前调用者可执行的能力，不返回被隐藏或未授权的条目。</summary>
    ValueTask<CapabilityCatalog> ListAsync(IPluginContext caller, ICurrentUser user, string? kind = null, string? query = null, CancellationToken cancellationToken = default);
    /// <summary>只把当前可见且允许模型使用的能力编入提示文本。</summary>
    ValueTask<string> ComposePromptAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>按与目录相同的规则重新判定后调用真实绑定；隐藏与不存在统一 NOT_FOUND。</summary>
    ValueTask<JsonElement> InvokeAsync(IPluginContext caller, ICurrentUser user, string id, JsonElement input, CancellationToken cancellationToken = default);
}
