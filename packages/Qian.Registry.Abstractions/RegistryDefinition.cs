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
    /// <summary>
    /// 挂载（PRD 7.2.1 Mount）：把一条 patch/v2 条目写入当前已验证租户 scope 的 scope_patches，经与文件 patch 相同的装配校验
    /// （格式、组件须在宿主预装集合、配置 Schema、依赖）后按条件修订写入，同一事务把 <see cref="RegistryEvents.ScopeMounted"/> 放进 Outbox。
    /// 条目不能带 disabled: true（卸载用 <see cref="UnmountAsync"/>）；expectedRevision 为该条目当前 patch 的修订，没有 patch 时为 0，不符抛
    /// REGISTRY_REVISION_CONFLICT。只接受与租户上下文一致的 tenant scope，租户须为小写规范 UUID；供 Lifecycle 等可信平台插件调用，调用方负责权限。
    /// </summary>
    ValueTask<ScopePatchSnapshot> MountAsync(ScopeKey scope, System.Text.Json.JsonElement entry, int expectedRevision, string actor, CancellationToken cancellationToken = default);
    /// <summary>
    /// 卸载（PRD 7.2.1 Unmount，不改数据库开关）：条目来自平台 profile 时写入 {id, disabled: true}，只由本 scope 的 patch 引入时撤销该 patch；
    /// 同一事务把 <see cref="RegistryEvents.ScopeUnmounted"/> 放进 Outbox。expectedRevision 同 <see cref="MountAsync"/>；返回卸载后的 patch 修订。
    /// 本 scope 本来就看不到该条目时抛 NOT_FOUND。调用约束同 <see cref="MountAsync"/>。
    /// </summary>
    ValueTask<int> UnmountAsync(ScopeKey scope, string entryId, int expectedRevision, string actor, CancellationToken cancellationToken = default);
    /// <summary>读取当前已验证租户 scope 中某条目的有效 patch（含修订）；没有时返回 null。供可信平台插件取得条件写入的预期修订。</summary>
    ValueTask<ScopePatchSnapshot?> GetScopePatchAsync(ScopeKey scope, string entryId, CancellationToken cancellationToken = default);
    /// <summary>停止监听并撤销拥有的局部条目；宿主须在 Runtime.Dispose 之前、在 Apply/effect 之外调用。</summary>
    new ValueTask DisposeAsync();
}
