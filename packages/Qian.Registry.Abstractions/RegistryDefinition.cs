using Qian.Identity.Abstractions;
using Qian.Runtime;

namespace Qian.Registry;

/// <summary>平台 Registry seam；管理 API 的权限边界由平台注册表服务负责。</summary>
public static class RegistryDefinition
{
    /// <summary>平台注册表服务键。</summary>
    public static readonly ServiceKey Key = new("Registry");
    /// <summary>管理面读取 scope 视图的权限点（R32）：持有者对未挂载条目得到 403 原因码，其他调用者与不存在同为 404。</summary>
    public const string ScopeReadPermission = "qian.registry:scope:read";
    /// <summary>取得已挂载的平台注册表。</summary>
    public static IRegistryService Registry(this IPluginContext context) => context.Inject<IRegistryService>(Key);
}

/// <summary>宿主的注册表运行时入口，不是可直接映射 HTTP 的管理写入 API。</summary>
public interface IRegistryService : IAsyncDisposable
{
    /// <summary>供可信平台插件登记权限等声明；在 Registry Ready 后、业务插件启动前可读，不是业务可见目录。</summary>
    IReadOnlyList<InstalledComponentMetadata> GetInstalledComponents();
    /// <summary>整棵初始插件树 Ready 后启动数据库监听；不得在该装载器的 Apply 内等待。</summary>
    ValueTask StartAsync(CancellationToken cancellationToken = default);
    /// <summary>合入当前已验证租户的 scope；当前只接受同租户 scope，应用/会话须另有归属证明。</summary>
    ValueTask SynchronizeAsync(ScopeKey scope, CancellationToken cancellationToken = default);
    /// <summary>同步当前已验证租户的数据库 scope 后，读取已挂载且未被遮蔽/restrict 过滤的服务绑定；不可把预装清单作为此视图。</summary>
    ValueTask<IReadOnlyList<RegistryScopeEntry>> GetScopeEntriesAsync(ScopeKey scope, ServiceKey key, CancellationToken cancellationToken = default);
    /// <summary>
    /// 管理面 scope 视图：同步后返回该 scope 已挂载且 Ready 的条目。调用者须是当前已验证租户的用户或服务主体（无委托），
    /// 并经宿主的权限判定端口持有 <see cref="RegistryDefinition.ScopeReadPermission"/>；否则与不存在同为 NOT_FOUND。
    /// </summary>
    ValueTask<IReadOnlyList<RegistryEntryView>> GetScopeViewAsync(ScopeKey scope, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>
    /// 管理面单个条目（R32 / R48）：已挂载且 Ready 时返回视图；未挂载或依赖未就绪时以 403 原因码抛出，
    /// message 说明哪一层导致、pointer 指向 patch 条目；未知条目以及权限、租户不符的调用者一律 NOT_FOUND。
    /// </summary>
    ValueTask<RegistryEntryView> GetScopeEntryAsync(ScopeKey scope, string entryId, ICurrentUser user, CancellationToken cancellationToken = default);
    /// <summary>
    /// 候选制品登记：核验完整字节的 D4 摘要后按 platform / server 做共用静态校验，inject 须由预装组件的 provides 满足，
    /// 通过后幂等保存为 Submitted；不合格时抛 INPUT_INVALID，字段错误带指向制品文件的 JSON Pointer，不写入任何记录。
    /// 供 Lifecycle 等可信平台插件调用，调用方负责权限；登记不等于验签、发布或任何租户已挂载。
    /// </summary>
    ValueTask<RegistryArtifactEntry> SubmitArtifactAsync(IReadOnlyDictionary<string, ReadOnlyMemory<byte>> files, string digest, string actor, CancellationToken cancellationToken = default);
    /// <summary>停止监听并撤销拥有的局部条目；宿主须在 Runtime.Dispose 之前、在 Apply/effect 之外调用。</summary>
    new ValueTask DisposeAsync();
}
