using System.Text.Json;
using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Capabilities;

/// <summary>平台能力目录 seam，独立模式不因此依赖平台 Provider。</summary>
public static class CapabilitiesDefinition
{
    /// <summary>当前 scope 的能力服务。</summary>
    public static readonly ServiceKey Key = new("Capabilities");
    /// <summary>版本化契约仓库的服务键。</summary>
    public static readonly ServiceKey ContractsKey = new("Contracts");
    /// <summary>菜单服务。</summary>
    public static readonly ServiceKey NavigationKey = new("Navigation");
    /// <summary>可遮蔽、可过滤的菜单贡献集合。</summary>
    public static readonly ServiceKey NavigationItemsKey = new("NavigationItems");
    /// <summary>平台挂载路径的契约准入服务键（R76）。</summary>
    public static readonly ServiceKey AdmissionKey = new("ContractAdmission");
    /// <summary>解析当前可见的菜单服务。</summary>
    public static INavigationService Navigation(this IPluginContext context) => context.Inject<INavigationService>(NavigationKey);
    /// <summary>读取平台预装制品的契约仓库。</summary>
    public static IContractRepository Contracts(this IPluginContext context) => context.Inject<IContractRepository>(ContractsKey);
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

/// <summary>一次能力调用的结果：执行输出，或同一幂等键首次成功调用的原样回放。</summary>
public sealed class CapabilityInvocation
{
    /// <summary>复制输出，避免调用方持有的文档被释放或修改。</summary>
    public CapabilityInvocation(JsonElement output, bool replayed)
    {
        Output = output.Clone(); Replayed = replayed;
    }
    /// <summary>能力输出，形状由条目的 outputSchema 描述。</summary>
    public JsonElement Output { get; }
    /// <summary>为 true 表示本次没有执行，返回的是同一主体、同一能力、同一幂等键首次成功调用的响应。</summary>
    public bool Replayed { get; }
}

/// <summary>权限与作用域一致的目录、模型提示和实际执行端口；身份来自已验证服务端口。</summary>
public interface ICapabilityService
{
    /// <summary>读取当前调用者可执行的能力，不返回被隐藏或未授权的条目。</summary>
    ValueTask<CapabilityCatalog> ListAsync(IPluginContext caller, ICurrentUser user, string? kind = null, string? query = null, CancellationToken cancellationToken = default);
    /// <summary>只把当前可见且允许模型使用的能力编入提示文本。</summary>
    ValueTask<string> ComposePromptAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>聚合当前实际可调用绑定的 OpenAPI 3.1；与目录使用同一身份、权限和 scope 判定。</summary>
    ValueTask<JsonElement> GetOpenApiAsync(IPluginContext caller, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>
    /// 按与目录相同的规则重新判定后调用真实绑定；隐藏与不存在统一 NOT_FOUND。输入先按条目 inputSchema 校验，
    /// 不符返回 INPUT_INVALID 与逐项 JSON Pointer；给出幂等键时，同一主体、同一能力、同一键的成功调用只执行一次，
    /// 回放窗口内重复请求原样返回首次响应，键相同而输入不同返回 CONFLICT。
    /// </summary>
    ValueTask<CapabilityInvocation> InvokeAsync(IPluginContext caller, ICurrentUser user, string id, JsonElement input, Guid? idempotencyKey = null, CancellationToken cancellationToken = default);
}
